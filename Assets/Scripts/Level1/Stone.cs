using UnityEngine;
using KinematicCharacterController.Examples;
public class Stone : MonoBehaviour, IInteractable
{
    [SerializeField] private GameObject _targetParent;
    public void Interact(int id)
    {
        gameObject.transform.SetParent(_targetParent.transform, false);
    }    
}
