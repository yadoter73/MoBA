using UnityEngine;
using PrimeTween;
public class Fading : MonoBehaviour
{
    [SerializeField] private GameObject Black;
    public void Fade(int a)
    {
        while (a <= 0)
        {
            Tween.Delay(1f).OnComplete(() => { Black.SetActive(true); a = a - 1; Black.SetActive(false); });
        }
    }
}
