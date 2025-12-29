using UnityEngine;
using Zenject;
using UnityEngine.AI;

public class RegObj : MonoInstaller
{
    [SerializeField] private Transform _player;
    [SerializeField] private Health _playerHealth;
    [SerializeField] private EnemyAI _enemyAI;
    public override void InstallBindings()
    {
        Container.Bind<Transform>().WithId("PlayerTransform").FromInstance(_player).AsCached();
        Container.Bind<Health>().FromInstance(_playerHealth).AsSingle();
        Container.Bind<EnemyAI>().FromInstance(_enemyAI).AsSingle();
    }
}
