using UnityEngine;
using UnityEngine.UI;
using PrimeTween;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Threading;

public class PointerInmpulse : MonoBehaviour
{
    private const float MaxEdgeArrowAlpha = 100f / 255f;

    public static PointerInmpulse Instance { get; private set; }

    [SerializeField] private GameObject[] _pointers;
    private float viewAngle = 85f;
    private float viewDistance = 400f;

    private readonly Dictionary<Transform, CancellationTokenSource> _tasks = new();

    private Transform _guidanceTarget;
    private bool _guidanceActive = false;
    private bool _worldPointersEnabled = true;
    private bool _questGuidanceActive;

    private Canvas _edgeArrowCanvas;
    private RectTransform _leftArrowRect;
    private RectTransform _rightArrowRect;
    private CanvasGroup _leftArrowGroup;
    private CanvasGroup _rightArrowGroup;
    private Image _leftArrowImage;
    private Image _rightArrowImage;
    private Sprite _arrowSprite;
    private Tween _leftFadeTween;
    private Tween _rightFadeTween;
    private bool _leftArrowVisible;
    private bool _rightArrowVisible;
    private float _leftArrowAlpha;
    private float _rightArrowAlpha;
    private Camera _cachedMainCam;

    private void Awake()
    {
        Instance = this;
        BuildEdgeArrowsHUD();
    }

