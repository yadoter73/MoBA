using UnityEngine;
using UnityEngine.AI;
using PrimeTween;
using Zenject;
using System.Collections;
public class EnemyAI : MonoBehaviour
{
    public LayerMask whatIsGround;
    public Vector3 walkPoint;

    [Inject(Id = "PlayerTransform")] private Transform _player;
    
    [SerializeField] private float _sightRange;
    [SerializeField] private float _attackRange;
    [SerializeField] private float _walkPointRange;

    private Animator _anim;
    private NavMeshAgent _agent;

    private bool walkPointSet;
    private bool playerInSightRange, playerInAttackRange;

    private void Start()
    {
        _anim = GetComponentInChildren<Animator>();
        _agent = GetComponent<NavMeshAgent>();
    }
    private void FixedUpdate()
    {
        playerInSightRange = Physics.CheckSphere(transform.position, _sightRange);
        playerInAttackRange = Physics.CheckSphere(transform.position, _attackRange);

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

        if (Physics.Raycast(walkPoint, -transform.up, 2f, whatIsGround)) walkPointSet = true;
        
    }
    private void LookAtPlayer()
    {
        transform.LookAt(_player);

    }
    private void Chasing()
    {
        _agent.SetDestination(_player.position);
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _sightRange);

        Gizmos.DrawSphere(walkPoint, 5);
    }
}