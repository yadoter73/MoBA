using System.Collections.Generic;
using KinematicCharacterController.Examples;
using PrimeTween;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Escape -> возврат в главное меню из любой игровой сцены с заморозкой текущей сессии,
/// чтобы кнопка Continue в меню возвращала игрока ровно на то же место и состояние.
///
/// Работает через новую Input System (Player/Pause = &lt;Keyboard&gt;/escape).
/// Объект создаётся автоматически один раз при старте игры (DontDestroyOnLoad).
/// </summary>
public class MenuReturn : MonoBehaviour
{
	public const string MenuSceneName = "Menu";
	private const string PauseActionPath = "Player/Pause";
	private const float FadeDuration = 0.45f;

	private static MenuReturn _instance;

	public static bool CanContinue =>
		_instance != null && _instance._suspendedScene.IsValid() && _instance._suspendedScene.isLoaded;

	public static Scene SuspendedScene => _instance != null ? _instance._suspendedScene : default;

	public static float BaseTimeScale { get; private set; } = 0.5f;

	private ExamplePlayer _player;
	private InputAction _pauseAction;
	private CanvasGroup _fade;
	private bool _isLoading;

	private Scene _suspendedScene;
	private Scene _menuScene;
	private float _savedTimeScale = 0.5f;
	private bool _savedCursorVisible;
	private CursorLockMode _savedCursorLockMode;
	private bool _hasSavedRenderSettings;
	private bool _savedFog;
	private Color _savedFogColor;
	private FogMode _savedFogMode;
	private float _savedFogDensity;
	private AmbientMode _savedAmbientMode;
	private Color _savedAmbientSkyColor;
	private Color _savedAmbientEquatorColor;
	private Color _savedAmbientGroundColor;
	private Color _savedAmbientLight;
	private float _savedAmbientIntensity;
	private Color _savedSubtractiveShadowColor;
	private Material _savedSkybox;
	private Light _savedSun;
	private readonly List<Behaviour> _disabledBehaviours = new();
	private readonly List<Renderer> _disabledRenderers = new();
	private readonly List<PlayableDirector> _pausedDirectors = new();
	private CinemachineVirtualCameraBase _savedLiveCamera;
	private readonly List<(CinemachineBrain brain, Vector3 pos, Quaternion rot, CinemachineBlendDefinition blend)> _savedBrains = new();
	private readonly List<(CinemachinePanTilt panTilt, InputAxis panAxis, InputAxis tiltAxis)> _savedPanTilts = new();
	private readonly List<(KinematicCharacterController.KinematicCharacterMotor motor, Vector3 pos, Quaternion rot, Vector3 vel)> _savedMotors = new();

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	private static void CaptureBaseTimeScale()
	{
		if (Time.timeScale > 0f) BaseTimeScale = Time.timeScale;
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Bootstrap()
	{
		if (_instance != null || FindAnyObjectByType<MenuReturn>() != null) return;
		var go = new GameObject("[MenuReturn]");
		DontDestroyOnLoad(go);
		go.AddComponent<MenuReturn>();
	}

	private void Awake()
	{
		if (_instance != null && _instance != this)
		{
			Destroy(gameObject);
			return;
		}
		_instance = this;
		BuildFadeOverlay();
		SceneManager.sceneLoaded += OnSceneLoaded;
		Bind(SceneManager.GetActiveScene());
	}

	private void OnDestroy()
	{
		if (_instance == this) _instance = null;
		SceneManager.sceneLoaded -= OnSceneLoaded;
		Unbind();
		ClearSuspendedState();
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		if (mode != LoadSceneMode.Single) return;
		ClearSuspendedState();
		_menuScene = scene.name == MenuSceneName ? scene : default;
		_isLoading = false;
		_fade.alpha = 0f;
		_fade.blocksRaycasts = false;
		Bind(scene);
	}

	private void Bind(Scene scene)
	{
		Unbind();
		if (!scene.IsValid() || scene.name == MenuSceneName) return;

		_player = FindAnyObjectByType<ExamplePlayer>();
		if (_player != null)
		{
			_player.OnPlayerPauseEvent.AddListener(OnPauseInput);
			return;
		}

		_pauseAction = InputSystem.actions != null ? InputSystem.actions.FindAction(PauseActionPath) : null;
		if (_pauseAction != null)
		{
			_pauseAction.Enable();
			_pauseAction.started += OnPauseActionStarted;
		}
		else
		{
			Debug.LogWarning($"[MenuReturn] Действие '{PauseActionPath}' не найдено в project-wide actions.");
		}
	}

	private void Unbind()
	{
		if (_player != null) _player.OnPlayerPauseEvent.RemoveListener(OnPauseInput);
		_player = null;

		if (_pauseAction != null) _pauseAction.started -= OnPauseActionStarted;
		_pauseAction = null;
	}

	private void OnPauseInput(ExamplePlayer.PressedStateEventArgs args)
	{
		if (args.State == ExamplePlayer.PressedState.Started) GoToMenu();
	}

	private void OnPauseActionStarted(InputAction.CallbackContext ctx) => GoToMenu();

	public void GoToMenu()
	{
		Scene current = SceneManager.GetActiveScene();
		if (_isLoading || current.name == MenuSceneName || CanContinue || PlayerDeathHandler.IsDead) return;
		_isLoading = true;
		Unbind();

		_savedTimeScale = Time.timeScale > 0f ? Time.timeScale : BaseTimeScale;
		_savedCursorVisible = Cursor.visible;
		_savedCursorLockMode = Cursor.lockState;

		Time.timeScale = 0f;
		AudioListener.pause = true;
		Cursor.visible = true;
		Cursor.lockState = CursorLockMode.None;

		_fade.blocksRaycasts = true;
		Tween.Alpha(_fade, 1f, FadeDuration, Ease.InQuad, useUnscaledTime: true)
			.OnComplete(() => OpenMenuOver(current));
	}

	private void OpenMenuOver(Scene gameplayScene)
	{
		CaptureRenderSettings();
		SuspendScene(gameplayScene);

		Scene existingMenu = SceneManager.GetSceneByName(MenuSceneName);
		if (existingMenu.IsValid() && existingMenu.isLoaded)
		{
			OnAdditiveMenuLoaded(existingMenu);
			return;
		}

		var op = SceneManager.LoadSceneAsync(MenuSceneName, LoadSceneMode.Additive);
		if (op == null)
		{
			RestoreRenderSettings();
			RestoreSuspendedScene();
			AudioListener.pause = false;
			Time.timeScale = _savedTimeScale > 0f ? _savedTimeScale : BaseTimeScale;
			_fade.alpha = 0f;
			_fade.blocksRaycasts = false;
			_isLoading = false;
			Bind(gameplayScene);
			return;
		}
		op.completed += _ => OnAdditiveMenuLoaded(SceneManager.GetSceneByName(MenuSceneName));
	}

	private void OnAdditiveMenuLoaded(Scene menuScene)
	{
		_menuScene = menuScene;
		if (menuScene.IsValid() && menuScene.isLoaded)
			SceneManager.SetActiveScene(menuScene);

		ApplyMenuRenderSettings();
		_fade.alpha = 0f;
		_fade.blocksRaycasts = false;
		_isLoading = false;
	}

	public static void ContinueSuspendedGame()
	{
		if (_instance == null)
		{
			Time.timeScale = BaseTimeScale;
			AudioListener.pause = false;
			SceneManager.LoadScene("CarCrash", LoadSceneMode.Single);
			return;
		}
		_instance.ResumeGameInternal();
	}

	public static void DiscardSuspendedGame()
	{
		if (_instance != null)
		{
			_instance.ClearSuspendedState();
			_instance._menuScene = default;
		}
		Time.timeScale = BaseTimeScale;
		AudioListener.pause = false;
	}

	private void ResumeGameInternal()
	{
		if (_isLoading) return;
		if (!CanContinue)
		{
			DiscardSuspendedGame();
			SceneManager.LoadScene("CarCrash", LoadSceneMode.Single);
			return;
		}

		_isLoading = true;
		_fade.alpha = 1f;
		_fade.blocksRaycasts = true;

		Scene menuToUnload = _menuScene.IsValid() && _menuScene.isLoaded
			? _menuScene
			: SceneManager.GetSceneByName(MenuSceneName);

		DisableMenuOutputs(menuToUnload);

		if (_suspendedScene.IsValid() && _suspendedScene.isLoaded)
			SceneManager.SetActiveScene(_suspendedScene);

		if (menuToUnload.IsValid() && menuToUnload.isLoaded)
		{
			var unloadOp = SceneManager.UnloadSceneAsync(menuToUnload);
			if (unloadOp != null)
			{
				unloadOp.completed += _ => FinishResume();
				return;
			}
		}

		FinishResume();
	}

	private void FinishResume()
	{
		_menuScene = default;
		Scene targetScene = _suspendedScene;

		RestoreSuspendedScene();
		RestoreRenderSettings();
		Cursor.visible = _savedCursorVisible;
		Cursor.lockState = _savedCursorLockMode;
		AudioListener.pause = false;
		Time.timeScale = _savedTimeScale > 0f ? _savedTimeScale : BaseTimeScale;

		if (targetScene.IsValid() && targetScene.isLoaded)
			Bind(targetScene);

		Tween.Alpha(_fade, 0f, 0.35f, Ease.OutQuad, useUnscaledTime: true)
			.OnComplete(() =>
			{
				_fade.blocksRaycasts = false;
				_isLoading = false;
			});
	}

	private void SuspendScene(Scene scene)
	{
		ClearSuspendedState();
		if (!scene.IsValid() || !scene.isLoaded) return;

		_suspendedScene = scene;

		// Запоминаем живую камеру Cinemachine, состояние Brain и позиции игроков ДО отключения компонентов.
		// Иначе при повторном включении Cinemachine может временно перехватить камеру кат-сцены у озера
		// или запустить плавный бленд вместо мгновенного возврата к игроку.
		foreach (var root in scene.GetRootGameObjects())
		{
			foreach (var brain in root.GetComponentsInChildren<CinemachineBrain>(false))
			{
				_savedBrains.Add((brain, brain.transform.position, brain.transform.rotation, brain.DefaultBlend));
				if (_savedLiveCamera == null)
				{
					if (brain.ActiveVirtualCamera is CinemachineVirtualCameraBase live && live != null)
						_savedLiveCamera = live;
					else
						_savedLiveCamera = FindHighestPriorityCamera(scene, brain);
				}
			}
			foreach (var panTilt in root.GetComponentsInChildren<CinemachinePanTilt>(true))
				_savedPanTilts.Add((panTilt, panTilt.PanAxis, panTilt.TiltAxis));
			foreach (var motor in root.GetComponentsInChildren<KinematicCharacterController.KinematicCharacterMotor>(false))
				_savedMotors.Add((motor, motor.TransientPosition, motor.TransientRotation, motor.BaseVelocity));
		}

		foreach (var root in scene.GetRootGameObjects())
		{
			foreach (var b in root.GetComponentsInChildren<Behaviour>(false))
			{
				if (!b.enabled || !ShouldDisableOnSuspend(b)) continue;
				b.enabled = false;
				_disabledBehaviours.Add(b);
			}

			foreach (var r in root.GetComponentsInChildren<Renderer>(false))
			{
				if (!r.enabled) continue;
				r.enabled = false;
				_disabledRenderers.Add(r);
			}

			foreach (var pd in root.GetComponentsInChildren<PlayableDirector>(false))
			{
				if (pd.state == PlayState.Playing)
				{
					pd.Pause();
					_pausedDirectors.Add(pd);
				}
			}
		}
	}

	private void RestoreSuspendedScene()
	{
		// Сначала восстанавливаем точные координаты игрока, пока камеры и физика ещё выключены
		foreach (var (motor, pos, rot, vel) in _savedMotors)
		{
			if (motor == null) continue;
			motor.SetPositionAndRotation(pos, rot, true);
			motor.BaseVelocity = vel;
		}
		if (_savedMotors.Count > 0)
			Physics.SyncTransforms();

		// Включаем все компоненты, КРОМЕ CinemachineBrain и виртуальных камер — сначала подготовим камеры
		foreach (var b in _disabledBehaviours)
		{
			if (b == null || b is CinemachineBrain || b is CinemachineVirtualCameraBase) continue;
			b.enabled = true;
		}

		// Восстанавливаем оси поворота камер (Range, Wrap, Center, Value) до включения Brain
		foreach (var (panTilt, panAxis, tiltAxis) in _savedPanTilts)
		{
			if (panTilt == null) continue;
			panTilt.PanAxis = panAxis;
			panTilt.TiltAxis = tiltAxis;
		}

		// Сначала включаем второстепенные камеры, затем живую камеру игрока и ставим её в приоритет
		foreach (var b in _disabledBehaviours)
		{
			if (b is CinemachineVirtualCameraBase cam && cam != null && cam != _savedLiveCamera)
				cam.enabled = true;
		}

		if (_savedLiveCamera != null)
		{
			_savedLiveCamera.enabled = true;
			_savedLiveCamera.Prioritize();
			_savedLiveCamera.PreviousStateIsValid = false;
			_savedLiveCamera.UpdateCameraState(Vector3.up, -1f);
		}

		// И только в самом конце включаем CinemachineBrain с мгновенным срезом (Cut) без перелёта через всю карту
		foreach (var (brain, pos, rot, savedBlend) in _savedBrains)
		{
			if (brain == null) continue;
			brain.transform.SetPositionAndRotation(pos, rot);
			brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
			brain.enabled = true;
			brain.ResetState();
			Tween.Delay(0.05f, () =>
			{
				if (brain != null) brain.DefaultBlend = savedBlend;
			}, useUnscaledTime: true);
		}

		foreach (var r in _disabledRenderers)
			if (r != null) r.enabled = true;

		foreach (var pd in _pausedDirectors)
			if (pd != null) pd.Resume();

		ClearSuspendedState();
	}

	private void CaptureRenderSettings()
	{
		_savedFog = RenderSettings.fog;
		_savedFogColor = RenderSettings.fogColor;
		_savedFogMode = RenderSettings.fogMode;
		_savedFogDensity = RenderSettings.fogDensity;
		_savedAmbientMode = RenderSettings.ambientMode;
		_savedAmbientSkyColor = RenderSettings.ambientSkyColor;
		_savedAmbientEquatorColor = RenderSettings.ambientEquatorColor;
		_savedAmbientGroundColor = RenderSettings.ambientGroundColor;
		_savedAmbientLight = RenderSettings.ambientLight;
		_savedAmbientIntensity = RenderSettings.ambientIntensity;
		_savedSubtractiveShadowColor = RenderSettings.subtractiveShadowColor;
		_savedSkybox = RenderSettings.skybox;
		_savedSun = RenderSettings.sun;
		_hasSavedRenderSettings = true;
	}

	private static void ApplyMenuRenderSettings()
	{
		RenderSettings.fog = true;
		RenderSettings.fogColor = new Color(0.045f, 0.018f, 0.030f, 1f);
		RenderSettings.fogMode = FogMode.ExponentialSquared;
		RenderSettings.fogDensity = 0.010f;
		RenderSettings.ambientMode = AmbientMode.Trilight;
		RenderSettings.ambientSkyColor = new Color(0.24f, 0.22f, 0.32f, 1f);
		RenderSettings.ambientEquatorColor = new Color(0.14f, 0.09f, 0.13f, 1f);
		RenderSettings.ambientGroundColor = new Color(0.08f, 0.05f, 0.07f, 1f);
		RenderSettings.ambientIntensity = 1.45f;
		RenderSettings.subtractiveShadowColor = new Color(0.55f, 0.08f, 0.06f, 1f);

		Scene menuScene = SceneManager.GetSceneByName(MenuSceneName);
		if (menuScene.IsValid() && menuScene.isLoaded)
		{
			foreach (var root in menuScene.GetRootGameObjects())
			{
				if (root.name == "Directional Light" && root.TryGetComponent(out Light dirLight))
				{
					RenderSettings.sun = dirLight;
					break;
				}
			}
		}
		DynamicGI.UpdateEnvironment();
	}

	private void RestoreRenderSettings()
	{
		if (!_hasSavedRenderSettings) return;
		_hasSavedRenderSettings = false;
		RenderSettings.fog = _savedFog;
		RenderSettings.fogColor = _savedFogColor;
		RenderSettings.fogMode = _savedFogMode;
		RenderSettings.fogDensity = _savedFogDensity;
		RenderSettings.ambientMode = _savedAmbientMode;
		RenderSettings.ambientSkyColor = _savedAmbientSkyColor;
		RenderSettings.ambientEquatorColor = _savedAmbientEquatorColor;
		RenderSettings.ambientGroundColor = _savedAmbientGroundColor;
		RenderSettings.ambientLight = _savedAmbientLight;
		RenderSettings.ambientIntensity = _savedAmbientIntensity;
		RenderSettings.subtractiveShadowColor = _savedSubtractiveShadowColor;
		RenderSettings.skybox = _savedSkybox;
		RenderSettings.sun = _savedSun;
		DynamicGI.UpdateEnvironment();
	}

	private void ClearSuspendedState()
	{
		_suspendedScene = default;
		_hasSavedRenderSettings = false;
		_savedLiveCamera = null;
		_savedBrains.Clear();
		_savedPanTilts.Clear();
		_savedMotors.Clear();
		_disabledBehaviours.Clear();
		_disabledRenderers.Clear();
		_pausedDirectors.Clear();
	}

	private static bool ShouldDisableOnSuspend(Behaviour b)
	{
		if (b is Camera || b is AudioListener || b is Canvas || b is GraphicRaycaster ||
			b is EventSystem || b is PlayerInput || b is Light || b is Terrain || b is Volume ||
			b is CinemachineVirtualCameraBase || b is CinemachineBrain || b is CinemachineComponentBase ||
			b is CinemachineInputAxisController)
			return true;

		return b.GetType().Name == "ConversationManager";
	}

	private static CinemachineVirtualCameraBase FindHighestPriorityCamera(Scene scene, CinemachineBrain brain)
	{
		if (!scene.IsValid() || !scene.isLoaded || brain == null) return null;
		CinemachineVirtualCameraBase best = null;
		int bestPriority = int.MinValue;
		foreach (var root in scene.GetRootGameObjects())
		{
			foreach (var vcam in root.GetComponentsInChildren<CinemachineVirtualCameraBase>(false))
			{
				if (vcam == null || !vcam.enabled || !brain.IsValidChannel(vcam)) continue;
				int prio = vcam.Priority.Value;
				if (best == null || prio > bestPriority)
				{
					best = vcam;
					bestPriority = prio;
				}
			}
		}
		return best;
	}

	private static void DisableMenuOutputs(Scene menuScene)
	{
		if (!menuScene.IsValid() || !menuScene.isLoaded) return;
		foreach (var root in menuScene.GetRootGameObjects())
		{
			foreach (var al in root.GetComponentsInChildren<AudioListener>(false)) al.enabled = false;
			foreach (var es in root.GetComponentsInChildren<EventSystem>(false)) es.enabled = false;
			foreach (var cam in root.GetComponentsInChildren<Camera>(false)) cam.enabled = false;
			foreach (var brain in root.GetComponentsInChildren<CinemachineBrain>(false)) brain.enabled = false;
			foreach (var vcam in root.GetComponentsInChildren<CinemachineVirtualCameraBase>(false)) vcam.enabled = false;
			foreach (var axisCtrl in root.GetComponentsInChildren<CinemachineInputAxisController>(false)) axisCtrl.enabled = false;
			foreach (var menuCtrl in root.GetComponentsInChildren<MainMenuController>(false)) menuCtrl.enabled = false;
		}
	}

	private void BuildFadeOverlay()
	{
		var canvasGo = new GameObject("FadeCanvas", typeof(Canvas), typeof(CanvasGroup));
		canvasGo.transform.SetParent(transform, false);
		var canvas = canvasGo.GetComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = short.MaxValue;

		_fade = canvasGo.GetComponent<CanvasGroup>();
		_fade.alpha = 0f;
		_fade.blocksRaycasts = false;
		_fade.interactable = false;

		var imageGo = new GameObject("Black", typeof(RectTransform), typeof(Image));
		imageGo.transform.SetParent(canvasGo.transform, false);
		var rt = (RectTransform)imageGo.transform;
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.offsetMin = rt.offsetMax = Vector2.zero;
		imageGo.GetComponent<Image>().color = Color.black;
	}
}
