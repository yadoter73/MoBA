using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.AI;
using Zenject;

public class PatrolState : EnemyState
{
    private bool walkPointSet;

    private Vector3 walkPoint;

    private CancellationTokenSource _cts;

    public PatrolState(EnemyAI enemy, ArenaBounds arenaBounds, LayerMask whatIsGround) : base(enemy, arenaBounds, whatIsGround)
    { 
        _cts = new CancellationTokenSource();
        StartPatrol(_cts.Token).Forget(); 
    }
    public override void UpdateState()
    {
        if (_enemy.playerInSightRange)
        {
            Stop();
            _enemy.SwitchState(new ChaseState(_enemy, _arenaBounds, _whatIsGround));
            return;
        }
        if (walkPointSet)
        {
            float distance = Vector3.Distance(_enemy.transform.position, walkPoint);

            if (distance < 2f) { walkPointSet = false; }
        }
    }
    private void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
    private async UniTask StartPatrol(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                Vector3 point = _enemy.FindPoint();
                walkPoint = point;
				walkPointSet = true;
				_enemy.MoveTo(walkPoint);
                

                await UniTask.WaitUntil(() => walkPointSet == false);
                await UniTask.WaitForSeconds(3);
            }
        }
        catch (System.OperationCanceledException) { }
    }
        
}
