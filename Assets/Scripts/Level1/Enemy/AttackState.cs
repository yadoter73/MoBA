using UnityEngine;

public class AttackState : EnemyState
{
    private const float DesiredBiteDistance = 1.9f;
    private const float MaxLungeDuration = 0.32f;

    private float _elapsed;
    private bool _hasStruck;

    public AttackState(EnemyAI enemy, ArenaBounds arenaBounds, LayerMask whatIsGround, Animator animator)
        : base(enemy, arenaBounds, whatIsGround, animator)
    {
        _enemy.SetChaseSpeed();
        _enemy.TriggerRoar();
    }

    public override void UpdateState()
    {
        if (_enemy == null || _enemy.player == null) return;

        _elapsed += Time.fixedDeltaTime;
        _enemy.FacePlayerSmoothly(14f);

        float dist = _enemy.HorizontalDistanceToPlayer();

        // Короткий выпад к игроку, чтобы удар пришёлся прямо перед камерой
        if (!_hasStruck && dist > DesiredBiteDistance && _elapsed < MaxLungeDuration)
        {
            _enemy.MoveTo(_enemy.player.position, chasing: true);
            return;
        }

        if (!_hasStruck)
        {
            _hasStruck = true;
            _enemy.StopMoving();
            _enemy.TriggerAttack();
            PlayerDeathHandler.TriggerPlayerDeath(_enemy);
        }
    }
}
