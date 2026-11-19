using KinematicCharacterController.Examples;
using MoBA.Narrative;
using PrimeTween;
using UnityEngine;
using Zenject;

namespace MoBA.Interaction
{
    /// <summary>
    /// Подбираемый квестовый товар на прилавке продуктового магазина.
    /// </summary>
    public class GroceryPickup : MonoBehaviour, IInteractable, IConditionalInteractionPrompt
    {
        [SerializeField] private GroceryItemType _itemType;
        [SerializeField] private float _pickupDuration = 0.25f;
        [SerializeField] private float _liftOffset = 0.14f;

        [Inject(Optional = true)] private KeyQuestService _questService;

        private Vector3 _initialScale;
        private bool _hasCapturedScale;
        private bool _isRevealed;
        private bool _collected;
        private Sequence _tweenSequence;

        public InteractionPromptType PromptType => InteractionPromptType.Interact;
        public bool CanInteractNow => !_collected
            && _isRevealed
            && _questService != null
            && _questService.AreCounterGoodsUnlocked;

        private void Awake()
        {
            CaptureInitialScale();
        }

        private void OnDisable()
        {
            _tweenSequence.Stop();
        }

        public void HideImmediate()
        {
            CaptureInitialScale();
            _tweenSequence.Stop();
            _isRevealed = false;
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
            gameObject.SetActive(false);
        }

        public void RevealAnimated(float delay)
        {
            if (_collected) return;
            CaptureInitialScale();
            _tweenSequence.Stop();

            _isRevealed = true;
            gameObject.SetActive(true);
            transform.localScale = Vector3.zero;

            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = true;

            _tweenSequence = Sequence.Create()
                .ChainDelay(delay)
                .Chain(Tween.Scale(transform, _initialScale, 0.3f, Ease.OutBack));
        }

        public void Interact(int id)
        {
            if (!CanInteractNow) return;
            if (!_questService.CollectGroceryItem(_itemType)) return;

            _collected = true;
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            _tweenSequence.Stop();
            _tweenSequence = Sequence.Create()
                .Group(Tween.LocalPositionY(transform, transform.localPosition.y + _liftOffset, _pickupDuration, Ease.OutQuad))
                .Group(Tween.Scale(transform, Vector3.zero, _pickupDuration, Ease.InBack))
                .ChainCallback(() =>
                {
                    if (this != null) gameObject.SetActive(false);
                });
        }

        private void CaptureInitialScale()
        {
            if (_hasCapturedScale) return;
            _initialScale = transform.localScale;
            _hasCapturedScale = true;
        }
    }
}
