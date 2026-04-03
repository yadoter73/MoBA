using UnityEngine;
using Zenject;
public abstract class EnemyState
{
	protected EnemyAI _enemy;
	protected ArenaBounds _arenaBounds;
	protected LayerMask _whatIsGround;
	protected Animator _anim;

	public EnemyState(EnemyAI enemy, ArenaBounds arenaBounds, LayerMask whatIsGround, Animator animator)
	{
		_anim = animator;
		_enemy = enemy;
		_arenaBounds = arenaBounds;
		_whatIsGround = whatIsGround;
	}

	public abstract void UpdateState();
}
