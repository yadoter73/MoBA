using DialogueEditor;
using UnityEngine;
using Zenject;

public class EnemyInstaller : MonoInstaller
{
	[SerializeField] private Animator _enemyAnim;

	public override void InstallBindings()
	{
		Container.Bind<Animator>().
			WithId("EnemyAnim").
			FromInstance(_enemyAnim).
			AsCached();
	}
}