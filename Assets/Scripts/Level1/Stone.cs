using UnityEngine;
using KinematicCharacterController.Examples;
using UnityEngine.Playables;
using PrimeTween;
public class Stone : MonoBehaviour, IInteractable
{
    [SerializeField] private PlayableDirector _cutscene;

    public void Interact(int id)
    {
       Tween.Delay(1.5f).OnComplete(() => _cutscene.Play());
    }    
}
