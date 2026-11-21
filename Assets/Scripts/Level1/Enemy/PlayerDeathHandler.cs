using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using HorrorPostProcess;
using KinematicCharacterController.Examples;
using PrimeTween;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Управляет кинематографичной смертью игрока:
/// 1) В первую секунду — удар и падение на землю с заваливанием камеры;
/// 2) Плавное затемнение экрана в тёмно-багровую виньетку;
/// 3) Появление надписи «Вы погибли...» фирменным шрифтом с каплями крови,
///    стекающими вниз прямо из многоточия, и перезапуск текущей сцены.
/// </summary>
public class PlayerDeathHandler : MonoBehaviour
{
    public static bool IsDead { get; private set; }

    private const float FallDuration = 1.0f;
    private const float FadeInDuration = 1.55f;
    private const float DeathScreenHoldDuration = 2.65f;
    private const float FinalFadeDuration = 0.45f;

    private static PlayerDeathHandler _activeInstance;

    private EnemyAI _killer;
    private Camera _mainCamera;
    private Transform _camTransform;
    private Vector3 _startCamPos;
    private Vector3 _groundCamPos;
    private Quaternion _startCamRot;
    private Quaternion _impactLookRot;
    private Quaternion _fallenCamRot;
    private float _elapsed;

    private readonly List<UnityEngine.Object> _runtimeAssets = new();
    private readonly List<BloodDrip> _drips = new();
    private bool _dripsActive;
    private float _dripsMasterAlpha;

    private sealed class BloodDrip
    {
        public Vector2 Origin;
        public RectTransform StreakRt;
        public Image StreakImg;
        public RectTransform DropRt;
        public Image DropImg;
        public float CycleDuration;
        public float Timer;
        public float MaxTravel;
        public float StreakMaxLength;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        IsDead = false;
        _activeInstance = null;
    }

    public static void TriggerPlayerDeath(EnemyAI killer)
    {
        if (IsDead) return;
        IsDead = true;

        var host = new GameObject("[PlayerDeathSequence]");
        _activeInstance = host.AddComponent<PlayerDeathHandler>();
        _activeInstance.BeginSequence(killer);
    }

    private void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        for (int i = 0; i < _runtimeAssets.Count; i++)
        {
            if (_runtimeAssets[i] != null) Destroy(_runtimeAssets[i]);
        }
        _runtimeAssets.Clear();

        if (_activeInstance == this)
        {
            _activeInstance = null;
            IsDead = false;
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        IsDead = false;
        _activeInstance = null;
    }

    private void BeginSequence(EnemyAI killer)
    {
        _killer = killer;
        ExampleCharacterController character = FreezePlayerControls();
        SpikeHorrorShader();

        _mainCamera = Camera.main;
        if (_mainCamera != null)
        {
            _camTransform = _mainCamera.transform;
            _startCamPos = _camTransform.position;
            _startCamRot = _camTransform.rotation;
            ComputeCollapsePose(character);
        }

        RunDeathRoutine().Forget();
    }

    private void ComputeCollapsePose(ExampleCharacterController character)
    {
        Vector3 awayFromKiller = Vector3.back;
        if (_killer != null)
        {
            Vector3 diff = _startCamPos - _killer.transform.position;
            diff.y = 0f;
            if (diff.sqrMagnitude > 0.001f) awayFromKiller = diff.normalized;

            Vector3 dirToHead = _killer.GetHeadAimPosition() - _startCamPos;
            _impactLookRot = dirToHead.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(dirToHead.normalized, Vector3.up)
                : _startCamRot;
        }
        else
        {
            _impactLookRot = _startCamRot;
        }

        float groundY = character != null ? character.transform.position.y : _startCamPos.y - 4.5f;
        if (Physics.Raycast(_startCamPos + Vector3.up * 0.25f, Vector3.down, out RaycastHit hit, 30f, ~0, QueryTriggerInteraction.Ignore))
        {
            groundY = hit.point.y;
        }

        // Опускаем камеру прямо к поверхности земли и слегка отбрасываем назад
        Vector3 rightOffset = Vector3.Cross(Vector3.up, awayFromKiller).normalized * 0.28f;
        _groundCamPos = new Vector3(_startCamPos.x, groundY + 0.34f, _startCamPos.z) + awayFromKiller * 0.45f + rightOffset;

        // Завал камеры на бок со взглядом чуть вверх при падении на землю
        Vector3 lookEuler = _impactLookRot.eulerAngles;
        _fallenCamRot = Quaternion.Euler(Mathf.DeltaAngle(0f, lookEuler.x) * 0.25f - 10f, lookEuler.y + 14f, 64f);
    }

