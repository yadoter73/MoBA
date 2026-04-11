using UnityEngine;
using PrimeTween;
using TMPro;
public class TextRenaming : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _text;
    private void Start()
    {
        _text.alpha = 0f;
        _text.transform.gameObject.SetActive(true);
        TextAlpha();

    }
    public void SayaText()
    {
        _text.text = "Talk to Saya";
        TextAlpha();
    }
    private void TextAlpha()
    {
        Sequence.Create()
            .ChainDelay(6f) 
            .Chain(Tween.Alpha(_text, endValue: 1f,duration: 2f,cycles: 6,cycleMode: CycleMode.Yoyo, ease: Ease.InOutQuad))
            .ChainCallback(() => _text.gameObject.SetActive(false));
    }
}
