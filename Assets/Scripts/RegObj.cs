using DialogueEditor;
using KinematicCharacterController.Examples;
using MoBA.Audio;
using MoBA.Narrative;
using MoBA.Player;
using Unity.Cinemachine;
using UnityEngine;
using Zenject;

public class RegObj : MonoInstaller
{
    [SerializeField] private Transform _player;
    [SerializeField] private ExamplePlayer _examPlayer;
    [SerializeField] private ExampleCharacterController _exampleCharacterController;
    [SerializeField] private NPCConversation _playerConversation;
    [SerializeField] private Transform _playerHead;
    [SerializeField] private InteractionManager _interManager;
    [SerializeField] private LayerMask WhatIsGround;
    [SerializeField] private ArenaBounds _arenaBounds;
    [SerializeField] private TextRenaming _textRename;

    [Header("Key Quest & Player Control")]
    [SerializeField] private Stone _stone;
    [SerializeField] private LineToSaya _lineToSaya;
    [SerializeField] private PointerInmpulse _pointerImpulse;
    [SerializeField] private Transform _gateGuidanceTarget;
    [SerializeField] private Transform _sayaGuidanceTarget;
    [SerializeField] private CinemachineInputAxisController _cameraInputAxisController;

    public override void InstallBindings()
    {
        if (_textRename != null)
        {
            Container.Bind<TextRenaming>().WithId("TextScript").FromInstance(_textRename).AsCached();
            Container.Bind<TextRenaming>().FromInstance(_textRename).AsSingle();
        }

        if (_playerConversation != null)
        {
            Container.Bind<NPCConversation>().WithId("NpcConversation").FromInstance(_playerConversation).AsCached();
        }

        Container.Bind<LayerMask>().WithId("Layer").FromInstance(WhatIsGround).AsCached();

        if (_player != null)
        {
            Container.Bind<Transform>().WithId("PlayerTransform").FromInstance(_player).AsCached();
        }

        if (_examPlayer != null)
        {
            Container.Bind<ExamplePlayer>().FromInstance(_examPlayer).AsSingle();
        }

        if (_interManager != null)
        {
            Container.Bind<InteractionManager>().FromInstance(_interManager).AsSingle();
        }

        if (_arenaBounds != null)
        {
            Container.Bind<ArenaBounds>().FromInstance(_arenaBounds).AsSingle().NonLazy();
        }

        if (_exampleCharacterController != null)
        {
            Container.Bind<ExampleCharacterController>().FromInstance(_exampleCharacterController).AsSingle();
            Container.BindInterfacesAndSelfTo<PlayerControlService>()
                .FromInstance(new PlayerControlService(_exampleCharacterController, _cameraInputAxisController))
                .AsSingle();

            if (_examPlayer != null)
            {
                Container.BindInterfacesAndSelfTo<InputMovementController>().AsSingle().NonLazy();
                Container.BindInterfacesAndSelfTo<StaminaService>().AsSingle().NonLazy();
            }

            PlayerFootstepAudio footstepAudio = _exampleCharacterController.GetComponent<PlayerFootstepAudio>();
            if (footstepAudio != null)
            {
                Container.Bind<PlayerFootstepAudio>().FromInstance(footstepAudio).AsSingle();
            }
        }

        if (_playerHead != null)
        {
            Container.Bind<Transform>().WithId("HeadTransform").FromInstance(_playerHead).AsSingle();
        }

        InstallKeyQuestBindings();
    }

    private void InstallKeyQuestBindings()
    {
        if (_stone == null)
        {
            if (_player != null)
            {
                Debug.LogError("[RegObj] Для квеста ключа не назначен Stone.", this);
            }
            return;
        }

        if (_textRename == null || _lineToSaya == null || _pointerImpulse == null || _gateGuidanceTarget == null)
        {
            Debug.LogError("[RegObj] Не все ссылки квеста ключа назначены (запустите MoBA/Setup Key Quest).", this);
        }

        Transform sayaTarget = _sayaGuidanceTarget != null
            ? _sayaGuidanceTarget
            : (_stone != null && _stone.transform != null ? _stone.transform : null);
        var questConfig = new KeyQuestConfig(_gateGuidanceTarget, sayaTarget);
        var groceryList = new GroceryList();
        var questService = new KeyQuestService(_stone, _textRename, _lineToSaya, _pointerImpulse, groceryList, questConfig);

        Container.Bind<Stone>().FromInstance(_stone).AsSingle();
        if (_lineToSaya != null) Container.Bind<LineToSaya>().FromInstance(_lineToSaya).AsSingle();
        if (_pointerImpulse != null) Container.Bind<PointerInmpulse>().FromInstance(_pointerImpulse).AsSingle();
        Container.Bind<GroceryList>().FromInstance(groceryList).AsSingle();
        Container.Bind<KeyQuestConfig>().FromInstance(questConfig).AsSingle();
        Container.BindInterfacesAndSelfTo<KeyQuestService>().FromInstance(questService).AsSingle().NonLazy();
    }
}
