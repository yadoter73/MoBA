using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;
using KinematicCharacterController.Examples;

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
        Time.timeScale = 0f;
        _pauseMenuCanvas.SetActive(true);
        isPaused = true;
    }

    public void Resume()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        Time.timeScale = 1f;
        _pauseMenuCanvas.SetActive(false);
        isPaused = false;
    }

    public void Play(string level)
    {
        SceneManager.LoadScene(level);
    }

    public void Exit()
    {
        Application.Quit();
    }
}