    private void Start()
    {
        // На старте боковые HUD-стрелки (слева/справа) выключены — игрок сам находит озеро по "Find Lake".
        // 3D-указатель над озером (_pointers) остаётся активным и продолжает плавно двигаться вверх-вниз.
        if (_pointers != null && _pointers.Length > 0 && _pointers[0] != null)
        {
            Stone stone = FindAnyObjectByType<Stone>();
            if (stone != null)
            {
                Vector3 stonePos = stone.transform.position;
                _pointers[0].transform.position = new Vector3(stonePos.x, stonePos.y + 5.2f, stonePos.z);
            }
        }

        if (!_guidanceActive)
        {
            ApplyEdgeArrowAlpha(isLeft: true, alpha: 0f);
            ApplyEdgeArrowAlpha(isLeft: false, alpha: 0f);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        _leftFadeTween.Stop();
        _rightFadeTween.Stop();
        if (_arrowMaterial != null)
        {
            Destroy(_arrowMaterial);
            _arrowMaterial = null;
        }
        CancelAllPointerTasks();
    }

    public void SetGuidanceTarget(Transform target, bool enableWorldPointers = false)
    {
        _guidanceTarget = target;
        _guidanceActive = target != null;
        _worldPointersEnabled = enableWorldPointers;

        if (!_worldPointersEnabled)
        {
            HideWorldPointers();
        }
    }

    public void SetQuestGuidanceTarget(Transform target, bool enableWorldPointers = false)
    {
        _questGuidanceActive = target != null;
        SetGuidanceTarget(target, enableWorldPointers);
    }

    public void ClearQuestGuidance()
    {
        _questGuidanceActive = false;
        DisableAllPointers();
    }

    public void DisableAllPointers()
    {
        if (_questGuidanceActive) return;
        _guidanceActive = false;
        _guidanceTarget = null;
        _worldPointersEnabled = false;
        HideWorldPointers();

        AnimateEdgeArrow(isLeft: true, nextVisible: false, fadeDuration: 1.0f);
        AnimateEdgeArrow(isLeft: false, nextVisible: false, fadeDuration: 1.0f);
    }

    private void HideWorldPointers()
    {
        if (_pointers == null) return;
        foreach (GameObject obj in _pointers)
        {
            if (obj != null) obj.SetActive(false);
        }
        CancelAllPointerTasks();
    }

    private void CancelAllPointerTasks()
    {
        foreach (var kv in _tasks)
        {
            if (kv.Value != null)
            {
                kv.Value.Cancel();
                kv.Value.Dispose();
            }
        }
        _tasks.Clear();
    }

    private void Update()
    {
        UpdateEdgeArrows();
    }

    private void FixedUpdate()
    {
        if (!_worldPointersEnabled || _pointers == null)
        {
            return;
        }

        if (_cachedMainCam == null) _cachedMainCam = Camera.main;
        Transform camTf = _cachedMainCam != null ? _cachedMainCam.transform : transform;
        float angleThreshold = Mathf.Cos(viewAngle * 0.5f * Mathf.Deg2Rad);
        Vector3 origin = camTf.position;
        Vector3 forward = camTf.forward;

        foreach (GameObject obj in _pointers)
        {
            if (obj == null) continue;
            Transform pointer = obj.transform;
            bool isVisible = CheckVisibility(pointer, origin, forward, angleThreshold);

            if (isVisible)
            {
                obj.SetActive(true);
                if (!_tasks.ContainsKey(pointer))
                {
                    var cts = new CancellationTokenSource();
                    _tasks[pointer] = cts;
                    PointersMoving(pointer, cts.Token).Forget();
                }
            }
            else
            {
                obj.SetActive(false);
                if (_tasks.TryGetValue(pointer, out var cts))
                {
                    cts.Cancel();
                    cts.Dispose();
                    _tasks.Remove(pointer);
                }
            }
        }
    }

    private void ApplyEdgeArrowAlpha(bool isLeft, float alpha)
    {
        alpha = Mathf.Clamp(alpha, 0f, MaxEdgeArrowAlpha);
        if (isLeft)
        {
            _leftArrowAlpha = alpha;
            if (_leftArrowGroup != null) _leftArrowGroup.alpha = alpha;
            if (_leftArrowImage != null)
            {
                Color c = _leftArrowImage.color;
                _leftArrowImage.color = new Color(c.r, c.g, c.b, 1f);
            }
        }
        else
        {
            _rightArrowAlpha = alpha;
            if (_rightArrowGroup != null) _rightArrowGroup.alpha = alpha;
            if (_rightArrowImage != null)
            {
                Color c = _rightArrowImage.color;
                _rightArrowImage.color = new Color(c.r, c.g, c.b, 1f);
            }
        }
    }

    private void AnimateEdgeArrow(bool isLeft, bool nextVisible, float fadeDuration)
    {
        if (isLeft)
        {
            if (_leftArrowVisible == nextVisible) return;
            _leftArrowVisible = nextVisible;
            _leftFadeTween.Stop();

            float targetAlpha = nextVisible ? MaxEdgeArrowAlpha : 0f;
            Ease ease = nextVisible ? Ease.OutSine : Ease.InOutSine;
            _leftFadeTween = Tween.Custom(
                _leftArrowAlpha,
                targetAlpha,
                fadeDuration,
                v => ApplyEdgeArrowAlpha(true, v),
                ease,
                useUnscaledTime: true);
        }
        else
        {
            if (_rightArrowVisible == nextVisible) return;
            _rightArrowVisible = nextVisible;
            _rightFadeTween.Stop();

            float targetAlpha = nextVisible ? MaxEdgeArrowAlpha : 0f;
            Ease ease = nextVisible ? Ease.OutSine : Ease.InOutSine;
            _rightFadeTween = Tween.Custom(
                _rightArrowAlpha,
                targetAlpha,
                fadeDuration,
                v => ApplyEdgeArrowAlpha(false, v),
                ease,
                useUnscaledTime: true);
        }
    }

    private void UpdateEdgeArrows()
    {
        if (_leftArrowGroup == null || _rightArrowGroup == null) return;

        if (!_guidanceActive || _guidanceTarget == null || !_guidanceTarget.gameObject.activeInHierarchy)
        {
            AnimateEdgeArrow(isLeft: true, nextVisible: false, fadeDuration: 1.15f);
            AnimateEdgeArrow(isLeft: false, nextVisible: false, fadeDuration: 1.15f);
            return;
        }

        if (_cachedMainCam == null) _cachedMainCam = Camera.main;
        Transform refTf = _cachedMainCam != null ? _cachedMainCam.transform : transform;
        Vector3 toTarget = _guidanceTarget.position - refTf.position;
        Vector3 flatToTarget = new Vector3(toTarget.x, 0f, toTarget.z);
        float flatDist = flatToTarget.magnitude;

        if (flatDist < 2.8f)
        {
            AnimateEdgeArrow(isLeft: true, nextVisible: false, fadeDuration: 1.15f);
            AnimateEdgeArrow(isLeft: false, nextVisible: false, fadeDuration: 1.15f);
            return;
        }

        Vector3 flatForward = new Vector3(refTf.forward.x, 0f, refTf.forward.z);
        if (flatForward.sqrMagnitude < 0.0001f) flatForward = transform.forward;
        flatForward.Normalize();
        flatToTarget /= flatDist;

        // SignedAngle < 0: цель слева; SignedAngle > 0: цель справа (с гистерезисом против мерцания)
        float signedAngle = Vector3.SignedAngle(flatForward, flatToTarget, Vector3.up);
        bool wantLeft = _leftArrowVisible ? (signedAngle < -14f) : (signedAngle < -18f);
        bool wantRight = _rightArrowVisible ? (signedAngle > 14f) : (signedAngle > 18f);

        AnimateEdgeArrow(isLeft: true, nextVisible: wantLeft, fadeDuration: wantLeft ? 0.65f : 1.25f);
        AnimateEdgeArrow(isLeft: false, nextVisible: wantRight, fadeDuration: wantRight ? 0.65f : 1.25f);

        if (_leftArrowAlpha <= 0.001f && _rightArrowAlpha <= 0.001f)
            return;

        float pulse = 0.88f + 0.12f * Mathf.Sin(Time.unscaledTime * 5.2f);
        float offsetBob = Mathf.Sin(Time.unscaledTime * 5.2f) * 10f;

        if (_leftArrowImage != null && _leftArrowAlpha > 0.001f)
        {
            _leftArrowImage.color = new Color(pulse, 0.08f * pulse, 0.08f * pulse, _leftArrowAlpha);
            _leftArrowRect.anchoredPosition = new Vector2(86f - offsetBob, 0f);
        }
        if (_rightArrowImage != null && _rightArrowAlpha > 0.001f)
        {
            _rightArrowImage.color = new Color(pulse, 0.08f * pulse, 0.08f * pulse, _rightArrowAlpha);
            _rightArrowRect.anchoredPosition = new Vector2(-86f + offsetBob, 0f);
        }
    }

    private Material _arrowMaterial;

    private void BuildEdgeArrowsHUD()
    {
        GameObject canvasGo = new GameObject("DirectionalArrowsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        canvasGo.layer = 5;

        _edgeArrowCanvas = canvasGo.GetComponent<Canvas>();
        _edgeArrowCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _edgeArrowCanvas.overrideSorting = true;
        _edgeArrowCanvas.sortingOrder = 25000; // Поверх PixelCanvas (10000), но ниже BlackImageCanvas (32767)

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GraphicRaycaster raycaster = canvasGo.GetComponent<GraphicRaycaster>();
        raycaster.enabled = false;

        _arrowSprite = CreateDoubleChevronSprite();

        Shader brightShader = Shader.Find("Custom/BrightInteractUI");
        if (brightShader != null)
        {
            _arrowMaterial = new Material(brightShader);
            _arrowMaterial.SetColor("_Color", Color.white);
        }

        // Стрелка слева (< <)
        _leftArrowRect = CreateArrowElement(
            canvasGo.transform,
            "LeftDirectionArrow",
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(86f, 0f),
            flipX: true,
            out _leftArrowGroup,
            out _leftArrowImage);

        // Стрелка справа (> >)
        _rightArrowRect = CreateArrowElement(
            canvasGo.transform,
            "RightDirectionArrow",
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(-86f, 0f),
            flipX: false,
            out _rightArrowGroup,
            out _rightArrowImage);
    }

    private RectTransform CreateArrowElement(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPos,
        bool flipX,
        out CanvasGroup group,
        out Image img)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);
        go.layer = 5;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(148f, 148f);
        rt.anchoredPosition = anchoredPos;
        rt.localScale = new Vector3(flipX ? -1f : 1f, 1f, 1f);

        img = go.GetComponent<Image>();
        img.sprite = _arrowSprite;
        img.material = _arrowMaterial;
        img.color = new Color(1f, 0.08f, 0.08f, 0f);
        img.raycastTarget = false;

        group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        return rt;
    }

