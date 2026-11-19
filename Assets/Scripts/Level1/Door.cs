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

    public bool IsOpen => _isOpen || IsVisuallyOpen();

    public bool IsVisuallyOpen()
    {
        float curYaw = transform.rotation.eulerAngles.y;
        float diff = Mathf.Abs(Mathf.DeltaAngle(curYaw, _startAngle));
        return diff > 25f;
    }

    private void Start()
    {
        _startAngle = transform.rotation.eulerAngles.y;
        if (_requiresKey)
        {
            EnsureTallInteractHitbox();
        }
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

            float lockedTarget = Mathf.Abs(Mathf.DeltaAngle(_startAngle, _lockedAngle)) > 2f
                ? _lockedAngle
                : _startAngle + 13f;

            Vector3 targetRotation1 = new(transform.rotation.eulerAngles.x, lockedTarget, transform.rotation.eulerAngles.z);
            Vector3 targetRotation2 = new(transform.rotation.eulerAngles.x, _startAngle, transform.rotation.eulerAngles.z);

            await Tween.Rotation(transform, endValue: targetRotation1, duration: 0.3f, ease: Ease.InOutBack).ToUniTask(cancellationToken: destroyToken);
            await Tween.Rotation(transform, endValue: targetRotation2, duration: 0.3f, ease: Ease.InOutBack).ToUniTask(cancellationToken: destroyToken);

            _isAnimating = false;
            return;
        }

        bool currentlyOpen = IsOpen;

        if (!currentlyOpen)
        {
            _isAnimating = true;
            if (_requiresKey)
            {
                _questService?.NotifyGateOpened();
            }

            float targetAngle = GetTargetOpenAngle();
            Vector3 targetRotation = new(transform.rotation.eulerAngles.x, targetAngle, transform.rotation.eulerAngles.z);
            await Tween.Rotation(transform, endValue: targetRotation, duration: 0.67f, ease: Ease.InOutBack).ToUniTask(cancellationToken: destroyToken);
            _isOpen = true;

            _isAnimating = false;
        }
        else
        {
            _isAnimating = true;

            Vector3 targetRotation = new(transform.rotation.eulerAngles.x, _startAngle, transform.rotation.eulerAngles.z);
            await Tween.Rotation(transform, endValue: targetRotation, duration: 0.85f, ease: Ease.InOutSine).ToUniTask(cancellationToken: destroyToken);
            _isOpen = false;

            _isAnimating = false;
        }
    }
}
