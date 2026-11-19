using KinematicCharacterController.Examples;
using MoBA.Narrative;
using PrimeTween;
using UnityEngine;
using Zenject;

namespace MoBA.Interaction
{
    /// <summary>
    /// Подбираемый ключ от калитки в комоде дома (доступен с этапа FindKey).
    /// </summary>
    public class KeyPickup : MonoBehaviour, IInteractable, IConditionalInteractionPrompt
    {
        [SerializeField] private float _pickupDuration = 0.28f;
        [SerializeField] private float _liftOffset = 0.12f;

        [Inject(Optional = true)] private KeyQuestService _questService;

        private bool _collected;
        private Sequence _pickupSequence;

        public InteractionPromptType PromptType => InteractionPromptType.Interact;
        public bool CanInteractNow => !_collected && _questService != null && _questService.CanCollectKey;

        private void OnDisable()
        {
            _pickupSequence.Stop();
        }

        public void Interact(int id)
        {
            if (_collected) return;
            if (_questService != null && !_questService.CanCollectKey) return;

            _collected = true;
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            if (_questService != null)
            {
                _questService.CollectGateKey();
            }
            else
            {
                Debug.LogError("[KeyPickup] Не внедрён KeyQuestService — проверьте RegObj на сцене.", this);
            }

            _pickupSequence.Stop();
            _pickupSequence = Sequence.Create()
                .Group(Tween.LocalPositionY(transform, transform.localPosition.y + _liftOffset, _pickupDuration, Ease.OutQuad))
                .Group(Tween.Scale(transform, Vector3.zero, _pickupDuration, Ease.InBack))
                .ChainCallback(() =>
                {
                    if (this != null) gameObject.SetActive(false);
                });
        }
    }
}
