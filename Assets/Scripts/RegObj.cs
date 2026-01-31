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
            Bind<ExampleCharacterController>().
            FromInstance(_exampleCharacterController).
            AsSingle();

        Container.
            BindInterfacesAndSelfTo<InputMovementController>().
            AsSingle().
            NonLazy();
    }

}
