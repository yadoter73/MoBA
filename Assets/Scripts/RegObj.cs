using UnityEngine;
using Zenject;
using UnityEngine.AI;

public class RegObj : MonoInstaller
{
    [SerializeField] private Transform _player;
    [SerializeField] private Transform _enemy;
    [SerializeField] private NavMeshAgent _agent;
    public override void InstallBindings()
    {
        Container.Bind<Transform>().WithId("PlayerTransform").FromInstance(_player).AsCached();
        Container.Bind<Transform>().WithId("EnemyTransform").FromInstance(_enemy).AsCached();
        Container.Bind<NavMeshAgent>().WithId("NavMeshAgent").FromInstance(_agent).AsCached();
    }
}