    private static ExampleCharacterController FreezePlayerControls()
    {
        var character = FindAnyObjectByType<ExampleCharacterController>();
        if (character != null)
        {
            var zeroInputs = new PlayerCharacterInputs();
            character.SetInputs(ref zeroInputs);
            if (character.Motor != null)
            {
                character.Motor.BaseVelocity = Vector3.zero;
            }
            character.enabled = false;
        }

        var examplePlayer = FindAnyObjectByType<ExamplePlayer>();
        if (examplePlayer != null)
        {
            var playerInput = examplePlayer.GetComponent<PlayerInput>();
            if (playerInput != null) playerInput.enabled = false;
            examplePlayer.enabled = false;
        }

        foreach (var axisController in FindObjectsByType<CinemachineInputAxisController>(FindObjectsInactive.Include))
        {
            axisController.enabled = false;
        }

        // Отключаем CinemachineBrain, чтобы опустить камеру на землю и направить взгляд при ударе
        if (Camera.main != null)
        {
            var brain = Camera.main.GetComponent<CinemachineBrain>();
            if (brain != null) brain.enabled = false;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        return character;
    }

    private static void SpikeHorrorShader()
    {
        foreach (var shader in FindObjectsByType<HorrorShaderController>(FindObjectsInactive.Include))
        {
            shader.SetFearLevelImmediate(1f);
        }
    }

    private void Update()
    {
        if (!_dripsActive || _drips.Count == 0) return;

        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < _drips.Count; i++)
        {
            BloodDrip d = _drips[i];
            d.Timer = (d.Timer + dt) % d.CycleDuration;
            float u = d.Timer / d.CycleDuration;

            // 0..0.38: капля набухает и вытягивает за собой полоску крови; 0.38..0.95: падает вниз с ускорением
            if (u < 0.38f)
            {
                float p = u / 0.38f;
                float streakLen = Mathf.Lerp(2f, d.StreakMaxLength, p * p);
                d.StreakRt.anchoredPosition = d.Origin;
                d.StreakRt.sizeDelta = new Vector2(2.2f, streakLen);
                d.StreakImg.color = new Color(0.62f, 0.02f, 0.03f, _dripsMasterAlpha * Mathf.SmoothStep(0.35f, 0.92f, p));

                float swell = Mathf.Lerp(0.35f, 1.0f, p);
                d.DropRt.anchoredPosition = d.Origin + new Vector2(0f, -streakLen + 1f);
                d.DropRt.localScale = new Vector3(swell, swell * Mathf.Lerp(0.8f, 1.25f, p), 1f);
                d.DropImg.color = new Color(0.76f, 0.03f, 0.04f, _dripsMasterAlpha * 0.96f);
            }
            else
            {
                float f = Mathf.Clamp01((u - 0.38f) / 0.57f);
                float fallDist = d.StreakMaxLength + d.MaxTravel * (f * f);
                float fadeOut = f > 0.72f ? 1f - Mathf.InverseLerp(0.72f, 1f, f) : 1f;

                float trailAlpha = (1f - Mathf.Clamp01(f * 1.4f)) * 0.65f;
                d.StreakRt.anchoredPosition = d.Origin;
                d.StreakRt.sizeDelta = new Vector2(1.8f, d.StreakMaxLength * (1f - f * 0.55f));
                d.StreakImg.color = new Color(0.55f, 0.02f, 0.03f, _dripsMasterAlpha * trailAlpha);

                d.DropRt.anchoredPosition = d.Origin + new Vector2(0f, -fallDist);
                d.DropRt.localScale = new Vector3(0.92f, 1.42f, 1f);
                d.DropImg.color = new Color(0.74f, 0.03f, 0.04f, _dripsMasterAlpha * fadeOut);
            }
        }
    }

