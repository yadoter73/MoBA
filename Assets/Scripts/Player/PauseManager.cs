using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    [SerializeField] private GameObject pauseMenuCanvas;
    private bool isPaused = false;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused) Resume();

            else Pause();
        }
    }
    public void Pause()
    {
        Time.timeScale = 0f;
        pauseMenuCanvas.SetActive(true);
        isPaused = true;
    }

    public void Resume()
    {
        Time.timeScale = 1f;
        pauseMenuCanvas.SetActive(false);
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