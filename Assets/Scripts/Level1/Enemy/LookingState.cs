using UnityEngine;
using PrimeTween;

public class LookingState : EnemyState
{
	private const float StareDuration = 2.4f;
	private readonly float enemyRotation = 6f;
	private bool _isTransitioning = false;
	private Tween _delayTween;

	public LookingState(EnemyAI enemy, ArenaBounds arenaBounds, LayerMask whatIsGround, Animator animator)
		: base(enemy, arenaBounds, whatIsGround, animator)
	{
		_enemy.playerWaitingToLeave = false;
		// При остановке рядом с игроком сразу включаем анимацию стояния на всех LOD
		_enemy.StopMoving();
	}

	public override void UpdateState()
	{
		if (PlayerDeathHandler.IsDead || _enemy.player == null)
		{
			_delayTween.Stop();
			_enemy.StopMoving();
			return;
		}

		float dist = _enemy.HorizontalDistanceToPlayer();

		// Игрок подошёл вплотную — мгновенная атака и убийство
		if (dist <= _enemy.ImmediateKillRange)
		{
			_delayTween.Stop();
			_enemy.SwitchState(new AttackState(_enemy, _arenaBounds, _whatIsGround, _anim));
			return;
		}

		// Игрок вырвался из зоны предупреждения — продолжаем погоню
		if (!_enemy.playerInAttackRange && dist > _enemy.AttackRange + 1.5f)
		{
			_delayTween.Stop();
			_enemy.SwitchState(new ChaseState(_enemy, _arenaBounds, _whatIsGround, _anim));
			return;
		}

		_enemy.SetMoving(false, false);
		_enemy.FacePlayerSmoothly(enemyRotation);

		if (!_isTransitioning)
		{
			_isTransitioning = true;
			_delayTween = Tween.Delay(StareDuration).OnComplete(() =>
			{
				if (_enemy == null || PlayerDeathHandler.IsDead) return;
				_enemy.playerWaitingToLeave = true;

				float currentDist = _enemy.HorizontalDistanceToPlayer();
				// Если игрок не убежал за время предупреждения — монстр атакует и убивает его
				if (currentDist <= _enemy.AttackRange + 1.5f)
				{
					_enemy.SwitchState(new AttackState(_enemy, _arenaBounds, _whatIsGround, _anim));
				}
				else
				{
					_enemy.SwitchState(new GetRidOfPlayer(_enemy, _arenaBounds, _whatIsGround, _anim));
				}
			});
		}
	}

	public override void ExitState()
	{
		_delayTween.Stop();
	}
}
