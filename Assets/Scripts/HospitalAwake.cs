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
		Tween.Delay(1).OnComplete(() => { Tween.Color(_image, _imageB.color, duration: duration, ease: Ease.OutQuad);
									      ConversationManager.Instance.StartConversation(_playerConversation);
		});
	}
}
