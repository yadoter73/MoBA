using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DialogueEditor;
using KinematicCharacterController.Examples;
using MoBA.Player;
using PrimeTween;
using UnityEngine;

namespace MoBA.Narrative
{
    /// <summary>
    /// Общий раннер диалогов для NPC: блокирует игрока, поворачивает NPC к игроку и ждёт завершения ConversationManager.
    /// </summary>
    public sealed class DialogueSessionRunner
    {
        public bool IsRunning { get; private set; }

        public async UniTask RunAsync(
            Transform npcTransform,
            ExampleCharacterController playerController,
            IPlayerControlLock playerControlLock,
            NPCConversation conversation,
            float turnDuration,
            bool rotateNpc,
            Action onCompleted,
            CancellationToken token)
        {
            if (IsRunning || conversation == null) return;
            IsRunning = true;

            IDisposable controlLock = playerControlLock?.AcquireLock(unlockCursor: true);
            bool conversationEnded = false;
            ConversationManager.ConversationEndEvent onEnded = () => conversationEnded = true;
            bool subscribed = false;
            Tween turnTween = default;

            try
            {
                if (rotateNpc && npcTransform != null && playerController != null)
                {
                    Vector3 dir = playerController.transform.position - npcTransform.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude >= 0.001f)
                    {
                        float targetYaw = Quaternion.LookRotation(dir.normalized, Vector3.up).eulerAngles.y;
                        Vector3 euler = npcTransform.rotation.eulerAngles;
                        Quaternion targetRot = Quaternion.Euler(euler.x, targetYaw, euler.z);
                        turnTween = Tween.Rotation(npcTransform, targetRot, turnDuration, Ease.OutQuad);
                        await turnTween.ToUniTask(cancellationToken: token);
                    }
                }

                if (ConversationManager.Instance != null)
                {
                    ConversationManager.OnConversationEnded += onEnded;
                    subscribed = true;
                    ConversationManager.Instance.StartConversation(conversation);

                    while (!conversationEnded && !token.IsCancellationRequested)
                    {
                        if (ConversationManager.Instance == null || !ConversationManager.Instance.IsConversationActive)
                        {
                            break;
                        }
                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                    }
                }
                else
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(1.5f), cancellationToken: token);
                }

                if (!token.IsCancellationRequested)
                {
                    onCompleted?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                turnTween.Stop();
                if (ConversationManager.Instance != null && ConversationManager.Instance.IsConversationActive)
                {
                    ConversationManager.Instance.EndConversation();
                }
            }
            finally
            {
                turnTween.Stop();
                if (subscribed)
                {
                    ConversationManager.OnConversationEnded -= onEnded;
                }
                controlLock?.Dispose();
                IsRunning = false;
            }
        }
    }
}
