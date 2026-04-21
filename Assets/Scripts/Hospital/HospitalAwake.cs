using UnityEngine;
using UnityEngine.UI;
using PrimeTween;
using DialogueEditor;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public class HospitalAwake : MonoBehaviour
{
    private Image _image;
	private Color _black;
	[SerializeField] Image _imageB;
	[SerializeField] private NPCConversation _playerConversation;
	[SerializeField] private PlayableDirector _cutscene;
	[SerializeField] private GameObject _conversation;
	[SerializeField] private GameObject alax;

	private float duration = 1f;
	private void Awake()
	{
		_image = GetComponent<Image>();
		Color color = _image.color;
		_black = color;
		Color color2 = _imageB.color;
		Tween.Color(_image, _imageB.color, duration: duration, ease: Ease.OutQuad);
		Tween.Color(_image, color, startDelay:1f , duration: duration, ease: Ease.OutQuad);
		Tween.Color(_image, color2, startDelay: 2f, duration: duration, ease: Ease.OutQuad);
		Tween.Delay(3).OnComplete(() => ConversationManager.Instance.StartConversation(_playerConversation));
	}
	public void CutScene()
    {
		_conversation.SetActive(false);
		Tween.Delay(1.2f).OnComplete(() => _cutscene.Play());
	}
	public void NextLevel(int level)
    {
		Sequence.Create()
			.Chain(Tween.Color(_image, _black , duration: duration, ease: Ease.OutQuad))
			.ChainCallback(() => Tween.Delay(0.5f))
			.ChainCallback(() => SceneManager.LoadScene(level));			
	}
}
