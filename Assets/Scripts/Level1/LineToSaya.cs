using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using PrimeTween;
using UnityEngine;

public class LineToSaya : MonoBehaviour
{
    [SerializeField] private GameObject _arrow;
    [SerializeField] private GameObject _target;
    [SerializeField] private LayerMask _groundLayer;

    [Header("Periodic Visibility")]
    [SerializeField] private float _hiddenIntervalSeconds = 5f;
    [SerializeField] private float _visibleDurationSeconds = 2f;
    [SerializeField] private float _fadeDurationSeconds = 0.4f;

    private Tween _fadeTween;
    private CancellationTokenSource _pulseCts;

    private float _offsetToGround = 1.35f;
    private float _distanceToplayer = 2.6f;
    private float _deactivateDistance = 5f;

    private GameObject _arrowInstatiate;
    private Camera _arrowCam;
    private bool _active = false;
    private bool _arrowVisible = false;
    private bool _questTargetActive = false;
    private Material _runtimeMaterial;

    private bool _hasSmoothPose;
    private Vector3 _lastRaycastXZ;
    private float _nextRaycastUnscaledTime;
    private float _cachedGroundY;
    private bool _hasCachedGroundY;

    [SerializeField] private Material _material;
    [SerializeField] private Color _targetColor = new Color(1f, 0.86f, 0.08f, 1f);
    [SerializeField] private Color _initColor = new Color(1f, 0.86f, 0.08f, 0f);

    private void Start()
    {
        EnsureArrowCamReady();
        EnsureArrowInstance();
        if (_arrowInstatiate != null)
        {
            _arrowInstatiate.SetActive(false);
        }
    }

    private void EnsureArrowCamReady()
    {
        int arrowLayer = LayerMask.NameToLayer("Arrow");
        int arrowMask = arrowLayer >= 0 ? (1 << arrowLayer) : 0;

        if (_arrowCam == null)
        {
            GameObject arrowCamObj = GameObject.Find("ArrowCam");
            if (arrowCamObj != null) arrowCamObj.TryGetComponent(out _arrowCam);
        }

        if (_arrowCam != null)
        {
            if (_arrowCam.farClipPlane < 50f) _arrowCam.farClipPlane = 50f;
            if (_arrowCam.nearClipPlane > 0.05f) _arrowCam.nearClipPlane = 0.05f;
            _arrowCam.clearFlags = CameraClearFlags.Depth;
            return;
        }

        foreach (Camera cam in Camera.allCameras)
        {
            if (cam.name.Contains("Arrow") || (arrowMask != 0 && cam.cullingMask == arrowMask))
            {
                _arrowCam = cam;
                if (cam.farClipPlane < 50f) cam.farClipPlane = 50f;
                if (cam.nearClipPlane > 0.05f) cam.nearClipPlane = 0.05f;
                cam.clearFlags = CameraClearFlags.Depth;
                break;
            }
        }
    }

    private void EnsureArrowInstance()
    {
        if (_arrow == null) return;

        if (_arrowInstatiate == null)
        {
            _arrowInstatiate = Instantiate(_arrow);
            _arrowInstatiate.transform.localScale = _arrow.transform.localScale * 1.15f;
        }

        int arrowLayer = LayerMask.NameToLayer("Arrow");
        if (arrowLayer >= 0)
        {
            SetLayerRecursively(_arrowInstatiate, arrowLayer);
        }

        if (_runtimeMaterial == null)
        {
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = Shader.Find("Unlit/Color");
            if (unlitShader != null)
            {
                _runtimeMaterial = new Material(unlitShader);
                // Включаем прозрачный режим URP Unlit для плавного появления/исчезания без смешивания в розовый
                if (_runtimeMaterial.HasProperty("_Surface")) _runtimeMaterial.SetFloat("_Surface", 1f);
                if (_runtimeMaterial.HasProperty("_SrcBlend")) _runtimeMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (_runtimeMaterial.HasProperty("_DstBlend")) _runtimeMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (_runtimeMaterial.HasProperty("_ZWrite")) _runtimeMaterial.SetFloat("_ZWrite", 1f);
                _runtimeMaterial.renderQueue = 3000;
                _material = _runtimeMaterial;
            }
            else if (_material == null)
            {
                Renderer r = _arrowInstatiate.GetComponentInChildren<Renderer>(true);
                if (r != null) _material = r.material;
            }
        }

        if (_material != null)
        {
            foreach (Renderer r in _arrowInstatiate.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = _material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }
    }

    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null) return;
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private void ApplyMaterialColor(Color color)
    {
        if (_material == null) return;
        if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", color);
        if (_material.HasProperty("_Color")) _material.SetColor("_Color", color);
    }

