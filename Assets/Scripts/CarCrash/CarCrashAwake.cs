using UnityEngine;
using PrimeTween;
using DialogueEditor;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System;


public class CarCrashAwake : MonoBehaviour
{
	[SerializeField] private NPCConversation _playerConversation;
	[SerializeField] private Image _image;
	[SerializeField] private Image _imageEnd;
	private float duration = 3f;
	public void Conversation(float delay)
	{
		Tween.Delay(delay).OnComplete(() => ConversationManager.Instance.StartConversation(_playerConversation));
	}
	public void Play(int level)
	{
		UniTask.Delay(TimeSpan.FromSeconds(0.5f)).ContinueWith(() =>
		{
			SceneManager.LoadScene(level);
		}).Forget();
	}
	public void fading()
	{
		_imageEnd.gameObject.SetActive(true);
		Color targetColor = _image.color;
		Tween.MaterialColor(_imageEnd.material,
							targetColor,
							duration,
							Ease.InOutQuad).OnComplete(() =>
											Play(1));
	}
}
