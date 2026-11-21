using MoBA.Narrative;
using MoBA.Settings;
using PrimeTween;
using TMPro;
using UnityEngine;
using Zenject;

namespace MoBA.UI
{
    /// <summary>
    /// View-компонент HUD-счётчика собранных продуктов ("Продукты: N/5") на этапе CollectGoods.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class GroceryHudView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _counterText;

        [Inject(Optional = true)] private KeyQuestService _questService;
        [Inject(Optional = true)] private GroceryList _groceryList;

        private CanvasGroup _group;
        private Tween _fadeTween;
        private bool _isVisible;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            if (_counterText == null) _counterText = GetComponentInChildren<TMP_Text>(true);
            if (_group != null)
            {
                _group.alpha = 0f;
                _group.blocksRaycasts = false;
                _group.interactable = false;
            }
        }

        private void OnEnable()
        {
            if (_questService != null) _questService.OnStageChanged += HandleStageChanged;
            if (_groceryList != null) _groceryList.OnProgressChanged += HandleProgressChanged;
            GameSettingsService.OnLanguageChanged += HandleLanguageChanged;
            RefreshView(immediate: true);
        }

        private void OnDisable()
        {
            _fadeTween.Stop();
            if (_questService != null) _questService.OnStageChanged -= HandleStageChanged;
            if (_groceryList != null) _groceryList.OnProgressChanged -= HandleProgressChanged;
            GameSettingsService.OnLanguageChanged -= HandleLanguageChanged;
        }

        private void HandleLanguageChanged()
        {
            if (_groceryList != null)
            {
                UpdateText(_groceryList.CollectedTotal, _groceryList.TotalRequired);
            }
        }

        private void HandleStageChanged(KeyQuestStage stage)
        {
            RefreshView(immediate: false);
        }

        private void HandleProgressChanged(int collected, int total)
        {
            UpdateText(collected, total);
        }

        private void RefreshView(bool immediate)
        {
            if (_groceryList != null)
            {
                UpdateText(_groceryList.CollectedTotal, _groceryList.TotalRequired);
            }

            bool shouldShow = _questService != null && _questService.Stage == KeyQuestStage.CollectGoods;
            if (shouldShow == _isVisible && !immediate) return;
            _isVisible = shouldShow;

            if (_group == null) return;
            _fadeTween.Stop();
            float targetAlpha = shouldShow ? 1f : 0f;
            if (immediate)
            {
                _group.alpha = targetAlpha;
                return;
            }

            _fadeTween = Tween.Alpha(_group, targetAlpha, 0.3f, Ease.OutQuad, useUnscaledTime: true);
        }

        private void UpdateText(int collected, int total)
        {
            if (_counterText != null)
            {
                _counterText.text = GameLocalization.FormatGroceryCounter(collected, total);
            }
        }
    }
}