    private static Sprite CreateDoubleChevronSprite()
    {
        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color32 clear = new Color32(0, 0, 0, 0);
        Color32[] pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

        // Чисто белая текстура без тёмных пикселей, чтобы цвет полностью задавался ярко-красным Image.color через Custom/BrightInteractUI
        DrawChevron(pixels, size, centerX: 24, halfHeight: 46, thickness: 18);
        DrawChevron(pixels, size, centerX: 60, halfHeight: 46, thickness: 18);

        tex.SetPixels32(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static void DrawChevron(Color32[] pixels, int size, int centerX, int halfHeight, int thickness)
    {
        int midY = size / 2;
        for (int dy = -halfHeight; dy <= halfHeight; dy++)
        {
            int y = midY + dy;
            if (y < 2 || y >= size - 2) continue;

            int shift = halfHeight - Mathf.Abs(dy);
            int xStart = centerX + shift - thickness / 2;

            for (int t = -1; t <= thickness + 1; t++)
            {
                int x = xStart + t;
                if (x < 2 || x >= size - 2) continue;

                bool isSoftEdge = (t == -1 || t == thickness + 1 || Mathf.Abs(dy) == halfHeight);
                byte alpha = isSoftEdge ? (byte)160 : (byte)255;
                Color32 existing = pixels[y * size + x];
                if (alpha > existing.a)
                {
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
        }
    }

    private bool CheckVisibility(Transform target, Vector3 origin, Vector3 forward, float threshold)
    {
        Vector3 dirToTarget = target.position - origin;
        float distance = dirToTarget.magnitude;

        if (distance > viewDistance || distance < 0.01f) return false;

        dirToTarget /= distance;
        float dot = Vector3.Dot(forward, dirToTarget);

        return dot > threshold;
    }

    private async UniTask PointersMoving(Transform pointer, CancellationToken token)
    {
        Vector3 startPos = pointer.localPosition;
        float upPos = startPos.y + 4f;
        float downPos = startPos.y;
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Tween.LocalPositionY(pointer, upPos, 1f, Ease.InOutBack).ToUniTask(cancellationToken: token);
                await UniTask.Delay(700, cancellationToken: token);
                await Tween.LocalPositionY(pointer, downPos, 1f, Ease.InOutBack).ToUniTask(cancellationToken: token);
                await UniTask.Yield(token);
            }
        }
        catch (System.OperationCanceledException) { }
    }
}
