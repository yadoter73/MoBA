using System.Collections.Generic;
using HorrorPostProcess;
using MoBA.Audio;
using UnityEngine;
using UnityEngine.AI;
using Zenject;

public class EnemyAI : MonoBehaviour
{
	private static readonly List<EnemyAI> ActiveEnemies = new();
	private static readonly int AnimIsMoving = Animator.StringToHash("isMoving");
	private static readonly int AnimIsChasing = Animator.StringToHash("isChasing");
	private static readonly int AnimAttack = Animator.StringToHash("Attack");
	private static readonly int AnimRoar = Animator.StringToHash("Roar");
	private static readonly int StateIdleHash = Animator.StringToHash("Idle");
	private static readonly int StateWalkHash = Animator.StringToHash("Walk");
	private static readonly int StateChaseHash = Animator.StringToHash("Chase");

	private static int _lastFearUpdateFrame = -1;
	private static HorrorShaderController _cachedHorrorShader;

	public LayerMask whatIsPlayer;

	public Vector3 walkPoint;

	[Inject(Id = "PlayerTransform")] public Transform player { get; private set; }
	[Inject(Id = "Layer")] private LayerMask _whatIsGround;
	[Inject(Id = "EnemyAnim", Optional = true)] private Animator _anim;

	[SerializeField] private float _sightRange = 20f;
	[SerializeField] private float _attackRange = 7f;
	[SerializeField] private float _immediateKillRange = 2.65f;
	[SerializeField] private float _patrolSpeed = 4.2f;
	[SerializeField] private float _chaseSpeed = 6.6f;
	[SerializeField] private float _arenaEdgeMargin = 14f;
	[SerializeField] private float _minPointTravelDistance = 12f;
	[SerializeField] private float _minSeparationBetweenMonsters = 14f;
	[SerializeField] private float _minNavMeshEdgeClearance = 2.2f;
	[SerializeField] private bool _skipLookingState = false;
	[SerializeField] private float _headAimHeight = 1.15f;

	[Inject(Optional = true)] private ArenaBounds _arenaBounds;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void ResetStaticState()
	{
		ActiveEnemies.Clear();
		_lastFearUpdateFrame = -1;
		_cachedHorrorShader = null;
	}

	private readonly List<Animator> _animators = new();
	private NavMeshAgent _agent;
	private NavMeshPath _pathCheck;
	private EnemyState _currentState;
	private EnemyAudioController _audioController;
	private bool _hasMovingParam;
	private bool _hasChasingParam;
	private bool _hasAttackParam;
	private bool _hasRoarParam;

	public bool playerInSightRange, playerInAttackRange, playerWaitingToLeave;

	public float SightRange => _sightRange;
	public float AttackRange => _attackRange;
	public float ImmediateKillRange => _immediateKillRange;
	public bool SkipLookingState => _skipLookingState;
	public bool HasValidPath => _agent != null && _agent.enabled && _agent.isOnNavMesh;
	public bool IsPathPending => _agent != null && _agent.pathPending;
	public float RemainingDistance => HasValidPath ? _agent.remainingDistance : float.MaxValue;
	public float StoppingDistance => _agent != null ? _agent.stoppingDistance : 0f;
	public NavMeshPathStatus PathStatus => _agent != null ? _agent.pathStatus : NavMeshPathStatus.PathInvalid;
	public float AgentSpeedSqr => _agent != null ? _agent.velocity.sqrMagnitude : 0f;

	private void OnEnable()
	{
		if (!ActiveEnemies.Contains(this)) ActiveEnemies.Add(this);
	}

	private void OnDisable()
	{
		ActiveEnemies.Remove(this);
	}

	private void Start()
	{
		_agent = GetComponent<NavMeshAgent>();
		_audioController = GetComponent<EnemyAudioController>();
		_pathCheck = new NavMeshPath();
		if (_arenaBounds == null) _arenaBounds = FindAnyObjectByType<ArenaBounds>();
		if (player == null)
		{
			var playerGo = GameObject.FindGameObjectWithTag("Player");
			if (playerGo != null) player = playerGo.transform;
		}

		InitializeAnimators();

		if (_agent != null)
		{
			_agent.speed = _patrolSpeed;
			if (!_agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit snap, 30f, NavMesh.AllAreas))
			{
				_agent.Warp(snap.position);
			}
		}

