using UnityEngine;
using UnityEngine.AI;
using Zenject;

public class EnemyAI : MonoBehaviour
{
    [Inject(Id = "PlayerTransform")] private Transform _player;
    [Inject(Id = "EnemyTransform")] private Transform _enemy;
    [Inject(Id = "NavMeshAgent")] private NavMeshAgent _agent;

    public LayerMask whatIsGround, whatIsPlayer;

    public Vector3 walkPoint;

    [SerializeField] private Health _health;

    public float EnemyDmg = 20f;

    [SerializeField] private float _walkPointRange;
    [SerializeField] private float _timeBetweenAttacks;
    [SerializeField] private float _sightRange, _attackRange;

    bool walkPointSet;
    bool alreadyAttacked;
    public bool playerInSightRange, playerInAttackRange;

    [SerializeField] private GameObject _particles;
    private void Start()
    {
        _health = FindAnyObjectByType<Health>();
    }
    private void FixedUpdate()
    {
        playerInSightRange = Physics.CheckSphere(transform.position, _sightRange, whatIsPlayer);
        playerInAttackRange = Physics.CheckSphere(transform.position, _attackRange, whatIsPlayer);

        if (!playerInSightRange && !playerInAttackRange) Patroling();
        if (playerInSightRange && !playerInAttackRange) ChasePlayer();
        if (playerInAttackRange && playerInSightRange) AttackPlayer();
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

        if (Physics.Raycast(walkPoint, -transform.up, 2f, whatIsGround))
            walkPointSet = true;
    }

    private void ChasePlayer()
    {
        _agent.SetDestination(_player.position);

    }

    private void AttackPlayer()
    {
        _agent.SetDestination(transform.position);

        transform.LookAt(_player);

        if (!alreadyAttacked)
        {
            float EnemyRotation = _enemy.eulerAngles.y - 35;
            GameObject gameobjectParticles = Instantiate(_particles, transform.position, Quaternion.Euler(0, EnemyRotation, 0));
            _health.isAttacked();
            Destroy(gameobjectParticles, 0.3f);
            alreadyAttacked = true;
            Invoke(nameof(ResetAttack), _timeBetweenAttacks);
        }
    }
    private void ResetAttack()
    {
        alreadyAttacked = false;
    }
    private void DestroyEnemy()
    {
        Destroy(gameObject);
    }
}




