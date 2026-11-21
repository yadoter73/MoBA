using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;
using KinematicCharacterController.Examples;
using MoBA.Player;
using MoBA.SaveSystem;
using MoBA.Settings;
using PrimeTween;
using TMPro;

public class PauseManager : MonoBehaviour
{
    [Inject] private ExamplePlayer _examPlayer;
    [Inject(Optional = true)] private IPlayerControlLock _playerControlLock;
    [SerializeField] private GameObject _pauseMenuCanvas;
    private bool isPaused = false;
    private float _savedTimeScale = 0.5f;
    void Start()
    {
        LocalizePauseMenu();
        GameSettingsService.OnLanguageChanged += LocalizePauseMenu;
        if (_pauseMenuCanvas != null) _pauseMenuCanvas.SetActive(false);
        if (FindAnyObjectByType<MenuReturn>() != null)
        {
            enabled = false;
            return;
        }
        _examPlayer.OnPlayerPauseEvent.AddListener(OnPauseInput);
    }

    private void OnDestroy()
    {
        GameSettingsService.OnLanguageChanged -= LocalizePauseMenu;
    }

    private void LocalizePauseMenu()
    {
        if (_pauseMenuCanvas == null) return;
        foreach (TMP_Text label in _pauseMenuCanvas.GetComponentsInChildren<TMP_Text>(true))
        {
            if (label != null && !string.IsNullOrEmpty(label.text))
            {
                label.text = GameLocalization.TranslateAny(label.text);
            }
        }
    }
    public void OnPauseInput(ExamplePlayer.PressedStateEventArgs args)
    {
        if (FindAnyObjectByType<MenuReturn>() != null) return;
        if (args.State == ExamplePlayer.PressedState.Started)
        {
            if (isPaused) Resume();
            else Pause();
        }
    }
    public void Pause()
    {
        LocalizePauseMenu();
        _savedTimeScale = Time.timeScale > 0f ? Time.timeScale : MenuReturn.BaseTimeScale;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        _pauseMenuCanvas.SetActive(true);
        isPaused = true;
        Time.timeScale = 0f;
    }

    public void Resume()
    {
        bool keepCursorUnlocked = _playerControlLock != null && _playerControlLock.IsCursorUnlocked;
        Cursor.visible = keepCursorUnlocked;
        Cursor.lockState = keepCursorUnlocked ? CursorLockMode.None : CursorLockMode.Locked;
        _pauseMenuCanvas.SetActive(false);
        isPaused = false;
        Time.timeScale = _savedTimeScale > 0f ? _savedTimeScale : MenuReturn.BaseTimeScale;
    }

    public void Play(string level)
    {
		Time.timeScale = _savedTimeScale > 0f ? _savedTimeScale : MenuReturn.BaseTimeScale;
		_pauseMenuCanvas.SetActive(false);
		isPaused = false;
		bool toMenu = level == MenuReturn.MenuSceneName;
		Cursor.lockState = toMenu ? CursorLockMode.None : CursorLockMode.Locked;
		Cursor.visible = toMenu;
		SceneManager.LoadScene(level);
	}

    public void SaveGame()
    {
        GameSaveService.SaveCurrentGame(showNotification: true);
    }

    public void LoadSavedGame()
    {
        if (!GameSaveService.HasSaveFile()) return;
        Time.timeScale = _savedTimeScale > 0f ? _savedTimeScale : MenuReturn.BaseTimeScale;
        if (_pauseMenuCanvas != null) _pauseMenuCanvas.SetActive(false);
        isPaused = false;
        GameSaveService.LoadSavedGame();
    }

    public void Exit()
    {
        Application.Quit();
    }
}