    public void SetTarget(GameObject newTarget, float deactivateDist = 5f)
    {
        _target = newTarget;
        _deactivateDistance = deactivateDist;
        _hasSmoothPose = false;
        _hasCachedGroundY = false;
    }

    public void SetQuestTarget(GameObject newTarget, float deactivateDist = 2.5f)
    {
        _questTargetActive = newTarget != null;
        SetTarget(newTarget, deactivateDist);
        if (_questTargetActive)
        {
            ActivateMarker(0.45f).Forget();
        }
    }

    public void ClearQuestTarget(float fadeDuration = 0.5f)
    {
        _questTargetActive = false;
        DeactivateLine(fadeDuration).Forget();
    }

    private void LateUpdate()
    {
        if (!_active || _arrowInstatiate == null || _target == null || !_target.activeInHierarchy) return;

        Vector3 origin = transform.position;
        Vector3 toTarget = _target.transform.position - origin;
        Vector3 flatToTarget = new Vector3(toTarget.x, 0f, toTarget.z);
        float sqrDistance = flatToTarget.sqrMagnitude;

        if (sqrDistance < _deactivateDistance * _deactivateDistance)
        {
            _active = false;
            DeactivateLine(0.6f).Forget();
            return;
        }

        if (!_arrowVisible || sqrDistance < 0.0001f) return;
        Vector3 direction = flatToTarget / Mathf.Sqrt(sqrDistance);
        Vector3 desiredPos = origin + direction * _distanceToplayer;

        // Троттлинг рейкаста по земле (~8 Гц или при смещении > 0.35 м) исключает лаги и дёрганье от FixedUpdate
        float dx = desiredPos.x - _lastRaycastXZ.x;
        float dz = desiredPos.z - _lastRaycastXZ.z;
        if (!_hasCachedGroundY || (dx * dx + dz * dz) > 0.1225f || Time.unscaledTime >= _nextRaycastUnscaledTime)
        {
            int mask = _groundLayer.value != 0 ? _groundLayer.value : Physics.DefaultRaycastLayers;
            if (Physics.Raycast(new Vector3(desiredPos.x, origin.y + 100f, desiredPos.z), Vector3.down, out RaycastHit hit, 200f, mask, QueryTriggerInteraction.Ignore))
            {
                _cachedGroundY = hit.point.y + _offsetToGround;
                _hasCachedGroundY = true;
            }
            _lastRaycastXZ = new Vector3(desiredPos.x, 0f, desiredPos.z);
            _nextRaycastUnscaledTime = Time.unscaledTime + 0.12f;
        }

        float minPlayerY = origin.y + 0.65f;
        desiredPos.y = _hasCachedGroundY ? Mathf.Max(_cachedGroundY, minPlayerY) : minPlayerY;
        Quaternion desiredRot = Quaternion.LookRotation(direction, Vector3.up);

        if (!_hasSmoothPose)
        {
            _arrowInstatiate.transform.position = desiredPos;
            _arrowInstatiate.transform.rotation = desiredRot;
            _hasSmoothPose = true;
            return;
        }

        // Покадрово-независимое плавное следование на полной частоте кадров (без ступенчатости FixedUpdate)
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        float smoothFactor = 1f - Mathf.Exp(-18f * dt);
        _arrowInstatiate.transform.position = Vector3.Lerp(_arrowInstatiate.transform.position, desiredPos, smoothFactor);
        _arrowInstatiate.transform.rotation = Quaternion.Slerp(_arrowInstatiate.transform.rotation, desiredRot, smoothFactor);
    }

    private void OnDestroy()
    {
        StopPulseLoop();
        _fadeTween.Stop();
        if (_runtimeMaterial != null)
        {
            Destroy(_runtimeMaterial);
            _runtimeMaterial = null;
        }
    }

    private void StopPulseLoop()
    {
        if (_pulseCts != null)
        {
            _pulseCts.Cancel();
            _pulseCts.Dispose();
            _pulseCts = null;
        }
    }

