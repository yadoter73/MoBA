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

    [SerializeField] private float _sightRange;
    [SerializeField] private float _attackRange;

    [Inject] private ArenaBounds _arenaBounds;

    private NavMeshAgent _agent;
    private EnemyState _currentState;

    public bool playerInSightRange, playerInAttackRange;

    private void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _currentState = new PatrolState(this, _arenaBounds, _whatIsGround);
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
        _agent.SetDestination(destination);
    }
    public Vector3 FindPoint()
    {
        if (Physics.Raycast(_arenaBounds.GetRandomPointInside() + Vector3.up * 50, Vector3.down, out var hit, 150f, _whatIsGround))
        {
            return walkPoint = hit.point;
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