		_currentState = new PatrolState(this, _arenaBounds, _whatIsGround, _anim);
	}

	private void InitializeAnimators()
	{
		_animators.Clear();
		Animator[] childAnimators = GetComponentsInChildren<Animator>(true);
		RuntimeAnimatorController sharedController = _anim != null ? _anim.runtimeAnimatorController : null;

		if (sharedController == null)
		{
			for (int i = 0; i < childAnimators.Length; i++)
			{
				if (childAnimators[i].runtimeAnimatorController != null)
				{
					sharedController = childAnimators[i].runtimeAnimatorController;
					break;
				}
			}
		}

		for (int i = 0; i < childAnimators.Length; i++)
		{
			Animator a = childAnimators[i];
			if (a == null) continue;
			a.enabled = true;
			a.applyRootMotion = false;
			// Держим оба LOD-аниматора (ближний и дальний) в актуальном состоянии при смене дистанции
			a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
			if (a.runtimeAnimatorController == null && sharedController != null)
			{
				a.runtimeAnimatorController = sharedController;
			}
			_animators.Add(a);
		}

		if (_anim == null && _animators.Count > 0)
		{
			_anim = _animators[0];
		}

		if (_anim != null && _anim.runtimeAnimatorController != null)
		{
			AnimatorControllerParameter[] parameters = _anim.parameters;
			for (int i = 0; i < parameters.Length; i++)
			{
				if (parameters[i].nameHash == AnimIsMoving) _hasMovingParam = true;
				else if (parameters[i].nameHash == AnimIsChasing) _hasChasingParam = true;
				else if (parameters[i].nameHash == AnimAttack) _hasAttackParam = true;
				else if (parameters[i].nameHash == AnimRoar) _hasRoarParam = true;
			}
		}

		SetMoving(false, false);
	}

	private void Update()
	{
		EnsureLoopingFallback();
	}

	private void LateUpdate()
	{
		UpdateGlobalHorrorFear();
	}

	private void EnsureLoopingFallback()
	{
		for (int i = 0; i < _animators.Count; i++)
		{
			Animator a = _animators[i];
			if (a == null || !a.isActiveAndEnabled || a.runtimeAnimatorController == null || a.IsInTransition(0))
				continue;

			AnimatorStateInfo info = a.GetCurrentAnimatorStateInfo(0);
			bool isLocomotionOrIdle = info.shortNameHash == StateIdleHash ||
			                          info.shortNameHash == StateWalkHash ||
			                          info.shortNameHash == StateChaseHash;
			if (isLocomotionOrIdle && !info.loop && info.normalizedTime >= 0.96f)
			{
				a.Play(info.fullPathHash, 0, 0f);
			}
		}
	}

	private void UpdateGlobalHorrorFear()
	{
		if (PlayerDeathHandler.IsDead || player == null) return;
		if (_lastFearUpdateFrame == Time.frameCount) return;
		_lastFearUpdateFrame = Time.frameCount;

		if (_cachedHorrorShader == null)
		{
			_cachedHorrorShader = FindAnyObjectByType<HorrorShaderController>();
		}
		if (_cachedHorrorShader == null) return;

		float minDist = float.MaxValue;
		Vector3 pPos = player.position;
		for (int i = 0; i < ActiveEnemies.Count; i++)
		{
			EnemyAI monster = ActiveEnemies[i];
			if (monster == null) continue;
			float dx = pPos.x - monster.transform.position.x;
			float dz = pPos.z - monster.transform.position.z;
			float d = Mathf.Sqrt(dx * dx + dz * dz);
			if (d < minDist) minDist = d;
		}

		float fear = Mathf.InverseLerp(_sightRange * 1.1f, _immediateKillRange, minDist);
		_cachedHorrorShader.SetFearLevel(fear * 0.85f);
	}

	private void FixedUpdate()
	{
		if (PlayerDeathHandler.IsDead)
		{
			StopMoving();
			playerInSightRange = false;
			playerInAttackRange = false;
			_currentState?.UpdateState();
			return;
		}

		float horizDist = HorizontalDistanceToPlayer();
		playerInSightRange = horizDist <= _sightRange;
		playerInAttackRange = horizDist <= _attackRange;

		_currentState?.UpdateState();
	}

	private void OnDestroy()
	{
		ActiveEnemies.Remove(this);
		_currentState?.ExitState();
	}

	public void SwitchState(EnemyState newState)
	{
		bool enteringChase = newState is ChaseState && !(_currentState is ChaseState);
		_currentState?.ExitState();
		_currentState = newState;
		if (enteringChase && _audioController != null)
		{
			_audioController.OnEnterChaseState();
		}
	}

	public float HorizontalDistanceToPlayer()
	{
		if (player == null) return float.MaxValue;
		float dx = player.position.x - transform.position.x;
		float dz = player.position.z - transform.position.z;
		return Mathf.Sqrt(dx * dx + dz * dz);
	}

	public void FacePlayerSmoothly(float turnSpeed)
	{
		if (player == null) return;
		Vector3 flatDir = player.position - transform.position;
		flatDir.y = 0f;
		if (flatDir.sqrMagnitude > 0.0001f)
		{
			Quaternion targetRotation = Quaternion.LookRotation(flatDir.normalized, Vector3.up);
			transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
		}
	}

	public Vector3 GetHeadAimPosition()
	{
		return transform.position + Vector3.up * _headAimHeight;
	}

	public void SetPatrolSpeed()
	{
		if (_agent != null) _agent.speed = _patrolSpeed;
	}

	public void SetChaseSpeed()
	{
		if (_agent != null) _agent.speed = _chaseSpeed;
	}

	public void SetMoving(bool moving, bool chasing)
	{
		for (int i = 0; i < _animators.Count; i++)
		{
			Animator a = _animators[i];
			if (a == null) continue;
			if (_hasMovingParam) a.SetBool(AnimIsMoving, moving);
			if (_hasChasingParam) a.SetBool(AnimIsChasing, chasing);
		}
		if (_audioController != null)
		{
			_audioController.OnMovementStateChanged(moving, chasing);
		}
	}

	public void TriggerAttack()
	{
		if (_audioController != null)
		{
			_audioController.OnMovementStateChanged(false, false);
		}
		for (int i = 0; i < _animators.Count; i++)
		{
			Animator a = _animators[i];
			if (a == null) continue;
			if (_hasMovingParam) a.SetBool(AnimIsMoving, false);
			if (_hasChasingParam) a.SetBool(AnimIsChasing, false);
			if (_hasAttackParam)
			{
				a.ResetTrigger(AnimRoar);
				a.SetTrigger(AnimAttack);
			}
		}
	}

	public void TriggerRoar()
	{
		for (int i = 0; i < _animators.Count; i++)
		{
			Animator a = _animators[i];
			if (a == null) continue;
			if (_hasRoarParam) a.SetTrigger(AnimRoar);
		}
	}

	public void MoveTo(Vector3 destination)
	{
		MoveTo(destination, chasing: false);
	}

	public void MoveTo(Vector3 destination, bool chasing)
	{
		SetMoving(true, chasing);
		if (HasValidPath)
		{
			_agent.isStopped = false;
			_agent.SetDestination(destination);
		}
	}

	public void StopMoving()
	{
		SetMoving(false, false);
		if (HasValidPath)
		{
			_agent.isStopped = true;
			_agent.ResetPath();
			_agent.velocity = Vector3.zero;
		}
	}

	public Vector3 FindPoint()
	{
		if (_arenaBounds == null) _arenaBounds = FindAnyObjectByType<ArenaBounds>();
		if (_pathCheck == null) _pathCheck = new NavMeshPath();

		Bounds bounds = _arenaBounds != null
			? _arenaBounds.Bounds
			: new Bounds(transform.position, new Vector3(120f, 30f, 120f));

		float minX = bounds.min.x + _arenaEdgeMargin;
		float maxX = bounds.max.x - _arenaEdgeMargin;
		float minZ = bounds.min.z + _arenaEdgeMargin;
		float maxZ = bounds.max.z - _arenaEdgeMargin;
		if (minX >= maxX) { minX = bounds.min.x; maxX = bounds.max.x; }
		if (minZ >= maxZ) { minZ = bounds.min.z; maxZ = bounds.max.z; }

		Vector3 selfPos = transform.position;
		Vector3 bestCandidate = selfPos;
		bool foundCandidate = false;

		const int totalAttempts = 28;
		for (int attempt = 0; attempt < totalAttempts; attempt++)
		{
			bool strict = attempt < 18;
			Vector3 probe;
			if (attempt % 2 == 0)
			{
				probe = new Vector3(
					Random.Range(minX, maxX),
					selfPos.y,
					Random.Range(minZ, maxZ)
				);
			}
			else
			{
				Vector2 dir = Random.insideUnitCircle;
				if (dir.sqrMagnitude < 0.001f) dir = Vector2.right;
				dir.Normalize();
				float radius = Random.Range(18f, 65f);
				probe = new Vector3(
					Mathf.Clamp(selfPos.x + dir.x * radius, minX, maxX),
					selfPos.y,
					Mathf.Clamp(selfPos.z + dir.y * radius, minZ, maxZ)
				);
			}

			if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 14f, NavMesh.AllAreas))
				continue;

			if (!IsInsideBoundsXZ(hit.position, bounds, strict ? _arenaEdgeMargin * 0.5f : 2f))
				continue;

			float dxSelf = hit.position.x - selfPos.x;
			float dzSelf = hit.position.z - selfPos.z;
			float distSelfSqr = dxSelf * dxSelf + dzSelf * dzSelf;
			if (distSelfSqr < _minPointTravelDistance * _minPointTravelDistance)
				continue;

			if (strict && NavMesh.FindClosestEdge(hit.position, out NavMeshHit edgeHit, NavMesh.AllAreas))
			{
				if (edgeHit.distance < _minNavMeshEdgeClearance)
					continue;
			}

			if (strict && IsPointTooCloseToOtherMonsters(hit.position))
				continue;

			if (HasValidPath)
			{
				if (!_agent.CalculatePath(hit.position, _pathCheck) || _pathCheck.status != NavMeshPathStatus.PathComplete)
					continue;
			}

			walkPoint = hit.position;
			return walkPoint;
		}

		if (!foundCandidate)
		{
			for (int i = 0; i < 8; i++)
			{
				Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(12f, 35f);
				Vector3 probe = new Vector3(
					Mathf.Clamp(selfPos.x + offset.x, bounds.min.x + 2f, bounds.max.x - 2f),
					selfPos.y,
					Mathf.Clamp(selfPos.z + offset.y, bounds.min.z + 2f, bounds.max.z - 2f)
				);
				if (NavMesh.SamplePosition(probe, out NavMeshHit hit, 20f, NavMesh.AllAreas))
				{
					if (!HasValidPath || (_agent.CalculatePath(hit.position, _pathCheck) && _pathCheck.status == NavMeshPathStatus.PathComplete))
					{
						bestCandidate = hit.position;
						foundCandidate = true;
						break;
					}
				}
			}
		}

		walkPoint = foundCandidate ? bestCandidate : selfPos;
		return walkPoint;
	}

	private static bool IsInsideBoundsXZ(Vector3 pos, Bounds bounds, float inset)
	{
		return pos.x >= bounds.min.x + inset && pos.x <= bounds.max.x - inset &&
		       pos.z >= bounds.min.z + inset && pos.z <= bounds.max.z - inset;
	}

	private bool IsPointTooCloseToOtherMonsters(Vector3 point)
	{
		float minSqr = _minSeparationBetweenMonsters * _minSeparationBetweenMonsters;
		for (int i = 0; i < ActiveEnemies.Count; i++)
		{
			EnemyAI other = ActiveEnemies[i];
			if (other == null || other == this) continue;

			float dx = point.x - other.walkPoint.x;
			float dz = point.z - other.walkPoint.z;
			if (dx * dx + dz * dz < minSqr) return true;

			Vector3 op = other.transform.position;
			float mdx = point.x - op.x;
			float mdz = point.z - op.z;
			if (mdx * mdx + mdz * mdz < minSqr * 0.5f) return true;
		}
		return false;
	}

	private void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.yellow;
		Gizmos.DrawWireSphere(transform.position, _sightRange);

		Gizmos.DrawSphere(walkPoint, 5);
	}
}
