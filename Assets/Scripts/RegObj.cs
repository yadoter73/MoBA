using UnityEngine;
using Zenject;
using UnityEngine.AI;
using KinematicCharacterController.Examples;
public class RegObj : MonoInstaller
{
    [SerializeField] private Transform _player;
    [SerializeField] private ExamplePlayer _examPlayer;
    [SerializeField] private ExampleCharacterController _exampleCharacterController;
    public override void InstallBindings()
    {
        Container.Bind<Transform>().WithId("PlayerTransform").FromInstance(_player).AsCached();
        Container.Bind<ExamplePlayer>().FromInstance(_examPlayer).AsSingle();
        Container.Bind<ExampleCharacterController>().FromInstance(_exampleCharacterController).AsSingle();
        Container.BindInterfacesAndSelfTo<InputMovementController>().AsSingle().NonLazy();
    }

}
