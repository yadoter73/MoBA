using UnityEngine;
using UnityEngine.AI;
using PrimeTween;
using Zenject;
using System.Collections;
using Cysharp.Threading.Tasks;
public class EnemyAI : MonoBehaviour
{
	public LayerMask whatIsPlayer;

	public Vector3 walkPoint;

	[Inject(Id = "PlayerTransform")] public Transform player { get; private set; }
	[Inject(Id = "Layer")] private LayerMask _whatIsGround;
	[Inject(Id = "EnemyAnimator")] private Animator _anim;

	[SerializeField] private float _sightRange;
	[SerializeField] private float _attackRange;

	[Inject] private ArenaBounds _arenaBounds;

	private NavMeshAgent _agent;
	private EnemyState _currentState;

	public bool playerInSightRange, playerInAttackRange, playerWaitingToLeave;

	private void Start()
	{
		_agent = GetComponent<NavMeshAgent>();
		_currentState = new PatrolState(this, _arenaBounds, _whatIsGround, _anim);
	}
	private void FixedUpdate()
	{
		_currentState?.UpdateState();
		playerInSightRange = Physics.CheckSphere(transform.position, _sightRange, whatIsPlayer);
		playerInAttackRange = Physics.CheckSphere(transform.position, _attackRange, whatIsPlayer);
	}
	public void SwitchState(EnemyState newState)
	{
		_currentState = newState;
	}
	public void MoveTo(Vector3 destination)
	{
		_anim.SetBool("isMoving", true);
		_agent.SetDestination(destination);
	}
	public Vector3 FindPoint()
	{
		float randomX = Random.Range(20, 67);
		float randomZ = Random.Range(20, 67);

		Vector3 randomPoint = new Vector3(transform.position.x + randomX, transform.position.y , transform.position.z + randomZ);
		NavMeshHit hit;

		if (NavMesh.SamplePosition(randomPoint, out hit, float.MaxValue , NavMesh.AllAreas))
		{
			return walkPoint = hit.position;
		}
		return Vector3.zero;
	}
	private void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.yellow;
		Gizmos.DrawWireSphere(transform.position, _sightRange);

		Gizmos.DrawSphere(walkPoint, 5);
	}
}