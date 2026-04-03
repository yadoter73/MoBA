using UnityEngine;
using Zenject;
public class ChaseState : EnemyState
{
    public ChaseState(EnemyAI enemy, ArenaBounds arenaBounds, LayerMask whatIsGround, Animator animator) : base(enemy, arenaBounds, whatIsGround, animator) { }
    public override void UpdateState()
    {
        if (_enemy.playerInAttackRange)
        {
            _enemy.SwitchState(new LookingState(_enemy, _arenaBounds, _whatIsGround, _anim));   
            return;
        }
        if (!_enemy.playerInSightRange)
        {
            _enemy.SwitchState(new PatrolState(_enemy, _arenaBounds, _whatIsGround, _anim));
            return;
        }
        _enemy.MoveTo(_enemy.player.position);
    }
}
