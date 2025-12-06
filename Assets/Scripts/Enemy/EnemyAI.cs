using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using UnityEngine.Animations.Rigging;
using UnityEngine.Events;
using System;
using Unity.VisualScripting;

public class EnemyAI : MonoBehaviour
{
	[Header("References")]
	public Transform Player;
	[SerializeField] private NavMeshAgent _agent;


	[Header("Patrol Settings")]
	public Vector3 PatrolCenter;
	public float PatrolRadius = 80f;
	public float PatrolWaitTime = 2f;

	[Header("Detection Settings")]
	[SerializeField] private float _detectionRange = 20f;
	[SerializeField] private float _attackRange = 1.5f;
	[SerializeField] private float _viewAngle = 90f;
	[SerializeField] private LayerMask _wallsAndPlayerLayer;
	[SerializeField] private float _searchDuration;

	[SerializeField] private Transform _head;

	private EnemyState _currentState;
	private Health _health;
	private float _distanceToPlayer;
	private Vector3 _lastKnownPosition;
	private bool _isMoving;

	public bool IsMoving => _isMoving;
	public float SearchDuration => _searchDuration;
	public float ViewAngle => _viewAngle;

	public event Action OnShoot;
	public event Action OnReload;
	public event Action OnReloadEnd;

	private void Start()
	{
		_health = GetComponent<Health>();
		_health.OnDie += Die;
		IsPlayerInFieldOfView();
		_currentState = new PatrolState(this);

	}

	private void Update()
	{
		_distanceToPlayer = Vector3.Distance(transform.position, Player.position);
		_isMoving = _agent.velocity.magnitude > _agent.speed / 4;
		_currentState?.UpdateState();
		Debug.Log(_currentState.ToString());
	}

	public void SwitchState(EnemyState newState)
	{
		_currentState = newState;
	}

	public void MoveTo(Vector3 destination)
	{
		if (_agent != null && _agent.isActiveAndEnabled)
		{
			_agent.SetDestination(destination);
		}
	}

	public bool IsPlayerInAttackRange() => _distanceToPlayer <= _attackRange;

	public bool IsPlayerInFieldOfView(float FOV = -1)
	{
		if (FOV < 0)
		{
			FOV = _viewAngle;
		}


		Vector3 directionToPlayer = (Player.position - _head.position).normalized;
		float angle = Vector3.Angle(_head.forward, directionToPlayer);

		if (angle > FOV / 2 || !(_distanceToPlayer <= _detectionRange))
		{
			return false;
		}

		if (Physics.Raycast(_head.position, directionToPlayer, out RaycastHit hit, _detectionRange, _wallsAndPlayerLayer))
		{
			if (hit.transform != Player)
			{
				return false;
			}
		}

		return true;
	}

	public void RotateTowardsPlayer()
	{
		Vector3 directionToPlayer = (Player.position - transform.position).normalized;

		Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);

		targetRotation.x = transform.rotation.x;
		targetRotation.z = transform.rotation.z;

		transform.rotation = Quaternion.Lerp(
			transform.rotation,
			targetRotation,
			Time.deltaTime * _agent.angularSpeed / 100
		);
	}


	public void Attack()
	{

	}

	public void Die()
	{
		_agent.enabled = false;
		this.enabled = false;
	}
	private IEnumerator AttackRoutine()
	{
		yield return new WaitForSeconds(2);
	}
}
