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
    [SerializeField] private NavMeshAgent _agent;
    [SerializeField] private LayerMask WhatIsGround;
    [SerializeField] private ArenaBounds _arenaBounds;
    [SerializeField] private Animator _enemyAnim;
    [SerializeField] private TextRenaming _textRename;
    public override void InstallBindings()
    {
        Container.
            Bind<TextRenaming>().
            WithId("TextScript").
            FromInstance(_textRename).
            AsCached();
        Container.
            Bind<NPCConversation>().
            WithId("NpcConversation").
            FromInstance(_playerConversation).
            AsCached();

        Container.
            Bind<Animator>().
            WithId("EnemyAnimator").
            FromInstance(_enemyAnim).
            AsCached();

        Container.
            Bind<LayerMask>().
            WithId("Layer").
            FromInstance(WhatIsGround).
            AsCached();

        Container.
            Bind<Transform>().
            WithId("PlayerTransform").
            FromInstance(_player).
            AsCached();

        Container.
           Bind<NavMeshAgent>().
           WithId("Agent").
           FromInstance(_agent).
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
            Bind<ArenaBounds>().
            FromInstance(_arenaBounds).
            AsSingle().NonLazy();

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
