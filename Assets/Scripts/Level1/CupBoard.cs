using PrimeTween;
using UnityEngine;

public class CupBoard : MonoBehaviour
{
	private bool _isOpen = false;
	[SerializeField] private Vector3 _vector3End;
	private Vector3 _startVector3;
	private void Start()
	{
		_startVector3 = transform.position;
	}
	public void Interact(int id)
	{
		if (!_isOpen)
		{
			Debug.Log("f");
			Tween.Position(this.transform, endValue: _vector3End, duration: 0.7f, ease: Ease.InOutBack)
				.OnComplete(() => _isOpen = true);
		}
		else
		{
			Tween.Position(this.transform, endValue: _startVector3, duration: 1f, ease: Ease.InOutBack)
				.OnComplete(() => _isOpen = false);
		}
	}
}
