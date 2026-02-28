using UnityEngine;
using PrimeTween;
public class GetRidOfPlayer : EnemyState
{
    public GetRidOfPlayer(EnemyAI enemy, ArenaBounds arenaBounds, LayerMask whatIsGround) : base(enemy, arenaBounds, whatIsGround) { }
    public override void UpdateState()
    {
        Vector3 point = _enemy.FindPoint();
        _enemy.MoveTo(point);
		float distance = Vector3.Distance(_enemy.transform.position, point);

		if (distance < 2f) 
        {
            Tween.Delay(3).OnComplete(() => new PatrolState(_enemy, _arenaBounds, _whatIsGround)); 
        }
	}
    
}
