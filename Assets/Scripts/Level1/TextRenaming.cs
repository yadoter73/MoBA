using UnityEngine;
using PrimeTween;
public class TextRenaming : MonoBehaviour
{
    [SerializeField] private GameObject _text;
    private void Start()
    {
        Tween.Delay(5).OnComplete(() => _text.SetActive(true));
    }
}
