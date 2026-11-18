using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;
using KinematicCharacterController.Examples;
using PrimeTween;

public class PauseManager : MonoBehaviour
{
    [Inject] private ExamplePlayer _examPlayer;
    [SerializeField] private GameObject _pauseMenuCanvas;
    private bool isPaused = false;
    private float _savedTimeScale = 0.5f;
    void Start()
    {
        if (_pauseMenuCanvas != null) _pauseMenuCanvas.SetActive(false);
        if (FindAnyObjectByType<MenuReturn>() != null)
        {
            enabled = false;
            return;
        }
        _examPlayer.OnPlayerPauseEvent.AddListener(OnPauseInput);
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
        _savedTimeScale = Time.timeScale > 0f ? Time.timeScale : MenuReturn.BaseTimeScale;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        _pauseMenuCanvas.SetActive(true);
        isPaused = true;
        Time.timeScale = 0f;
    }

    public void Resume()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
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

    public void Exit()
    {
        Application.Quit();
    }
}