using UnityEngine;

public class ChaseState : EnemyState
{
    public ChaseState(EnemyAI enemy, ArenaBounds arenaBounds, LayerMask whatIsGround, Animator animator)
        : base(enemy, arenaBounds, whatIsGround, animator)
    {
        _enemy.SetChaseSpeed();
    }

    public override void UpdateState()
    {
        if (PlayerDeathHandler.IsDead || _enemy.player == null)
        {
            _enemy.StopMoving();
            return;
        }

        float dist = _enemy.HorizontalDistanceToPlayer();
        if (dist <= _enemy.ImmediateKillRange)
        {
            _enemy.SwitchState(new AttackState(_enemy, _arenaBounds, _whatIsGround, _anim));
            return;
        }

        if (_enemy.playerInAttackRange)
        {
            _enemy.SwitchState(new LookingState(_enemy, _arenaBounds, _whatIsGround, _anim));
            return;
        }

        if (!_enemy.playerInSightRange && dist > _enemy.SightRange * 1.25f)
        {
            _enemy.SwitchState(new PatrolState(_enemy, _arenaBounds, _whatIsGround, _anim));
            return;
        }

        _enemy.SetChaseSpeed();
        _enemy.MoveTo(_enemy.player.position, chasing: true);
    }
}
