using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    public GameObject pauseMenuCanvas; 
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
        MonoBehaviour[] allScripts = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (var script in allScripts)
        {
            if (script != this)
            {
                script.enabled = false;
            }
        }
        pauseMenuCanvas.SetActive(true);           
        isPaused = true;
    }

    public void Resume()
    {
        MonoBehaviour[] allScripts = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (var script in allScripts)
        {
            if (script != this)
            {
                script.enabled = true;
            }
        }
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