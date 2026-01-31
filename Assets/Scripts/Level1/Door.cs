using KinematicCharacterController.Examples;
using UnityEngine;
using PrimeTween;
public class Door : MonoBehaviour, IInteractable
{
	private bool _isOpen = false;
	[SerializeField] private float _angle;
	private float _startAngle;
    private void Start()
    {
		_startAngle = transform.rotation.eulerAngles.y;
    }
    public void Interact()
	{
		if (!_isOpen)
		{
			Vector3 targetRotation = new(0, _angle, 0);
			Tween.EulerAngles(this.transform,startValue:transform.rotation.eulerAngles ,endValue: targetRotation,duration: 0.5f, ease: Ease.OutQuad).OnComplete(() => _isOpen = true);
		}
		else 
		{
			Vector3 targetRotation = new(0, _startAngle, 0);
			Tween.EulerAngles(this.transform, startValue: transform.rotation.eulerAngles, endValue: targetRotation, duration: 1f, ease: Ease.InOutBack).OnComplete(() => _isOpen = false);
		}
	}
}
