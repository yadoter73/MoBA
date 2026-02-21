using UnityEngine;
using UnityEngine.AI;
using PrimeTween;
using Zenject;
using System.Collections;
using Cysharp.Threading.Tasks;
public class EnemyAI : MonoBehaviour
{
    public LayerMask whatIsGround, whatIsPlayer;
    public Vector3 walkPoint;

    [Inject(Id = "PlayerTransform")] private Transform _player;

    [SerializeField] private float _sightRange;
    [SerializeField] private float _attackRange;
    [SerializeField] private float _walkPointRange;
	[SerializeField] private float _enemyRotation;

	[SerializeField] private ArenaBounds _arenaBounds;
   
    private NavMeshAgent _agent;
    private EnemyState _currentState;

    private bool walkPointSet;
    private bool playerInSightRange, playerInAttackRange;

    private void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
    }
    private void FixedUpdate()
    {
        playerInSightRange = Physics.CheckSphere(transform.position, _sightRange, whatIsPlayer);
        playerInAttackRange = Physics.CheckSphere(transform.position, _attackRange, whatIsPlayer);

        if (!playerInSightRange && !playerInAttackRange) Patroling();
        if (playerInSightRange && !playerInAttackRange) Chasing();
        if (playerInSightRange && playerInAttackRange) LookAtPlayer();
    }

    private void Patroling()
    {
        if (!walkPointSet) SearchWalkPoint();

        if (walkPointSet)
            _agent.SetDestination(walkPoint);

        Vector3 distanceToWalkPoint = transform.position - walkPoint;

        if (distanceToWalkPoint.magnitude < 1f)
            walkPointSet = false;
    }
    private void SearchWalkPoint()
    {
        float randomZ = Random.Range(-_walkPointRange, _walkPointRange);
        float randomX = Random.Range(-_walkPointRange, _walkPointRange);

        walkPoint = new Vector3(transform.position.x + randomX, transform.position.y, transform.position.z + randomZ);

        if (Physics.Raycast(_arenaBounds.GetRandomPointInside(), Vector3.down, out var hit, 100f, whatIsGround))
        {
            walkPointSet = true;
        }
    }
    private void LookAtPlayer()
    {
		Vector3 direction = (_player.position - transform.position).normalized;
		Quaternion targetRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _enemyRotation * Time.deltaTime);
    }
    private void Chasing()
    {
        _agent.SetDestination(_player.position);
    }
    private async UniTask Waiting()
    {

    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _sightRange);

        Gizmos.DrawSphere(walkPoint, 5);
    }
}