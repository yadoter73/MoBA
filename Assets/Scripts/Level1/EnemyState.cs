using UnityEngine;
using Zenject;
public abstract class EnemyState
{
	protected EnemyAI _enemy;
	protected ArenaBounds _arenaBounds;
	protected LayerMask _whatIsGround;

	public EnemyState(EnemyAI enemy, ArenaBounds arenaBounds, LayerMask whatIsGround)
	{ 
		_enemy = enemy;
		_arenaBounds = arenaBounds;
		_whatIsGround = whatIsGround;
	}

	public abstract void UpdateState();
}
