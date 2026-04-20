using UnityEngine;
using KinematicCharacterController.Examples;
using UnityEngine.Playables;
using PrimeTween;
using Zenject;
using Cysharp.Threading.Tasks;
public class Stone : MonoBehaviour, IInteractable
{
    [SerializeField] private PlayableDirector _cutscene;
    [SerializeField] private GameObject _saya;
    [SerializeField] private GameObject _sayaRoped;
    [SerializeField] private LineToSaya _lineToSaya;

    [Inject(Id = "TextScript")] private TextRenaming _textRen;

    public void Interact(int id)
    {
        Tween.Delay(1).OnComplete(() => _cutscene.Play());
    }
    void OnEnable()
    {
        _cutscene.stopped += OnCutsceneEnd;
    }
    void OnDisable()
    {
        _cutscene.stopped -= OnCutsceneEnd;
    }
    private void OnCutsceneEnd(PlayableDirector director)
    {
        _sayaRoped.SetActive(true);
        _saya.transform.position = new Vector3(-53, 8, 25);
        _saya.transform.rotation = Quaternion.Euler(0, 270, 0);
        _saya.SetActive(false);
        _textRen.SayaText();
        
        _lineToSaya.ActivateMarker(1f).Forget();
    }
}
