using UnityEngine;
using UnityEngine.UI;
using PrimeTween;
using DialogueEditor;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public class HospitalAwake : MonoBehaviour
{
	private static readonly Color Black = new Color(0f, 0f, 0f, 1f);
	private static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

	private Image _image;
	[SerializeField] Image _imageB;
	[SerializeField] private NPCConversation _playerConversation;
	[SerializeField] private PlayableDirector _cutscene;
	[SerializeField] private GameObject _conversation;
	[SerializeField] private GameObject alax;

	private float duration = 1f;

	private void Awake()
	{
		_image = GetComponent<Image>();

		// В сцене картинка прозрачная (чтобы в Edit-режиме Game-вид не был чёрным),
		// поэтому стартуем с чёрного прямо в рантайме.
		_image.color = Black;
		_image.raycastTarget = false; // не блокируем клики по диалогу

		Color half = _imageB.color;

		// "Открываем глаза": приоткрыл -> моргнул -> открыл полностью.
		Sequence.Create()
			.Chain(Tween.Color(_image, half, duration: duration, ease: Ease.OutQuad))
			.Chain(Tween.Color(_image, Black, duration: duration * 0.6f, ease: Ease.InQuad))
			.Chain(Tween.Color(_image, Clear, duration: duration * 1.4f, ease: Ease.OutQuad))
			.ChainCallback(() => ConversationManager.Instance.StartConversation(_playerConversation));
	}

	public void CutScene()
	{
		_conversation.SetActive(false);
		Tween.Delay(1.2f).OnComplete(() => _cutscene.Play());
	}

	public void NextLevel(int level)
	{
		Sequence.Create()
			.Chain(Tween.Color(_image, Black, duration: duration, ease: Ease.OutQuad))
			.ChainDelay(0.5f)
			.ChainCallback(() => SceneManager.LoadScene(level));
	}
}
