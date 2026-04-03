using UnityEngine;
using PrimeTween;
public class GetRidOfPlayer : EnemyState
{
	private bool _isWaiting = false;
	private Vector3 point;
	public GetRidOfPlayer(EnemyAI enemy, ArenaBounds arenaBounds, LayerMask whatIsGround, Animator animator) : base(enemy, arenaBounds, whatIsGround, animator) 
	{
		point = _enemy.FindPoint();
		_enemy.playerWaitingToLeave = false;
	}
	public override void UpdateState()
	{
		_enemy.MoveTo(point);

		float distance = Vector3.Distance(_enemy.transform.position, point);
		if (distance < 2f && !_isWaiting)
		{
			_isWaiting = true;
			Tween.Delay(4).OnComplete(() =>
			{ 
				_enemy.SwitchState(new PatrolState(_enemy, _arenaBounds, _whatIsGround,_anim));
				_enemy.playerWaitingToLeave = false;
			});

		}
	}

}
