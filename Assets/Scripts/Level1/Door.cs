using KinematicCharacterController.Examples;
using UnityEngine;
using PrimeTween;
public class Door : MonoBehaviour, IInteractable
{
	private Animator _anim;
	private bool _isOpen = false;
	private void Start()
	{
		_anim = GetComponent<Animator>();
	}
	public void Interact()
	{
		if (!_isOpen)
		{
			Debug.Log("fff");
			_anim.Play("DoorOpen");
			Tween.Delay(1).OnComplete(() => _isOpen = true);
		}
		else 
		{
			_anim.Play("DoorClose");
			Tween.Delay(1).OnComplete(() => _isOpen = false);
		}
	}
}
