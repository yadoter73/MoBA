using UnityEngine;
using PrimeTween;
using Zenject;
public class UIInteract : MonoBehaviour
{
    [Inject] private InteractionManager _interactionManager;
    private CanvasGroup _group;
    private void Start()
    {
        _group = GetComponent<CanvasGroup>();
        _interactionManager.OnInteractebleEvent.AddListener(ColorChange);
    }
    public void ColorChange(bool isActive)
    {
        Tween.CompleteAll(_group);
        float startVal = isActive ? 0 : 1;
        float endVal = isActive ? 1 : 0;
        Tween.Alpha(_group, startValue: startVal, endValue: endVal, 0.1f, ease: Ease.OutQuad);
    }    
}
