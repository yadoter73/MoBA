using UnityEngine;
using UnityEngine.UI;
using PrimeTween;
using DialogueEditor;
public class HospitalAwake : MonoBehaviour
{
    private Image _image;
	[SerializeField] Image _imageB;
	[SerializeField] private NPCConversation _playerConversation;
	private float duration = 1f;
	private void Start()
	{	
		_image = GetComponent<Image>();
		Color color = _image.color;
		Color color2 = _imageB.color;
		Sequence.Create()
			.Chain(Tween.Color(_image, _imageB.color, duration: duration, ease: Ease.OutQuad))
			.Chain(Tween.Color(_imageB, _image.color, duration: duration, ease: Ease.OutQuad));
		ConversationManager.Instance.StartConversation(_playerConversation);
	}
}