    private void LateUpdate()
    {
        if (!IsDead || _camTransform == null) return;

        _elapsed += Time.unscaledDeltaTime;

        // Резкий взгляд на монстра в первую четверть секунды
        float aimT = Mathf.Clamp01(_elapsed / 0.22f);
        Quaternion baseAim = Quaternion.Slerp(_startCamRot, _impactLookRot, aimT);

        // В течение первой секунды игрок физически проваливается / падает на землю с завалом камеры
        float fallRaw = Mathf.Clamp01((_elapsed - 0.06f) / (FallDuration - 0.06f));
        float fallPosT = fallRaw * fallRaw; // ускорение падения
        float settleBounce = fallRaw > 0.85f ? Mathf.Sin((fallRaw - 0.85f) / 0.15f * Mathf.PI) * 0.08f : 0f;
        _camTransform.position = Vector3.Lerp(_startCamPos, _groundCamPos, fallPosT) + Vector3.up * settleBounce;

        float rotFallT = Mathf.SmoothStep(0f, 1f, fallRaw);
        Quaternion collapsedRot = Quaternion.Slerp(baseAim, _fallenCamRot, rotFallT);

        // Тряска в момент удара и касания земли
        Vector3 shakeEuler = Vector3.zero;
        if (_elapsed < 0.45f)
        {
            float decay = 1f - (_elapsed / 0.45f);
            shakeEuler = SampleCameraShake(_elapsed, 1.3f * decay);
        }
        else if (_elapsed > 0.82f && _elapsed < 1.12f)
        {
            float hitDecay = 1f - Mathf.Abs(_elapsed - 0.94f) / 0.18f;
            if (hitDecay > 0f) shakeEuler = SampleCameraShake(_elapsed + 9f, 0.45f * hitDecay);
        }

        _camTransform.rotation = collapsedRot * Quaternion.Euler(shakeEuler);
    }

    private static Vector3 SampleCameraShake(float t, float intensity)
    {
        return new Vector3(
            (Mathf.PerlinNoise(t * 38f, 0f) - 0.5f) * 14f,
            (Mathf.PerlinNoise(0f, t * 41f) - 0.5f) * 14f,
            (Mathf.PerlinNoise(t * 33f, 7f) - 0.5f) * 10f
        ) * intensity;
    }

    private async UniTaskVoid RunDeathRoutine()
    {
        BuildDeathOverlay(
            out Canvas canvas,
            out Image crimsonFlash,
            out Image blackFade,
            out Image vignette,
            out TextMeshProUGUI deathShadow,
            out TextMeshProUGUI deathText,
            out Image dividerLine,
            out TextMeshProUGUI subText);

        // Кровавая вспышка удара + плавное угасание экрана во время падения на землю
        _ = Tween.Alpha(crimsonFlash, 0.58f, 0.08f, Ease.OutQuad, useUnscaledTime: true);
        _ = Tween.Alpha(vignette, 0.92f, 0.90f, Ease.InOutSine, useUnscaledTime: true);
        _ = Tween.Alpha(blackFade, 0.95f, FadeInDuration, Ease.InOutQuad, useUnscaledTime: true);

        await UniTask.Delay(TimeSpan.FromSeconds(0.18), ignoreTimeScale: true);
        if (this == null) return;

        _ = Tween.Alpha(crimsonFlash, 0.12f, 1.10f, Ease.OutQuad, useUnscaledTime: true);

        // Ждём окончания падения (~1-я секунда), затем проявляем «Вы погибли...» и запускаем капли крови
        await UniTask.Delay(TimeSpan.FromSeconds(0.82), ignoreTimeScale: true);
        if (this == null) return;

        _dripsActive = true;
        _ = Tween.Custom(0f, 1f, 0.65f, v => _dripsMasterAlpha = v, Ease.OutQuad, useUnscaledTime: true);
        if (deathShadow != null) _ = Tween.Alpha(deathShadow, 0.85f, 0.65f, Ease.OutQuad, useUnscaledTime: true);
        if (deathText != null) _ = Tween.Alpha(deathText, 1f, 0.65f, Ease.OutQuad, useUnscaledTime: true);
        if (dividerLine != null) _ = Tween.Alpha(dividerLine, 0.90f, 0.75f, Ease.OutQuad, useUnscaledTime: true);
        if (subText != null)
        {
            Tween.Delay(0.25f, () =>
            {
                if (subText != null) _ = Tween.Alpha(subText, 0.90f, 0.65f, Ease.OutQuad, useUnscaledTime: true);
            }, useUnscaledTime: true);
        }

        await UniTask.Delay(TimeSpan.FromSeconds(DeathScreenHoldDuration), ignoreTimeScale: true);
        if (this == null) return;

        // Финальное полное затемнение перед перезапуском сцены
        _ = Tween.Custom(_dripsMasterAlpha, 0f, FinalFadeDuration, v => _dripsMasterAlpha = v, Ease.InQuad, useUnscaledTime: true);
        _ = Tween.Alpha(blackFade, 1f, FinalFadeDuration, Ease.InQuad, useUnscaledTime: true);
        if (deathShadow != null) _ = Tween.Alpha(deathShadow, 0f, FinalFadeDuration, Ease.InQuad, useUnscaledTime: true);
        if (deathText != null) _ = Tween.Alpha(deathText, 0f, FinalFadeDuration, Ease.InQuad, useUnscaledTime: true);
        if (dividerLine != null) _ = Tween.Alpha(dividerLine, 0f, FinalFadeDuration, Ease.InQuad, useUnscaledTime: true);
        if (subText != null) _ = Tween.Alpha(subText, 0f, FinalFadeDuration, Ease.InQuad, useUnscaledTime: true);

        await UniTask.Delay(TimeSpan.FromSeconds(FinalFadeDuration + 0.08f), ignoreTimeScale: true);
        if (this == null) return;

        string activeSceneName = SceneManager.GetActiveScene().name;
        Time.timeScale = MenuReturn.BaseTimeScale > 0f ? MenuReturn.BaseTimeScale : 0.5f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        IsDead = false;

        if (canvas != null) Destroy(canvas.gameObject);
        SceneManager.LoadScene(activeSceneName, LoadSceneMode.Single);
    }

