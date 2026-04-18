using UnityEngine;
using UnityEngine.UI;
using PrimeTween;
using DialogueEditor;
using UnityEngine.Playables;

public class HospitalAwake : MonoBehaviour
{
    private Image _image;

	[SerializeField] Image _imageB;
	[SerializeField] private NPCConversation _playerConversation;
	[SerializeField] private PlayableDirector _cutscene;
	[SerializeField] private GameObject _conversation;

	private float duration = 1f;
	private void Start()
	{	
		_image = GetComponent<Image>();
		Color color = _image.color;
		Color color2 = _imageB.color;
		Tween.Color(_image, _imageB.color, duration: duration, ease: Ease.OutQuad);
		Tween.Color(_image, color, startDelay:1f , duration: duration, ease: Ease.OutQuad);
		Tween.Color(_image, color2, startDelay: 2f, duration: duration, ease: Ease.OutQuad);
		Tween.Delay(3).OnComplete(() => ConversationManager.Instance.StartConversation(_playerConversation));
	}
	public void CutScene()
    {
		_conversation.SetActive(false);
		Tween.Delay(2).OnComplete(() => _cutscene.Play());
	}
}
