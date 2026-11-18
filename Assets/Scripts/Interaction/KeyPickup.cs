using KinematicCharacterController.Examples;
using MoBA.Narrative;
using PrimeTween;
using UnityEngine;
using Zenject;

namespace MoBA.Interaction
{
    /// <summary>
    /// Подбираемый ключ от калитки в комоде дома.
    /// </summary>
    public class KeyPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private float _pickupDuration = 0.28f;
        [SerializeField] private float _liftOffset = 0.12f;

        [Inject(Optional = true)] private KeyQuestService _questService;

        private bool _collected;
        private Sequence _pickupSequence;

        private void OnDisable()
        {
            _pickupSequence.Stop();
        }

        public void Interact(int id)
        {
            if (_collected) return;
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
