using System.Threading;
using Cysharp.Threading.Tasks;
using KinematicCharacterController.Examples;
using MoBA.Narrative;
using PrimeTween;
using UnityEngine;
using Zenject;

public class Door : MonoBehaviour, IInteractable
{
    [SerializeField] private int _doorId;
    [SerializeField] private float _angle;
    [SerializeField] private float _lockedAngle;
    [SerializeField] private bool _requiresKey = false;

    [Inject(Optional = true)] private KeyQuestService _questService;

    private bool _isAnimating = false;
    private bool _isOpen = false;

    private float _startAngle;

    private void Start()
    {
        _startAngle = transform.rotation.eulerAngles.y;
    }

    public async void Interact(int id)
    {
        if (_isAnimating) return;

        bool isLocked = _requiresKey
            ? (_questService == null || !_questService.HasGateKey)
            : _doorId == 1;

        CancellationToken destroyToken = this.GetCancellationTokenOnDestroy();

        if (isLocked)
        {
            _isAnimating = true;

            Vector3 targetRotation1 = new(transform.rotation.eulerAngles.x, _lockedAngle, transform.rotation.eulerAngles.z);
            Vector3 targetRotation2 = new(transform.rotation.eulerAngles.x, _startAngle, transform.rotation.eulerAngles.z);

            await Tween.Rotation(transform, endValue: targetRotation1, duration: 0.3f, ease: Ease.InOutBack).ToUniTask(cancellationToken: destroyToken);
            await Tween.Rotation(transform, endValue: targetRotation2, duration: 0.3f, ease: Ease.InOutBack).ToUniTask(cancellationToken: destroyToken);

            _isAnimating = false;
            return;
        }

        if (!_isOpen)
        {
            _isAnimating = true;
            if (_requiresKey)
            {
                _questService?.NotifyGateOpened();
            }

            Vector3 targetRotation = new(transform.rotation.eulerAngles.x, _angle, transform.rotation.eulerAngles.z);
            await Tween.Rotation(transform, endValue: targetRotation, duration: 0.67f, ease: Ease.InOutBack).ToUniTask(cancellationToken: destroyToken);
            _isOpen = true;

            _isAnimating = false;
        }
        else
        {
            _isAnimating = true;

            Vector3 targetRotation = new(transform.rotation.eulerAngles.x, _startAngle, transform.rotation.eulerAngles.z);
            await Tween.Rotation(transform, endValue: targetRotation, duration: 1f, ease: Ease.InExpo).ToUniTask(cancellationToken: destroyToken);
            _isOpen = false;

            _isAnimating = false;
        }
    }
}