    public async UniTask ActivateMarker(float duration)
    {
        StopPulseLoop();
        _fadeTween.Stop();
        _active = true;
        _arrowVisible = false;
        _hasSmoothPose = false;
        _hasCachedGroundY = false;

        EnsureArrowCamReady();
        EnsureArrowInstance();

        if (_arrowInstatiate != null)
        {
            ApplyMaterialColor(new Color(_targetColor.r, _targetColor.g, _targetColor.b, 0f));
            _arrowInstatiate.SetActive(false);
        }

        float fadeDur = duration > 0.05f ? Mathf.Clamp(duration * 0.4f, 0.25f, 0.55f) : _fadeDurationSeconds;
        _pulseCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        RunPulseCycleAsync(fadeDur, _pulseCts.Token).Forget();
        await UniTask.CompletedTask;
    }

    private async UniTaskVoid RunPulseCycleAsync(float fadeDur, CancellationToken token)
    {
        try
        {
            while (_active && !token.IsCancellationRequested)
            {
                // Стрелки вообще нет 5 секунд
                _arrowVisible = false;
                if (_arrowInstatiate != null)
                {
                    ApplyMaterialColor(new Color(_targetColor.r, _targetColor.g, _targetColor.b, 0f));
                    _arrowInstatiate.SetActive(false);
                }

                await UniTask.Delay(TimeSpan.FromSeconds(_hiddenIntervalSeconds), cancellationToken: token);
                if (!_active || token.IsCancellationRequested) break;

                // Плавно появляется
                EnsureArrowCamReady();
                EnsureArrowInstance();
                _hasSmoothPose = false;
                _arrowVisible = true;
                if (_arrowInstatiate != null)
                {
                    ApplyMaterialColor(new Color(_targetColor.r, _targetColor.g, _targetColor.b, 0f));
                    _arrowInstatiate.SetActive(true);
                }

                await FadeArrowAlphaAsync(0f, _targetColor.a, fadeDur, Ease.OutQuad, token);
                if (!_active || token.IsCancellationRequested) break;

                // Держится 2 секунды
                await UniTask.Delay(TimeSpan.FromSeconds(_visibleDurationSeconds), cancellationToken: token);
                if (!_active || token.IsCancellationRequested) break;

                // Плавно пропадает перед следующей 5-секундной паузой
                await FadeArrowAlphaAsync(_targetColor.a, 0f, fadeDur, Ease.InOutQuad, token);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async UniTask FadeArrowAlphaAsync(float fromAlpha, float toAlpha, float duration, Ease ease, CancellationToken token)
    {
        _fadeTween.Stop();
        if (_material == null || duration <= 0.01f)
        {
            ApplyMaterialColor(new Color(_targetColor.r, _targetColor.g, _targetColor.b, toAlpha));
            return;
        }

        ApplyMaterialColor(new Color(_targetColor.r, _targetColor.g, _targetColor.b, fromAlpha));
        _fadeTween = Tween.Custom(fromAlpha, toAlpha, duration, alpha =>
        {
            ApplyMaterialColor(new Color(_targetColor.r, _targetColor.g, _targetColor.b, alpha));
        }, ease);

        while (_fadeTween.isAlive)
        {
            if (token.IsCancellationRequested)
            {
                _fadeTween.Stop();
                token.ThrowIfCancellationRequested();
            }
            await UniTask.Yield();
        }
    }

    public async UniTask DeactivateLine(float duration)
    {
        _active = false;
        StopPulseLoop();
        _fadeTween.Stop();

        if (_arrowVisible && _arrowInstatiate != null && _arrowInstatiate.activeSelf && _material != null && duration > 0.01f)
        {
            float currentAlpha = _material.HasProperty("_BaseColor")
                ? _material.GetColor("_BaseColor").a
                : (_material.HasProperty("_Color") ? _material.GetColor("_Color").a : _targetColor.a);
            if (currentAlpha > 0.01f)
            {
                _fadeTween = Tween.Custom(currentAlpha, 0f, duration, alpha =>
                {
                    ApplyMaterialColor(new Color(_targetColor.r, _targetColor.g, _targetColor.b, alpha));
                }, Ease.InOutQuad);
                await _fadeTween.ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());
            }
        }

        _arrowVisible = false;
        if (_arrowInstatiate != null)
        {
            _arrowInstatiate.SetActive(false);
        }
    }

    public void DeactivateImmediate()
    {
        if (_questTargetActive) return;
        _active = false;
        _arrowVisible = false;
        StopPulseLoop();
        _fadeTween.Stop();
        if (_arrowInstatiate != null)
        {
            _arrowInstatiate.SetActive(false);
        }
    }
}
