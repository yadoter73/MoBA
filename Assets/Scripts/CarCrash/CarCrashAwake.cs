using UnityEngine;
using PrimeTween;
using DialogueEditor;
using UnityEngine.SceneManagement;
//using Zenject;

public class CarCrashAwake : MonoBehaviour
{
    [SerializeField] private NPCConversation _playerConversation;
    //[Inject(Id = "NpcConversation")] private NPCConversation _playerConversation;
    public void Conversation(float delay)
    {
        Tween.Delay(delay).OnComplete(() => ConversationManager.Instance.StartConversation(_playerConversation));
    }
    public void Play(string level)
    {
        Tween.Delay(3).OnComplete(() => SceneManager.LoadScene(level)); 
    }
}