    private void BuildDeathOverlay(
        out Canvas canvas,
        out Image crimsonFlash,
        out Image blackFade,
        out Image vignette,
        out TextMeshProUGUI deathShadow,
        out TextMeshProUGUI deathText,
        out Image dividerLine,
        out TextMeshProUGUI subText)
    {
        var canvasGo = new GameObject("[DeathOverlayCanvas]");
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32765;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        canvasGo.AddComponent<GraphicRaycaster>();

        blackFade = CreateFullscreenImage(canvasGo.transform, "DeathBlackFade", new Color(0.02f, 0.004f, 0.005f, 0f));
        crimsonFlash = CreateFullscreenImage(canvasGo.transform, "DeathCrimsonFlash", new Color(0.62f, 0.02f, 0.03f, 0f));

        Sprite vignetteSprite = CreateVignetteSprite();
        vignette = CreateFullscreenImage(canvasGo.transform, "DeathVignette", new Color(1f, 1f, 1f, 0f));
        vignette.sprite = vignetteSprite;
        vignette.raycastTarget = false;

        TMP_FontAsset font = ResolveMenuFont();
        var panelGo = new GameObject("DeathContent", typeof(RectTransform));
        panelGo.transform.SetParent(canvasGo.transform, false);
        RectTransform panelRt = (RectTransform)panelGo.transform;
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(680f, 240f);
        panelRt.anchoredPosition = new Vector2(0f, 18f);

        deathShadow = CreateLabel(
            panelGo.transform,
            "DeathLabelShadow",
            MoBA.Settings.GameLocalization.Get("You died..."),
            font,
            36f,
            10f,
            new Color(0.32f, 0.01f, 0.02f, 0f),
            new Vector2(0f, 14f));

        deathText = CreateLabel(
            panelGo.transform,
            "DeathLabel",
            MoBA.Settings.GameLocalization.Get("You died..."),
            font,
            36f,
            10f,
            new Color(0.91f, 0.87f, 0.78f, 0f),
            new Vector2(0f, 16f));

        // Тонкая багровая линия-разделитель как на утверждённом концепте
        Sprite lineSprite = CreateDividerSprite();
        var lineGo = new GameObject("DeathDivider", typeof(RectTransform), typeof(Image));
        lineGo.transform.SetParent(panelGo.transform, false);
        RectTransform lineRt = (RectTransform)lineGo.transform;
        lineRt.anchorMin = new Vector2(0.5f, 0.5f);
        lineRt.anchorMax = new Vector2(0.5f, 0.5f);
        lineRt.pivot = new Vector2(0.5f, 0.5f);
        lineRt.anchoredPosition = new Vector2(0f, -18f);
        lineRt.sizeDelta = new Vector2(310f, 2f);
        dividerLine = lineGo.GetComponent<Image>();
        dividerLine.sprite = lineSprite;
        dividerLine.color = new Color(0.68f, 0.04f, 0.05f, 0f);
        dividerLine.raycastTarget = false;

        subText = CreateLabel(
            panelGo.transform,
            "DeathSubLabel",
            MoBA.Settings.GameLocalization.Get("IT FOUND YOU"),
            font,
            12.5f,
            20f,
            new Color(0.62f, 0.58f, 0.56f, 0f),
            new Vector2(0f, -44f));

        SpawnBloodDripsFromEllipsis(deathText);
    }

