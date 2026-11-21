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
        private CupBoard _parentCupBoard;

        public InteractionPromptType PromptType => InteractionPromptType.Interact;
        public bool CanInteractNow =>
            !_collected
            && _questService != null
            && _questService.CanCollectKey
            && (_parentCupBoard == null || _parentCupBoard.IsOpen);

        private void Awake()
        {
            _parentCupBoard = GetComponentInParent<CupBoard>();
            EnsureGenerousHitbox();
        }

        private void OnDisable()
        {
            _pickupSequence.Stop();
        }

        private void EnsureGenerousHitbox()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null)
            {
                box = gameObject.AddComponent<BoxCollider>();
            }

            box.isTrigger = true;
            box.center = new Vector3(box.center.x, Mathf.Max(box.center.y, 0.10f), box.center.z);
            box.size = new Vector3(
                Mathf.Max(box.size.x, 0.45f),
                Mathf.Max(box.size.y, 0.32f),
                Mathf.Max(box.size.z, 0.50f));
        }

        public void Interact(int id)
        {
            if (_collected) return;
            if (_questService != null && !_questService.CanCollectKey) return;
            if (_parentCupBoard != null && !_parentCupBoard.IsOpen) return;

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
