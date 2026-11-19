using System.Threading;
using Cysharp.Threading.Tasks;
using DialogueEditor;
using KinematicCharacterController.Examples;
using MoBA.Interaction;
using MoBA.Player;
using UnityEngine;
using Zenject;

namespace MoBA.Narrative
{
    /// <summary>
    /// Интерактивный диалог с розовым монстром-продавцом (выдача заказа и повторная реплика).
    /// </summary>
    public class MonsterTalkInteractable : MonoBehaviour, IInteractable, IConditionalInteractionPrompt
    {
        [SerializeField] private NPCConversation _stubConversation;
        [SerializeField] private NPCConversation _repeatConversation;
        [SerializeField] private float _turnDuration = 0.45f;

        [Inject(Optional = true)] private KeyQuestService _questService;
        [Inject(Optional = true)] private IPlayerControlLock _playerControlLock;
        [Inject(Optional = true)] private ExampleCharacterController _playerController;

        private readonly DialogueSessionRunner _runner = new DialogueSessionRunner();
        private readonly OneTimeConversationGate _conversationGate = new OneTimeConversationGate();
        private CancellationTokenSource _talkCts;

        public InteractionPromptType PromptType => InteractionPromptType.Talk;
        public bool CanInteractNow => !_runner.IsRunning && _questService != null && _questService.CanTalkToMonster;
        public bool CanTalkNow => CanInteractNow;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Start()
        {
            if (_questService == null)
            {
                Debug.LogError("[MonsterTalkInteractable] KeyQuestService не внедрён через Zenject!", this);
            }
        }
#endif

        private void OnDisable()
        {
            CancelActiveTalk();
        }

        private void OnDestroy()
        {
            CancelActiveTalk();
        }

        public void Interact(int id)
        {
            if (!CanInteractNow) return;

            bool wantInitialOrder = !_questService.IsMonsterOrderCompleted;
            DialogueBranchKind branch = _conversationGate.SelectConversation(
                transform,
                wantInitialOrder,
                wantFinal: false,
                _stubConversation,
                finalConversation: null,
                _repeatConversation,
                "Shopkeeper",
                () => "...You already have my wares... Go on, hurry back to her...",
                out NPCConversation targetConv);
            if (targetConv == null) return;

            bool isInitialOrder = branch == DialogueBranchKind.Intro;
            if (isInitialOrder)
            {
                _questService.NotifyMonsterTalkStarted();
            }

            CancelActiveTalk();
            _talkCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            _runner.RunAsync(
                transform,
                _playerController,
                _playerControlLock,
                targetConv,
                _turnDuration,
                rotateNpc: true,
                onCompleted: () =>
                {
                    if (isInitialOrder)
                    {
                        _questService?.NotifyMonsterOrderDialogueFinished();
                    }
                },
                _talkCts.Token).Forget();
        }

        private void CancelActiveTalk()
        {
            if (_talkCts != null)
            {
                _talkCts.Cancel();
                _talkCts.Dispose();
                _talkCts = null;
            }
        }
    }
}
