using UnityEngine;
using UnityEngine.AI;
using Zenject;

public class PatrolState : EnemyState
{
    private bool walkPointSet;

    private float _walkPointRange = 20f;

    private Vector3 walkPoint;

    [Inject(Id = "Agent")] private NavMeshAgent _agent;
    [Inject(Id = "Layer")] private LayerMask whatIsGround;

    [Inject] private ArenaBounds _arenaBounds;

    public PatrolState(EnemyAI enemy) : base(enemy)
    {
        Patrolling();
    }
    public override void UpdateState()
    {

    }
    private void Patrolling()
    {
        float randomZ = Random.Range(-_walkPointRange, _walkPointRange);
        float randomX = Random.Range(-_walkPointRange, _walkPointRange);

        walkPoint = new Vector3(_agent.transform.position.x + randomX, _agent.transform.position.y, _agent.transform.position.z + randomZ);

        if (Physics.Raycast(_arenaBounds.GetRandomPointInside(), Vector3.down, out var hit, 100f, whatIsGround))
        {
            walkPointSet = true;
        }
    }
}
