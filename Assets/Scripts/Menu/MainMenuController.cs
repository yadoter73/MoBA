using System;
using System.Collections.Generic;
using PrimeTween;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Random = UnityEngine.Random;

/// <summary>
/// Атмосферное главное меню. Весь UI строится из кода поверх 3D-сцены Menu:
/// виньетка, плёночное зерно, сканлайны, "сердцебиение" красным, глитч заголовка,
/// случайные шёпоты на экране, панели выбора глав и управления, затемнение при загрузке.
/// Escape / Cancel (новая Input System, UI/Cancel) закрывает открытую панель.
/// </summary>
[DefaultExecutionOrder(200)]
public class MainMenuController : MonoBehaviour
{
	[Serializable]
	public class Chapter
	{
		public string Title;
		public string Description;
		public string Scene;
	}

	[Header("Тексты")]
	[SerializeField] private string _title = "SAYA";
	[SerializeField] private string _subtitle = "she is still out there";
	[SerializeField] private string _newGameScene = "CarCrash";
	[SerializeField] private string _version = "v0.1";

	[Header("Главы")]
	[SerializeField] private List<Chapter> _chapters = new()
	{
		new Chapter { Title = "I.   THE CRASH",    Scene = "CarCrash", Description = "A wet road. Headlights. Then nothing." },
		new Chapter { Title = "II.  THE HOSPITAL", Scene = "Hospital", Description = "You wake up. She is not here." },
		new Chapter { Title = "III. THE FOREST",   Scene = "Level1",   Description = "Find Saya. Do not let them see you." },
	};

	[Header("Оформление")]
	[SerializeField] private TMP_FontAsset _font;
	[SerializeField] private Color _boneColor = new Color(0.82f, 0.79f, 0.74f, 1f);
	[SerializeField] private Color _bloodColor = new Color(0.62f, 0.03f, 0.03f, 1f);
	[SerializeField] private Color _dimColor = new Color(0.55f, 0.52f, 0.5f, 1f);

	[Header("Звук (необязательно)")]
	[SerializeField] private AudioClip _hoverClip;
	[SerializeField] private AudioClip _clickClip;
	[SerializeField] private AudioClip _ambientLoop;

	private static readonly string[] Whispers =
	{
		"saya?", "don't look back", "they can hear you", "it wasn't your fault",
		"wake up", "she is waiting", "keep your lights off", "who was driving?",
	};

	private RectTransform _root;
	private CanvasGroup _mainGroup;
	private CanvasGroup _fade;
	private CanvasGroup _currentPanel;
	private CanvasGroup _chaptersPanel;
	private CanvasGroup _controlsPanel;
	private Image _pulse;
	private TextMeshProUGUI _titleText;
	private TextMeshProUGUI _titleGhost;
	private TextMeshProUGUI _whisper;
	private AudioSource _audio;
	private InputAction _cancelAction;
	private Light _hospitalLamp;
	private float _lampBaseIntensity;
	private Transform _sayaRopedAnchor;
	private Quaternion _sayaBaseRotation;
	private CinemachineCamera _vrCamera;
	private CinemachinePanTilt _vrPanTilt;
	private CinemachinePositionComposer _vrComposer;
	private CinemachineInputAxisController _vrAxisController;
	private Button _continueButton;
	private Button _newGameButton;
	private Button _chaptersButton;
	private Button _controlsButton;
	private readonly List<Button> _mainButtons = new();
	private readonly List<Texture2D> _generated = new();

	private float _nextWhisper;
	private bool _busy;

	// ------------------------------------------------------------------ lifecycle

	private void Awake()
	{
		if (!MenuReturn.CanContinue)
		{
			Time.timeScale = MenuReturn.BaseTimeScale;
			AudioListener.pause = false;
		}
		Cursor.visible = true;
		Cursor.lockState = CursorLockMode.None;

		if (_font == null) _font = TMP_Settings.defaultFontAsset;

		_audio = gameObject.AddComponent<AudioSource>();
		_audio.playOnAwake = false;
		_audio.ignoreListenerPause = true;
		if (_ambientLoop != null)
		{
			_audio.clip = _ambientLoop;
			_audio.loop = true;
			_audio.volume = 0f;
			_audio.Play();
			Tween.AudioVolume(_audio, 0.55f, 3f, useUnscaledTime: true);
		}

		EnsureEventSystem();
		var lampObj = FindObjectInMenuScene("LanternLight") ?? FindObjectInMenuScene("HospitalLampLight");
		if (lampObj != null && lampObj.TryGetComponent(out _hospitalLamp))
			_lampBaseIntensity = _hospitalLamp.intensity;
		var sayaObj = FindObjectInMenuScene("SayaRopedAnchor");
		if (sayaObj != null)
		{
			_sayaRopedAnchor = sayaObj.transform;
			_sayaBaseRotation = _sayaRopedAnchor.localRotation;
		}
		EnsureMenuCinemachineLook();
		BuildUI();
	}