    private void SpawnBloodDripsFromEllipsis(TextMeshProUGUI deathText)
    {
        _drips.Clear();
        if (deathText == null) return;

        deathText.ForceMeshUpdate();
        TMP_TextInfo textInfo = deathText.textInfo;
        string txt = deathText.text;

        var dotPositions = new List<Vector2>(3);
        for (int i = Mathf.Max(0, txt.Length - 3); i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo ch = textInfo.characterInfo[i];
            if (ch.character != '.') continue;
            float x = (ch.bottomLeft.x + ch.topRight.x) * 0.5f;
            float y = ch.bottomLeft.y + 1.5f;
            dotPositions.Add(new Vector2(x, y));
        }

        if (dotPositions.Count == 0)
        {
            dotPositions.Add(new Vector2(124f, -6f));
            dotPositions.Add(new Vector2(132f, -6f));
            dotPositions.Add(new Vector2(140f, -6f));
        }

        Sprite dropSprite = CreateBloodDropSprite();
        Sprite streakSprite = CreateBloodStreakSprite();

        float[] cycleOffsets = { 0.0f, 0.48f, 0.22f };
        float[] cycleDurations = { 1.35f, 1.65f, 1.48f };
        float[] streakLengths = { 28f, 36f, 24f };
        float[] travelDists = { 125f, 155f, 110f };

        for (int i = 0; i < dotPositions.Count; i++)
        {
            var streakGo = new GameObject($"BloodStreak_{i}", typeof(RectTransform), typeof(Image));
            streakGo.transform.SetParent(deathText.rectTransform, false);
            RectTransform streakRt = (RectTransform)streakGo.transform;
            streakRt.anchorMin = new Vector2(0.5f, 0.5f);
            streakRt.anchorMax = new Vector2(0.5f, 0.5f);
            streakRt.pivot = new Vector2(0.5f, 1f);
            streakRt.anchoredPosition = dotPositions[i];
            streakRt.sizeDelta = new Vector2(2f, 2f);
            Image streakImg = streakGo.GetComponent<Image>();
            streakImg.sprite = streakSprite;
            streakImg.color = new Color(0.62f, 0.02f, 0.03f, 0f);
            streakImg.raycastTarget = false;

            var dropGo = new GameObject($"BloodDrop_{i}", typeof(RectTransform), typeof(Image));
            dropGo.transform.SetParent(deathText.rectTransform, false);
            RectTransform dropRt = (RectTransform)dropGo.transform;
            dropRt.anchorMin = new Vector2(0.5f, 0.5f);
            dropRt.anchorMax = new Vector2(0.5f, 0.5f);
            dropRt.pivot = new Vector2(0.5f, 1f);
            dropRt.anchoredPosition = dotPositions[i];
            dropRt.sizeDelta = new Vector2(6.5f, 11.5f);
            Image dropImg = dropGo.GetComponent<Image>();
            dropImg.sprite = dropSprite;
            dropImg.color = new Color(0.76f, 0.03f, 0.04f, 0f);
            dropImg.raycastTarget = false;

            _drips.Add(new BloodDrip
            {
                Origin = dotPositions[i],
                StreakRt = streakRt,
                StreakImg = streakImg,
                DropRt = dropRt,
                DropImg = dropImg,
                CycleDuration = cycleDurations[i % cycleDurations.Length],
                Timer = cycleOffsets[i % cycleOffsets.Length],
                MaxTravel = travelDists[i % travelDists.Length],
                StreakMaxLength = streakLengths[i % streakLengths.Length],
            });
        }
    }

