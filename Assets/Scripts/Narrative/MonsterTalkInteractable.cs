using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DialogueEditor;
using KinematicCharacterController.Examples;
using MoBA.Player;
using PrimeTween;
using UnityEngine;
using Zenject;

namespace MoBA.Narrative
{
    /// <summary>
    /// Интерактивный диалог с розовым монстром, доступный после открытия калитки.
    /// </summary>
    public class MonsterTalkInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private NPCConversation _stubConversation;
        [SerializeField] private float _turnDuration = 0.45f;

        [Inject(Optional = true)] private KeyQuestService _questService;
        [Inject(Optional = true)] private IPlayerControlLock _playerControlLock;
        [Inject(Optional = true)] private ExampleCharacterController _playerController;

        private bool _isTalking;
        private Tween _turnTween;
        private CancellationTokenSource _talkCts;

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
            if (_isTalking) return;
            if (_questService == null || !_questService.CanTalkToMonster) return;

            CancelActiveTalk();
            _talkCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            RunDialogueRoutineAsync(_talkCts.Token).Forget();
        }

        private async UniTaskVoid RunDialogueRoutineAsync(CancellationToken token)
        {
            _isTalking = true;
            _questService?.NotifyMonsterTalkStarted();

            IDisposable controlLock = _playerControlLock?.AcquireLock(unlockCursor: true);
            bool conversationEnded = false;
            ConversationManager.ConversationEndEvent onEnded = () => conversationEnded = true;
            bool subscribed = false;

            try
            {
                await RotateTowardPlayerAsync(token);

                if (_stubConversation != null && ConversationManager.Instance != null)
                {
                    ConversationManager.OnConversationEnded += onEnded;
                    subscribed = true;
                    ConversationManager.Instance.StartConversation(_stubConversation);

                    while (!conversationEnded && !token.IsCancellationRequested)
                    {
                        if (!ConversationManager.Instance.IsConversationActive) break;
                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                    }
                }
                else
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(1.5f), cancellationToken: token);
                }
            }
            catch (OperationCanceledException)
            {
                if (ConversationManager.Instance != null && ConversationManager.Instance.IsConversationActive)
                {
                    ConversationManager.Instance.EndConversation();
                }
            }
            finally
            {
                if (subscribed)
                {
                    ConversationManager.OnConversationEnded -= onEnded;
                }
                controlLock?.Dispose();
                _isTalking = false;
            }
        }

        private async UniTask RotateTowardPlayerAsync(CancellationToken token)
        {
            if (_playerController == null) return;

            Vector3 dir = _playerController.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;

            float targetYaw = Quaternion.LookRotation(dir.normalized, Vector3.up).eulerAngles.y;
            Vector3 currentEuler = transform.rotation.eulerAngles;
            Quaternion targetRotation = Quaternion.Euler(currentEuler.x, targetYaw, currentEuler.z);

            _turnTween.Stop();
            _turnTween = Tween.Rotation(transform, targetRotation, _turnDuration, Ease.OutQuad);
            await _turnTween.ToUniTask(cancellationToken: token);
        }

        private void CancelActiveTalk()
        {
            _turnTween.Stop();
            if (_talkCts != null)
            {
                _talkCts.Cancel();
                _talkCts.Dispose();
                _talkCts = null;
            }
        }
    }
}