	private void OnEnable()
	{
		_cancelAction = InputSystem.actions != null ? InputSystem.actions.FindAction("UI/Cancel") : null;
		if (_cancelAction != null)
		{
			_cancelAction.Enable();
			_cancelAction.started += OnCancel;
		}
		EnsureMenuCinemachineLook();
	}

	private T FindInMenuScene<T>() where T : Component
	{
		var scene = gameObject.scene;
		if (!scene.IsValid()) return null;

		if (scene.isLoaded)
		{
			foreach (var root in scene.GetRootGameObjects())
			{
				var found = root.GetComponentInChildren<T>(true);
				if (found != null) return found;
			}
			return null;
		}

		// При аддитивной загрузке scene.isLoaded в Awake/OnEnable ещё false,
		// поэтому нельзя вызывать общий FindAnyObjectByType (он схватит камеру из игровой сцены).
		foreach (var candidate in FindObjectsByType<T>(FindObjectsInactive.Include))
		{
			if (candidate != null && candidate.gameObject.scene == scene)
				return candidate;
		}
		return null;
	}

	private GameObject FindObjectInMenuScene(string objectName)
	{
		var scene = gameObject.scene;
		if (!scene.IsValid() || string.IsNullOrEmpty(objectName)) return null;

		if (scene.isLoaded)
		{
			foreach (var root in scene.GetRootGameObjects())
			{
				foreach (var t in root.GetComponentsInChildren<Transform>(true))
				{
					if (t.name == objectName) return t.gameObject;
				}
			}
			return null;
		}

		foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include))
		{
			if (t != null && t.gameObject.scene == scene && t.name == objectName)
				return t.gameObject;
		}
		return null;
	}

	private void EnsureMenuCinemachineLook()
	{
		var menuScene = gameObject.scene;
		if (_vrCamera != null && _vrCamera.gameObject.scene != menuScene) _vrCamera = null;
		if (_vrPanTilt != null && _vrPanTilt.gameObject.scene != menuScene) _vrPanTilt = null;
		if (_vrComposer != null && _vrComposer.gameObject.scene != menuScene) _vrComposer = null;
		if (_vrAxisController != null && _vrAxisController.gameObject.scene != menuScene) _vrAxisController = null;

		if (_vrCamera == null) _vrCamera = FindInMenuScene<CinemachineCamera>();
		if (_vrPanTilt == null) _vrPanTilt = FindInMenuScene<CinemachinePanTilt>();
		if (_vrComposer == null) _vrComposer = FindInMenuScene<CinemachinePositionComposer>();
		if (_vrAxisController == null) _vrAxisController = FindInMenuScene<CinemachineInputAxisController>();

		if (_vrCamera != null)
		{
			_vrCamera.Priority.Enabled = true;
			_vrCamera.Priority.Value = 1000;
			_vrCamera.Lens.FieldOfView = 52f;
		}
		if (_vrComposer != null)
		{
			_vrComposer.CameraDistance = 5.4f;
		}
		if (_vrPanTilt != null)
		{
			_vrPanTilt.ReferenceFrame = CinemachinePanTilt.ReferenceFrames.ParentObject;
			_vrPanTilt.PanAxis.Wrap = false;
			_vrPanTilt.PanAxis.Center = 0f;
			_vrPanTilt.PanAxis.Range = new Vector2(-10f, 10f);
			_vrPanTilt.PanAxis.Value = 0f;

			_vrPanTilt.TiltAxis.Wrap = false;
			_vrPanTilt.TiltAxis.Center = 2f;
			_vrPanTilt.TiltAxis.Range = new Vector2(-8f, 12f);
			_vrPanTilt.TiltAxis.Value = 2f;
		}

		if (_vrAxisController == null) return;

		_vrAxisController.enabled = true;
		_vrAxisController.IgnoreTimeScale = true;
		if (_vrAxisController.Controllers != null)
		{
			foreach (var ctrl in _vrAxisController.Controllers)
			{
				if (ctrl?.Input == null) continue;
				ctrl.Enabled = true;
				float sign = ctrl.Name != null && ctrl.Name.Contains("Tilt") ? -1f : 1f;
				ctrl.Input.Gain = sign * 0.45f;
				if (ctrl.Input.InputAction != null && ctrl.Input.InputAction.action != null)
					ctrl.Input.InputAction.action.Enable();
			}
		}
	}

	private void ClampMenuCameraAxes()
	{
		if (_vrPanTilt == null || _vrPanTilt.gameObject.scene != gameObject.scene) return;
		_vrPanTilt.PanAxis.Wrap = false;
		_vrPanTilt.PanAxis.Range = new Vector2(-10f, 10f);
		_vrPanTilt.PanAxis.Value = Mathf.Clamp(_vrPanTilt.PanAxis.Value, -10f, 10f);

		_vrPanTilt.TiltAxis.Wrap = false;
		_vrPanTilt.TiltAxis.Range = new Vector2(-8f, 12f);
		_vrPanTilt.TiltAxis.Value = Mathf.Clamp(_vrPanTilt.TiltAxis.Value, -8f, 12f);
	}

	private void OnDisable()
	{
		if (_cancelAction != null) _cancelAction.started -= OnCancel;
	}

	private void OnDestroy()
	{
		foreach (var tex in _generated) if (tex != null) Destroy(tex);
	}

	private void Start()
	{
		if (_vrCamera == null || _vrPanTilt == null || _vrAxisController == null)
			EnsureMenuCinemachineLook();
		PlayIntro();
	}

	private void Update()
	{
		float t = Time.unscaledTime;

		// Очень медленное спокойное дыхание глубокого винного оттенка (без резких вспышек).
		if (_pulse != null)
		{
			float wave = 0.5f + 0.5f * Mathf.Sin(t * 1.15f);
			Color c = _bloodColor;
			c.a = 0.015f + wave * 0.025f;
			_pulse.color = c;
		}

		// Медленный дрейф призрачной подписи вместо резких глитч-скачков.
		if (_titleGhost != null && !_busy && _titleText != null && _titleText.alpha > 0.9f)
		{
			Vector2 basePos = _titleText.rectTransform.anchoredPosition;
			float driftX = Mathf.Sin(t * 0.75f) * 2.5f;
			float driftY = Mathf.Cos(t * 0.55f) * 1.5f;
			_titleGhost.rectTransform.anchoredPosition = basePos + new Vector2(2f + driftX, -2f + driftY);
			_titleGhost.alpha = 0.16f + 0.06f * Mathf.Sin(t * 1.1f);
		}

		if (_whisper != null && t >= _nextWhisper && !_busy) Whisper();

		// Ровное мягкое свечение фонаря без стробоскопического мерцания.
		if (_hospitalLamp != null)
		{
			float slowBreath = Mathf.PerlinNoise(t * 0.55f, 1.3f);
			float mul = Mathf.Lerp(0.88f, 1.06f, slowBreath);
			_hospitalLamp.intensity = _lampBaseIntensity * mul;
		}

		// Лёгкое, почти незаметное покачивание повешенной Саи на ветру.
		if (_sayaRopedAnchor != null)
		{
			float swayZ = Mathf.Sin(t * 0.85f) * 1.4f;
			float swayX = Mathf.Cos(t * 0.62f) * 0.6f;
			_sayaRopedAnchor.localRotation = _sayaBaseRotation * Quaternion.Euler(swayX, 0f, swayZ);
		}

		ClampMenuCameraAxes();
	}

	private void LateUpdate() => ClampMenuCameraAxes();

	// ------------------------------------------------------------------ build

	private void BuildUI()
	{
		var canvasGo = new GameObject("HorrorMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
		canvasGo.transform.SetParent(transform, false);
		var canvas = canvasGo.GetComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 10;
		var scaler = canvasGo.GetComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920, 1080);
		scaler.matchWidthOrHeight = 0.5f;
		_root = (RectTransform)canvasGo.transform;

		// --- чистые атмосферные слои без шумового зерна и строб-частиц
		var leftShade = CreateRaw("LeftShade", _root, MakeHorizontalGradient());
		Stretch(leftShade.rectTransform);
		leftShade.raycastTarget = false;

		_pulse = CreateImage("HeartbeatTint", _root, new Color(0, 0, 0, 0));
		Stretch(_pulse.rectTransform);

		var vignette = CreateRaw("Vignette", _root, MakeVignette());
		Stretch(vignette.rectTransform);

		_whisper = CreateText("Whisper", _root, "", 28, _bloodColor, TextAlignmentOptions.Center);
		_whisper.rectTransform.sizeDelta = new Vector2(900, 60);
		_whisper.alpha = 0f;
		_whisper.characterSpacing = 18f;

		// --- основной блок
		var main = CreateRect("Main", _root);
		Stretch(main);
		_mainGroup = main.gameObject.AddComponent<CanvasGroup>();

		// Тематический маркер дела поверх заголовка (атмосфера больницы и расследования)
		var caseTag = CreateText("CaseTag", main, "PATIENT RECORD // 04 — SAYA", 18, new Color(0.62f, 0.22f, 0.20f, 0.85f), TextAlignmentOptions.Left);
		Place(caseTag.rectTransform, new Vector2(158, 362), new Vector2(720, 32));
		caseTag.characterSpacing = 12f;

		// Заголовок + мягкая кровавая тень
		_titleGhost = CreateText("TitleGhost", main, _title, 200, _bloodColor, TextAlignmentOptions.Left);
		Place(_titleGhost.rectTransform, new Vector2(152, 248), new Vector2(1200, 240));
		_titleGhost.characterSpacing = 34f;
		_titleGhost.alpha = 0f;

		_titleText = CreateText("Title", main, _title, 200, _boneColor, TextAlignmentOptions.Left);
		Place(_titleText.rectTransform, new Vector2(150, 250), new Vector2(1200, 240));
		_titleText.characterSpacing = 34f;

		var line = CreateImage("TitleLine", main, _bloodColor);
		Place(line.rectTransform, new Vector2(156, 140), new Vector2(460, 2));
		line.rectTransform.pivot = new Vector2(0f, 0.5f);

		var subtitle = CreateText("Subtitle", main, _subtitle, 34, _dimColor, TextAlignmentOptions.Left);
		Place(subtitle.rectTransform, new Vector2(158, 100), new Vector2(900, 50));
		subtitle.characterSpacing = 14f;

		// Кнопки
		bool canContinue = MenuReturn.CanContinue;
		_continueButton = CreateMenuButton(main, "CONTINUE", new Vector2(150, 10), ContinueGame, 52f);
		ConfigureContinueButton(_continueButton, canContinue);
		_mainButtons.Add(_continueButton);

		_newGameButton = CreateMenuButton(main, "NEW GAME", new Vector2(150, -64), () => LoadScene(_newGameScene), 52f);
		_mainButtons.Add(_newGameButton);

		_chaptersButton = CreateMenuButton(main, "CHAPTERS", new Vector2(150, -138), () => OpenPanel(_chaptersPanel), 52f);
		_mainButtons.Add(_chaptersButton);

		_controlsButton = CreateMenuButton(main, "CONTROLS", new Vector2(150, -212), () => OpenPanel(_controlsPanel), 52f);
		_mainButtons.Add(_controlsButton);

		_mainButtons.Add(CreateMenuButton(main, "QUIT", new Vector2(150, -286), Quit, 52f));
		LinkVertical(canContinue ? _mainButtons : _mainButtons.GetRange(1, _mainButtons.Count - 1));

		var hint = CreateText("Hint", main, "headphones recommended. play in the dark.", 22, new Color(0.45f, 0.43f, 0.42f, 1f), TextAlignmentOptions.BottomLeft);
		hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0, 0);
		hint.rectTransform.pivot = new Vector2(0, 0);
		hint.rectTransform.anchoredPosition = new Vector2(156, 56);
		hint.rectTransform.sizeDelta = new Vector2(900, 40);
		hint.characterSpacing = 8f;

		var version = CreateText("Version", main, _version, 22, new Color(0.4f, 0.38f, 0.37f, 1f), TextAlignmentOptions.BottomRight);
		version.rectTransform.anchorMin = version.rectTransform.anchorMax = new Vector2(1, 0);
		version.rectTransform.pivot = new Vector2(1, 0);
		version.rectTransform.anchoredPosition = new Vector2(-60, 56);
		version.rectTransform.sizeDelta = new Vector2(300, 40);

		// --- панели
		_chaptersPanel = BuildChaptersPanel();
		_controlsPanel = BuildControlsPanel();

		// --- затемнение поверх всего
		var fadeImg = CreateImage("Fade", _root, Color.black);
		Stretch(fadeImg.rectTransform);
		_fade = fadeImg.gameObject.AddComponent<CanvasGroup>();
		_fade.alpha = 1f;
		_fade.blocksRaycasts = true;
	}

	private CanvasGroup BuildChaptersPanel()
	{
		var panel = CreatePanel("ChaptersPanel", "CHAPTERS", out var body);
		var buttons = new List<Button>();
		for (int i = 0; i < _chapters.Count; i++)
		{
			var ch = _chapters[i];
			float y = -i * 150f;
			var btn = CreateMenuButton(body, ch.Title, new Vector2(0, y), () => LoadScene(ch.Scene), 46, true);
			buttons.Add(btn);
			var desc = CreateText("Desc", body, ch.Description, 24, _dimColor, TextAlignmentOptions.TopLeft);
			Place(desc.rectTransform, new Vector2(52, y - 52), new Vector2(760, 40), true);
			desc.characterSpacing = 4f;
		}
		buttons.Add(CreateMenuButton(body, "BACK", new Vector2(0, -_chapters.Count * 150f - 30f), ClosePanel, 36, true));
		LinkVertical(buttons);
		panel.gameObject.SetActive(false);
		return panel;
	}

	private CanvasGroup BuildControlsPanel()
	{
		var panel = CreatePanel("ControlsPanel", "CONTROLS", out var body);
		string[,] rows =
		{
			{ "W A S D", "move" },
			{ "MOUSE", "look around" },
			{ "SHIFT", "run" },
			{ "CTRL", "crouch" },
			{ "SPACE", "jump" },
			{ "E", "interact" },
			{ "1 / 2", "previous / next" },
			{ "ESC", "back to menu" },
		};
		for (int i = 0; i < rows.GetLength(0); i++)
		{
			float y = -i * 58f;
			var key = CreateText("Key", body, rows[i, 0], 32, _boneColor, TextAlignmentOptions.Left);
			Place(key.rectTransform, new Vector2(0, y), new Vector2(260, 50), true);
			key.characterSpacing = 10f;
			var action = CreateText("Action", body, rows[i, 1], 30, _dimColor, TextAlignmentOptions.Left);
			Place(action.rectTransform, new Vector2(280, y), new Vector2(500, 50), true);
			action.characterSpacing = 6f;
		}
		var back = CreateMenuButton(body, "BACK", new Vector2(0, -rows.GetLength(0) * 58f - 40f), ClosePanel, 36, true);
		LinkVertical(new List<Button> { back });
		panel.gameObject.SetActive(false);
		return panel;
	}

	private CanvasGroup CreatePanel(string name, string header, out RectTransform body)
	{
		var panel = CreateRect(name, _root);
		Stretch(panel);
		var group = panel.gameObject.AddComponent<CanvasGroup>();

		var dim = CreateImage("Dim", panel, new Color(0, 0, 0, 0.55f));
		Stretch(dim.rectTransform);
		dim.raycastTarget = true;

		var frame = CreateRect("Frame", panel);
		frame.anchorMin = frame.anchorMax = new Vector2(1f, 0.5f);
		frame.pivot = new Vector2(1f, 0.5f);
		frame.anchoredPosition = new Vector2(-140, 0);
		frame.sizeDelta = new Vector2(900, 820);

		var bg = CreateImage("Bg", frame, new Color(0.03f, 0.02f, 0.02f, 0.82f));
		Stretch(bg.rectTransform);
		var edge = CreateImage("Edge", frame, _bloodColor);
		edge.rectTransform.anchorMin = new Vector2(0, 0);
		edge.rectTransform.anchorMax = new Vector2(0, 1);
		edge.rectTransform.pivot = new Vector2(0, 0.5f);
		edge.rectTransform.sizeDelta = new Vector2(4, 0);
		edge.rectTransform.anchoredPosition = Vector2.zero;

		var title = CreateText("Header", frame, header, 64, _boneColor, TextAlignmentOptions.TopLeft);
		Place(title.rectTransform, new Vector2(60, -50), new Vector2(780, 80), true);
		title.characterSpacing = 24f;

		var line = CreateImage("HeaderLine", frame, _bloodColor);
		Place(line.rectTransform, new Vector2(62, -140), new Vector2(300, 2), true);

		body = CreateRect("Body", frame);
		body.anchorMin = body.anchorMax = new Vector2(0, 1);
		body.pivot = new Vector2(0, 1);
		body.anchoredPosition = new Vector2(60, -190);
		body.sizeDelta = new Vector2(780, 600);
		return group;
	}

	private Button CreateMenuButton(RectTransform parent, string text, Vector2 pos, Action onClick, float size = 54f, bool topAnchored = false)
	{
		var rect = CreateRect("Button_" + text.Trim(), parent);
		Place(rect, pos, new Vector2(760, size + 24f), topAnchored);

		var hit = rect.gameObject.AddComponent<Image>();
		hit.color = new Color(0, 0, 0, 0); // невидимая зона клика

		var marker = CreateImage("Marker", rect, _bloodColor);
		marker.raycastTarget = false;
		var mrt = marker.rectTransform;
		mrt.anchorMin = mrt.anchorMax = new Vector2(0, 0.5f);
		mrt.pivot = new Vector2(1f, 0.5f);
		mrt.anchoredPosition = new Vector2(-14, -2);
		mrt.sizeDelta = new Vector2(40, 3);
		mrt.localScale = new Vector3(0f, 1f, 1f);

		var label = CreateText("Label", rect, text, size, _boneColor, TextAlignmentOptions.Left);
		Stretch(label.rectTransform);
		label.characterSpacing = 16f;
		label.raycastTarget = false;

		var button = rect.gameObject.AddComponent<Button>();
		button.transition = Selectable.Transition.None;
		button.targetGraphic = hit;
		button.onClick.AddListener(() => { if (!_busy) onClick(); });

		var horror = rect.gameObject.AddComponent<HorrorMenuButton>();
		horror.Label = label;
		horror.Marker = mrt;
		horror.NormalColor = _boneColor;
		horror.HighlightColor = new Color(0.86f, 0.1f, 0.08f, 1f);
		horror.Audio = _audio;
		horror.HoverClip = _hoverClip;
		horror.ClickClip = _clickClip;
		return button;
	}

	// ------------------------------------------------------------------ behaviour

	private void PlayIntro()
	{
		_busy = true;
		_mainGroup.alpha = 0f;
		_titleText.alpha = 0f;
		_titleGhost.alpha = 0f;

		bool fastIntro = MenuReturn.CanContinue;
		float fadeDur = fastIntro ? 0.45f : 1.8f;
		float groupDelay = fastIntro ? 0.1f : 0.45f;
		float groupDur = fastIntro ? 0.4f : 1.1f;
		float titleDur = fastIntro ? 0.35f : 1.1f;
		float btnStartDelay = fastIntro ? 0.15f : 1.25f;
		float btnStep = fastIntro ? 0.05f : 0.14f;

		Sequence.Create(useUnscaledTime: true)
			.Chain(Tween.Alpha(_fade, 0f, fadeDur, Ease.InOutSine, useUnscaledTime: true))
			.Group(Tween.Alpha(_mainGroup, 1f, groupDur, Ease.OutQuad, startDelay: groupDelay, useUnscaledTime: true))
			.Chain(Tween.Custom(0f, 1f, titleDur, v =>
			{
				_titleText.alpha = v;
				_titleGhost.alpha = v * 0.16f;
			}, Ease.OutQuad, useUnscaledTime: true))
			.ChainCallback(() =>
			{
				_fade.blocksRaycasts = false;
				_busy = false;
				_nextWhisper = Time.unscaledTime + Random.Range(4.5f, 8.5f);
				Select(MenuReturn.CanContinue ? _continueButton : _newGameButton);
			});

		for (int i = 0; i < _mainButtons.Count; i++)
		{
			var rt = (RectTransform)_mainButtons[i].transform;
			var cg = rt.gameObject.AddComponent<CanvasGroup>();
			cg.alpha = 0f;
			Tween.Alpha(cg, 1f, 0.55f, Ease.OutQuad, startDelay: btnStartDelay + i * btnStep, useUnscaledTime: true);
		}
	}

	private void Whisper()
	{
		_nextWhisper = Time.unscaledTime + Random.Range(8f, 15f);
		_whisper.text = Whispers[Random.Range(0, Whispers.Length)];
		// Справа в спокойной зоне, чтобы не перекрывать диораму и текст меню.
		_whisper.rectTransform.anchoredPosition = new Vector2(Random.Range(180f, 620f), Random.Range(-340f, 340f));
		Tween.StopAll(_whisper);
		Sequence.Create(useUnscaledTime: true)
			.Chain(Tween.Custom(0f, 0.24f, 1.4f, a => _whisper.alpha = a, Ease.InOutSine, useUnscaledTime: true))
			.ChainDelay(2.0f)
			.Chain(Tween.Custom(0.24f, 0f, 2.0f, a => _whisper.alpha = a, Ease.InOutSine, useUnscaledTime: true));
	}

	private void OpenPanel(CanvasGroup panel)
	{
		if (panel == null || _currentPanel != null) return;
		_currentPanel = panel;
		panel.gameObject.SetActive(true);
		panel.alpha = 0f;
		panel.interactable = true;
		panel.blocksRaycasts = true;
		_mainGroup.interactable = false;

		var frame = (RectTransform)panel.transform.Find("Frame");
		frame.anchoredPosition = new Vector2(-80, 0);
		Tween.Alpha(panel, 1f, 0.35f, Ease.OutQuad, useUnscaledTime: true);
		Tween.UIAnchoredPositionX(frame, -140f, 0.45f, Ease.OutCubic, useUnscaledTime: true);
		Tween.Alpha(_mainGroup, 0.25f, 0.35f, Ease.OutQuad, useUnscaledTime: true);

		var first = panel.GetComponentInChildren<Button>();
		if (first != null) Select(first);
	}

	private void ClosePanel()
	{
		if (_currentPanel == null) return;
		var panel = _currentPanel;
		_currentPanel = null;
		panel.interactable = false;
		panel.blocksRaycasts = false;
		_mainGroup.interactable = true;

		Tween.Alpha(panel, 0f, 0.25f, Ease.InQuad, useUnscaledTime: true)
			.OnComplete(() => panel.gameObject.SetActive(false));
		Tween.Alpha(_mainGroup, 1f, 0.3f, Ease.OutQuad, useUnscaledTime: true);
		Select(panel == _chaptersPanel ? _chaptersButton : _controlsButton);
	}

	private void OnCancel(InputAction.CallbackContext ctx)
	{
		if (!_busy) ClosePanel();
	}

	private void ContinueGame()
	{
		if (_busy || !MenuReturn.CanContinue) return;
		_busy = true;
		if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
		_fade.blocksRaycasts = true;

		var seq = Sequence.Create(useUnscaledTime: true)
			.Chain(Tween.Alpha(_fade, 1f, 0.45f, Ease.InQuad, useUnscaledTime: true));
		if (_audio.isPlaying && _audio.loop)
			seq.Group(Tween.AudioVolume(_audio, 0f, 0.45f, useUnscaledTime: true));
		seq.ChainCallback(MenuReturn.ContinueSuspendedGame);
	}

	private void ConfigureContinueButton(Button btn, bool canContinue)
	{
		if (canContinue) return;
		btn.interactable = false;
		btn.navigation = new Navigation { mode = Navigation.Mode.None };
		if (btn.TryGetComponent(out HorrorMenuButton horror))
		{
			Color disabledColor = new Color(_dimColor.r * 0.48f, _dimColor.g * 0.48f, _dimColor.b * 0.48f, 0.55f);
			horror.NormalColor = disabledColor;
			if (horror.Label != null) horror.Label.color = disabledColor;
		}
	}

	private void LoadScene(string scene)
	{
		if (_busy || string.IsNullOrEmpty(scene)) return;
		_busy = true;
		if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
		_fade.blocksRaycasts = true;

		var seq = Sequence.Create(useUnscaledTime: true)
			.Chain(Tween.Alpha(_fade, 1f, 1.2f, Ease.InQuad, useUnscaledTime: true));
		if (_audio.isPlaying && _audio.loop)
			seq.Group(Tween.AudioVolume(_audio, 0f, 1.2f, useUnscaledTime: true));
		seq.ChainDelay(0.3f)
			.ChainCallback(() =>
			{
				MenuReturn.DiscardSuspendedGame();
				SceneManager.LoadScene(scene, LoadSceneMode.Single);
			});
	}

	private void Quit()
	{
		if (_busy) return;
		_busy = true;
		_fade.blocksRaycasts = true;
		Tween.Alpha(_fade, 1f, 0.8f, Ease.InQuad, useUnscaledTime: true).OnComplete(() =>
		{
#if UNITY_EDITOR
			UnityEditor.EditorApplication.isPlaying = false;
#else
			Application.Quit();
#endif
		});
	}

	private static void Select(Selectable s)
	{
		if (EventSystem.current == null || s == null) return;
		EventSystem.current.SetSelectedGameObject(null);
		EventSystem.current.SetSelectedGameObject(s.gameObject);
	}

	private static void LinkVertical(List<Button> buttons)
	{
		for (int i = 0; i < buttons.Count; i++)
		{
			var nav = new Navigation
			{
				mode = Navigation.Mode.Explicit,
				selectOnUp = buttons[(i - 1 + buttons.Count) % buttons.Count],
				selectOnDown = buttons[(i + 1) % buttons.Count],
			};
			buttons[i].navigation = nav;
		}
	}

	private static void EnsureEventSystem()
	{
		if (EventSystem.current != null || FindFirstObjectByType<EventSystem>() != null) return;
		new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
	}

	// ------------------------------------------------------------------ helpers

	private static RectTransform CreateRect(string name, Transform parent)
	{
		var go = new GameObject(name, typeof(RectTransform));
		go.layer = 5; // UI
		go.transform.SetParent(parent, false);
		return (RectTransform)go.transform;
	}

	private static Image CreateImage(string name, Transform parent, Color color)
	{
		var img = CreateRect(name, parent).gameObject.AddComponent<Image>();
		img.color = color;
		img.raycastTarget = false;
		return img;
	}

	private static RawImage CreateRaw(string name, Transform parent, Texture tex)
	{
		var raw = CreateRect(name, parent).gameObject.AddComponent<RawImage>();
		raw.texture = tex;
		raw.raycastTarget = false;
		return raw;
	}

	private TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
	{
		var tmp = CreateRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
		if (_font != null) tmp.font = _font;
		tmp.text = text;
		tmp.fontSize = size;
		tmp.color = color;
		tmp.alignment = align;
		tmp.textWrappingMode = TextWrappingModes.NoWrap;
		tmp.overflowMode = TextOverflowModes.Overflow;
		tmp.raycastTarget = false;
		return tmp;
	}

	private static void Stretch(RectTransform rt)
	{
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.offsetMin = rt.offsetMax = Vector2.zero;
	}

	/// <summary>Позиция относительно левого-центра (или левого-верха, если topAnchored) родителя.</summary>
	private static void Place(RectTransform rt, Vector2 pos, Vector2 size, bool topAnchored = false)
	{
		var anchor = topAnchored ? new Vector2(0f, 1f) : new Vector2(0f, 0.5f);
		rt.anchorMin = rt.anchorMax = anchor;
		rt.pivot = topAnchored ? new Vector2(0f, 1f) : new Vector2(0f, 0.5f);
		rt.anchoredPosition = pos;
		rt.sizeDelta = size;
	}

	private Texture2D NewTex(int w, int h, TextureWrapMode wrap, FilterMode filter)
	{
		var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = wrap, filterMode = filter };
		_generated.Add(tex);
		return tex;
	}

	private Texture2D MakeVignette()
	{
		const int s = 256;
		var tex = NewTex(s, s, TextureWrapMode.Clamp, FilterMode.Bilinear);
		var px = new Color32[s * s];
		for (int y = 0; y < s; y++)
		for (int x = 0; x < s; x++)
		{
			float dx = (x / (s - 1f) - 0.5f) * 2f;
			float dy = (y / (s - 1f) - 0.5f) * 2f;
			float d = Mathf.Sqrt(dx * dx * 0.72f + dy * dy * 0.9f);
			float a = Mathf.Pow(Mathf.SmoothStep(0.80f, 1.45f, d), 1.5f) * 0.24f;
			px[y * s + x] = new Color32(8, 0, 2, (byte)(a * 255));
		}
		tex.SetPixels32(px);
		tex.Apply();
		return tex;
	}

	private Texture2D MakeHorizontalGradient()
	{
		const int w = 256;
		var tex = NewTex(w, 1, TextureWrapMode.Clamp, FilterMode.Bilinear);
		for (int x = 0; x < w; x++)
		{
			float t = x / (w - 1f);
			float a = Mathf.Lerp(0.54f, 0f, Mathf.SmoothStep(0.04f, 0.34f, t));
			tex.SetPixel(x, 0, new Color(0.015f, 0f, 0.005f, a));
		}
		tex.Apply();
		return tex;
	}
}
