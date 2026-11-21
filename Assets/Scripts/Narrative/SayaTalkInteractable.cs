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
    /// Интерактивные диалоги с сидящей Саей в MainHouse (этапы TalkToSaya и BringToSaya).
    /// </summary>
    public class SayaTalkInteractable : MonoBehaviour, IInteractable, IConditionalInteractionPrompt
    {
        [SerializeField] private NPCConversation _introConversation;
        [SerializeField] private NPCConversation _finalConversation;
        [SerializeField] private NPCConversation _repeatConversation;

        [Inject(Optional = true)] private KeyQuestService _questService;
        [Inject(Optional = true)] private IPlayerControlLock _playerControlLock;
        [Inject(Optional = true)] private ExampleCharacterController _playerController;

        private readonly DialogueSessionRunner _runner = new DialogueSessionRunner();
        private readonly OneTimeConversationGate _conversationGate = new OneTimeConversationGate();
        private CancellationTokenSource _talkCts;

        public InteractionPromptType PromptType => InteractionPromptType.Talk;
        public bool CanInteractNow => !_runner.IsRunning
            && _questService != null
            && _questService.IsQuestStarted
            && _questService.Stage != KeyQuestStage.Completed
            && _questService.Stage != KeyQuestStage.DinnerWithSaya;

        private void Start()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_questService == null)
            {
                Debug.LogError("[SayaTalkInteractable] KeyQuestService не внедрён через Zenject!", this);
            }
            if (_introConversation == null || _finalConversation == null)
            {
                Debug.LogError("[SayaTalkInteractable] Не назначены _introConversation / _finalConversation.", this);
            }
#endif
            if (_questService != null)
            {
                _questService.OnStageChanged += HandleQuestStageChanged;
                FitTalkHitboxToVisualBounds();
            }
        }

        private void OnDisable()
        {
            CancelActiveTalk();
        }

        private void OnDestroy()
        {
            if (_questService != null)
            {
                _questService.OnStageChanged -= HandleQuestStageChanged;
            }
            CancelActiveTalk();
        }

        private void HandleQuestStageChanged(KeyQuestStage stage)
        {
            FitTalkHitboxAfterPoseAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTaskVoid FitTalkHitboxAfterPoseAsync(CancellationToken token)
        {
            FitTalkHitboxToVisualBounds();
            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, token);
            FitTalkHitboxToVisualBounds();
        }

        /// <summary>
        /// Подгоняет BoxCollider хитбокса разговора по реальным границам сидящей модели (включая голову).
        /// </summary>
        private void FitTalkHitboxToVisualBounds()
        {
            Transform hitboxTf = transform.Find("SayaTalkHitbox");
            if (hitboxTf == null) return;

            BoxCollider talkBox = hitboxTf.GetComponent<BoxCollider>();
            if (talkBox == null) return;

            Renderer[] renderers = GetComponentsInChildren<Renderer>(false);
            if (renderers == null || renderers.Length == 0) return;

            bool hasBounds = false;
            Vector3 localMin = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 localMax = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || !r.enabled) continue;

                Bounds wb = r.bounds;
                Vector3 c = wb.center;
                Vector3 e = wb.extents;
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    for (int sy = -1; sy <= 1; sy += 2)
                    {
                        for (int sz = -1; sz <= 1; sz += 2)
                        {
                            Vector3 cornerWorld = new Vector3(c.x + sx * e.x, c.y + sy * e.y, c.z + sz * e.z);
                            Vector3 cornerLocal = hitboxTf.InverseTransformPoint(cornerWorld);
                            localMin = Vector3.Min(localMin, cornerLocal);
                            localMax = Vector3.Max(localMax, cornerLocal);
                            hasBounds = true;
                        }
                    }
                }
            }

            if (!hasBounds) return;

            // По высоте (Y) берём полные границы модели + небольшой запас над головой,
            // а ширину/глубину (X/Z) ограничиваем, чтобы T-pose AABB SkinnedMeshRenderer не раздувал хитбокс.
            float minY = localMin.y;
            float maxY = localMax.y + 0.15f;
            float height = Mathf.Clamp(maxY - minY, 1.35f, 2.4f);
            float centerY = (minY + maxY) * 0.5f;

            float width = Mathf.Clamp(localMax.x - localMin.x, 0.45f, 0.62f);
            float depth = Mathf.Clamp(localMax.z - localMin.z, 0.5f, 0.72f);

            talkBox.center = new Vector3((localMin.x + localMax.x) * 0.5f, centerY, (localMin.z + localMax.z) * 0.5f);
            talkBox.size = new Vector3(width, height, depth);
        }

        public void Interact(int id)
        {
            if (!CanInteractNow) return;

            bool wantIntro = _questService != null && _questService.CanTalkToSayaIntro;
            bool wantFinal = _questService != null && _questService.CanTalkToSayaFinal;

            DialogueBranchKind branch = _conversationGate.SelectConversation(
                transform,
                wantIntro,
                wantFinal,
                _introConversation,
                _finalConversation,
                _repeatConversation,
                "Saya",
                ResolveRepeatLineText,
                out NPCConversation targetConv);

            if (targetConv == null)
            {
                Debug.LogError("[SayaTalkInteractable] Не назначен NPCConversation для текущего этапа.", this);
                return;
            }

            CancelActiveTalk();
            _talkCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            _runner.RunAsync(
                transform,
                _playerController,
                _playerControlLock,
                targetConv,
                turnDuration: 0f,
                rotateNpc: false,
                onCompleted: () =>
                {
                    if (branch == DialogueBranchKind.Intro)
                    {
                        _questService?.NotifySayaIntroDialogueFinished();
                    }
                    else if (branch == DialogueBranchKind.Final)
                    {
                        _questService?.NotifySayaFinalDialogueFinished();
                    }
                },
                _talkCts.Token).Forget();
        }

        private string ResolveRepeatLineText()
        {
            if (_questService == null)
            {
                return "Please hurry to the gray store... and stay out of sight.";
            }

            switch (_questService.Stage)
            {
                case KeyQuestStage.FindKey:
                    return "Did you check the chest of drawers in this house? I hid the spare gate key inside one of them... please hurry.";
                case KeyQuestStage.GoToGate:
                    return "You found the key! Hurry to the gray grocery store past the gate and get the food... please be careful out there.";
                default:
                    return "Please hurry back with the vodka, three apples, and bread... I will wait right here.";
            }
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
