#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DialogueEditor;
using HorrorPostProcess;
using KinematicCharacterController.Examples;
using MoBA.Interaction;
using MoBA.Narrative;
using MoBA.VFX;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MoBA.Editor
{
    /// <summary>
    /// Песочница Level2: копирование сцены/террейна, очистка геймплея, сглаживание рельефа, NavMesh, атмосфера и 4K Top-Down снимки (Блок D).
    /// </summary>
    public static class Level2SandboxTools
    {
        private const string Level1ScenePath = "Assets/Scenes/Level1.unity";
        private const string Level2ScenePath = "Assets/Scenes/Level2.unity";
        private const string TerrainsFolder = "Assets/Terrains";
        private const string Level2TerrainDataPath = TerrainsFolder + "/Level2_TerrainData.asset";
        private const string NavCheckDataPath = TerrainsFolder + "/Level2_NavCheckPoints.json";
        private const string Level2AssetsFolder = "Assets/Prefabs/Level2";
        private const string ScreenshotsFolder = "Assets/Screenshots";
        private const string AdditionsRootName = "[Level2_Additions]";
        private const string CarPrefabPath = "Assets/CarCrash/Azerilo/Car Model No.1201 Asset/Prefab/Car_NO.1201.prefab";

        [Serializable]
        private class NavCheckPointData
        {
            public string name;
            public Vector3 position;
        }

        [Serializable]
        private class NavCheckPointList
        {
            public List<NavCheckPointData> spawns = new List<NavCheckPointData>();
            public Vector3 lakeCenter;
            public Vector3 lakeSize;
            public bool hasLakeBounds;
        }

        [MenuItem("MoBA/Create Level2 Sandbox")]
        public static void CreateLevel2Sandbox()
        {
            EnsureFolder(TerrainsFolder);
            EnsureFolder(Level2AssetsFolder);

            if (!AssetDatabase.CopyAsset(Level1ScenePath, Level2ScenePath))
            {
                Debug.LogError("[Level2Sandbox] Не удалось скопировать Level1.unity в Level2.unity.");
                return;
            }
            AssetDatabase.Refresh();

            Scene level2 = EditorSceneManager.OpenScene(Level2ScenePath, OpenSceneMode.Single);
            if (!level2.IsValid())
            {
                Debug.LogError("[Level2Sandbox] Не удалось открыть Level2.unity.");
                return;
            }

            CaptureSpawnAndLakeMetadata(level2);
            DuplicateAndBindTerrainData(level2);
            StripGameplayObjects(level2);
            EnsureOverviewCamera(level2);

            EditorSceneManager.SaveScene(level2);
            AssetDatabase.SaveAssets();
            Debug.Log("[Level2Sandbox] Level2.unity создана с отдельной копией TerrainData и без геймплея.");
        }

        [MenuItem("MoBA/Setup Level2 Full Pipeline")]
        public static void SetupLevel2FullPipeline()
        {
            CreateLevel2Sandbox();
            SmoothTerrain();
            PopulateLevel2Atmosphere();
            BakeLevel2NavMesh();
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            CaptureBothScenesTopDown();
        }

        [MenuItem("MoBA/Apply Terrain & NavMesh To Level1")]
        public static void ApplyTerrainAndNavMeshToLevel1()
        {
            Scene scene = EditorSceneManager.OpenScene(Level1ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[Level1TerrainNav] Не удалось открыть Assets/Scenes/Level1.unity.");
                return;
            }

            Terrain terrain = FindFirstInScene<Terrain>(scene);
            TerrainData td = terrain != null ? terrain.terrainData : null;
            if (terrain == null || td == null)
            {
                Debug.LogError("[Level1TerrainNav] В Level1 не найден Terrain или TerrainData.");
                return;
            }

            EnsureFolder(TerrainsFolder);
            string backupPath = TerrainsFolder + "/Level1_TerrainData_Backup.asset";
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(backupPath) == null)
            {
                string srcPath = AssetDatabase.GetAssetPath(td);
                if (!string.IsNullOrEmpty(srcPath))
                {
                    AssetDatabase.CopyAsset(srcPath, backupPath);
                }
            }

            var snapTargets = CollectSnappableRoots(scene, terrain);
            var oldTerrainHeights = new float[snapTargets.Count];
            for (int i = 0; i < snapTargets.Count; i++)
            {
                oldTerrainHeights[i] = terrain.SampleHeight(snapTargets[i].transform.position) + terrain.transform.position.y;
            }

            Undo.RegisterCompleteObjectUndo(td, "Smooth Level1 Terrain");
            TerrainData level2Td = AssetDatabase.LoadAssetAtPath<TerrainData>(Level2TerrainDataPath);
            if (level2Td != null &&
                level2Td.heightmapResolution == td.heightmapResolution &&
                level2Td.size == td.size)
            {
                int res = td.heightmapResolution;
                td.SetHeights(0, 0, level2Td.GetHeights(0, 0, res, res));
            }
            else
            {
                TryGetLakeMetadata(scene, out Bounds lakeBounds);
                SmoothTerrainHeightsInPlace(td, terrain, lakeBounds.size.sqrMagnitude > 1f, lakeBounds);
            }

            EditorUtility.SetDirty(td);
            TerrainCollider tc = terrain.GetComponent<TerrainCollider>();
            if (tc != null)
            {
                tc.terrainData = td;
                EditorUtility.SetDirty(tc);
            }
            Physics.SyncTransforms();

            int snappedCount = SnapOutdoorObjectsAfterTerrainChange(scene, terrain, tc, snapTargets, oldTerrainHeights);

            // Переносим атмосферу (гробы, кресты, мёртвые деревья, сломанные заборы, фонари, туман, машину и тропу)
            int propsPlaced = PopulateAtmosphereInScene(
                scene,
                terrain,
                includeColorVolume: FindFirstInScene<Volume>(scene) == null,
                paintTrails: true);

            var report = new StringBuilder();
            report.AppendLine($"[Level1TerrainNav] Террейн сглажен без разрыва ссылок, подогнано объектов: {snappedCount}, декораций перенесено: {propsPlaced}.");
            ConfigureNavExclusionsAndBake(scene, terrain, captureSpawnCheck: true, report);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log(report.ToString());
        }

        [MenuItem("MoBA/Transfer Level2 Atmosphere To Level1")]
        public static void TransferLevel2AtmosphereToLevel1()
        {
            ApplyTerrainAndNavMeshToLevel1();
        }

        [MenuItem("MoBA/Run All Task1 & Task2 (Level2 + Level1)")]
        public static void RunAllTask1AndTask2()
        {
            Scene level1 = EditorSceneManager.OpenScene(Level1ScenePath, OpenSceneMode.Single);
            if (level1.IsValid())
            {
                QuestAndShopSetup.ValidateDoors();
                QuestAndShopSetup.PopulateShop();
                EditorSceneManager.SaveOpenScenes();
                AssetDatabase.SaveAssets();
            }

            SetupLevel2FullPipeline();
            ApplyTerrainAndNavMeshToLevel1();
            CaptureBothScenesTopDown();
            VerifyAdditionsGroundingAndAudit();
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
        }

        private static void VerifyAdditionsGroundingAndAudit()
        {
            string prev = SceneManager.GetActiveScene().path;
            Scene l2 = EditorSceneManager.OpenScene(Level2ScenePath, OpenSceneMode.Single);
            if (!l2.IsValid()) return;

            Terrain terrain = FindFirstInScene<Terrain>(l2);
            TerrainCollider tc = terrain != null ? terrain.GetComponent<TerrainCollider>() : null;
            GameObject root = FindSceneObjectByName(l2, AdditionsRootName);
            if (terrain != null && root != null)
            {
                var groups = new List<string>();
                float worstFloatGap = float.MinValue;
                int overlapCount = 0;
                var allBounds = new List<Bounds>();

                foreach (Transform group in root.transform)
                {
                    if (group.name == "DarkAtmosphereVolume" || group.name == "ForestMist") continue;
                    if (group.name == "AbandonedCar")
                    {
                        if (TryComputeRendererOrColliderBounds(group.gameObject, out Bounds cb))
                        {
                            float cg = SampleTerrainRaycastOrHeight(terrain, tc, cb.center);
                            float gap = cb.min.y - cg;
                            if (gap > worstFloatGap) worstFloatGap = gap;
                            allBounds.Add(cb);
                            groups.Add($"AbandonedCar(h={cb.size.y:F2}м, len={Mathf.Max(cb.size.x, cb.size.z):F2}м)");
                        }
                        continue;
                    }
                    int count = group.childCount;
                    float minH = float.MaxValue;
                    float maxH = 0f;
                    float maxLen = 0f;

                    foreach (Transform item in group)
                    {
                        if (!TryComputeRendererOrColliderBounds(item.gameObject, out Bounds b)) continue;
                        if (b.size.y < minH) minH = b.size.y;
                        if (b.size.y > maxH) maxH = b.size.y;
                        float hLen = Mathf.Max(b.size.x, b.size.z);
                        if (hLen > maxLen) maxLen = hLen;

                        float groundAtMin = SampleTerrainRaycastOrHeight(terrain, tc, new Vector3(b.center.x, b.center.y, b.center.z));
                        float gap = b.min.y - groundAtMin;
                        if (gap > worstFloatGap) worstFloatGap = gap;

                        foreach (Bounds prevB in allBounds)
                        {
                            if (b.max.x > prevB.min.x && b.min.x < prevB.max.x && b.max.z > prevB.min.z && b.min.z < prevB.max.z)
                            {
                                overlapCount++;
                            }
                        }
                        allBounds.Add(b);
                    }

                    if (count > 0)
                    {
                        groups.Add($"{group.name}(n={count}, h={minH:F2}..{maxH:F2}м, len={maxLen:F2}м)");
                    }
                }

                Debug.Log($"[Level2Audit] {string.Join("; ", groups)} | maxGapAboveTerrain={worstFloatGap:F3}м | xzOverlaps={overlapCount}");
            }

            if (!string.IsNullOrEmpty(prev) && File.Exists(prev))
            {
                EditorSceneManager.OpenScene(prev, OpenSceneMode.Single);
            }
        }

        private static void CaptureSpawnAndLakeMetadata(Scene level2)
        {
            var data = new NavCheckPointList();
            string[] spawnNames = { "FirstEnemySP", "SecondEnemySP", "ThirdEnemySp" };
            foreach (string spName in spawnNames)
            {
                GameObject sp = FindSceneObjectByName(level2, spName);
                if (sp != null)
                {
                    data.spawns.Add(new NavCheckPointData { name = spName, position = sp.transform.position });
                }
            }

            if (TryFindLakeBounds(level2, out Bounds lakeBounds))
            {
                data.lakeCenter = lakeBounds.center;
                data.lakeSize = lakeBounds.size;
                data.hasLakeBounds = true;
            }

            File.WriteAllText(NavCheckDataPath, JsonUtility.ToJson(data, true));
            AssetDatabase.ImportAsset(NavCheckDataPath);
        }

        private static void DuplicateAndBindTerrainData(Scene level2)
        {
            Terrain terrain = FindFirstInScene<Terrain>(level2);
            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogError("[Level2Sandbox] В сцене не найден Terrain или TerrainData.");
                return;
            }

            string srcPath = AssetDatabase.GetAssetPath(terrain.terrainData);
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(Level2TerrainDataPath) != null)
            {
                AssetDatabase.DeleteAsset(Level2TerrainDataPath);
            }

            if (!AssetDatabase.CopyAsset(srcPath, Level2TerrainDataPath))
            {
                Debug.LogError("[Level2Sandbox] Не удалось продублировать TerrainData: " + srcPath);
                return;
            }

            AssetDatabase.ImportAsset(Level2TerrainDataPath);
            TerrainData copiedData = AssetDatabase.LoadAssetAtPath<TerrainData>(Level2TerrainDataPath);

            terrain.terrainData = copiedData;
            TerrainCollider tc = terrain.GetComponent<TerrainCollider>();
            if (tc != null)
            {
                tc.terrainData = copiedData;
                EditorUtility.SetDirty(tc);
            }
            EditorUtility.SetDirty(terrain);
        }

        private static void StripGameplayObjects(Scene scene)
        {
            var toDestroy = new HashSet<GameObject>();

            CollectComponentRoots<ExampleCharacterController>(scene, toDestroy);
            CollectComponentRoots<ExamplePlayer>(scene, toDestroy);
            CollectComponentRoots<RegObj>(scene, toDestroy);
            CollectComponentRoots<EnemyAI>(scene, toDestroy);
            CollectComponentRoots<EnemySpawner>(scene, toDestroy);
            CollectComponentRoots<Stone>(scene, toDestroy);
            CollectComponentRoots<HouseZoneTrigger>(scene, toDestroy);
            CollectComponentRoots<KeyPickup>(scene, toDestroy);
            CollectComponentRoots<MonsterTalkInteractable>(scene, toDestroy);
            CollectComponentRoots<LineToSaya>(scene, toDestroy);
            CollectComponentRoots<PointerInmpulse>(scene, toDestroy);
            CollectComponentRoots<ArenaBounds>(scene, toDestroy);
            CollectComponentRoots<PlayerDeathHandler>(scene, toDestroy);
            CollectComponentRoots<PauseManager>(scene, toDestroy);
            CollectComponentRoots<HorrorShaderController>(scene, toDestroy);
            CollectComponentRoots<ConversationManager>(scene, toDestroy);
            CollectComponentRoots<NPCConversation>(scene, toDestroy);
            CollectComponentRoots<PlayableDirector>(scene, toDestroy);
            CollectComponentRoots<Canvas>(scene, toDestroy);
            CollectComponentRoots<UnityEngine.EventSystems.EventSystem>(scene, toDestroy);

            string[] namedGameplayObjects =
            {
                "Player", "RegObj", "SpawnPoints", "FirstEnemySP", "SecondEnemySP", "ThirdEnemySp",
                "Saya", "SayaRoped", "Demon", "LakePointer", "ArrowCam", "VRCamera", "CinemachineCamera",
                "CinemachineCamera (1)", "BlackImageCanvas", "EventSystem", "EnemySpawner"
            };
            foreach (string objName in namedGameplayObjects)
            {
                GameObject found = FindSceneObjectByName(scene, objName);
                if (found != null) toDestroy.Add(found);
            }

            foreach (GameObject go in toDestroy)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }

            // Удаляем интерактивные скрипты с оставшихся построек, сохраняя визуал и коллайдеры
            foreach (Door d in FindAllInScene<Door>(scene)) UnityEngine.Object.DestroyImmediate(d);
            foreach (CupBoard cb in FindAllInScene<CupBoard>(scene)) UnityEngine.Object.DestroyImmediate(cb);
            foreach (NpcDialoguePrompt p in FindAllInScene<NpcDialoguePrompt>(scene)) UnityEngine.Object.DestroyImmediate(p);
        }

        private static void CollectComponentRoots<T>(Scene scene, HashSet<GameObject> targetSet) where T : Component
        {
            foreach (T comp in FindAllInScene<T>(scene))
            {
                if (comp == null) continue;
                GameObject go = comp.gameObject;
                if (go.name == "MainHouse" || go.name == "Shop" || go.GetComponent<Terrain>() != null) continue;
                targetSet.Add(go);
            }
        }

        private static void EnsureOverviewCamera(Scene scene)
        {
            Camera cam = FindFirstInScene<Camera>(scene);
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera", typeof(Camera), typeof(UniversalAdditionalCameraData));
                camGo.tag = "MainCamera";
                cam = camGo.GetComponent<Camera>();
            }
            CinemachineBrain brain = cam.GetComponent<CinemachineBrain>();
            if (brain != null) UnityEngine.Object.DestroyImmediate(brain);
            cam.enabled = true;
        }

        [MenuItem("MoBA/Smooth Terrain")]
        public static void SmoothTerrain()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!IsLevel2Scene(scene, "Smooth Terrain")) return;

            Terrain terrain = FindFirstInScene<Terrain>(scene);
            TerrainData td = terrain != null ? terrain.terrainData : null;
            if (td == null)
            {
                Debug.LogError("[SmoothTerrain] На сцене Level2 не найден Terrain/TerrainData.");
                return;
            }

            if (AssetDatabase.GetAssetPath(td) != Level2TerrainDataPath)
            {
                Debug.LogError("[SmoothTerrain] Level2 должен ссылаться на копию TerrainData, а не на оригинал!");
                return;
            }

            var envObjects = CollectSnappableRoots(scene, terrain);
            var oldTerrainHeights = new float[envObjects.Count];
            for (int i = 0; i < envObjects.Count; i++)
            {
                oldTerrainHeights[i] = terrain.SampleHeight(envObjects[i].transform.position) + terrain.transform.position.y;
            }

            bool hasLake = TryGetLakeMetadata(scene, out Bounds lakeBounds);
            Undo.RegisterCompleteObjectUndo(td, "Smooth Level2 Terrain");
            SmoothTerrainHeightsInPlace(td, terrain, hasLake, lakeBounds);
            Physics.SyncTransforms();

            TerrainCollider tc = terrain.GetComponent<TerrainCollider>();
            int snapped = SnapOutdoorObjectsAfterTerrainChange(scene, terrain, tc, envObjects, oldTerrainHeights);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SmoothTerrain] Рельеф сглажен (5 проходов, ограничение уклона <= 38°), скорректировано объектов: {snapped}.");
        }

        private static void SmoothTerrainHeightsInPlace(TerrainData td, Terrain terrain, bool hasLake, Bounds lakeBounds)
        {
            int res = td.heightmapResolution;
            float[,] heights = td.GetHeights(0, 0, res, res);
            Vector3 size = td.size;
            float cellX = size.x / (res - 1);
            float cellZ = size.z / (res - 1);
            float maxSlopeRad = 38f * Mathf.Deg2Rad;
            float maxNormDelta = (Mathf.Min(cellX, cellZ) * Mathf.Tan(maxSlopeRad)) / size.y;
            float lakeWaterNormY = hasLake ? (lakeBounds.center.y - terrain.transform.position.y) / size.y : -1f;

            float[,] buffer = new float[res, res];
            for (int pass = 0; pass < 5; pass++)
            {
                for (int z = 1; z < res - 1; z++)
                {
                    float worldZ = terrain.transform.position.z + (z / (float)(res - 1)) * size.z;
                    for (int x = 1; x < res - 1; x++)
                    {
                        float worldX = terrain.transform.position.x + (x / (float)(res - 1)) * size.x;
                        float current = heights[z, x];

                        // Сохраняем чашу озера и прибрежную линию
                        if (hasLake && IsInsideLakeMargin(worldX, worldZ, lakeBounds, 5f) && current <= lakeWaterNormY + 2.5f / size.y)
                        {
                            buffer[z, x] = current;
                            continue;
                        }

                        float avg = (
                            heights[z - 1, x - 1] + 2f * heights[z - 1, x] + heights[z - 1, x + 1] +
                            2f * heights[z, x - 1] + 4f * current + 2f * heights[z, x + 1] +
                            heights[z + 1, x - 1] + 2f * heights[z + 1, x] + heights[z + 1, x + 1]) / 16f;

                        float maxNeighborDiff = Mathf.Max(
                            Mathf.Abs(current - heights[z - 1, x]),
                            Mathf.Abs(current - heights[z + 1, x]),
                            Mathf.Abs(current - heights[z, x - 1]),
                            Mathf.Abs(current - heights[z, x + 1]));

                        float blend = maxNeighborDiff > maxNormDelta ? 0.92f : 0.45f;
                        buffer[z, x] = Mathf.Lerp(current, avg, blend);
                    }
                }

                for (int z = 1; z < res - 1; z++)
                {
                    for (int x = 1; x < res - 1; x++) heights[z, x] = buffer[z, x];
                }
            }

            td.SetHeights(0, 0, heights);
        }

        private static int SnapOutdoorObjectsAfterTerrainChange(
            Scene scene,
            Terrain terrain,
            TerrainCollider tc,
            List<GameObject> envObjects,
            float[] oldTerrainHeights)
        {
            int count = 0;
            for (int i = 0; i < envObjects.Count; i++)
            {
                GameObject go = envObjects[i];
                if (go == null) continue;
                Vector3 pos = go.transform.position;
                float newTerrainY = SampleTerrainRaycastOrHeight(terrain, tc, pos);
                float deltaY = newTerrainY - oldTerrainHeights[i];
                if (Mathf.Abs(deltaY) > 0.001f)
                {
                    Undo.RecordObject(go.transform, "Snap Object To Smoothed Terrain");
                    go.transform.position = pos + new Vector3(0f, deltaY, 0f);
                    EditorUtility.SetDirty(go.transform);
                    count++;
                }
            }

            // Точки спавна врагов (в Level1) всегда должны стоять прямо на поверхности террейна
            string[] explicitGroundingNames = { "FirstEnemySP", "SecondEnemySP", "ThirdEnemySp" };
            foreach (string spName in explicitGroundingNames)
            {
                GameObject sp = FindSceneObjectByName(scene, spName);
                if (sp == null) continue;
                float groundY = SampleTerrainRaycastOrHeight(terrain, tc, sp.transform.position);
                if (Mathf.Abs(sp.transform.position.y - (groundY + 0.08f)) > 0.05f)
                {
                    Undo.RecordObject(sp.transform, "Ground Enemy Spawn Point");
                    sp.transform.position = new Vector3(sp.transform.position.x, groundY + 0.08f, sp.transform.position.z);
                    EditorUtility.SetDirty(sp.transform);
                    count++;
                }
            }

            Physics.SyncTransforms();
            return count;
        }

        [MenuItem("MoBA/Bake Level2 NavMesh")]
        public static void BakeLevel2NavMesh()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!IsLevel2Scene(scene, "Bake Level2 NavMesh")) return;

            Terrain terrain = FindFirstInScene<Terrain>(scene);
            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogError("[BakeLevel2NavMesh] Не найден Terrain.");
                return;
            }

            var log = new StringBuilder();
            ConfigureNavExclusionsAndBake(scene, terrain, captureSpawnCheck: false, log);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(log.ToString());
        }

        private static void ConfigureNavExclusionsAndBake(Scene scene, Terrain terrain, bool captureSpawnCheck, StringBuilder log)
        {
            NavMeshSurface surface = terrain.GetComponent<NavMeshSurface>();
            if (surface == null) surface = Undo.AddComponent<NavMeshSurface>(terrain.gameObject);
            surface.agentTypeID = 0; // Humanoid
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            int notWalkableArea = NavMesh.GetAreaFromName("Not Walkable");
            if (notWalkableArea < 0) notWalkableArea = 1;

            // 1. Очищаем старый контейнер объёмов исключения и создаём заново
            GameObject oldMods = FindSceneObjectByName(scene, "[NavMesh_Exclusions]");
            if (oldMods != null) Undo.DestroyObjectImmediate(oldMods);

            GameObject modsRoot = new GameObject("[NavMesh_Exclusions]");
            Undo.RegisterCreatedObjectUndo(modsRoot, "Create NavMesh Exclusions");

            // 2. Исключаем все постройки (MainHouse, Shop, BigHouses, OldHouses), чтобы враги не заходили внутрь
            var buildingRoots = CollectBuildingRoots(scene);
            int buildingIdx = 0;
            foreach (GameObject bld in buildingRoots)
            {
                NavMeshModifier mod = bld.GetComponent<NavMeshModifier>() ?? Undo.AddComponent<NavMeshModifier>(bld);
                mod.overrideArea = true;
                mod.area = notWalkableArea;
                mod.ignoreFromBuild = false;
                mod.applyToChildren = true;
                EditorUtility.SetDirty(mod);

                if (TryComputeRendererOrColliderBounds(bld, out Bounds b))
                {
                    var volGo = new GameObject($"BuildingNoNav_{buildingIdx++}_{bld.name}");
                    volGo.transform.SetParent(modsRoot.transform, false);
                    volGo.transform.position = b.center;
                    var vol = volGo.AddComponent<NavMeshModifierVolume>();
                    vol.center = Vector3.zero;
                    vol.size = new Vector3(b.size.x + 0.8f, Mathf.Max(b.size.y + 4f, 10f), b.size.z + 0.8f);
                    vol.area = notWalkableArea;
                }
            }

            // 3. Исключаем озеро ("Lake" / "Water_Plane") и его прибрежную зону
            GameObject existingLakeVol = FindSceneObjectByName(scene, "LakeNotWalkableVolume");
            if (existingLakeVol != null) Undo.DestroyObjectImmediate(existingLakeVol);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.IndexOf("Water_Plane", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        t.name.Equals("Lake", StringComparison.OrdinalIgnoreCase))
                    {
                        NavMeshModifier waterMod = t.GetComponent<NavMeshModifier>() ?? Undo.AddComponent<NavMeshModifier>(t.gameObject);
                        waterMod.overrideArea = true;
                        waterMod.area = notWalkableArea;
                        waterMod.ignoreFromBuild = false;
                        waterMod.applyToChildren = true;
                        EditorUtility.SetDirty(waterMod);
                    }
                }
            }

            if (TryGetLakeMetadata(scene, out Bounds lakeBounds))
            {
                var lakeVolGo = new GameObject("LakeNotWalkableVolume");
                lakeVolGo.transform.SetParent(modsRoot.transform, false);
                lakeVolGo.transform.position = new Vector3(lakeBounds.center.x, lakeBounds.center.y, lakeBounds.center.z);
                var vol = lakeVolGo.AddComponent<NavMeshModifierVolume>();
                vol.center = Vector3.zero;
                vol.size = new Vector3(lakeBounds.size.x + 22f, 28f, lakeBounds.size.z + 22f);
                vol.area = notWalkableArea;
            }

            Physics.SyncTransforms();
            surface.BuildNavMesh();
            EditorUtility.SetDirty(surface);

            float walkableArea = ComputeNavMeshAreaXZ();
            Vector3 terrainSize = terrain.terrainData.size;
            float totalTerrainArea = terrainSize.x * terrainSize.z;
            float coveragePct = totalTerrainArea > 0f ? (walkableArea / totalTerrainArea) * 100f : 0f;

            log.AppendLine($"[{scene.name} NavMesh] Запечено. Площадь NavMesh: {walkableArea:F1} м² из {totalTerrainArea:F1} м² ({coveragePct:F1}%), исключено построек: {buildingRoots.Count}.");
            VerifySpawnPaths(scene, terrain, terrain.GetComponent<TerrainCollider>(), log);
        }

        private static List<GameObject> CollectBuildingRoots(Scene scene)
        {
            var result = new List<GameObject>();
            var seen = new HashSet<GameObject>();

            void AddBuilding(GameObject go)
            {
                if (go != null && seen.Add(go)) result.Add(go);
            }

            AddBuilding(FindSceneObjectByName(scene, "MainHouse"));
            AddBuilding(FindSceneObjectByName(scene, "Shop"));

            string[] containerNames = { "BigHouses", "OldHouses" };
            foreach (string cname in containerNames)
            {
                GameObject container = FindSceneObjectByName(scene, cname);
                if (container == null) continue;
                foreach (Transform child in container.transform)
                {
                    AddBuilding(child.gameObject);
                }
            }
            return result;
        }

        private static bool TrySampleExteriorNavPoint(GameObject building, Terrain terrain, TerrainCollider tc, out NavMeshHit hit)
        {
            hit = default;
            if (building == null || !TryComputeRendererOrColliderBounds(building, out Bounds b)) return false;

            float halfX = b.extents.x + 3.2f;
            float halfZ = b.extents.z + 3.2f;
            Vector3 center = b.center;

            Vector3[] offsets =
            {
                new Vector3(0f, 0f, -halfZ),
                new Vector3(0f, 0f, halfZ),
                new Vector3(-halfX, 0f, 0f),
                new Vector3(halfX, 0f, 0f),
                new Vector3(-halfX * 0.85f, 0f, -halfZ * 0.85f),
                new Vector3(halfX * 0.85f, 0f, halfZ * 0.85f),
                new Vector3(-halfX * 0.85f, 0f, halfZ * 0.85f),
                new Vector3(halfX * 0.85f, 0f, -halfZ * 0.85f)
            };

            NavMeshHit best = default;
            float bestDist = float.MaxValue;
            foreach (Vector3 off in offsets)
            {
                Vector3 probe = center + off;
                probe.y = SampleTerrainRaycastOrHeight(terrain, tc, probe);
                if (NavMesh.SamplePosition(probe, out NavMeshHit candidate, 6f, NavMesh.AllAreas))
                {
                    if (candidate.distance < bestDist)
                    {
                        bestDist = candidate.distance;
                        best = candidate;
                    }
                }
            }

            hit = best;
            return best.hit;
        }

        private static void VerifySpawnPaths(Scene scene, Terrain terrain, TerrainCollider tc, StringBuilder log)
        {
            GameObject mainHouse = FindSceneObjectByName(scene, "MainHouse");
            GameObject shop = FindSceneObjectByName(scene, "Shop");
            if (mainHouse == null || shop == null)
            {
                log.AppendLine("  MainHouse или Shop не найдены на сцене.");
                return;
            }

            bool hasHouseTarget = TrySampleExteriorNavPoint(mainHouse, terrain, tc, out NavMeshHit mhHit);
            bool hasShopTarget = TrySampleExteriorNavPoint(shop, terrain, tc, out NavMeshHit shopHit);

            var spawnEntries = new List<NavCheckPointData>();
            string[] spawnNames = { "FirstEnemySP", "SecondEnemySP", "ThirdEnemySp" };
            foreach (string spName in spawnNames)
            {
                GameObject sp = FindSceneObjectByName(scene, spName);
                if (sp != null)
                {
                    spawnEntries.Add(new NavCheckPointData { name = spName, position = sp.transform.position });
                }
            }

            if (spawnEntries.Count == 0 && File.Exists(NavCheckDataPath))
            {
                var saved = JsonUtility.FromJson<NavCheckPointList>(File.ReadAllText(NavCheckDataPath));
                if (saved != null) spawnEntries.AddRange(saved.spawns);
            }

            if (spawnEntries.Count == 0)
            {
                log.AppendLine("  Точки спавна врагов не найдены.");
                return;
            }

            foreach (NavCheckPointData sp in spawnEntries)
            {
                Vector3 probePos = sp.position;
                probePos.y = SampleTerrainRaycastOrHeight(terrain, tc, probePos);
                if (!NavMesh.SamplePosition(probePos, out NavMeshHit spHit, 6f, NavMesh.AllAreas))
                {
                    log.AppendLine($"  {sp.name} ({sp.position:F1}): ВНЕ NavMesh!");
                    continue;
                }

                var pathToHouse = new NavMeshPath();
                bool okHouse = hasHouseTarget &&
                    NavMesh.CalculatePath(spHit.position, mhHit.position, NavMesh.AllAreas, pathToHouse) &&
                    pathToHouse.status == NavMeshPathStatus.PathComplete;

                var pathToShop = new NavMeshPath();
                bool okShop = hasShopTarget &&
                    NavMesh.CalculatePath(spHit.position, shopHit.position, NavMesh.AllAreas, pathToShop) &&
                    pathToShop.status == NavMeshPathStatus.PathComplete;

                log.AppendLine($"  {sp.name}: onNavMesh=True (offset={spHit.distance:F2}м), MainHouse={(okHouse ? "PathComplete" : pathToHouse.status.ToString())}, Shop={(okShop ? "PathComplete" : pathToShop.status.ToString())}");
            }
        }

        private class PlacementContext
        {
            public Scene Scene;
            public Terrain Terrain;
            public TerrainCollider Collider;
            public Bounds TerrainBounds;
            public bool HasLake;
            public Bounds LakeBounds;
            public System.Random Rng;
            public Vector3 TrailHouse;
            public Vector3 TrailMid;
            public Vector3 TrailShop;
            public readonly List<Bounds> ForbiddenBounds = new List<Bounds>();
            public readonly List<Vector2> TreePositionsXZ = new List<Vector2>();
        }

        [MenuItem("MoBA/Populate Level2 Atmosphere")]
        public static void PopulateLevel2Atmosphere()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!IsLevel2Scene(scene, "Populate Level2 Atmosphere")) return;

            Terrain terrain = FindFirstInScene<Terrain>(scene);
            if (terrain == null || terrain.terrainData == null) return;

            PopulateAtmosphereInScene(scene, terrain, includeColorVolume: true, paintTrails: true);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
        }

        private static int PopulateAtmosphereInScene(Scene scene, Terrain terrain, bool includeColorVolume, bool paintTrails)
        {
            EnsureFolder(Level2AssetsFolder);
            EnsureFolder(TerrainsFolder);

            GameObject existing = FindSceneObjectByName(scene, AdditionsRootName);
            if (existing != null) Undo.DestroyObjectImmediate(existing);

            GameObject root = new GameObject(AdditionsRootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Level2 Additions");

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material deadBarkMat = EnsureMaterial(Level2AssetsFolder + "/Mat_DeadBark.mat", urpLit, new Color(0.28f, 0.24f, 0.21f), 0f, 0.14f);
            Material woodDarkMat = EnsureMaterial(Level2AssetsFolder + "/Mat_GraveWood.mat", urpLit, new Color(0.35f, 0.27f, 0.20f), 0f, 0.18f);
            Material boneMat = EnsureMaterial(Level2AssetsFolder + "/Mat_Bone.mat", urpLit, new Color(0.74f, 0.70f, 0.62f), 0f, 0.25f);
            Material crowMat = EnsureMaterial(Level2AssetsFolder + "/Mat_Crow.mat", urpLit, new Color(0.06f, 0.06f, 0.08f), 0.1f, 0.4f);
            Material ironMat = EnsureMaterial(Level2AssetsFolder + "/Mat_LampIron.mat", urpLit, new Color(0.16f, 0.16f, 0.18f), 0.6f, 0.38f);
            Material lampGlowMat = EnsureEmissiveMaterial(Level2AssetsFolder + "/Mat_LampGlow.mat", urpLit, new Color(1f, 0.58f, 0.25f), 2.4f);
            Material mistMat = EnsureMistParticleMaterial(Level2AssetsFolder + "/Mat_ForestMist.mat");

            // 1. Тёмный URP Volume (цветокор + виньетка)
            if (includeColorVolume)
            {
                SetupDarkColorGradingVolume(root.transform);
            }

            // 2. Покраска тропинок (MainHouse <-> центр <-> Shop)
            GetTrailAnchorPoints(scene, out Vector3 trailHouse, out Vector3 trailMid, out Vector3 trailShop);
            if (paintTrails)
            {
                PaintDirtTrails(terrain, trailHouse, trailMid, trailShop);
            }

            // 3. Контекст анти-наложения и реальных масштабов сцены
            PlacementContext ctx = BuildPlacementContext(scene, terrain, trailHouse, trailMid, trailShop, seed: 202611);
            float fenceHeight = Mathf.Clamp(MeasureReferenceHeight(scene, "Fences", fallback: 2.3f), 1.8f, 2.6f);
            float refTreeHeight = MeasureReferenceTreeHeight(scene, terrain, fallback: 10.5f);

            int placedCount = 0;

            var deadTreesParent = CreateChildGroup(root.transform, "DeadTrees");
            for (int i = 0; i < 28; i++)
            {
                if (TrySpawnGroundedProp(
                    ctx,
                    minClearanceRadius: 2.8f,
                    maxSlopeDeg: 34f,
                    roadMinDist: 6.0f,
                    roadMaxDist: 999f,
                    embedDepth: 0.04f,
                    buildProp: pt =>
                    {
                        float treeH = Mathf.Clamp(refTreeHeight * (0.82f + (float)ctx.Rng.NextDouble() * 0.36f), 8.5f, 13.0f);
                        GameObject tree = BuildDeadTree(deadTreesParent.transform, pt, treeH, ctx.Rng, deadBarkMat);
                        if (i % 4 == 0) SpawnStaticCrow(tree.transform, new Vector3(0.55f, treeH * 0.76f, 0.12f), ctx.Rng, crowMat);
                        return tree;
                    }))
                {
                    placedCount++;
                }
            }

            var gravesParent = CreateChildGroup(root.transform, "GravesAndCrosses");
            for (int i = 0; i < 16; i++)
            {
                if (TrySpawnGroundedProp(
                    ctx,
                    minClearanceRadius: 1.4f,
                    maxSlopeDeg: 24f,
                    roadMinDist: 6.2f,
                    roadMaxDist: 999f,
                    embedDepth: 0.045f,
                    buildProp: pt =>
                    {
                        float crossH = 1.55f + (float)ctx.Rng.NextDouble() * 0.65f; // 1.55–2.20 м (человеческий рост)
                        GameObject grave = BuildCrookedGrave(gravesParent.transform, pt, crossH, ctx.Rng, woodDarkMat, deadBarkMat, boneMat);
                        if (i % 3 == 0) SpawnStaticCrow(grave.transform, new Vector3(0.18f, crossH - 0.18f, -0.35f), ctx.Rng, crowMat);
                        return grave;
                    }))
                {
                    placedCount++;
                }
            }

            var fencesParent = CreateChildGroup(root.transform, "BrokenFences");
            for (int i = 0; i < 14; i++)
            {
                if (TrySpawnGroundedProp(
                    ctx,
                    minClearanceRadius: 1.9f,
                    maxSlopeDeg: 26f,
                    roadMinDist: 6.0f,
                    roadMaxDist: 999f,
                    embedDepth: 0.04f,
                    buildProp: pt =>
                    {
                        GameObject fence = BuildBrokenFence(fencesParent.transform, pt, fenceHeight, ctx.Rng, woodDarkMat);
                        if (i % 3 == 1) SpawnStaticCrow(fence.transform, new Vector3(-0.9f, fenceHeight * 0.92f, 0f), ctx.Rng, crowMat);
                        return fence;
                    }))
                {
                    placedCount++;
                }
            }

            var lanternsParent = CreateChildGroup(root.transform, "FlickeringLanterns");
            for (int i = 0; i < 10; i++)
            {
                if (TrySpawnGroundedProp(
                    ctx,
                    minClearanceRadius: 1.6f,
                    maxSlopeDeg: 30f,
                    roadMinDist: 5.5f,
                    roadMaxDist: 16f,
                    embedDepth: 0.04f,
                    buildProp: pt =>
                    {
                        float lanternH = 3.6f + (float)ctx.Rng.NextDouble() * 0.9f; // 3.6–4.5 м
                        return BuildFlickeringLantern(lanternsParent.transform, pt, lanternH, ctx.Rng, ironMat, lampGlowMat);
                    }))
                {
                    placedCount++;
                }
            }

            var mistParent = CreateChildGroup(root.transform, "ForestMist");
            for (int i = 0; i < 12; i++)
            {
                if (TrySampleLandPoint(ctx, 1.0f, 38f, 0f, 999f, out Vector3 pt))
                {
                    BuildMistPatch(mistParent.transform, pt + Vector3.up * 1.1f, mistMat);
                }
            }

            // 4. Брошенная машина (~4.3 м длиной, URP Lit материалы, ровная площадка без наложений)
            if (SpawnAbandonedCar(root.transform, ctx))
            {
                placedCount++;
            }

            Debug.Log($"[Level2Atmosphere] Атмосфера обновлена в сцене '{scene.name}' ([Level2_Additions]), размещено пропсов: {placedCount} (эталон забора: {fenceHeight:F2}м, деревьев: {refTreeHeight:F2}м).");
            return placedCount;
        }

        [MenuItem("MoBA/Capture Top Down")]
        public static void CaptureActiveSceneTopDown()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return;

            string fileName = string.Equals(scene.name, "Level2", StringComparison.OrdinalIgnoreCase)
                ? "Level2_TopDown.png"
                : "Level1_TopDown.png";
            CaptureSceneTopDownInternal(scene, $"{ScreenshotsFolder}/{fileName}");
        }

        [MenuItem("MoBA/Capture Both Top Down (Level1 + Level2)")]
        public static void CaptureBothScenesTopDown()
        {
            string previousPath = SceneManager.GetActiveScene().path;
            Scene s1 = EditorSceneManager.OpenScene(Level1ScenePath, OpenSceneMode.Single);
            CaptureSceneTopDownInternal(s1, $"{ScreenshotsFolder}/Level1_TopDown.png");

            if (File.Exists(Level2ScenePath))
            {
                Scene s2 = EditorSceneManager.OpenScene(Level2ScenePath, OpenSceneMode.Single);
                CaptureSceneTopDownInternal(s2, $"{ScreenshotsFolder}/Level2_TopDown.png");
            }

            if (!string.IsNullOrEmpty(previousPath) && File.Exists(previousPath))
            {
                EditorSceneManager.OpenScene(previousPath, OpenSceneMode.Single);
            }
        }

        private static void CaptureSceneTopDownInternal(Scene scene, string outputAssetPath)
        {
            Terrain terrain = FindFirstInScene<Terrain>(scene);
            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogError("[CaptureTopDown] На сцене не найден Terrain.");
                return;
            }

            EnsureFolder(ScreenshotsFolder);

            Vector3 size = terrain.terrainData.size;
            Vector3 center = terrain.transform.position + size * 0.5f;
            float maxSpan = Mathf.Max(size.x, size.z);
            float maxSampledY = EstimateMaxTerrainWorldY(terrain);

            GameObject camGo = new GameObject("TempTopDownCam");
            Camera cam = camGo.AddComponent<Camera>();
            var urpCamData = camGo.AddComponent<UniversalAdditionalCameraData>();
            urpCamData.renderPostProcessing = false;
            cam.orthographic = true;
            cam.orthographicSize = maxSpan * 0.5f;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = Mathf.Max(900f, size.y + 400f);
            cam.cullingMask = ~0;
            cam.allowHDR = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.14f, 0.16f, 0.18f, 1f);
            camGo.transform.position = new Vector3(center.x, maxSampledY + 140f, center.z);
            camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            const int res = 4096;
            RenderTexture rt = new RenderTexture(res, res, 24, RenderTextureFormat.ARGB32);
            Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);

            bool prevFog = RenderSettings.fog;
            AmbientMode prevAmbientMode = RenderSettings.ambientMode;
            Color prevAmbientLight = RenderSettings.ambientLight;
            float prevAmbientIntensity = RenderSettings.ambientIntensity;

            Light mainDirLight = FindMainDirectionalLight(scene);
            Color prevLightColor = mainDirLight != null ? mainDirLight.color : Color.white;
            float prevLightIntensity = mainDirLight != null ? mainDirLight.intensity : 1f;
            Quaternion prevLightRot = mainDirLight != null ? mainDirLight.transform.rotation : Quaternion.identity;

            try
            {
                RenderSettings.fog = false;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.76f, 0.78f, 0.80f, 1f);
                RenderSettings.ambientIntensity = 1.2f;

                if (mainDirLight != null)
                {
                    mainDirLight.color = Color.white;
                    mainDirLight.intensity = 1.45f;
                    mainDirLight.transform.rotation = Quaternion.Euler(78f, 25f, 0f);
                }

                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, res, res), 0, 0);
                tex.Apply();

                File.WriteAllBytes(outputAssetPath, tex.EncodeToPNG());
                AssetDatabase.ImportAsset(outputAssetPath);
                Debug.Log($"[CaptureTopDown] Снимок сохранён: {outputAssetPath} (camY={camGo.transform.position.y:F1}, far={cam.farClipPlane:F0}).");
            }
            finally
            {
                RenderSettings.fog = prevFog;
                RenderSettings.ambientMode = prevAmbientMode;
                RenderSettings.ambientLight = prevAmbientLight;
                RenderSettings.ambientIntensity = prevAmbientIntensity;
                if (mainDirLight != null)
                {
                    mainDirLight.color = prevLightColor;
                    mainDirLight.intensity = prevLightIntensity;
                    mainDirLight.transform.rotation = prevLightRot;
                }
                RenderTexture.active = null;
                cam.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(tex);
                UnityEngine.Object.DestroyImmediate(camGo);
            }
        }

        private static float EstimateMaxTerrainWorldY(Terrain terrain)
        {
            TerrainData td = terrain.terrainData;
            int res = td.heightmapResolution;
            float[,] h = td.GetHeights(0, 0, res, res);
            float maxNorm = 0f;
            int step = Mathf.Max(1, res / 64);
            for (int z = 0; z < res; z += step)
            {
                for (int x = 0; x < res; x += step)
                {
                    if (h[z, x] > maxNorm) maxNorm = h[z, x];
                }
            }
            return terrain.transform.position.y + maxNorm * td.size.y;
        }

        private static Light FindMainDirectionalLight(Scene scene)
        {
            foreach (Light l in FindAllInScene<Light>(scene))
            {
                if (l.type == LightType.Directional && l.gameObject.activeInHierarchy) return l;
            }
            return null;
        }

        private static void SetupDarkColorGradingVolume(Transform parent)
        {
            GameObject volGo = new GameObject("DarkAtmosphereVolume");
            volGo.transform.SetParent(parent, false);
            Volume vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10f;

            string profilePath = TerrainsFolder + "/Level2_PostProfile.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }

            if (!profile.TryGet(out ColorAdjustments ca)) ca = profile.Add<ColorAdjustments>(true);
            ca.postExposure.Override(-0.45f);
            ca.contrast.Override(20f);
            ca.saturation.Override(-28f);
            ca.colorFilter.Override(new Color(0.84f, 0.80f, 0.86f, 1f));

            if (!profile.TryGet(out Vignette vig)) vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.45f);
            vig.smoothness.Override(0.35f);

            EditorUtility.SetDirty(profile);
            vol.sharedProfile = profile;
        }

        private static void GetTrailAnchorPoints(Scene scene, out Vector3 pHouse, out Vector3 pMid, out Vector3 pShop)
        {
            GameObject mainHouse = FindSceneObjectByName(scene, "MainHouse");
            GameObject shop = FindSceneObjectByName(scene, "Shop");
            pHouse = mainHouse != null ? mainHouse.transform.position + new Vector3(-7f, 0f, 18f) : new Vector3(70f, 0f, -55f);
            pShop = shop != null ? shop.transform.position + new Vector3(0f, 0f, -10f) : new Vector3(193f, 0f, 130f);
            pMid = Vector3.Lerp(pHouse, pShop, 0.5f) + new Vector3(-18f, 0f, 12f);
        }

        private static void PaintDirtTrails(Terrain terrain, Vector3 pHouse, Vector3 pMid, Vector3 pShop)
        {
            TerrainData td = terrain.terrainData;
            if (td.terrainLayers == null || td.terrainLayers.Length == 0) return;

            int layerIndex = 0;
            for (int i = 0; i < td.terrainLayers.Length; i++)
            {
                string lname = td.terrainLayers[i] != null ? td.terrainLayers[i].name : string.Empty;
                if (lname.IndexOf("dirt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    lname.IndexOf("mud", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    lname.IndexOf("soil", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    lname.IndexOf("road", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    lname.IndexOf("path", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    layerIndex = i;
                    break;
                }
            }

            int aw = td.alphamapWidth;
            int ah = td.alphamapHeight;
            int layers = td.alphamapLayers;
            float[,,] alpha = td.GetAlphamaps(0, 0, aw, ah);

            PaintSegment(terrain, alpha, aw, ah, layers, layerIndex, pHouse, pMid, 4.5f);
            PaintSegment(terrain, alpha, aw, ah, layers, layerIndex, pMid, pShop, 4.5f);
            td.SetAlphamaps(0, 0, alpha);
        }

        private static void PaintSegment(Terrain terrain, float[,,] alpha, int aw, int ah, int layers, int targetLayer, Vector3 a, Vector3 b, float width)
        {
            if (layers <= 1) return;
            Vector3 size = terrain.terrainData.size;
            Vector3 origin = terrain.transform.position;
            int steps = 64;
            for (int s = 0; s <= steps; s++)
            {
                Vector3 p = Vector3.Lerp(a, b, s / (float)steps);
                float nx = (p.x - origin.x) / size.x;
                float nz = (p.z - origin.z) / size.z;
                int cx = Mathf.RoundToInt(nx * (aw - 1));
                int cz = Mathf.RoundToInt(nz * (ah - 1));
                int r = Mathf.Max(1, Mathf.RoundToInt((width / size.x) * aw));

                for (int dz = -r; dz <= r; dz++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int x = cx + dx;
                        int z = cz + dz;
                        if (x < 0 || x >= aw || z < 0 || z >= ah) continue;
                        float d = Mathf.Sqrt(dx * dx + dz * dz) / r;
                        if (d > 1f) continue;

                        float targetWeight = (1f - d) * 0.72f;
                        float keep = 1f - targetWeight;
                        for (int l = 0; l < layers; l++) alpha[z, x, l] *= keep;
                        alpha[z, x, targetLayer] += targetWeight;

                        float sum = 0f;
                        for (int l = 0; l < layers; l++) sum += alpha[z, x, l];
                        if (sum > 0.0001f)
                        {
                            for (int l = 0; l < layers; l++) alpha[z, x, l] /= sum;
                        }
                    }
                }
            }
        }

        private static PlacementContext BuildPlacementContext(
            Scene scene,
            Terrain terrain,
            Vector3 trailHouse,
            Vector3 trailMid,
            Vector3 trailShop,
            int seed)
        {
            var ctx = new PlacementContext
            {
                Scene = scene,
                Terrain = terrain,
                Collider = terrain.GetComponent<TerrainCollider>(),
                TerrainBounds = new Bounds(terrain.transform.position + terrain.terrainData.size * 0.5f, terrain.terrainData.size),
                Rng = new System.Random(seed),
                TrailHouse = trailHouse,
                TrailMid = trailMid,
                TrailShop = trailShop
            };

            TryGetLakeMetadata(scene, out ctx.LakeBounds);
            ctx.HasLake = ctx.LakeBounds.size.sqrMagnitude > 1f;

            // Исключаем все постройки (+4.5 м)
            foreach (GameObject bld in CollectBuildingRoots(scene))
            {
                if (TryComputeRendererOrColliderBounds(bld, out Bounds b))
                {
                    b.Expand(new Vector3(9f, 4f, 9f));
                    ctx.ForbiddenBounds.Add(b);
                }
            }

            // Исключаем существующие заборы (+2 м) и отдельные уличные деревья/камни
            GameObject fencesRoot = FindSceneObjectByName(scene, "Fences");
            if (fencesRoot != null)
            {
                foreach (Transform child in fencesRoot.transform)
                {
                    if (TryComputeRendererOrColliderBounds(child.gameObject, out Bounds fb))
                    {
                        fb.Expand(new Vector3(3.6f, 2f, 3.6f));
                        ctx.ForbiddenBounds.Add(fb);
                    }
                }
            }

            // Только настоящие деревья террейна (без мелкой травы Grass_* и кустов Bush_*)
            TreePrototype[] protos = terrain.terrainData.treePrototypes;
            var isTallTree = new bool[protos != null ? protos.Length : 0];
            for (int i = 0; i < isTallTree.Length; i++)
            {
                GameObject pf = protos[i].prefab;
                isTallTree[i] = pf != null
                    && pf.name.IndexOf("Grass", StringComparison.OrdinalIgnoreCase) < 0
                    && pf.name.IndexOf("Bush", StringComparison.OrdinalIgnoreCase) < 0;
            }
            Vector3 tOrigin = terrain.transform.position;
            Vector3 tSize = terrain.terrainData.size;
            foreach (TreeInstance ti in terrain.terrainData.treeInstances)
            {
                if (ti.prototypeIndex >= 0 && ti.prototypeIndex < isTallTree.Length && isTallTree[ti.prototypeIndex])
                {
                    Vector3 wp = tOrigin + Vector3.Scale(ti.position, tSize);
                    ctx.TreePositionsXZ.Add(new Vector2(wp.x, wp.z));
                }
            }

            // Точки спавна врагов, игрок и ключевые NPC сцены (не захламлять вокруг них)
            string[] protectedActorNames =
            {
                "FirstEnemySP", "SecondEnemySP", "ThirdEnemySp",
                "Player", "SAYAAA", "SayaRoped", "Demon", "Stone"
            };
            foreach (string actorName in protectedActorNames)
            {
                GameObject actor = FindSceneObjectByName(scene, actorName);
                if (actor != null)
                {
                    ctx.ForbiddenBounds.Add(new Bounds(actor.transform.position, new Vector3(10f, 12f, 10f)));
                }
            }
            if (File.Exists(NavCheckDataPath))
            {
                var navPoints = JsonUtility.FromJson<NavCheckPointList>(File.ReadAllText(NavCheckDataPath));
                if (navPoints != null)
                {
                    foreach (NavCheckPointData sp in navPoints.spawns)
                    {
                        ctx.ForbiddenBounds.Add(new Bounds(sp.position, new Vector3(10f, 12f, 10f)));
                    }
                }
            }

            return ctx;
        }

        private static float MeasureReferenceHeight(Scene scene, string groupName, float fallback)
        {
            GameObject group = FindSceneObjectByName(scene, groupName);
            if (group == null) return fallback;

            float sum = 0f;
            int count = 0;
            foreach (Transform child in group.transform)
            {
                if (TryComputeRendererOrColliderBounds(child.gameObject, out Bounds b) && b.size.y > 0.6f && b.size.y < 12f)
                {
                    sum += b.size.y;
                    count++;
                }
            }
            return count > 0 ? Mathf.Clamp(sum / count, 1.5f, 4.0f) : fallback;
        }

        private static float MeasureReferenceTreeHeight(Scene scene, Terrain terrain, float fallback)
        {
            float sum = 0f;
            int count = 0;
            if (terrain != null && terrain.terrainData != null && terrain.terrainData.treePrototypes != null)
            {
                foreach (TreePrototype tp in terrain.terrainData.treePrototypes)
                {
                    if (tp.prefab != null && TryComputeRendererOrColliderBounds(tp.prefab, out Bounds b) && b.size.y > 3.5f)
                    {
                        sum += b.size.y;
                        count++;
                    }
                }
            }

            GameObject treesGroup = FindSceneObjectByName(scene, "Trees");
            if (treesGroup != null)
            {
                foreach (Transform child in treesGroup.transform)
                {
                    if (TryComputeRendererOrColliderBounds(child.gameObject, out Bounds b) && b.size.y > 3.5f)
                    {
                        sum += b.size.y;
                        count++;
                    }
                }
            }
            return count > 0 ? Mathf.Clamp(sum / count, 7f, 16f) : fallback;
        }

        private static bool TrySpawnGroundedProp(
            PlacementContext ctx,
            float minClearanceRadius,
            float maxSlopeDeg,
            float roadMinDist,
            float roadMaxDist,
            float embedDepth,
            Func<Vector3, GameObject> buildProp)
        {
            for (int attempt = 0; attempt < 18; attempt++)
            {
                if (!TrySampleLandPoint(ctx, minClearanceRadius, maxSlopeDeg, roadMinDist, roadMaxDist, out Vector3 pt))
                {
                    continue;
                }

                GameObject prop = buildProp(pt);
                if (prop == null) return false;

                SnapPropToTerrainByBounds(prop, ctx.Terrain, ctx.Collider, embedDepth);

                if (TryComputeRendererOrColliderBounds(prop, out Bounds finalBounds))
                {
                    if (OverlapsForbiddenXZ(ctx, finalBounds, padding: 0.4f))
                    {
                        UnityEngine.Object.DestroyImmediate(prop);
                        continue;
                    }

                    finalBounds.Expand(new Vector3(minClearanceRadius, 1f, minClearanceRadius));
                    ctx.ForbiddenBounds.Add(finalBounds);
                }

                return true;
            }

            return false;
        }

        private static bool TrySampleLandPoint(
            PlacementContext ctx,
            float clearanceRadius,
            float maxSlopeDeg,
            float roadMinDist,
            float roadMaxDist,
            out Vector3 worldPos)
        {
            Bounds tb = ctx.TerrainBounds;
            TerrainData td = ctx.Terrain.terrainData;
            Vector3 tPos = ctx.Terrain.transform.position;
            bool preferTrailCorridor = roadMaxDist < 60f;

            for (int attempt = 0; attempt < 60; attempt++)
            {
                float x, z;
                bool useCorridor = preferTrailCorridor && attempt < 42;
                if (useCorridor)
                {
                    bool firstSeg = ctx.Rng.NextDouble() < 0.5;
                    Vector3 segA = firstSeg ? ctx.TrailHouse : ctx.TrailMid;
                    Vector3 segB = firstSeg ? ctx.TrailMid : ctx.TrailShop;
                    float t = 0.12f + (float)ctx.Rng.NextDouble() * 0.76f;
                    Vector3 spine = Vector3.Lerp(segA, segB, t);
                    Vector2 dir = new Vector2(segB.x - segA.x, segB.z - segA.z).normalized;
                    Vector2 perp = new Vector2(-dir.y, dir.x);
                    float side = ctx.Rng.NextDouble() < 0.5 ? -1f : 1f;
                    float dist = roadMinDist + (float)ctx.Rng.NextDouble() * Mathf.Max(1f, roadMaxDist - roadMinDist);
                    x = spine.x + perp.x * (side * dist);
                    z = spine.z + perp.y * (side * dist);
                    if (x < tb.min.x + 16f || x > tb.max.x - 16f || z < tb.min.z + 16f || z > tb.max.z - 16f) continue;
                }
                else
                {
                    x = tb.min.x + 18f + (float)ctx.Rng.NextDouble() * (tb.size.x - 36f);
                    z = tb.min.z + 18f + (float)ctx.Rng.NextDouble() * (tb.size.z - 36f);
                }

                if (ctx.HasLake && IsInsideLakeMargin(x, z, ctx.LakeBounds, 11f)) continue;

                float roadDist = Mathf.Min(
                    DistancePointToSegmentXZ(x, z, ctx.TrailHouse, ctx.TrailMid),
                    DistancePointToSegmentXZ(x, z, ctx.TrailMid, ctx.TrailShop));
                float effMaxDist = useCorridor ? roadMaxDist : 999f;
                if (roadDist < roadMinDist || roadDist > effMaxDist) continue;

                float nx = (x - tPos.x) / td.size.x;
                float nz = (z - tPos.z) / td.size.z;
                if (td.GetSteepness(nx, nz) > maxSlopeDeg) continue;

                float y = SampleTerrainRaycastOrHeight(ctx.Terrain, ctx.Collider, new Vector3(x, 0f, z));
                if (ctx.HasLake && y <= ctx.LakeBounds.center.y + 0.8f) continue;

                Bounds probe = new Bounds(new Vector3(x, y + 1f, z), new Vector3(clearanceRadius * 2f, 20f, clearanceRadius * 2f));
                if (OverlapsForbiddenXZ(ctx, probe, padding: 0f)) continue;

                worldPos = new Vector3(x, y, z);
                return true;
            }

            worldPos = Vector3.zero;
            return false;
        }

        private static bool OverlapsForbiddenXZ(PlacementContext ctx, Bounds candidate, float padding)
        {
            float cMinX = candidate.min.x - padding;
            float cMaxX = candidate.max.x + padding;
            float cMinZ = candidate.min.z - padding;
            float cMaxZ = candidate.max.z + padding;

            for (int i = 0; i < ctx.ForbiddenBounds.Count; i++)
            {
                Bounds fb = ctx.ForbiddenBounds[i];
                if (cMaxX >= fb.min.x && cMinX <= fb.max.x && cMaxZ >= fb.min.z && cMinZ <= fb.max.z)
                {
                    return true;
                }
            }

            float cx = candidate.center.x;
            float cz = candidate.center.z;
            float minTreeDistSq = 6.25f; // >= 2.5 м от деревьев террейна
            for (int i = 0; i < ctx.TreePositionsXZ.Count; i++)
            {
                Vector2 tp = ctx.TreePositionsXZ[i];
                float dx = tp.x - cx;
                float dz = tp.y - cz;
                if (dx * dx + dz * dz < minTreeDistSq) return true;
            }

            return false;
        }

        private static float DistancePointToSegmentXZ(float px, float pz, Vector3 a, Vector3 b)
        {
            float vx = b.x - a.x;
            float vz = b.z - a.z;
            float wx = px - a.x;
            float wz = pz - a.z;
            float lenSq = vx * vx + vz * vz;
            float t = lenSq > 0.0001f ? Mathf.Clamp01((wx * vx + wz * vz) / lenSq) : 0f;
            float dx = px - (a.x + t * vx);
            float dz = pz - (a.z + t * vz);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static void SnapPropToTerrainByBounds(GameObject go, Terrain terrain, TerrainCollider tc, float embedDepth)
        {
            if (go == null) return;
            Physics.SyncTransforms();

            if (!TryComputeRendererOrColliderBounds(go, out Bounds b))
            {
                float y = SampleTerrainRaycastOrHeight(terrain, tc, go.transform.position);
                go.transform.position = new Vector3(go.transform.position.x, y - embedDepth, go.transform.position.z);
                return;
            }

            float inset = Mathf.Min(0.06f, Mathf.Min(b.extents.x, b.extents.z) * 0.25f);
            float minX = b.min.x + inset;
            float maxX = b.max.x - inset;
            float minZ = b.min.z + inset;
            float maxZ = b.max.z - inset;

            float yCenter = SampleTerrainRaycastOrHeight(terrain, tc, b.center);
            float y00 = SampleTerrainRaycastOrHeight(terrain, tc, new Vector3(minX, b.center.y, minZ));
            float y01 = SampleTerrainRaycastOrHeight(terrain, tc, new Vector3(minX, b.center.y, maxZ));
            float y10 = SampleTerrainRaycastOrHeight(terrain, tc, new Vector3(maxX, b.center.y, minZ));
            float y11 = SampleTerrainRaycastOrHeight(terrain, tc, new Vector3(maxX, b.center.y, maxZ));

            float minGroundY = Mathf.Min(yCenter, Mathf.Min(Mathf.Min(y00, y01), Mathf.Min(y10, y11)));
            float shiftY = (minGroundY - embedDepth) - b.min.y;
            go.transform.position += new Vector3(0f, shiftY, 0f);
            Physics.SyncTransforms();
        }

        private static bool TryComputeRendererOrColliderBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;

            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
                else bounds.Encapsulate(r.bounds);
            }

            if (hasBounds) return true;

            foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
            {
                if (c == null) continue;
                if (!hasBounds) { bounds = c.bounds; hasBounds = true; }
                else bounds.Encapsulate(c.bounds);
            }

            return hasBounds;
        }

        private static GameObject BuildDeadTree(Transform parent, Vector3 worldPos, float totalHeight, System.Random rng, Material barkMat)
        {
            var tree = new GameObject("DeadTree");
            tree.transform.SetParent(parent, false);
            tree.transform.position = worldPos;
            tree.transform.rotation = Quaternion.Euler((float)(rng.NextDouble() * 6 - 3), (float)(rng.NextDouble() * 360), (float)(rng.NextDouble() * 6 - 3));

            float baseWidth = 0.68f + (float)rng.NextDouble() * 0.28f;
            float lowerH = totalHeight * 0.56f;
            float upperH = totalHeight * 0.48f;

            AddSimplePrimitive(tree.transform, PrimitiveType.Cylinder, "LowerTrunk", new Vector3(0f, lowerH * 0.5f, 0f), Vector3.zero, new Vector3(baseWidth, lowerH * 0.5f, baseWidth), barkMat);
            AddSimplePrimitive(tree.transform, PrimitiveType.Cylinder, "UpperTrunk", new Vector3(0.08f, lowerH + upperH * 0.45f, 0.04f), new Vector3(4f, 0f, -6f), new Vector3(baseWidth * 0.58f, upperH * 0.5f, baseWidth * 0.58f), barkMat);

            for (int b = 0; b < 6; b++)
            {
                float yaw = b * 60f + (float)(rng.NextDouble() * 35 - 17);
                float bh = 2.4f + (float)rng.NextDouble() * 2.0f;
                float branchY = totalHeight * (0.54f + b * 0.065f);
                Vector3 pos = Quaternion.Euler(0f, yaw, 0f) * new Vector3(bh * 0.34f, branchY, 0f);
                float thickness = Mathf.Lerp(baseWidth * 0.36f, 0.14f, b / 6f);
                AddSimplePrimitive(tree.transform, PrimitiveType.Cylinder, "Branch_" + b, pos, new Vector3(0f, yaw, -48f), new Vector3(thickness, bh * 0.5f, thickness), barkMat);
            }

            MarkStaticAndObstacles(tree);
            return tree;
        }

        private static GameObject BuildCrookedGrave(Transform parent, Vector3 worldPos, float crossHeight, System.Random rng, Material crossMat, Material moundMat, Material boneMat)
        {
            var grave = new GameObject("Grave");
            grave.transform.SetParent(parent, false);
            grave.transform.position = worldPos;
            grave.transform.rotation = Quaternion.Euler((float)(rng.NextDouble() * 8 - 4), (float)(rng.NextDouble() * 360), (float)(rng.NextDouble() * 8 - 4));

            AddSimplePrimitive(grave.transform, PrimitiveType.Cube, "Mound", new Vector3(0f, 0.17f, 0.65f), Vector3.zero, new Vector3(1.15f, 0.34f, 2.15f), moundMat);
            AddSimplePrimitive(grave.transform, PrimitiveType.Cube, "CrossV", new Vector3(0f, crossHeight * 0.5f, -0.45f), new Vector3(0f, 0f, (float)(rng.NextDouble() * 8 - 4)), new Vector3(0.14f, crossHeight, 0.11f), crossMat);
            AddSimplePrimitive(grave.transform, PrimitiveType.Cube, "CrossH", new Vector3(0f, crossHeight * 0.72f, -0.45f), new Vector3(0f, 0f, (float)(rng.NextDouble() * 10 - 5)), new Vector3(crossHeight * 0.55f, 0.12f, 0.11f), crossMat);

            if (rng.NextDouble() < 0.65)
            {
                AddSimplePrimitive(grave.transform, PrimitiveType.Sphere, "Skull", new Vector3(0.24f, 0.36f, 0.35f), Vector3.zero, Vector3.one * 0.24f, boneMat);
                AddSimplePrimitive(grave.transform, PrimitiveType.Cylinder, "Bone", new Vector3(-0.20f, 0.35f, 0.85f), new Vector3(90f, 35f, 0f), new Vector3(0.06f, 0.28f, 0.06f), boneMat);
            }

            MarkStaticAndObstacles(grave);
            return grave;
        }

        private static GameObject BuildBrokenFence(Transform parent, Vector3 worldPos, float fenceHeight, System.Random rng, Material woodMat)
        {
            var fence = new GameObject("BrokenFence");
            fence.transform.SetParent(parent, false);
            fence.transform.position = worldPos;
            fence.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360), 0f);

            float hL = fenceHeight;
            float hR = fenceHeight * 0.92f;
            float span = 2.8f;

            AddSimplePrimitive(fence.transform, PrimitiveType.Cube, "PostL", new Vector3(-span * 0.48f, hL * 0.5f, 0f), new Vector3(0f, 0f, -6f), new Vector3(0.18f, hL, 0.18f), woodMat);
            AddSimplePrimitive(fence.transform, PrimitiveType.Cube, "PostR", new Vector3(span * 0.48f, hR * 0.5f, 0f), new Vector3(5f, 0f, 10f), new Vector3(0.18f, hR, 0.18f), woodMat);
            AddSimplePrimitive(fence.transform, PrimitiveType.Cube, "RailTop", new Vector3(-0.12f, fenceHeight * 0.72f, 0f), new Vector3(0f, 0f, -12f), new Vector3(span, 0.14f, 0.08f), woodMat);
            AddSimplePrimitive(fence.transform, PrimitiveType.Cube, "RailMid", new Vector3(0.06f, fenceHeight * 0.42f, 0f), new Vector3(0f, 0f, 5f), new Vector3(span * 0.95f, 0.14f, 0.08f), woodMat);
            AddSimplePrimitive(fence.transform, PrimitiveType.Cube, "RailFallen", new Vector3(0.35f, 0.16f, 0.35f), new Vector3(6f, 18f, -16f), new Vector3(span * 0.8f, 0.13f, 0.08f), woodMat);

            MarkStaticAndObstacles(fence);
            return fence;
        }

        private static GameObject BuildFlickeringLantern(Transform parent, Vector3 worldPos, float totalHeight, System.Random rng, Material ironMat, Material glowMat)
        {
            var lantern = new GameObject("FlickeringLantern");
            lantern.transform.SetParent(parent, false);
            lantern.transform.position = worldPos;
            lantern.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360), (float)(rng.NextDouble() * 4 - 2));

            float postH = totalHeight - 0.15f;
            AddSimplePrimitive(lantern.transform, PrimitiveType.Cylinder, "Plinth", new Vector3(0f, 0.22f, 0f), Vector3.zero, new Vector3(0.42f, 0.22f, 0.42f), ironMat);
            AddSimplePrimitive(lantern.transform, PrimitiveType.Cylinder, "Post", new Vector3(0f, postH * 0.5f, 0f), Vector3.zero, new Vector3(0.16f, postH * 0.5f, 0.16f), ironMat);
            AddSimplePrimitive(lantern.transform, PrimitiveType.Cube, "Arm", new Vector3(0.48f, postH - 0.08f, 0f), Vector3.zero, new Vector3(1.05f, 0.12f, 0.12f), ironMat);
            AddSimplePrimitive(lantern.transform, PrimitiveType.Cube, "Housing", new Vector3(0.92f, postH - 0.38f, 0f), Vector3.zero, new Vector3(0.36f, 0.46f, 0.36f), glowMat);

            var lightGo = new GameObject("PointLight", typeof(Light), typeof(UniversalAdditionalLightData), typeof(FlickeringLight));
            lightGo.transform.SetParent(lantern.transform, false);
            lightGo.transform.localPosition = new Vector3(0.92f, postH - 0.48f, 0f);
            Light l = lightGo.GetComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.56f, 0.22f);
            l.range = 18f;
            l.intensity = 3.0f;
            l.shadows = LightShadows.None;

            MarkStaticAndObstacles(lantern);
            return lantern;
        }

        private static void SpawnStaticCrow(Transform parent, Vector3 localPos, System.Random rng, Material crowMat)
        {
            var crow = new GameObject("StaticCrow");
            crow.transform.SetParent(parent, false);
            crow.transform.localPosition = localPos;
            crow.transform.localRotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360), 0f);

            AddSimplePrimitive(crow.transform, PrimitiveType.Sphere, "Body", new Vector3(0f, 0.11f, 0f), new Vector3(-20f, 0f, 0f), new Vector3(0.18f, 0.16f, 0.34f), crowMat);
            AddSimplePrimitive(crow.transform, PrimitiveType.Sphere, "Head", new Vector3(0f, 0.24f, 0.14f), Vector3.zero, Vector3.one * 0.14f, crowMat);
            AddSimplePrimitive(crow.transform, PrimitiveType.Cube, "Beak", new Vector3(0f, 0.24f, 0.25f), Vector3.zero, new Vector3(0.04f, 0.04f, 0.12f), crowMat);
            MarkStaticNoObstacle(crow);
        }

        private static void BuildMistPatch(Transform parent, Vector3 worldPos, Material mistMat)
        {
            var mistGo = new GameObject("MistPatch", typeof(ParticleSystem));
            mistGo.transform.SetParent(parent, false);
            mistGo.transform.position = worldPos;

            ParticleSystem ps = mistGo.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(6f, 11f);
            main.startColor = new Color(0.55f, 0.58f, 0.62f, 0.16f);
            main.maxParticles = 14;

            var emission = ps.emission;
            emission.rateOverTime = 1.6f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(14f, 1.8f, 14f);

            var rend = mistGo.GetComponent<ParticleSystemRenderer>();
            if (rend != null && mistMat != null) rend.sharedMaterial = mistMat;
        }

        private static bool SpawnAbandonedCar(Transform parent, PlacementContext ctx)
        {
            GameObject carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefabPath);
            if (carPrefab == null) return false;

            return TrySpawnGroundedProp(
                ctx,
                minClearanceRadius: 3.0f,
                maxSlopeDeg: 24f,
                roadMinDist: 4.5f,
                roadMaxDist: 45f,
                embedDepth: 0.04f,
                buildProp: pt =>
                {
                    GameObject car = (GameObject)PrefabUtility.InstantiatePrefab(carPrefab, parent);
                    car.name = "AbandonedCar";
                    car.transform.position = pt;
                    TerrainData td = ctx.Terrain.terrainData;
                    float nx = (pt.x - ctx.Terrain.transform.position.x) / td.size.x;
                    float nz = (pt.z - ctx.Terrain.transform.position.z) / td.size.z;
                    Vector3 normal = td.GetInterpolatedNormal(nx, nz);
                    car.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, 132f, 0f);
                    car.transform.localScale = Vector3.one;
                    Physics.SyncTransforms();

                    if (TryComputeRendererOrColliderBounds(car, out Bounds rawBounds))
                    {
                        float maxLen = Mathf.Max(rawBounds.size.x, rawBounds.size.z);
                        if (maxLen > 0.05f)
                        {
                            float targetLen = 4.3f; // 4.0–4.5 м
                            car.transform.localScale = Vector3.one * (targetLen / maxLen);
                        }
                    }

                    ConvertRenderersToUrpIfNeeded(car);
                    MarkStaticAndObstacles(car);
                    return car;
                });
        }

        private static void ConvertRenderersToUrpIfNeeded(GameObject go)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) return;

            string carMatsFolder = Level2AssetsFolder + "/CarMaterials";
            EnsureFolder(carMatsFolder);

            foreach (Renderer rend in go.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = rend.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null || m.shader == null || m.shader.name.Contains("Universal")) continue;

                    string outPath = $"{carMatsFolder}/{m.name}_URP.mat";
                    Material urpMat = AssetDatabase.LoadAssetAtPath<Material>(outPath);
                    if (urpMat == null)
                    {
                        urpMat = new Material(urpLit);
                        Texture mainTex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                        Color col = m.HasProperty("_Color") ? m.GetColor("_Color") : new Color(0.28f, 0.26f, 0.25f, 1f);
                        urpMat.SetColor("_BaseColor", col * 0.7f);
                        if (mainTex != null) urpMat.SetTexture("_BaseMap", mainTex);
                        urpMat.SetFloat("_Smoothness", 0.2f);
                        AssetDatabase.CreateAsset(urpMat, outPath);
                    }
                    mats[i] = urpMat;
                    changed = true;
                }
                if (changed) rend.sharedMaterials = mats;
            }
        }

        private static Material EnsureMistParticleMaterial(string path)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
            if (mat == null)
            {
                mat = new Material(s);
                AssetDatabase.CreateAsset(mat, path);
            }

            string texPath = Level2AssetsFolder + "/MistRadialTex.png";
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null)
            {
                const int sz = 64;
                tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
                for (int y = 0; y < sz; y++)
                {
                    for (int x = 0; x < sz; x++)
                    {
                        float dx = (x + 0.5f) / sz * 2f - 1f;
                        float dy = (y + 0.5f) / sz * 2f - 1f;
                        float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                    }
                }
                tex.Apply();
                File.WriteAllBytes(texPath, tex.EncodeToPNG());
                AssetDatabase.ImportAsset(texPath);
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            }

            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_BaseMap") && tex != null) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex") && tex != null) mat.SetTexture("_MainTex", tex);
            mat.renderQueue = 3000;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static List<GameObject> CollectSnappableRoots(Scene scene, Terrain terrain)
        {
            var list = new List<GameObject>();
            var added = new HashSet<GameObject>();

            void AddTarget(GameObject go)
            {
                if (go == null) return;
                if (added.Add(go)) list.Add(go);
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root == terrain.gameObject ||
                    root.name == AdditionsRootName ||
                    root.name == "LakeNotWalkableVolume" ||
                    root.name == "[NavMesh_Exclusions]")
                {
                    continue;
                }
                if (root.GetComponent<Light>() != null ||
                    root.GetComponent<Volume>() != null ||
                    root.GetComponent<Camera>() != null ||
                    root.GetComponent<Canvas>() != null ||
                    root.GetComponent<RegObj>() != null ||
                    root.GetComponent<UnityEngine.EventSystems.EventSystem>() != null ||
                    root.GetComponent<PlayableDirector>() != null ||
                    root.GetComponent<ConversationManager>() != null)
                {
                    continue;
                }
                if (root.name.IndexOf("Lake", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    root.name.IndexOf("Water", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    root.name.IndexOf("Cinemachine", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    root.name.IndexOf("Canvas", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    root.name.IndexOf("ArrowCam", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                if (root.name == "BigHouses" ||
                    root.name == "OldHouses" ||
                    root.name.Contains("Fence") ||
                    root.name.Contains("Trees") ||
                    root.name == "SpawnPoints")
                {
                    foreach (Transform child in root.transform)
                    {
                        AddTarget(child.gameObject);
                    }
                    continue;
                }

                AddTarget(root);
            }

            return list;
        }

        private static float SampleTerrainRaycastOrHeight(Terrain terrain, TerrainCollider tc, Vector3 pos)
        {
            if (tc != null)
            {
                float topY = terrain.transform.position.y + terrain.terrainData.size.y + 50f;
                float rayLen = terrain.terrainData.size.y + 150f;
                if (tc.Raycast(new Ray(new Vector3(pos.x, topY, pos.z), Vector3.down), out RaycastHit hit, rayLen))
                {
                    return hit.point.y;
                }
            }
            return terrain.SampleHeight(pos) + terrain.transform.position.y;
        }

        private static float ComputeNavMeshAreaXZ()
        {
            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            Vector3[] v = tri.vertices;
            int[] idx = tri.indices;
            float total = 0f;
            for (int i = 0; i < idx.Length; i += 3)
            {
                Vector3 a = v[idx[i]];
                Vector3 b = v[idx[i + 1]];
                Vector3 c = v[idx[i + 2]];
                float crossY = (b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x);
                total += 0.5f * Mathf.Abs(crossY);
            }
            return total;
        }

        private static bool TryGetLakeMetadata(Scene scene, out Bounds bounds)
        {
            if (TryFindLakeBounds(scene, out bounds)) return true;

            if (File.Exists(NavCheckDataPath))
            {
                var data = JsonUtility.FromJson<NavCheckPointList>(File.ReadAllText(NavCheckDataPath));
                if (data != null && data.hasLakeBounds)
                {
                    bounds = new Bounds(data.lakeCenter, data.lakeSize);
                    return true;
                }
            }
            bounds = default;
            return false;
        }

        private static bool TryFindLakeBounds(Scene scene, out Bounds bounds)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.gameObject.name.IndexOf("Lake", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        r.gameObject.name.IndexOf("Water", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        bounds = r.bounds;
                        return true;
                    }
                }
            }
            bounds = default;
            return false;
        }

        private static bool IsInsideLakeMargin(float x, float z, Bounds lakeBounds, float margin)
        {
            return x >= lakeBounds.min.x - margin && x <= lakeBounds.max.x + margin &&
                   z >= lakeBounds.min.z - margin && z <= lakeBounds.max.z + margin;
        }

        private static bool IsLevel2Scene(Scene scene, string opName)
        {
            if (scene.IsValid() && string.Equals(scene.name, "Level2", StringComparison.OrdinalIgnoreCase)) return true;
            Debug.LogError($"[{opName}] Операция разрешена ТОЛЬКО в открытой сцене Level2 (сейчас открыта: {scene.name}).");
            return false;
        }

        private static GameObject CreateChildGroup(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void AddSimplePrimitive(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 euler, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
        }

        private static void MarkStaticAndObstacles(GameObject go)
        {
            StaticEditorFlags flags = StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
            }
        }

        private static void MarkStaticNoObstacle(GameObject go)
        {
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
            MarkStaticAndObstacles(go);
        }

        private static Material EnsureMaterial(string path, Shader shader, Color color, float metallic, float smoothness)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material EnsureEmissiveMaterial(string path, Shader shader, Color color, float emissionIntensity)
        {
            Material mat = EnsureMaterial(path, shader, color, 0f, 0.8f);
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", color * emissionIntensity);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static List<T> FindAllInScene<T>(Scene scene) where T : Component
        {
            var list = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                list.AddRange(root.GetComponentsInChildren<T>(true));
            }
            return list;
        }

        private static T FindFirstInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        private static GameObject FindSceneObjectByName(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (string.Equals(t.name, name, StringComparison.Ordinal)) return t.gameObject;
                }
            }
            return null;
        }

        private static void EnsureFolder(string assetFolderPath)
        {
            string[] parts = assetFolderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
