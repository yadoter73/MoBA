using UnityEngine;
using PrimeTween;
using TMPro;

public class TextRenaming : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _text;
    private Sequence _currentSequence;
    private bool _questObjectiveActive;

    private void Start()
    {
        if (_text == null) return;

        ConfigureTopCenterLayout();
        _text.alpha = 0f;
        _text.gameObject.SetActive(true);
        TextAlpha();
    }

    private void ConfigureTopCenterLayout()
    {
        if (_text == null) return;
        RectTransform rt = _text.rectTransform;
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -10f);
            rt.sizeDelta = new Vector2(460f, 44f);
        }
        _text.enableAutoSizing = true;
        _text.fontSizeMin = 14f;
        _text.fontSizeMax = 28f;
        _text.textWrappingMode = TextWrappingModes.NoWrap;
        _text.alignment = TextAlignmentOptions.Center;
    }

    public void SayaText()
    {
        if (_text == null) return;
        _questObjectiveActive = false;
        ConfigureTopCenterLayout();
        _currentSequence.Stop();
        _text.gameObject.SetActive(true);
        _text.text = "Найти Саю";
        _text.alpha = 0f;

        _currentSequence = Sequence.Create()
            .Chain(Tween.Alpha(_text, endValue: 1f, duration: 1.2f, ease: Ease.OutQuad))
            .ChainDelay(4.5f)
            .Chain(Tween.Alpha(_text, endValue: 0.85f, duration: 1.5f, ease: Ease.InOutSine));
    }

    public void ShowFollowSayaObjective(string objectiveText = "Следовать за Саей")
    {
        if (_text == null) return;
        _questObjectiveActive = false;
        ConfigureTopCenterLayout();
        _currentSequence.Stop();
        _text.gameObject.SetActive(true);
        _text.text = objectiveText;
        _text.alpha = 0f;

        // Показываем надпись посередине сверху и удерживаем её видимой, пока игрок идёт за Саей
        _currentSequence = Sequence.Create()
            .Chain(Tween.Alpha(_text, endValue: 1f, duration: 0.9f, ease: Ease.OutQuad));
    }

    public void ShowQuestObjective(string objectiveText)
    {
        if (_text == null) return;
        _questObjectiveActive = true;
        ConfigureTopCenterLayout();
        _currentSequence.Stop();
        _text.gameObject.SetActive(true);
        _text.text = objectiveText;
        _text.alpha = 0f;

        _currentSequence = Sequence.Create()
            .Chain(Tween.Alpha(_text, endValue: 1f, duration: 0.5f, ease: Ease.OutQuad));
    }

    public void ClearQuestObjective(float fadeDuration = 0.6f)
    {
        _questObjectiveActive = false;
        HideObjectiveInternal(fadeDuration);
    }

    public void HideObjective(float fadeDuration = 0.6f)
    {
        if (_questObjectiveActive) return;
        HideObjectiveInternal(fadeDuration);
    }

    private void HideObjectiveInternal(float fadeDuration)
    {
        if (_text == null || !_text.gameObject.activeInHierarchy) return;
        _currentSequence.Stop();
        if (fadeDuration <= 0.01f)
        {
            _text.alpha = 0f;
            _text.gameObject.SetActive(false);
            return;
        }

        _currentSequence = Sequence.Create()
            .Chain(Tween.Alpha(_text, endValue: 0f, duration: fadeDuration, ease: Ease.InQuad))
            .ChainCallback(() =>
            {
                if (_text != null) _text.gameObject.SetActive(false);
            });
    }

    private void TextAlpha()
    {
        if (_text == null) return;
        _currentSequence.Stop();
        _currentSequence = Sequence.Create()
            .ChainDelay(2f)
            .Chain(Tween.Alpha(_text, endValue: 1f, duration: 2f, cycles: 6, cycleMode: CycleMode.Yoyo, ease: Ease.InOutQuad))
            .ChainCallback(() =>
            {
                if (_text != null) _text.gameObject.SetActive(false);
            });
    }
}
