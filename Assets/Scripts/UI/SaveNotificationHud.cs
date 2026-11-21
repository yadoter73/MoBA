using MoBA.Settings;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoBA.UI
{
    /// <summary>
    /// Уведомление «Игра сохранена» в правом верхнем углу экрана поверх игровых и меню-канвасов.
    /// </summary>
    public sealed class SaveNotificationHud : MonoBehaviour
    {
        private static SaveNotificationHud _instance;

        private CanvasGroup _group;
        private TextMeshProUGUI _label;
        private Sequence _sequence;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        public static void ShowSaved(string message = "Игра сохранена")
        {
            SaveNotificationHud hud = GetOrCreate();
            hud.PlayNotification(message);
        }

        private static SaveNotificationHud GetOrCreate()
        {
            if (_instance != null) return _instance;

            var go = new GameObject("[SaveNotificationHud]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SaveNotificationHud>();
            _instance.BuildUi();
            return _instance;
        }

        private void OnDestroy()
        {
            _sequence.Stop();
            if (_instance == this) _instance = null;
        }

        private void BuildUi()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 27000;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var raycaster = gameObject.AddComponent<GraphicRaycaster>();
            raycaster.enabled = false;

            var panelGo = new GameObject("SaveToast", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            panelGo.transform.SetParent(transform, false);

            var rt = panelGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-36f, -28f);
            rt.sizeDelta = new Vector2(310f, 54f);

            var bg = panelGo.GetComponent<Image>();
            bg.color = new Color(0.05f, 0.04f, 0.05f, 0.84f);
            bg.raycastTarget = false;

            _group = panelGo.GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;

            var accentGo = new GameObject("AccentBar", typeof(RectTransform), typeof(Image));
            accentGo.transform.SetParent(panelGo.transform, false);
            var accentRt = accentGo.GetComponent<RectTransform>();
            accentRt.anchorMin = new Vector2(0f, 0f);
            accentRt.anchorMax = new Vector2(0f, 1f);
            accentRt.pivot = new Vector2(0f, 0.5f);
            accentRt.anchoredPosition = Vector2.zero;
            accentRt.sizeDelta = new Vector2(4f, 0f);
            var accentImg = accentGo.GetComponent<Image>();
            accentImg.color = new Color(0.72f, 0.08f, 0.08f, 0.95f);
            accentImg.raycastTarget = false;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(panelGo.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(18f, 6f);
            textRt.offsetMax = new Vector2(-14f, -6f);

            _label = textGo.GetComponent<TextMeshProUGUI>();
            _label.text = GameLocalization.Get("Game Saved");
            _label.fontSize = 24f;
            _label.fontStyle = FontStyles.Bold;
            _label.color = new Color(0.92f, 0.89f, 0.84f, 1f);
            _label.alignment = TextAlignmentOptions.MidlineRight;
            _label.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null)
            {
                _label.font = TMP_Settings.defaultFontAsset;
            }
        }

        private void PlayNotification(string message)
        {
            if (_group == null || _label == null) return;

            _label.text = string.IsNullOrEmpty(message)
                ? GameLocalization.Get("Game Saved")
                : GameLocalization.TranslateAny(message);
            _sequence.Stop();
            _group.alpha = 0f;

            _sequence = Sequence.Create(useUnscaledTime: true)
                .Chain(Tween.Alpha(_group, 1f, 0.25f, Ease.OutQuad, useUnscaledTime: true))
                .ChainDelay(2.2f)
                .Chain(Tween.Alpha(_group, 0f, 0.55f, Ease.InQuad, useUnscaledTime: true));
        }
    }
}
