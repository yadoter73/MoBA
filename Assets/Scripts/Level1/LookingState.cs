using Cysharp.Threading.Tasks;
using UnityEngine;

public class LookingState : EnemyState
{
    private float enemyRotation = 5f;
    public LookingState(EnemyAI enemy, ArenaBounds arenaBounds, LayerMask whatIsGround) : base(enemy, arenaBounds, whatIsGround) { }
    public override void UpdateState()
    {
        if (!_enemy.playerInAttackRange)
        {
            _enemy.SwitchState(new ChaseState(_enemy, _arenaBounds, _whatIsGround));
            return;
        }
        _enemy.MoveTo(_enemy.transform.position);
        Vector3 direction = (_enemy.player.position - _enemy.transform.position).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
        _enemy.transform.rotation = Quaternion.Slerp(_enemy.transform.rotation, targetRotation, enemyRotation * Time.deltaTime);
        UniTask.Delay(10000).ContinueWith(() => new GetRidOfPlayer(_enemy, _arenaBounds, _whatIsGround)).Forget();
    }
}
