#if UNITY_EDITOR
using System;
using System.IO;
using KinematicCharacterController;
using KinematicCharacterController.Examples;
using MoBA.Audio;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoBA.EditorTools
{
    /// <summary>
    /// Копирует и проверяет 8 аудиофайлов из Downloads в Assets/Audio,
    /// настраивает слой Floor и маски земли KCC/RegObj, подключает PlayerFootstepAudio и EnemyAudioController.
    /// </summary>
    public static class AudioAndFloorSetup
    {
        public const string AudioFolder = "Assets/Audio";
        private const string DownloadsFolder = @"C:\Users\user\Downloads";
        private const string FloorLayerName = "Floor";

        public const string FloorWalkFile = "Хождение по полу.mp3";
        public const string FloorRunFile = "Бег ппо полу.mp3";
        public const string GrassWalkFile = "Хождение человека по траве.mp3";
        public const string GrassRunFile = "Бег по траве.mp3";
        public const string GrassJumpFile = "Прыжок человека на траве.mp3";
        public const string GiantStepFile = "ШагГиганскогоМонстра.wav";
        public const string GiantSpotFile = "ЗвукКогдаГиганскийМонстрУвиделТебя.wav";
        public const string RegularChaseFile = "ЗвукиБегаОбычногоМонстраЗатобой.mp3";

        private static readonly string[] AllAudioFiles =
        {
            FloorWalkFile,
            FloorRunFile,
            GrassWalkFile,
            GrassRunFile,
            GrassJumpFile,
            GiantStepFile,
            GiantSpotFile,
            RegularChaseFile
        };

        public static void EnsureAudioFilesImported()
        {
            if (!AssetDatabase.IsValidFolder(AudioFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Audio");
            }

            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string destDir = Path.Combine(projectRoot, "Assets", "Audio");
            Directory.CreateDirectory(destDir);

            bool copiedAny = false;
            foreach (string fileName in AllAudioFiles)
            {
                string dstPath = Path.Combine(destDir, fileName);
                string srcPath = Path.Combine(DownloadsFolder, fileName);
                if (!File.Exists(dstPath) && File.Exists(srcPath))
                {
                    File.Copy(srcPath, dstPath, overwrite: true);
                    copiedAny = true;
                }
            }

            if (copiedAny)
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            foreach (string fileName in AllAudioFiles)
            {
                string assetPath = $"{AudioFolder}/{fileName}";
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                if (clip != null)
                {
                    Debug.Log($"[AudioAndFloorSetup] AudioClip '{fileName}': length={clip.length:F3}s, channels={clip.channels}, freq={clip.frequency}Hz");
                }
                else
                {
                    Debug.LogError($"[AudioAndFloorSetup] Не удалось загрузить AudioClip по пути {assetPath}");
                }
            }
        }

        public static int EnsureFloorLayer()
        {
            int existing = LayerMask.NameToLayer(FloorLayerName);
            if (existing >= 0) return existing;

            UnityEngine.Object[] tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManagerAssets == null || tagManagerAssets.Length == 0)
            {
                Debug.LogError("[AudioAndFloorSetup] Не найден ProjectSettings/TagManager.asset");
                return 0;
            }

            var so = new SerializedObject(tagManagerAssets[0]);
            SerializedProperty layersProp = so.FindProperty("layers");
            for (int i = 8; i < layersProp.arraySize; i++)
            {
                SerializedProperty sp = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(sp.stringValue))
                {
                    sp.stringValue = FloorLayerName;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[AudioAndFloorSetup] Создан слой '{FloorLayerName}' с индексом {i}.");
                    return i;
                }
            }

            Debug.LogWarning("[AudioAndFloorSetup] Свободный слот слоя для Floor не найден.");
            return LayerMask.NameToLayer("Ground");
        }

        public static void ConfigureGiantDemonAudio(GameObject demonRoot)
        {
            AudioClip stepClip = LoadClip(GiantStepFile);
            AudioClip spotClip = LoadClip(GiantSpotFile);

            AudioSource[] sources = demonRoot.GetComponents<AudioSource>();
            AudioSource moveSource = sources.Length >= 1 ? sources[0] : demonRoot.AddComponent<AudioSource>();
            AudioSource spotSource = sources.Length >= 2 ? sources[1] : demonRoot.AddComponent<AudioSource>();

            Configure3DSource(moveSource, stepClip, loop: true, minDist: 8f, maxDist: 95f, volume: 0.95f);
            Configure3DSource(spotSource, null, loop: false, minDist: 10f, maxDist: 115f, volume: 1.0f);

            EnemyAudioController ctrl = demonRoot.GetComponent<EnemyAudioController>();
            if (ctrl == null) ctrl = demonRoot.AddComponent<EnemyAudioController>();

            var so = new SerializedObject(ctrl);
            so.FindProperty("_isGiantMonster").boolValue = true;
            so.FindProperty("_moveLoopSource").objectReferenceValue = moveSource;
            so.FindProperty("_spotVoiceSource").objectReferenceValue = spotSource;
            so.FindProperty("_moveLoopClip").objectReferenceValue = stepClip;
            so.FindProperty("_spotPlayerClip").objectReferenceValue = spotClip;
            so.FindProperty("_moveVolume").floatValue = 0.95f;
            so.FindProperty("_spotVolume").floatValue = 1.0f;
            so.FindProperty("_patrolPitch").floatValue = 1.0f;
            so.FindProperty("_chasePitch").floatValue = 1.24f;
            so.FindProperty("_spotCooldown").floatValue = 4.5f;
            so.FindProperty("_minDistance").floatValue = 8f;
            so.FindProperty("_maxDistance").floatValue = 95f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void ConfigureRegularEnemyPrefabAudio(string prefabPath)
        {
            AudioClip chaseClip = LoadClip(RegularChaseFile);
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                AudioSource moveSource = root.GetComponent<AudioSource>();
                if (moveSource == null) moveSource = root.AddComponent<AudioSource>();
                Configure3DSource(moveSource, chaseClip, loop: true, minDist: 4f, maxDist: 42f, volume: 0.88f);

                EnemyAudioController ctrl = root.GetComponent<EnemyAudioController>();
                if (ctrl == null) ctrl = root.AddComponent<EnemyAudioController>();

                var so = new SerializedObject(ctrl);
                so.FindProperty("_isGiantMonster").boolValue = false;
                so.FindProperty("_moveLoopSource").objectReferenceValue = moveSource;
                so.FindProperty("_spotVoiceSource").objectReferenceValue = null;
                so.FindProperty("_moveLoopClip").objectReferenceValue = chaseClip;
                so.FindProperty("_spotPlayerClip").objectReferenceValue = null;
                so.FindProperty("_moveVolume").floatValue = 0.88f;
                so.FindProperty("_patrolPitch").floatValue = 1.0f;
                so.FindProperty("_chasePitch").floatValue = 1.0f;
                so.FindProperty("_minDistance").floatValue = 4f;
                so.FindProperty("_maxDistance").floatValue = 42f;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static void ConfigureSceneFloorAndPlayerAudio(Scene scene, int floorLayer)
        {
            AssignFloorLayerToHouses(scene, floorLayer);
            UpdateCameraAndLightCullingMasks(scene, floorLayer);
            UpdateGroundMasks(scene, floorLayer);
            ConfigurePlayerAudioInScene(scene);
        }

        private static void UpdateCameraAndLightCullingMasks(Scene scene, int floorLayer)
        {
            if (floorLayer < 0) return;
            int floorBit = 1 << floorLayer;
            int updatedCameras = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Camera cam in root.GetComponentsInChildren<Camera>(true))
                {
                    // Добавляем слой Floor ко всем основным камерам (которые рендерят Default-слой 0),
                    // не трогая специализированные камеры вроде ArrowCam (только слой Arrow) или пустые UI-камеры (cullingMask == 0).
                    if ((cam.cullingMask & 1) != 0 && (cam.cullingMask & floorBit) == 0)
                    {
                        Undo.RecordObject(cam, "Add Floor Layer to Camera CullingMask");
                        cam.cullingMask |= floorBit;
                        EditorUtility.SetDirty(cam);
                        updatedCameras++;
                        Debug.Log($"[AudioAndFloorSetup] Добавлен слой Floor в Camera '{cam.name}' (cullingMask={cam.cullingMask}).");
                    }
                }

                foreach (Light light in root.GetComponentsInChildren<Light>(true))
                {
                    if ((light.cullingMask & 1) != 0 && (light.cullingMask & floorBit) == 0)
                    {
                        Undo.RecordObject(light, "Add Floor Layer to Light CullingMask");
                        light.cullingMask |= floorBit;
                        EditorUtility.SetDirty(light);
                    }
                }
            }

            Debug.Log($"[AudioAndFloorSetup] Обновлено камер с cullingMask под слой Floor: {updatedCameras}");
        }

        private static void AssignFloorLayerToHouses(Scene scene, int floorLayer)
        {
            if (floorLayer < 0) return;
            int interactLayer = LayerMask.NameToLayer("Interact");
            int playerLayer = LayerMask.NameToLayer("Player");

            string[] houseRootNames = { "MainHouse", "Shop", "BigHouses" };
            int updatedCount = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                bool isHouseRoot = false;
                foreach (string hn in houseRootNames)
                {
                    if (root.name == hn) { isHouseRoot = true; break; }
                }
                if (!isHouseRoot) continue;

                foreach (Collider col in root.GetComponentsInChildren<Collider>(true))
                {
                    if (col == null || col.isTrigger) continue;
                    int curLayer = col.gameObject.layer;
                    if (curLayer == interactLayer || curLayer == playerLayer) continue;
                    if (col.GetComponent<Door>() != null) continue;

                    if (curLayer != floorLayer)
                    {
                        Undo.RecordObject(col.gameObject, "Set Floor Layer");
                        col.gameObject.layer = floorLayer;
                        EditorUtility.SetDirty(col.gameObject);
                        updatedCount++;
                    }
                }
            }

            Debug.Log($"[AudioAndFloorSetup] Слой '{FloorLayerName}' ({floorLayer}) назначен на {updatedCount} коллайдеров домов.");
        }

        private static void UpdateGroundMasks(Scene scene, int floorLayer)
        {
            int floorBit = floorLayer >= 0 ? (1 << floorLayer) : 0;
            int groundLayer = LayerMask.NameToLayer("Ground");
            int groundBit = groundLayer >= 0 ? (1 << groundLayer) : (1 << 6);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                KinematicCharacterMotor motor = root.GetComponentInChildren<KinematicCharacterMotor>(true);
                if (motor != null)
                {
                    Undo.RecordObject(motor, "Add Floor to StableGroundLayers");
                    motor.StableGroundLayers |= (floorBit | groundBit | 1);
                    EditorUtility.SetDirty(motor);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(motor);
                }

                RegObj reg = root.GetComponentInChildren<RegObj>(true);
                if (reg != null)
                {
                    Undo.RecordObject(reg, "Add Floor to WhatIsGround");
                    var so = new SerializedObject(reg);
                    SerializedProperty prop = so.FindProperty("WhatIsGround");
                    if (prop != null)
                    {
                        prop.intValue |= (floorBit | groundBit);
                        so.ApplyModifiedProperties();
                    }
                    EditorUtility.SetDirty(reg);
                }
            }
        }

        private static void ConfigurePlayerAudioInScene(Scene scene)
        {
            ExampleCharacterController cc = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                cc = root.GetComponentInChildren<ExampleCharacterController>(true);
                if (cc != null) break;
            }
            if (cc == null)
            {
                Debug.LogError("[AudioAndFloorSetup] ExampleCharacterController не найден в Level1.");
                return;
            }

            GameObject playerGo = cc.gameObject;
            AudioSource[] sources = playerGo.GetComponents<AudioSource>();
            AudioSource locoSource = sources.Length >= 1 ? sources[0] : Undo.AddComponent<AudioSource>(playerGo);
            AudioSource jumpSource = sources.Length >= 2 ? sources[1] : Undo.AddComponent<AudioSource>(playerGo);

            locoSource.playOnAwake = false;
            locoSource.loop = true;
            locoSource.spatialBlend = 0f;

            jumpSource.playOnAwake = false;
            jumpSource.loop = false;
            jumpSource.spatialBlend = 0f;

            PlayerFootstepAudio footstep = playerGo.GetComponent<PlayerFootstepAudio>();
            if (footstep == null) footstep = Undo.AddComponent<PlayerFootstepAudio>(playerGo);

            var so = new SerializedObject(footstep);
            so.FindProperty("_locomotionSource").objectReferenceValue = locoSource;
            so.FindProperty("_jumpSource").objectReferenceValue = jumpSource;
            so.FindProperty("_floorWalkClip").objectReferenceValue = LoadClip(FloorWalkFile);
            so.FindProperty("_floorRunClip").objectReferenceValue = LoadClip(FloorRunFile);
            so.FindProperty("_grassWalkClip").objectReferenceValue = LoadClip(GrassWalkFile);
            so.FindProperty("_grassRunClip").objectReferenceValue = LoadClip(GrassRunFile);
            so.FindProperty("_grassJumpClip").objectReferenceValue = LoadClip(GrassJumpFile);
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(locoSource);
            EditorUtility.SetDirty(jumpSource);
            EditorUtility.SetDirty(footstep);
            PrefabUtility.RecordPrefabInstancePropertyModifications(locoSource);
            PrefabUtility.RecordPrefabInstancePropertyModifications(jumpSource);
            PrefabUtility.RecordPrefabInstancePropertyModifications(footstep);

            Debug.Log("[AudioAndFloorSetup] PlayerFootstepAudio и AudioSource настроены на ExampleCharacter.");
        }

        private static void Configure3DSource(AudioSource src, AudioClip clip, bool loop, float minDist, float maxDist, float volume)
        {
            src.playOnAwake = false;
            src.loop = loop;
            src.clip = clip;
            src.volume = volume;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = minDist;
            src.maxDistance = maxDist;
            src.dopplerLevel = 0f;
        }

        private static AudioClip LoadClip(string fileName)
        {
            return AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/{fileName}");
        }
    }
}
#endif
