using KinematicCharacterController;
using KinematicCharacterController.Examples;
using MoBA.Interaction;
using UnityEngine;

/// <summary>
/// Выдвижной ящик комода/тумбы, открывающийся и закрывающийся по взаимодействию игрока.
/// Использует PhysicsMover для корректного физического выталкивания персонажа.
/// </summary>
public class CupBoard : MonoBehaviour, IInteractable, IMoverController
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
    private Vector3 _currentLocalPosition;
    private Quaternion _initialLocalRotation;

    private bool _segmentActive;
    private Vector3 _segmentStartLocalPos;
    private Vector3 _segmentEndLocalPos;
    private float _segmentElapsed;
    private float _segmentDuration;
    private PhysicsMoverHelper.EaseType _segmentEase;
    private bool _targetOpenState;

    public bool IsOpen => _isOpen;

    private void Awake()
    {
        _closedLocalPosition = transform.localPosition;
        _currentLocalPosition = _closedLocalPosition;
        _initialLocalRotation = transform.localRotation;

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

        PhysicsMoverHelper.EnsurePhysicsMover(gameObject, this);
    }

    public void UpdateMovement(out Vector3 goalPosition, out Quaternion goalRotation, float deltaTime)
    {
        if (_segmentActive)
        {
            _segmentElapsed += deltaTime;
            float t = _segmentDuration > 0.0001f ? Mathf.Clamp01(_segmentElapsed / _segmentDuration) : 1f;
            float eased = PhysicsMoverHelper.EvaluateEase(t, _segmentEase);
            _currentLocalPosition = Vector3.LerpUnclamped(_segmentStartLocalPos, _segmentEndLocalPos, eased);

            if (t >= 1f)
            {
                _currentLocalPosition = _segmentEndLocalPos;
                _segmentActive = false;
                _isOpen = _targetOpenState;
                _isAnimating = false;
            }
        }

        goalPosition = transform.parent != null
            ? transform.parent.TransformPoint(_currentLocalPosition)
            : _currentLocalPosition;
        goalRotation = transform.parent != null
            ? transform.parent.rotation * _initialLocalRotation
            : _initialLocalRotation;
    }

    public void Interact(int id)
    {
        if (_isAnimating) return;

        _isAnimating = true;
        _segmentStartLocalPos = _currentLocalPosition;
        _segmentElapsed = 0f;

        if (!_isOpen)
        {
            _segmentEndLocalPos = _openedLocalPosition;
            _segmentDuration = _openDuration;
            _segmentEase = PhysicsMoverHelper.EaseType.OutCubic;
            _targetOpenState = true;
        }
        else
        {
            _segmentEndLocalPos = _closedLocalPosition;
            _segmentDuration = _closeDuration;
            _segmentEase = PhysicsMoverHelper.EaseType.InOutQuad;
            _targetOpenState = false;
        }

        _segmentActive = true;
    }
}
