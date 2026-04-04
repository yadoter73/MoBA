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
    void Start()
    {
        _examPlayer.OnPlayerPauseEvent.AddListener(OnPauseInput);
    }
    public void OnPauseInput(ExamplePlayer.PressedStateEventArgs args)
    {
        if (args.State == ExamplePlayer.PressedState.Started)
        {
            if (isPaused) Resume();
            else Pause();
        }
    }
    public void Pause()
    {
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
        Time.timeScale = 1f;
    }

    public void Play(string level)
    {
		Time.timeScale = 1f;
		_pauseMenuCanvas.SetActive(false);
		isPaused = false;
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;
	}

    public void Exit()
    {
        Application.Quit();
    }
}