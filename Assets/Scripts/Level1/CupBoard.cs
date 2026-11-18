using KinematicCharacterController.Examples;
using PrimeTween;
using UnityEngine;

/// <summary>
/// Выдвижной ящик комода/тумбы, открывающийся и закрывающийся по взаимодействию игрока.
/// </summary>
public class CupBoard : MonoBehaviour, IInteractable
{
    [SerializeField] private Vector3 _openOffset = new Vector3(0f, 0f, 0.32f);
    [SerializeField] private float _openDuration = 0.55f;
    [SerializeField] private float _closeDuration = 0.65f;

    // Оставлено для обратной совместимости сериализованных данных на сцене/префабе
    [SerializeField, HideInInspector] private Vector3 _vector3End;

    private bool _isOpen = false;
    private bool _isAnimating = false;
    private Vector3 _closedLocalPosition;
    private Vector3 _openedLocalPosition;

    private void Awake()
    {
        _closedLocalPosition = transform.localPosition;
        if (_openOffset.sqrMagnitude > 0.0001f)
        {
            _openedLocalPosition = _closedLocalPosition + _openOffset;
        }
        else if (_vector3End.sqrMagnitude > 0.0001f)
        {
            _openedLocalPosition = _vector3End;
        }
        else
        {
            _openedLocalPosition = _closedLocalPosition + new Vector3(0f, 0f, 0.32f);
        }
    }

    public void Interact(int id)
    {
        if (_isAnimating) return;

        _isAnimating = true;
        if (!_isOpen)
        {
            Tween.LocalPosition(transform, endValue: _openedLocalPosition, duration: _openDuration, ease: Ease.OutCubic)
                .OnComplete(() =>
                {
                    _isOpen = true;
                    _isAnimating = false;
                });
        }
        else
        {
            Tween.LocalPosition(transform, endValue: _closedLocalPosition, duration: _closeDuration, ease: Ease.InOutQuad)
                .OnComplete(() =>
                {
                    _isOpen = false;
                    _isAnimating = false;
                });
        }
    }
}
