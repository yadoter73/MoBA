using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PrimeTween;
using TMPro;

/// <summary>
/// Кнопка главного меню в стиле хоррора: при наведении/выборе текст уезжает вправо,
/// краснеет, слева вырастает кровавая черта, а сам текст слегка "дрожит".
/// Подсветка завязана на Selection, поэтому мышь, клавиатура и геймпад работают одинаково.
/// </summary>
[RequireComponent(typeof(Button))]
public class HorrorMenuButton : MonoBehaviour,
	IPointerEnterHandler, ISelectHandler, IDeselectHandler, ISubmitHandler, IPointerClickHandler
{
	public TextMeshProUGUI Label;
	public RectTransform Marker;
	public Color NormalColor = new Color(0.78f, 0.75f, 0.72f, 1f);
	public Color HighlightColor = new Color(0.86f, 0.08f, 0.08f, 1f);
	public float HoverOffset = 34f;
	public AudioSource Audio;
	public AudioClip HoverClip;
	public AudioClip ClickClip;

	private RectTransform _rect;
	private float _baseX;
	private bool _highlighted;
	private float _jitterSeed;

	private void Awake()
	{
		_rect = (RectTransform)transform;
		_baseX = _rect.anchoredPosition.x;
		_jitterSeed = Random.value * 100f;
		if (Label != null) Label.color = NormalColor;
		if (Marker != null) Marker.localScale = new Vector3(0f, 1f, 1f);
	}

	private void Start()
	{
		if (!_highlighted)
		{
			if (Label != null) Label.color = NormalColor;
			if (Marker != null) Marker.localScale = new Vector3(0f, 1f, 1f);
		}
	}

	private void Update()
	{
		if (!_highlighted || Label == null) return;
		// Еле заметная нервная дрожь выбранного пункта.
		float t = Time.unscaledTime * 22f + _jitterSeed;
		float jitter = Mathf.PerlinNoise(t, 0f) - 0.5f;
		Label.rectTransform.anchoredPosition = new Vector2(jitter * 2.2f, (Mathf.PerlinNoise(0f, t) - 0.5f) * 1.6f);
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		if (EventSystem.current != null && GetComponent<Button>().IsInteractable())
			EventSystem.current.SetSelectedGameObject(gameObject);
	}

	public void OnSelect(BaseEventData eventData)
	{
		if (GetComponent<Button>().IsInteractable()) SetHighlighted(true);
	}
	public void OnDeselect(BaseEventData eventData) => SetHighlighted(false);
	public void OnSubmit(BaseEventData eventData)
	{
		if (GetComponent<Button>().IsInteractable()) PlayClick();
	}
	public void OnPointerClick(PointerEventData eventData)
	{
		if (GetComponent<Button>().IsInteractable()) PlayClick();
	}

	public void SetHighlighted(bool value)
	{
		if (_highlighted == value) return;
		_highlighted = value;
		if (_rect == null) Awake();

		Tween.StopAll(_rect);
		Tween.UIAnchoredPositionX(_rect, value ? _baseX + HoverOffset : _baseX, 0.25f, Ease.OutCubic, useUnscaledTime: true);

		if (Label != null)
		{
			Tween.StopAll(Label);
			Tween.Color(Label, value ? HighlightColor : NormalColor, 0.18f, Ease.OutQuad, useUnscaledTime: true);
			if (!value) Label.rectTransform.anchoredPosition = Vector2.zero;
		}

		if (Marker != null)
		{
			Tween.StopAll(Marker);
			Tween.Scale(Marker, new Vector3(value ? 1f : 0f, 1f, 1f), 0.22f, Ease.OutCubic, useUnscaledTime: true);
		}

		if (value && Audio != null && HoverClip != null) Audio.PlayOneShot(HoverClip, 0.6f);
	}

	private void PlayClick()
	{
		if (Audio != null && ClickClip != null) Audio.PlayOneShot(ClickClip);
	}
}
