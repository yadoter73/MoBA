using System;
using System.IO;
using Cysharp.Threading.Tasks;
using KinematicCharacterController.Examples;
using MoBA.Narrative;
using MoBA.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoBA.SaveSystem
{
    [Serializable]
    public sealed class GameSaveData
    {
        public string SceneName;
        public float PlayerPosX;
        public float PlayerPosY;
        public float PlayerPosZ;
        public float PlayerRotY;
        public int QuestStage;
        public bool HasGateKey;
        public bool IsMonsterOrderCompleted;
        public bool IsSayaSeated;
        public string SavedAtUtc;
    }

    /// <summary>
    /// Единый сервис сохранения и загрузки прогресса игры на диск (JSON в persistentDataPath).
    /// </summary>
    public static class GameSaveService
    {
        private const string SaveFileName = "moba_save.json";

        private static ExampleCharacterController _activePlayerController;
        private static Transform _activePlayerTransform;
        private static KeyQuestService _activeQuestService;
        private static Stone _activeStone;
        private static GameSaveData _pendingLoadData;
        private static bool _sceneLoadedSubscribed;

        private static string SaveFilePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _activePlayerController = null;
            _activePlayerTransform = null;
            _activeQuestService = null;
            _activeStone = null;
            _pendingLoadData = null;
            if (_sceneLoadedSubscribed)
            {
                SceneManager.sceneLoaded -= OnSceneLoadedApplySave;
                _sceneLoadedSubscribed = false;
            }
        }

        public static void RegisterGameplayContext(
            Transform playerTransform,
            ExampleCharacterController playerController,
            KeyQuestService questService,
            Stone stone)
        {
            _activePlayerTransform = playerTransform;
            _activePlayerController = playerController;
            _activeQuestService = questService;
            _activeStone = stone;
        }

        public static bool HasSaveFile()
        {
            return File.Exists(SaveFilePath);
        }

        public static GameSaveData ReadSaveMetadata()
        {
            if (!HasSaveFile()) return null;
            try
            {
                string json = File.ReadAllText(SaveFilePath);
                return JsonUtility.FromJson<GameSaveData>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GameSaveService] Не удалось прочитать сохранение: {ex.Message}");
                return null;
            }
        }

        public static bool SaveCurrentGame(bool showNotification = true)
        {
            Scene targetScene = ResolveTargetGameplayScene();
            if (!targetScene.IsValid() || !targetScene.isLoaded || string.Equals(targetScene.name, MenuReturn.MenuSceneName, StringComparison.Ordinal))
            {
                Debug.LogWarning("[GameSaveService] Нет активной игровой сцены для сохранения.");
                return false;
            }

            Vector3 pos = Vector3.zero;
            float yaw = 0f;
            if (_activePlayerTransform != null && _activePlayerTransform.gameObject.scene == targetScene)
            {
                pos = _activePlayerTransform.position;
                yaw = _activePlayerTransform.eulerAngles.y;
            }
            else if (_activePlayerController != null && _activePlayerController.gameObject.scene == targetScene)
            {
                pos = _activePlayerController.transform.position;
                yaw = _activePlayerController.transform.eulerAngles.y;
            }

            int stage = 0;
            bool hasKey = false;
            bool monsterDone = false;
            if (_activeQuestService != null)
            {
                stage = (int)_activeQuestService.Stage;
                hasKey = _activeQuestService.HasGateKey;
                monsterDone = _activeQuestService.IsMonsterOrderCompleted;
            }

            bool sayaSeated = _activeStone != null && _activeStone.IsSayaSeated;

            var data = new GameSaveData
            {
                SceneName = targetScene.name,
                PlayerPosX = pos.x,
                PlayerPosY = pos.y,
                PlayerPosZ = pos.z,
                PlayerRotY = yaw,
                QuestStage = stage,
                HasGateKey = hasKey,
                IsMonsterOrderCompleted = monsterDone,
                IsSayaSeated = sayaSeated,
                SavedAtUtc = DateTime.UtcNow.ToString("o")
            };

            try
            {
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                File.WriteAllText(SaveFilePath, json);
                if (showNotification)
                {
                    SaveNotificationHud.ShowSaved("Игра сохранена");
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameSaveService] Ошибка записи сохранения: {ex.Message}");
                return false;
            }
        }

        public static bool LoadSavedGame()
        {
            GameSaveData data = ReadSaveMetadata();
            if (data == null || string.IsNullOrEmpty(data.SceneName))
            {
                Debug.LogWarning("[GameSaveService] Файл сохранения отсутствует или повреждён.");
                return false;
            }

            _pendingLoadData = data;
            if (!_sceneLoadedSubscribed)
            {
                SceneManager.sceneLoaded += OnSceneLoadedApplySave;
                _sceneLoadedSubscribed = true;
            }

            MenuReturn.DiscardSuspendedGame();
            Time.timeScale = MenuReturn.BaseTimeScale > 0f ? MenuReturn.BaseTimeScale : 0.5f;
            AudioListener.pause = false;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

            SceneManager.LoadScene(data.SceneName, LoadSceneMode.Single);
            return true;
        }

        private static Scene ResolveTargetGameplayScene()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.isLoaded && !string.Equals(active.name, MenuReturn.MenuSceneName, StringComparison.Ordinal))
            {
                return active;
            }

            Scene suspended = MenuReturn.SuspendedScene;
            if (suspended.IsValid() && suspended.isLoaded)
            {
                return suspended;
            }

            return default;
        }

        private static void OnSceneLoadedApplySave(Scene scene, LoadSceneMode mode)
        {
            if (_pendingLoadData == null) return;
            if (!string.Equals(scene.name, _pendingLoadData.SceneName, StringComparison.Ordinal)) return;

            GameSaveData data = _pendingLoadData;
            _pendingLoadData = null;
            if (_sceneLoadedSubscribed)
            {
                SceneManager.sceneLoaded -= OnSceneLoadedApplySave;
                _sceneLoadedSubscribed = false;
            }

            ApplySaveAfterSceneStartAsync(data).Forget();
        }

        private static async UniTaskVoid ApplySaveAfterSceneStartAsync(GameSaveData data)
        {
            // Ждём 1 кадр, чтобы Zenject SceneContext и Start() завершили инициализацию сцены
            await UniTask.Yield(PlayerLoopTiming.Update);

            Vector3 targetPos = new Vector3(data.PlayerPosX, data.PlayerPosY, data.PlayerPosZ);
            Quaternion targetRot = Quaternion.Euler(0f, data.PlayerRotY, 0f);

            if (_activePlayerController != null && _activePlayerController.Motor != null)
            {
                _activePlayerController.Motor.SetPositionAndRotation(targetPos, targetRot);
                _activePlayerController.Motor.BaseVelocity = Vector3.zero;
            }
            else if (_activePlayerTransform != null)
            {
                _activePlayerTransform.SetPositionAndRotation(targetPos, targetRot);
            }

            if (data.IsSayaSeated && _activeStone != null)
            {
                _activeStone.RestoreSayaSeatedStateImmediate();
            }

            if (_activeQuestService != null)
            {
                _activeQuestService.RestoreState(
                    (KeyQuestStage)data.QuestStage,
                    data.HasGateKey,
                    data.IsMonsterOrderCompleted);
            }
        }
    }
}
