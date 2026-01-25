using UnityEngine;
using UnityEngine.SceneManagement;
public class Buttons : MonoBehaviour
{
    public void Play(string level)
    {
        SceneManager.LoadScene(level);
    }
    public void Exit()
    {
        Application.Quit();
    }

}
