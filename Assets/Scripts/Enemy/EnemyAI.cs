using UnityEngine;
using UnityEngine.AI;
using Zenject;

public class EnemyAI : MonoBehaviour
{
    [Inject(Id = "PlayerTransform")] private Transform _player;
    [Inject] private Health _playerHealth;

    public LayerMask whatIsGround, whatIsPlayer;

    public Vector3 walkPoint;

    public float EnemyDmg = 20f;

    [SerializeField] private float _walkPointRange;
    [SerializeField] private float _timeBetweenAttacks;
    [SerializeField] private float _sightRange, _attackRange;

    bool walkPointSet;
    bool alreadyAttacked;
    public bool playerInSightRange, playerInAttackRange;

    private NavMeshAgent _agent;

    [SerializeField] private GameObject _particles;
    private void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
    }
    void Update()
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

        if (Physics.Raycast(walkPoint, Vector3.down, 2f, whatIsGround))
            walkPointSet = true;
    }

    private void ChasePlayer()
    {
        _agent.SetDestination(_player.position);
    }

    private void AttackPlayer()
    {
        _agent.SetDestination(transform.position);

        transform.LookAt(new Vector3(_player.position.x, transform.position.y, _player.position.z));

        if (!alreadyAttacked)
        {
            GameObject gameobjectParticles = Instantiate(_particles, transform.position, transform.rotation);
            _playerHealth.isAttacked(EnemyDmg);
            Destroy(gameobjectParticles, 0.3f);
            alreadyAttacked = true;
            Invoke(nameof(ResetAttack), _timeBetweenAttacks);
        }
    }
    private void ResetAttack()
    {
        alreadyAttacked = false;
    }
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, _attackRange);
    }
}




