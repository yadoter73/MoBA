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

        Sequence.Create()
            .ChainDelay(5f) 
            .Chain(Tween.Alpha(_text, endValue: 1f,duration: 2f,cycles: 5,cycleMode: CycleMode.Restart, ease: Ease.OutQuad))
            .ChainCallback(() => _text.gameObject.SetActive(false));

    }
}
