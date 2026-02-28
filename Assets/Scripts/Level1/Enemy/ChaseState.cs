using UnityEngine;
using Zenject;
public class ChaseState : EnemyState
{
    public ChaseState(EnemyAI enemy, ArenaBounds arenaBounds, LayerMask whatIsGround) : base(enemy, arenaBounds, whatIsGround) { }
    public override void UpdateState()
    {
        if (_enemy.playerInAttackRange)
        {
            _enemy.SwitchState(new LookingState(_enemy, _arenaBounds, _whatIsGround));   
            return;
        }
        if (!_enemy.playerInSightRange)
        {
            _enemy.SwitchState(new PatrolState(_enemy, _arenaBounds, _whatIsGround));
            return;
        }
        _enemy.MoveTo(_enemy.player.position);
    }
}