    private static TMP_FontAsset ResolveMenuFont()
    {
        foreach (TMP_FontAsset loaded in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (loaded != null && loaded.name.IndexOf("baskerville", StringComparison.OrdinalIgnoreCase) >= 0)
                return loaded;
        }
        TextMeshProUGUI anyTmp = FindAnyObjectByType<TextMeshProUGUI>();
        if (anyTmp != null && anyTmp.font != null) return anyTmp.font;
        return TMP_Settings.defaultFontAsset;
    }

    private static TextMeshProUGUI CreateLabel(
        Transform parent,
        string name,
        string text,
        TMP_FontAsset font,
        float fontSize,
        float characterSpacing,
        Color color,
        Vector2 anchoredPos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(640f, 80f);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.characterSpacing = characterSpacing;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Image CreateFullscreenImage(Transform parent, string name, Color initialColor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.AddComponent<Image>();
        img.color = initialColor;
        img.raycastTarget = true;
        return img;
    }

    private Sprite CreateVignetteSprite()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        for (int y = 0; y < size; y++)
        {
            float ny = (y + 0.5f) / size * 2f - 1f;
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(nx * nx * 0.72f + ny * ny);
                float edge = Mathf.SmoothStep(0.32f, 1.15f, r);
                tex.SetPixel(x, y, new Color(0.10f, 0.004f, 0.006f, edge * 0.88f));
            }
        }
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        _runtimeAssets.Add(tex);
        _runtimeAssets.Add(sprite);
        return sprite;
    }

    private Sprite CreateDividerSprite()
    {
        const int w = 128;
        var tex = new Texture2D(w, 2, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        for (int x = 0; x < w; x++)
        {
            float u = (x + 0.5f) / w * 2f - 1f;
            float a = Mathf.Clamp01(1f - Mathf.Pow(Mathf.Abs(u), 2.4f));
            Color c = new Color(1f, 1f, 1f, a);
            tex.SetPixel(x, 0, c);
            tex.SetPixel(x, 1, c);
        }
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, w, 2), new Vector2(0.5f, 0.5f));
        _runtimeAssets.Add(tex);
        _runtimeAssets.Add(sprite);
        return sprite;
    }

    private Sprite CreateBloodDropSprite()
    {
        const int w = 24;
        const int h = 40;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        for (int y = 0; y < h; y++)
        {
            float v = (y + 0.5f) / h; // 0 = низ капли, 1 = острый верх
            float radius = v < 0.36f
                ? Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((0.36f - v) / 0.36f, 2f))) * 0.85f
                : Mathf.Pow(1f - (v - 0.36f) / 0.64f, 1.35f) * 0.85f;
            for (int x = 0; x < w; x++)
            {
                float nx = Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                float a = radius > 0.01f ? Mathf.SmoothStep(radius + 0.12f, radius - 0.12f, nx) : 0f;
                float shine = (v > 0.18f && v < 0.45f && x > w * 0.25f && x < w * 0.48f) ? 1.15f : 1f;
                tex.SetPixel(x, y, new Color(shine, shine * 0.9f, shine * 0.9f, a));
            }
        }
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 1f));
        _runtimeAssets.Add(tex);
        _runtimeAssets.Add(sprite);
        return sprite;
    }

    private Sprite CreateBloodStreakSprite()
    {
        const int w = 8;
        const int h = 32;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        for (int y = 0; y < h; y++)
        {
            float v = (y + 0.5f) / h;
            float fadeEnds = Mathf.SmoothStep(0f, 0.2f, v) * Mathf.SmoothStep(1f, 0.82f, v);
            for (int x = 0; x < w; x++)
            {
                float nx = Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                float core = Mathf.SmoothStep(1f, 0.15f, nx);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, core * fadeEnds));
            }
        }
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 1f));
        _runtimeAssets.Add(tex);
        _runtimeAssets.Add(sprite);
        return sprite;
    }
}
