using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using PrimeTween;
using Zenject;
public class EnemyAI : MonoBehaviour
{
    private NavMeshAgent _agent;
    [Inject(Id = "PlayerTransform")] private Transform _player;
    public LayerMask whatIsPlayer;

    public Vector3 walkPoint;

    [SerializeField] private float _walkPointRange;
    [SerializeField] private float _sightRange;
    [SerializeField] private ArenaBounds _arenaBounds;
    [SerializeField] private Animator _anim;
    private bool walkPointSet;
    private bool playerInSightRange;

    private void Start()
    {
        _anim = GetComponentInChildren<Animator>();
        _agent = GetComponent<NavMeshAgent>();
    }
    private void FixedUpdate()
    {
        playerInSightRange = Physics.CheckSphere(transform.position, _sightRange, whatIsPlayer);

        if (!playerInSightRange) Patroling();
        if (playerInSightRange) ChasePlayer();
    }

    private void Patroling()
    {
        _anim.SetBool("Walk", true);
        if (!walkPointSet) SearchWalkPoint();

        if (walkPointSet)
            _agent.SetDestination(walkPoint);

        Vector3 distanceToWalkPoint = transform.position - walkPoint;

        if (distanceToWalkPoint.magnitude < 1f)
            walkPointSet = false;
    }
    private void SearchWalkPoint()
    {
        _anim.SetBool("Walk", true);
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

    private void ChasePlayer()
    {
        _anim.SetBool("Walk", true);
        _agent.SetDestination(_player.position);

    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _sightRange);

        Gizmos.DrawSphere(walkPoint, 5);
    }
}