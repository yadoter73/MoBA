using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoBA.Settings
{
    public enum GameLanguage
    {
        English = 0,
        Russian = 1
    }

    /// <summary>
    /// Хранит и применяет глобальные настройки игры (язык и чувствительность мыши)
    /// через PlayerPrefs, автоматически масштабируя CinemachineInputAxisController при загрузке сцен.
    /// </summary>
    public static class GameSettingsService
    {
        private const string LanguagePrefKey = "MoBA.Settings.Language";
        private const string SensitivityPrefKey = "MoBA.Settings.MouseSensitivity";

        public const float MinSensitivity = 0.20f;
        public const float MaxSensitivity = 2.50f;
        public const float DefaultSensitivity = 1.00f;

        private static bool _initialized;
        private static bool _sceneHooked;
        private static GameLanguage _language = GameLanguage.English;
        private static float _mouseSensitivity = DefaultSensitivity;
        private static readonly Dictionary<int, float[]> BaseAxisGains = new();

        public static event Action OnLanguageChanged;
        public static event Action<float> OnSensitivityChanged;

        public static GameLanguage Language
        {
            get
            {
                EnsureInitialized();
                return _language;
            }
            set
            {
                EnsureInitialized();
                if (_language == value) return;
                _language = value;
                PlayerPrefs.SetInt(LanguagePrefKey, (int)_language);
                PlayerPrefs.Save();
                GameLocalization.TranslateLoadedSceneTexts();
                OnLanguageChanged?.Invoke();
            }
        }

        public static float MouseSensitivity
        {
            get
            {
                EnsureInitialized();
                return _mouseSensitivity;
            }
            set
            {
                EnsureInitialized();
                float clamped = Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
                if (Mathf.Approximately(_mouseSensitivity, clamped)) return;
                _mouseSensitivity = clamped;
                PlayerPrefs.SetFloat(SensitivityPrefKey, _mouseSensitivity);
                PlayerPrefs.Save();
                ApplySensitivityToLoadedScenes();
                OnSensitivityChanged?.Invoke(_mouseSensitivity);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _initialized = false;
            _sceneHooked = false;
            _language = GameLanguage.English;
            _mouseSensitivity = DefaultSensitivity;
            BaseAxisGains.Clear();
            OnLanguageChanged = null;
            OnSensitivityChanged = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitAfterSceneLoad()
        {
            EnsureInitialized();
            if (!_sceneHooked)
            {
                SceneManager.sceneLoaded += HandleSceneLoaded;
                _sceneHooked = true;
            }
            ApplySensitivityToLoadedScenes();
            GameLocalization.TranslateLoadedSceneTexts();
        }

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            int savedLang = PlayerPrefs.GetInt(LanguagePrefKey, (int)GameLanguage.English);
            _language = savedLang == (int)GameLanguage.Russian ? GameLanguage.Russian : GameLanguage.English;
            _mouseSensitivity = Mathf.Clamp(
                PlayerPrefs.GetFloat(SensitivityPrefKey, DefaultSensitivity),
                MinSensitivity,
                MaxSensitivity);
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplySensitivityToLoadedScenes();
            GameLocalization.TranslateLoadedSceneTexts();
        }

        public static void ApplySensitivityToLoadedScenes()
        {
            EnsureInitialized();
            CinemachineInputAxisController[] controllers = UnityEngine.Object.FindObjectsByType<CinemachineInputAxisController>(FindObjectsInactive.Include);
            for (int i = 0; i < controllers.Length; i++)
            {
                ApplySensitivityToController(controllers[i]);
            }
        }

        public static void ApplySensitivityToController(CinemachineInputAxisController controller)
        {
            if (controller == null || controller.Controllers == null) return;
            EnsureInitialized();

            int id = controller.GetInstanceID();
            var list = controller.Controllers;
            if (!BaseAxisGains.TryGetValue(id, out float[] baseGains) || baseGains.Length != list.Count)
            {
                baseGains = new float[list.Count];
                for (int i = 0; i < list.Count; i++)
                {
                    baseGains[i] = list[i].Input != null ? list[i].Input.Gain : 1f;
                }
                BaseAxisGains[id] = baseGains;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Input != null)
                {
                    list[i].Input.Gain = baseGains[i] * _mouseSensitivity;
                }
            }
        }
    }
}
