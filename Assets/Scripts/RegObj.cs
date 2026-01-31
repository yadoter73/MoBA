using UnityEngine;
using Zenject;
using UnityEngine.AI;
using KinematicCharacterController.Examples;
using DialogueEditor;
using System.ComponentModel;
public class RegObj : MonoInstaller
{
    [SerializeField] private Transform _player;
    [SerializeField] private ExamplePlayer _examPlayer;
	[SerializeField] private ExampleCharacterController _exampleCharacterController;
    [SerializeField] private NPCConversation _playerConversation;
    [SerializeField] private Transform _playerHead;
    [SerializeField] private InteractionManager _interManager;
    public override void InstallBindings()
    {
        Container.
            Bind<NPCConversation>().
            WithId("NpcConversation").
            FromInstance(_playerConversation).
            AsCached();

        Container.
            Bind<Transform>().
            WithId("PlayerTransform").
            FromInstance(_player).
            AsCached();

        Container.
            Bind<ExamplePlayer>().
            FromInstance(_examPlayer).
            AsSingle();
        Container.
            Bind<InteractionManager>().
            FromInstance(_interManager).
            AsSingle();

        Container.
            Bind<ExampleCharacterController>().
            FromInstance(_exampleCharacterController).
            AsSingle();

        Container.
            BindInterfacesAndSelfTo<InputMovementController>().
            AsSingle().
            NonLazy();

        Container.Bind<Transform>().
            WithId("HeadTransform").
            FromInstance(_playerHead).
            AsSingle();
    }

}
