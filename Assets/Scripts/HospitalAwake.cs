using UnityEngine;
using UnityEngine.UI;
using PrimeTween;
using System.Collections;
public class HospitalAwake : MonoBehaviour
{
    private Image _image;
	[SerializeField] Image _imageB;
	private float duration = 1f;
	private void Start()
	{
		_image = GetComponent<Image>();
		Tween.Delay(1).OnComplete(() =>Changing());
	}
	IEnumerator Changing()
	{
		float time = 0f;
		while (time < duration)
		{
			time += Time.deltaTime;
			Color color = Color.Lerp(_image.color, _imageB.color, duration / time);
			_image.color = color;
			yield return null;
		}
	}
}
