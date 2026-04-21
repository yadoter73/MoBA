using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

public class HidingMonster : MonoBehaviour
{
    [SerializeField] private Image _image;

    [SerializeField] private Image _imageB;
    void Hide()
    {
        float duration = 2f;
        Color color = _image.color;
        Color color2 = _imageB.color;
        Sequence.Create()
            .Chain(Tween.Color(_image, _imageB.color, duration: duration, ease: Ease.OutQuad))
            .ChainCallback(() => gameObject.SetActive(false))
            .ChainDelay(1f)
            .Chain(Tween.Color(_image, color, duration: duration, ease: Ease.OutQuad));
    }
}
