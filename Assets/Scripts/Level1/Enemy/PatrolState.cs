using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.AI;

public class PatrolState : EnemyState
{
    private bool walkPointSet;
    private Vector3 walkPoint;
    private float _travelElapsed;
    private float _stuckTimer;
    private Vector3 _lastProgressPos;

    private CancellationTokenSource _cts;

    public PatrolState(EnemyAI enemy, ArenaBounds arenaBounds, LayerMask whatIsGround, Animator animator)
        : base(enemy, arenaBounds, whatIsGround, animator)
    {
        _cts = new CancellationTokenSource();
        StartPatrol(_cts.Token).Forget();
    }

    public override void UpdateState()
    {
        if (_enemy.playerInSightRange)
        {
            _enemy.SwitchState(new ChaseState(_enemy, _arenaBounds, _whatIsGround, _anim));
            return;
        }

        if (!walkPointSet) return;

        float dt = Time.fixedDeltaTime;
        _travelElapsed += dt;

        Vector3 enemyPos = _enemy.transform.position;
        float dx = enemyPos.x - walkPoint.x;
        float dz = enemyPos.z - walkPoint.z;
        float distanceXZ = Mathf.Sqrt(dx * dx + dz * dz);

        bool agentArrived = _enemy.HasValidPath &&
                            !_enemy.IsPathPending &&
                            _enemy.RemainingDistance <= Mathf.Max(_enemy.StoppingDistance, 2.5f);

        bool pathBroken = _enemy.HasValidPath &&
                          !_enemy.IsPathPending &&
                          _enemy.PathStatus != NavMeshPathStatus.PathComplete &&
                          _travelElapsed > 1.2f;

        if ((enemyPos - _lastProgressPos).sqrMagnitude > 0.25f)
        {
            _lastProgressPos = enemyPos;
            _stuckTimer = 0f;
        }
        else if (_travelElapsed > 1.0f && _enemy.AgentSpeedSqr < 0.04f)
        {
            _stuckTimer += dt;
        }

        if (distanceXZ < 2.8f || agentArrived || pathBroken || _stuckTimer > 2.5f || _travelElapsed > 16f)
        {
            walkPointSet = false;
            _enemy.StopMoving();
        }
    }

    public override void ExitState()
    {
        Stop();
    }

    private void Stop()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }

    private async UniTask StartPatrol(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && _enemy != null)
            {
                if (PlayerDeathHandler.IsDead)
                {
                    _enemy.StopMoving();
                    await UniTask.Yield(token);
                    continue;
                }

                _enemy.SetPatrolSpeed();
                Vector3 point = _enemy.FindPoint();
                walkPoint = point;
                _travelElapsed = 0f;
                _stuckTimer = 0f;
                _lastProgressPos = _enemy.transform.position;
                walkPointSet = true;
                _enemy.MoveTo(walkPoint, chasing: false);

                await UniTask.WaitUntil(() => !walkPointSet || _enemy == null, cancellationToken: token);
                if (_enemy != null) _enemy.StopMoving();
                await UniTask.Delay(System.TimeSpan.FromSeconds(Random.Range(1.8f, 3.4f)), cancellationToken: token);
            }
        }
        catch (System.OperationCanceledException) { }
    }
}
