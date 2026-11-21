using System.Threading;
using Cysharp.Threading.Tasks;
using KinematicCharacterController;
using KinematicCharacterController.Examples;
using MoBA.Interaction;
using MoBA.Narrative;
using UnityEngine;
using Zenject;

public class Door : MonoBehaviour, IInteractable, IMoverController
{
    [SerializeField] private int _doorId;
    [SerializeField] private float _angle;
    [SerializeField] private float _lockedAngle;
    [SerializeField] private bool _requiresKey = false;

    [Inject(Optional = true)] private KeyQuestService _questService;

    private bool _isAnimating = false;
    private bool _isOpen = false;

    private float _startAngle;
    private Vector3 _initialLocalPosition;
    private Vector3 _baseEuler;
    private Quaternion _currentWorldRotation;

    private bool _segmentActive;
    private float _segmentStartYaw;
    private float _segmentEndYaw;
    private float _segmentElapsed;
    private float _segmentDuration;
    private PhysicsMoverHelper.EaseType _segmentEase;
    private UniTaskCompletionSource _segmentTcs;

    public bool IsOpen => _isOpen || IsVisuallyOpen();

    public bool IsVisuallyOpen()
    {
        float curYaw = transform.rotation.eulerAngles.y;
        float diff = Mathf.Abs(Mathf.DeltaAngle(curYaw, _startAngle));
        return diff > 25f;
    }

    private void Awake()
    {
        _initialLocalPosition = transform.localPosition;
        _baseEuler = transform.rotation.eulerAngles;
        _startAngle = _baseEuler.y;
        _currentWorldRotation = transform.rotation;
        PhysicsMoverHelper.EnsurePhysicsMover(gameObject, this);
    }

    private void Start()
    {
        if (_requiresKey)
        {
            EnsureTallInteractHitbox();
        }
    }

    private void OnDestroy()
    {
        _segmentActive = false;
        _segmentTcs?.TrySetCanceled();
        _segmentTcs = null;
    }

    public void UpdateMovement(out Vector3 goalPosition, out Quaternion goalRotation, float deltaTime)
    {
        goalPosition = transform.parent != null
            ? transform.parent.TransformPoint(_initialLocalPosition)
            : _initialLocalPosition;

        if (_segmentActive)
        {
            _segmentElapsed += deltaTime;
            float t = _segmentDuration > 0.0001f ? Mathf.Clamp01(_segmentElapsed / _segmentDuration) : 1f;
            float eased = PhysicsMoverHelper.EvaluateEase(t, _segmentEase);
            float currentYaw = Mathf.LerpUnclamped(_segmentStartYaw, _segmentEndYaw, eased);
            _currentWorldRotation = Quaternion.Euler(_baseEuler.x, currentYaw, _baseEuler.z);

            if (t >= 1f)
            {
                _currentWorldRotation = Quaternion.Euler(_baseEuler.x, _segmentEndYaw, _baseEuler.z);
                _segmentActive = false;
                UniTaskCompletionSource tcs = _segmentTcs;
                _segmentTcs = null;
                tcs?.TrySetResult();
            }
        }

        goalRotation = _currentWorldRotation;
    }

    /// <summary>
    /// Расширяет зону наведения луча вверх для невысокой калитки, чтобы с ней можно было
    /// взаимодействовать стоя, не приседая. Не влияет на физический коллайдер.
    /// </summary>
    private void EnsureTallInteractHitbox()
    {
        const string hitboxName = "GateInteractHitbox";
        if (transform.Find(hitboxName) != null) return;

        Bounds localBounds;
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            localBounds = mf.sharedMesh.bounds;
        }
        else
        {
            Collider existingCol = GetComponent<Collider>();
            if (existingCol == null) return;
            Vector3 localCenter = transform.InverseTransformPoint(existingCol.bounds.center);
            Vector3 lossy = transform.lossyScale;
            Vector3 localSize = new Vector3(
                lossy.x > 0.001f ? existingCol.bounds.size.x / lossy.x : existingCol.bounds.size.x,
                lossy.y > 0.001f ? existingCol.bounds.size.y / lossy.y : existingCol.bounds.size.y,
                lossy.z > 0.001f ? existingCol.bounds.size.z / lossy.z : existingCol.bounds.size.z);
            localBounds = new Bounds(localCenter, localSize);
        }

        float scaleY = Mathf.Abs(transform.lossyScale.y);
        float extraLocalHeight = scaleY > 0.001f ? 0.8f / scaleY : 0.8f;

        GameObject hitboxGo = new GameObject(hitboxName);
        hitboxGo.transform.SetParent(transform, false);
        hitboxGo.layer = gameObject.layer;

        BoxCollider box = hitboxGo.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = localBounds.center + new Vector3(0f, extraLocalHeight * 0.5f, 0f);
        box.size = new Vector3(
            Mathf.Max(localBounds.size.x, 0.05f),
            Mathf.Max(localBounds.size.y, 0.05f) + extraLocalHeight,
            Mathf.Max(localBounds.size.z, 0.05f));
    }

    private float GetTargetOpenAngle()
    {
        if (Mathf.Abs(Mathf.DeltaAngle(_startAngle, _angle)) > 20f)
        {
            return _angle;
        }
        return _startAngle + (_angle != 0f ? _angle : 88f);
    }

    /// <summary>
    /// Открывает дверь, только если она сейчас закрыта (не закрывает уже открытую).
    /// </summary>
    public void Open()
    {
        if (IsOpen || _isAnimating) return;
        Interact(0);
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
            try
            {
                float lockedTarget = Mathf.Abs(Mathf.DeltaAngle(_startAngle, _lockedAngle)) > 2f
                    ? _lockedAngle
                    : _startAngle + 13f;

                await AnimateYawAsync(lockedTarget, 0.3f, PhysicsMoverHelper.EaseType.InOutSine, destroyToken);
                await AnimateYawAsync(_startAngle, 0.3f, PhysicsMoverHelper.EaseType.InOutSine, destroyToken);
            }
            finally
            {
                _isAnimating = false;
            }
            return;
        }

        bool currentlyOpen = IsOpen;
        _isAnimating = true;
        try
        {
            if (!currentlyOpen)
            {
                if (_requiresKey)
                {
                    _questService?.NotifyGateOpened();
                }

                float targetAngle = GetTargetOpenAngle();
                await AnimateYawAsync(targetAngle, 0.67f, PhysicsMoverHelper.EaseType.InOutSine, destroyToken);
                _isOpen = true;
            }
            else
            {
                await AnimateYawAsync(_startAngle, 0.85f, PhysicsMoverHelper.EaseType.InOutSine, destroyToken);
                _isOpen = false;
            }
        }
        finally
        {
            _isAnimating = false;
        }
    }

    private async UniTask AnimateYawAsync(float targetYaw, float duration, PhysicsMoverHelper.EaseType ease, CancellationToken token)
    {
        _segmentTcs?.TrySetCanceled();

        float startYaw = _currentWorldRotation.eulerAngles.y;
        float delta = Mathf.DeltaAngle(startYaw, targetYaw);
        _segmentStartYaw = startYaw;
        _segmentEndYaw = startYaw + delta;
        _segmentDuration = duration;
        _segmentElapsed = 0f;
        _segmentEase = ease;

        UniTaskCompletionSource tcs = new UniTaskCompletionSource();
        _segmentTcs = tcs;
        _segmentActive = true;

        using (token.Register(() =>
        {
            _segmentActive = false;
            if (_segmentTcs == tcs) _segmentTcs = null;
            tcs.TrySetCanceled(token);
        }))
        {
            await tcs.Task;
        }
    }
}
