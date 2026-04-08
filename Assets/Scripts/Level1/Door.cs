using KinematicCharacterController.Examples;
using UnityEngine;
using PrimeTween;
using Cysharp.Threading.Tasks;

public class Door : MonoBehaviour, IInteractable
{
    [SerializeField] private int _doorId;
    [SerializeField] private float _angle;
    [SerializeField] private float _lockedAngle;

    private bool _isAnimating = false;
    private bool _isOpen = false;

    private float _startAngle;

    private void Start()
    {
        _startAngle = transform.rotation.eulerAngles.y;
    }

    public async void Interact(int id)
    {

        if (_doorId == 1 && !_isAnimating)
        {
            _isAnimating = true;

            Vector3 targetRotation1 = new(transform.rotation.eulerAngles.x, _lockedAngle, transform.rotation.eulerAngles.z);
            Vector3 targetRotation2 = new(transform.rotation.eulerAngles.x, _startAngle, transform.rotation.eulerAngles.z);

            await Tween.Rotation(this.transform, endValue: targetRotation1, duration: 0.3f, ease: Ease.InOutBack).ToUniTask();
            await Tween.Rotation(this.transform, endValue: targetRotation2, duration: 0.3f, ease: Ease.InOutBack).ToUniTask();

            _isAnimating = false;
        }
        if (_doorId != 1 && !_isAnimating)
        {
            if (!_isOpen)
            {
                _isAnimating = true;

                Vector3 targetRotation = new(transform.rotation.eulerAngles.x, _angle, transform.rotation.eulerAngles.z);
                await Tween.Rotation(this.transform, endValue: targetRotation, duration: 0.67f, ease: Ease.InOutBack).ToUniTask();
                _isOpen = true;

                _isAnimating = false;
            }
            else
            {
                _isAnimating = true;

                Vector3 targetRotation = new(transform.rotation.eulerAngles.x, _startAngle, transform.rotation.eulerAngles.z);
                await Tween.Rotation(this.transform, endValue: targetRotation, duration: 1f, ease: Ease.InExpo).ToUniTask();
                _isOpen = false;

                _isAnimating = false;
            }
        }
    }
}
