using UnityEngine;
using UnityEngine.AI;
using PrimeTween;
using Zenject;
using System.Collections;
public class EnemyAI : MonoBehaviour
{
    public LayerMask whatIsGround;
    public Vector3 walkPoint;

    [SerializeField] private float _walkPointRange;
    [SerializeField] private float _sightRange;
    [SerializeField] private ArenaBounds _arenaBounds;

    private Animator _anim;
    private NavMeshAgent _agent;

    private bool walkPointSet;
    private bool playerInSightRange;

    private void Start()
    {
        _anim = GetComponentInChildren<Animator>();
        _agent = GetComponent<NavMeshAgent>();
    }
    private void FixedUpdate()
    {
        playerInSightRange = Physics.CheckSphere(transform.position, _sightRange);

        if (!playerInSightRange) Patroling();
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
        int maxAttempts = 30;

        for (int i = 0; i < maxAttempts; i++)
        {
            walkPoint = _arenaBounds.GetRandomPointInside();

            if (NavMesh.SamplePosition(walkPoint, out NavMeshHit hitnav, 2f, NavMesh.AllAreas))
            {
                NavMeshPath path = new NavMeshPath();

                if (_agent.CalculatePath(hitnav.position, path))
                {
                    if (path.status == NavMeshPathStatus.PathComplete || path.status == NavMeshPathStatus.PathPartial)
                    {
                        walkPoint = hitnav.position;
                        walkPointSet = true;
                        return;
                    }
                }
            }
        }

        walkPointSet = false;
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _sightRange);

        Gizmos.DrawSphere(walkPoint, 5);
    }
}