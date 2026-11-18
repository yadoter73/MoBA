using KinematicCharacterController.Examples;
using UnityEngine;
using Zenject;

namespace MoBA.Narrative
{
    /// <summary>
    /// Триггер зоны MainHouse: отслеживает вход/выход игрока и обновляет подсказку этапа поиска ключа.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class HouseZoneTrigger : MonoBehaviour
    {
        [Inject(Optional = true)] private KeyQuestService _questService;
        [Inject(Optional = true)] private ExampleCharacterController _playerController;

        private BoxCollider _boxCollider;
        private bool _isInside;
        private int _triggerContacts;

        private void Awake()
        {
            _boxCollider = GetComponent<BoxCollider>();
            if (_boxCollider != null) _boxCollider.isTrigger = true;
        }

        private void Start()
        {
            SyncContainmentState(forceNotify: true);
        }

        private void FixedUpdate()
        {
            // Контрольная проверка локальных границ BoxCollider исключает рассинхрон при движении KCC без событий физики.
            if (_playerController != null && _boxCollider != null)
            {
                bool insideNow = ContainsPlayerBounds(_playerController.transform.position + Vector3.up * 0.6f);
                if (insideNow != _isInside)
                {
                    _isInside = insideNow;
                    _triggerContacts = insideNow ? 1 : 0;
                    _questService?.SetPlayerInsideHouse(_isInside);
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayerCollider(other)) return;
            _triggerContacts++;
            if (!_isInside)
            {
                _isInside = true;
                _questService?.SetPlayerInsideHouse(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsPlayerCollider(other)) return;
            _triggerContacts = Mathf.Max(0, _triggerContacts - 1);
            if (_triggerContacts == 0 && _isInside)
            {
                _isInside = false;
                _questService?.SetPlayerInsideHouse(false);
            }
        }

        private void SyncContainmentState(bool forceNotify)
        {
            if (_playerController == null || _boxCollider == null) return;
            bool insideNow = ContainsPlayerBounds(_playerController.transform.position + Vector3.up * 0.6f);
            if (forceNotify || insideNow != _isInside)
            {
                _isInside = insideNow;
                _triggerContacts = insideNow ? 1 : 0;
                _questService?.SetPlayerInsideHouse(_isInside);
            }
        }

        private bool IsPlayerCollider(Collider other)
        {
            if (other == null) return false;
            if (_playerController != null)
            {
                return other.transform.root == _playerController.transform.root;
            }
            return other.GetComponentInParent<ExampleCharacterController>() != null;
        }

        private bool ContainsPlayerBounds(Vector3 worldPos)
        {
            Vector3 localPos = transform.InverseTransformPoint(worldPos) - _boxCollider.center;
            Vector3 half = _boxCollider.size * 0.5f;
            return Mathf.Abs(localPos.x) <= half.x &&
                   Mathf.Abs(localPos.y) <= half.y &&
                   Mathf.Abs(localPos.z) <= half.z;
        }
    }
}
