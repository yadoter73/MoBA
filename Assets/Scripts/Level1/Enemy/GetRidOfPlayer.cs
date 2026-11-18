using UnityEngine;
using PrimeTween;

public class GetRidOfPlayer : EnemyState
{
	private bool _isWaiting = false;
	private float _travelElapsed;
	private Tween _waitTween;
	private Vector3 point;

	public GetRidOfPlayer(EnemyAI enemy, ArenaBounds arenaBounds, LayerMask whatIsGround, Animator animator)
		: base(enemy, arenaBounds, whatIsGround, animator)
	{
		point = _enemy.FindPoint();
		_enemy.playerWaitingToLeave = false;
		_enemy.SetPatrolSpeed();
		_enemy.MoveTo(point, chasing: false);
	}

	public override void UpdateState()
	{
		if (PlayerDeathHandler.IsDead)
		{
			_waitTween.Stop();
			_enemy.StopMoving();
			return;
		}
		if (_isWaiting) return;

		_travelElapsed += Time.fixedDeltaTime;
		_enemy.MoveTo(point, chasing: false);

		Vector3 pos = _enemy.transform.position;
		float dx = pos.x - point.x;
		float dz = pos.z - point.z;
		float distanceXZ = Mathf.Sqrt(dx * dx + dz * dz);

		bool arrived = distanceXZ < 2.8f ||
		               (_enemy.HasValidPath && !_enemy.IsPathPending && _enemy.RemainingDistance <= 2.5f) ||
		               _travelElapsed > 10f;

		if (arrived)
		{
			_isWaiting = true;
			_enemy.StopMoving();
			_waitTween = Tween.Delay(3.5f).OnComplete(() =>
			{
				if (_enemy == null) return;
				_enemy.playerWaitingToLeave = false;
				_enemy.SwitchState(new PatrolState(_enemy, _arenaBounds, _whatIsGround, _anim));
			});
		}
	}

	public override void ExitState()
	{
		_waitTween.Stop();
	}
}
