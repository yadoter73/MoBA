using MoBA.Interaction;
using MoBA.Settings;
using PrimeTween;
using TMPro;
using UnityEngine;
using Zenject;

public class UIInteract : MonoBehaviour
{
    private const string DefaultInteractText = "Press E to Interact";
    private const string TalkPromptText = "Press E to talk";
    private const float PromptTopOffsetY = -62f;

    [Inject] private InteractionManager _interactionManager;

    private CanvasGroup _group;
    private TMP_Text _promptLabel;
    private string _interactText = DefaultInteractText;
    private InteractionPromptType? _activePrompt;
    private Sequence _transitionSequence;

    private void Start()
    {
        _group = GetComponent<CanvasGroup>();
        _promptLabel = GetComponentInChildren<TMP_Text>(true);
        ConfigurePromptLayout();
        if (_promptLabel != null && !string.IsNullOrEmpty(_promptLabel.text))
        {
            _interactText = _promptLabel.text;
        }

        if (_interactionManager != null)
        {
            _interactionManager.OnInteractionPromptChanged.AddListener(OnPromptChanged);
        }
        GameSettingsService.OnLanguageChanged += RefreshPromptLabel;
    }

    private void OnDisable()
    {
        _transitionSequence.Stop();
    }

    private void OnDestroy()
    {
        _transitionSequence.Stop();
        GameSettingsService.OnLanguageChanged -= RefreshPromptLabel;
        if (_interactionManager != null)
        {
            _interactionManager.OnInteractionPromptChanged.RemoveListener(OnPromptChanged);
        }
    }

    private void ConfigurePromptLayout()
    {
        if (_promptLabel == null) return;
        RectTransform rt = _promptLabel.rectTransform;
        if (rt == null) return;

        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, PromptTopOffsetY);
    }

    private void RefreshPromptLabel()
    {
        if (_promptLabel == null || !_activePrompt.HasValue) return;
        _promptLabel.text = ResolvePromptText(_activePrompt.Value);
    }

    private string ResolvePromptText(InteractionPromptType promptType)
    {
        return promptType == InteractionPromptType.Talk
            ? GameLocalization.Get(TalkPromptText)
            : GameLocalization.TranslateAny(_interactText);
    }

    public void OnPromptChanged(InteractionPromptType? promptType)
    {
        if (_group == null) return;
        if (_activePrompt == promptType && ((promptType.HasValue && _group.alpha > 0.99f) || (!promptType.HasValue && _group.alpha < 0.01f)))
        {
            return;
        }

        _transitionSequence.Stop();

        if (!promptType.HasValue)
        {
            _activePrompt = null;
            _transitionSequence = Sequence.Create(useUnscaledTime: true)
                .Chain(Tween.Alpha(_group, endValue: 0f, duration: 0.1f, ease: Ease.OutQuad, useUnscaledTime: true));
            return;
        }

        string nextText = ResolvePromptText(promptType.Value);
        bool switchingWhileVisible = _group.alpha > 0.05f && _activePrompt.HasValue && _activePrompt.Value != promptType.Value;
        _activePrompt = promptType;

        if (switchingWhileVisible)
        {
            _transitionSequence = Sequence.Create(useUnscaledTime: true)
                .Chain(Tween.Alpha(_group, endValue: 0f, duration: 0.08f, ease: Ease.InQuad, useUnscaledTime: true))
                .ChainCallback(() =>
                {
                    if (_promptLabel != null) _promptLabel.text = nextText;
                })
                .Chain(Tween.Alpha(_group, endValue: 1f, duration: 0.12f, ease: Ease.OutQuad, useUnscaledTime: true));
        }
        else
        {
            if (_promptLabel != null) _promptLabel.text = nextText;
            _transitionSequence = Sequence.Create(useUnscaledTime: true)
                .Chain(Tween.Alpha(_group, endValue: 1f, duration: 0.1f, ease: Ease.OutQuad, useUnscaledTime: true));
        }
    }

    public void ColorChange(bool isActive)
    {
        OnPromptChanged(isActive ? InteractionPromptType.Interact : (InteractionPromptType?)null);
    }
}
