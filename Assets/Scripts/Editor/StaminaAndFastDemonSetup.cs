#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using KinematicCharacterController.Examples;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Zenject;

namespace MoBA.EditorTools
{
    /// <summary>
    /// Единая Editor-утилита:
    /// - импортирует 8 звуков в Assets/Audio и привязывает их к игроку и монстрам;
    /// - создаёт и назначает слой Floor + обновляет маски StableGroundLayers и WhatIsGround;
    /// - пересобирает низкую анимацию четверенек без прыжков и плотный коллайдер от y = 0 на DemonFast Variant.
    /// </summary>
    [InitializeOnLoad]
    public static class StaminaAndFastDemonSetup
    {
        private const string Level1Path = "Assets/Scenes/Level1.unity";
        private const string AgentBasePath = "Assets/Prefabs/Agent.prefab";
        private const string Agent1Path = "Assets/Prefabs/Agent (1) Variant.prefab";
        private const string Agent2Path = "Assets/Prefabs/Agent (2) Variant.prefab";
        private const string Agent3Path = "Assets/Prefabs/Agent (3) Variant.prefab";
        private const string Creep3MatPath = "Assets/Packs/Creep Horror Creature/Materials/Creep3_mat.mat";

        private const string DemonBasePath = "Assets/Prefabs/Demon.prefab";
        private const string DemonFastPrefabPath = "Assets/Prefabs/DemonFast Variant.prefab";
        private const string DemonFastMatPath = "Assets/Prefabs/DemonFast_URP.mat";

        private const string DemonBaseTexPath = "Assets/Packs/Demon Horror Creature with Weapon/Textures/Demon_tex/Demon_damaged_tex/Demon2_BaseColor.png";
        private const string DemonNormalTexPath = "Assets/Packs/Demon Horror Creature with Weapon/Textures/Demon_tex/Demon_damaged_tex/Demon2_Normal_OpenGL.png";
        private const string DemonAoTexPath = "Assets/Packs/Demon Horror Creature with Weapon/Textures/Demon_tex/Demon1_AO.png";
        private const string SprintActionId = "641cd816-40e6-41b4-8c3d-04687c349290";
        private const string AutoRunMarkerPath = "Library/MoBA_AudioAndGiantDemon_v2.done";

        static StaminaAndFastDemonSetup()
        {
            EditorApplication.delayCall += TryAutoRunOnce;
        }

        private static void TryAutoRunOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryAutoRunOnce;
                return;
            }
            if (File.Exists(AutoRunMarkerPath))
            {
                return;
            }

            File.WriteAllText(AutoRunMarkerPath, DateTime.UtcNow.ToString("O"));
            Run();
        }

        [MenuItem("MoBA/Setup Audio & Fix Giant Demon")]
        [MenuItem("MoBA/Setup Stamina & Fast Demon")]
        public static void Run()
        {
            AudioAndFloorSetup.EnsureAudioFilesImported();
            int floorLayer = AudioAndFloorSetup.EnsureFloorLayer();

            // Подключаем 3D-звук бега ко всем обычным монстрам (через базовый Agent.prefab и варианты)
            AudioAndFloorSetup.ConfigureRegularEnemyPrefabAudio(AgentBasePath);

            Scene activeScene = SceneManager.GetActiveScene();
            Scene scene = activeScene.IsValid() && activeScene.path == Level1Path
                ? activeScene
                : EditorSceneManager.OpenScene(Level1Path, OpenSceneMode.Single);
            float mainHouseHeight = MeasureMainHouseHeight(scene);

            GameObject agent3Variant = BuildFourthEnemyVariant();
            Material demonMat = BuildDemonFastMaterial();

            GameObject baseDemon = AssetDatabase.LoadAssetAtPath<GameObject>(DemonBasePath);
            if (baseDemon == null)
            {
                Debug.LogError($"[StaminaAndFastDemonSetup] Не найден базовый префаб {DemonBasePath}");
                return;
            }

            DemonAnimMetrics metrics = DemonFastAnimationBuilder.BuildAnimationsAndController(baseDemon);
            GameObject demonFastVariant = BuildFifthEnemyVariant(baseDemon, demonMat, metrics, mainHouseHeight);

            VerifyFinalDemonDimensions(demonFastVariant, mainHouseHeight);

            AudioAndFloorSetup.ConfigureSceneFloorAndPlayerAudio(scene, floorLayer);
            WireSprintInput(scene);
            WireEnemySpawner(scene, agent3Variant, demonFastVariant);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[StaminaAndFastDemonSetup] Успешно завершено: 8 аудиоклипов, слой Floor, анимация четверенек и гигантский DemonFast Variant сохранены.");
        }

        private static float MeasureMainHouseHeight(Scene scene)
        {
            GameObject mainHouse = FindRootByName(scene, "MainHouse");
            if (mainHouse == null) return 18.25f;

            Transform buildingBase = mainHouse.transform.Find("PFB_Building_Base");
            Renderer baseRenderer = buildingBase != null ? buildingBase.GetComponent<Renderer>() : null;
            if (baseRenderer != null)
            {
                float h = baseRenderer.bounds.size.y;
                Debug.Log($"[StaminaAndFastDemonSetup] MainHouse (PFB_Building_Base без дымохода): minY={baseRenderer.bounds.min.y:F2}, maxY={baseRenderer.bounds.max.y:F2}, height={h:F2} м");
                return h;
            }
            return 18.25f;
        }

        private static GameObject BuildFourthEnemyVariant()
        {
            GameObject agentBase = AssetDatabase.LoadAssetAtPath<GameObject>(AgentBasePath);
            if (agentBase == null) throw new InvalidOperationException($"Не найден {AgentBasePath}");

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(agentBase);
            try
            {
                instance.name = "Agent (3) Variant";

                Material creep3Mat = AssetDatabase.LoadAssetAtPath<Material>(Creep3MatPath);
                if (creep3Mat != null)
                {
                    foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
                    {
                        r.sharedMaterial = creep3Mat;
                    }
                }

                EnemyAI ai = instance.GetComponent<EnemyAI>();
                if (ai != null)
                {
                    var so = new SerializedObject(ai);
                    SerializedProperty skipProp = so.FindProperty("_skipLookingState");
                    if (skipProp != null) skipProp.boolValue = false;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, Agent3Path);
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static Material BuildDemonFastMaterial()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) throw new InvalidOperationException("Шейдер Universal Render Pipeline/Lit не найден.");

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(DemonFastMatPath);
            if (mat == null)
            {
                mat = new Material(urpLit);
                AssetDatabase.CreateAsset(mat, DemonFastMatPath);
            }
            else
            {
                mat.shader = urpLit;
            }

            Texture2D baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(DemonBaseTexPath);
            Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(DemonNormalTexPath);
            Texture2D aoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(DemonAoTexPath);

            EnsureNormalMapImporter(DemonNormalTexPath);

            mat.SetTexture("_BaseMap", baseTex);
            mat.SetColor("_BaseColor", new Color(0.42f, 0.36f, 0.38f, 1f));
            mat.SetFloat("_Metallic", 0.08f);
            mat.SetFloat("_Smoothness", 0.58f);
            mat.SetFloat("_EnvironmentReflections", 1f);
            mat.SetFloat("_SpecularHighlights", 1f);

            if (normalTex != null)
            {
                mat.SetTexture("_BumpMap", normalTex);
                mat.SetFloat("_BumpScale", 1.35f);
                mat.EnableKeyword("_NORMALMAP");
            }

            if (aoTex != null)
            {
                mat.SetTexture("_OcclusionMap", aoTex);
                mat.SetFloat("_OcclusionStrength", 0.85f);
            }

            mat.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void EnsureNormalMapImporter(string texPath)
        {
            var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
        }

        private static GameObject BuildFifthEnemyVariant(
            GameObject baseDemon,
            Material demonMat,
            DemonAnimMetrics metrics,
            float mainHouseHeight)
        {
            float targetQuadrupedHeight = mainHouseHeight * 0.96f;
            float scaleFactor = targetQuadrupedHeight / Mathf.Max(0.1f, metrics.QuadrupedHeight);
            float targetUniformScale = Mathf.Round(metrics.BaseScale * scaleFactor * 100f) / 100f;
            float effectiveRatio = targetUniformScale / Mathf.Max(0.01f, metrics.BaseScale);

            float giantQuadHeight = metrics.QuadrupedHeight * effectiveRatio;
            float giantQuadWidth = metrics.QuadrupedWidth * effectiveRatio;
            float giantCenterZ = metrics.QuadrupedCenterZ * effectiveRatio;
            float giantSnoutReachZ = metrics.QuadrupedMaxZ * effectiveRatio;
            float giantHeadY = metrics.QuadrupedHeadY * effectiveRatio;

            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(baseDemon);
            try
            {
                inst.name = "DemonFast Variant";
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                inst.transform.localScale = Vector3.one * targetUniformScale;

                foreach (Component comp in inst.GetComponents<Component>())
                {
                    if (comp != null && comp.GetType().Name == "BoneRenderer")
                    {
                        UnityEngine.Object.DestroyImmediate(comp);
                    }
                }

                foreach (SkinnedMeshRenderer smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.sharedMaterial = demonMat;
                    smr.updateWhenOffscreen = true;
                }

                Animator animator = GetOrAdd<Animator>(inst);
                animator.runtimeAnimatorController = metrics.Controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                NavMeshAgent nav = GetOrAdd<NavMeshAgent>(inst);
                nav.agentTypeID = 0;
                nav.radius = 2.2f;
                nav.height = Mathf.Clamp(giantQuadHeight, 8f, 22f);
                // Слегка заглубляем baseOffset (-0.65 м), чтобы гигантские лапы плотно стояли на земле и склонах без левитации над триангуляцией NavMesh
                nav.baseOffset = -0.65f;
                nav.speed = 4.2f;
                nav.acceleration = 32f;
                nav.angularSpeed = 360f;
                nav.stoppingDistance = 0f;
                nav.autoBraking = true;
                nav.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

                // Плотный CapsuleCollider ровно от земли (minY = 0) по высоте низкого тела на четвереньках
                float worldColRadius = Mathf.Clamp(giantQuadWidth * 0.22f, 2.2f, 3.2f);
                float worldColHeight = Mathf.Max(worldColRadius * 2.05f, giantQuadHeight * 0.90f);
                CapsuleCollider col = GetOrAdd<CapsuleCollider>(inst);
                col.direction = 1;
                col.radius = worldColRadius / targetUniformScale;
                col.height = worldColHeight / targetUniformScale;
                col.center = new Vector3(0f, (worldColHeight * 0.5f) / targetUniformScale, (giantCenterZ * 0.45f) / targetUniformScale);
                col.isTrigger = false;

                float immediateKillRange = Mathf.Clamp(Mathf.Max(worldColRadius + 4.5f, giantSnoutReachZ * 0.85f), 6.5f, 10.5f);
                float headAimHeight = Mathf.Clamp(giantHeadY * 0.90f, 5f, 14f);

                EnemyAI ai = GetOrAdd<EnemyAI>(inst);
                ConfigureFastDemonAI(ai, immediateKillRange, headAimHeight);

                AudioAndFloorSetup.ConfigureGiantDemonAudio(inst);

                EnemyInstaller installer = GetOrAdd<EnemyInstaller>(inst);
                var installerSo = new SerializedObject(installer);
                installerSo.FindProperty("_enemyAnim").objectReferenceValue = animator;
                installerSo.ApplyModifiedPropertiesWithoutUndo();

                GameObjectContext ctx = GetOrAdd<GameObjectContext>(inst);
                var ctxSo = new SerializedObject(ctx);
                SerializedProperty monoInstallers = ctxSo.FindProperty("_monoInstallers");
                monoInstallers.arraySize = 1;
                monoInstallers.GetArrayElementAtIndex(0).objectReferenceValue = installer;
                ctxSo.FindProperty("_autoRun").boolValue = true;
                ctxSo.ApplyModifiedPropertiesWithoutUndo();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(inst, DemonFastPrefabPath);
                Debug.Log($"[StaminaAndFastDemonSetup] Гигантский 5-й враг сохранён: {DemonFastPrefabPath} | scale={targetUniformScale:F2} | worldColR={worldColRadius:F2} H={worldColHeight:F2} minY=0.00 | NavMeshR={nav.radius:F2} H={nav.height:F2} | killRange={immediateKillRange:F2}");
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(inst);
            }
        }

        private static void VerifyFinalDemonDimensions(GameObject demonPrefab, float mainHouseHeight)
        {
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(demonPrefab);
            Mesh tempMesh = new Mesh();
            try
            {
                inst.transform.position = Vector3.zero;
                inst.transform.rotation = Quaternion.identity;
                SkinnedMeshRenderer smr = inst.GetComponentInChildren<SkinnedMeshRenderer>(true);

                string[] clipNames = { "DemonFast_PatrolWalk.anim", "DemonFast_QuadrupedChase.anim" };
                foreach (string clipName in clipNames)
                {
                    AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{DemonFastAnimationBuilder.AnimFolder}/{clipName}");
                    if (clip == null || smr == null) continue;

                    Bounds agg = new Bounds();
                    bool has = false;
                    float minLFoot = float.MaxValue, minRFoot = float.MaxValue, minLHand = float.MaxValue, minRHand = float.MaxValue, avgPelvis = 0f;
                    Transform lFoot = inst.transform.Find("Арматура/Bone.026/Bone.029/Bone.030/Bone.032/Bone.032_end");
                    Transform rFoot = inst.transform.Find("Арматура/Bone.027/Bone.028/Bone.031/Bone.033/Bone.033_end");
                    Transform lHand = inst.transform.Find("Арматура/Bone/Bone.001/Bone.002/Bone.005/Bone.016/Bone.017/Bone.018/Bone.021/Bone.022/Bone.022_end");
                    Transform rHand = inst.transform.Find("Арматура/Bone/Bone.001/Bone.002/Bone.004/Bone.006/Bone.007/Bone.008/Bone.009/Bone.010/Bone.010_end");
                    Transform pelvis = inst.transform.Find("Арматура/Bone");
                    for (int i = 0; i <= 8; i++)
                    {
                        clip.SampleAnimation(inst, (i / 8f) * clip.length);
                        Bounds b = DemonFastAnimationBuilder.ComputeWorldSkinnedMeshBounds(smr, tempMesh);
                        if (!has) { agg = b; has = true; } else agg.Encapsulate(b);
                        if (lFoot != null) minLFoot = Mathf.Min(minLFoot, lFoot.position.y);
                        if (rFoot != null) minRFoot = Mathf.Min(minRFoot, rFoot.position.y);
                        if (lHand != null) minLHand = Mathf.Min(minLHand, lHand.position.y);
                        if (rHand != null) minRHand = Mathf.Min(minRHand, rHand.position.y);
                        if (pelvis != null) avgPelvis += pelvis.position.y / 9f;
                    }
                    Debug.Log($"[StaminaAndFastDemonSetup] VERIFY {clipName} @ scale={inst.transform.localScale.y:F2}: meshMinY={agg.min.y:F3} smrBoundsMinY={smr.bounds.min.y:F3} maxY={agg.max.y:F2} H={agg.size.y:F2}м (MainHouse={mainHouseHeight:F2}м) | PelvisY={avgPelvis:F2} LFootMinY={minLFoot:F2} RFootMinY={minRFoot:F2} LHandMinY={minLHand:F2} RHandMinY={minRHand:F2}");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tempMesh);
                UnityEngine.Object.DestroyImmediate(inst);
            }
        }

        private static void ConfigureFastDemonAI(EnemyAI ai, float immediateKillRange, float headAimHeight)
        {
            var so = new SerializedObject(ai);
            so.FindProperty("whatIsPlayer").intValue = 1 << 3;
            so.FindProperty("_sightRange").floatValue = 20f;
            so.FindProperty("_attackRange").floatValue = 7f;
            so.FindProperty("_immediateKillRange").floatValue = immediateKillRange;
            so.FindProperty("_patrolSpeed").floatValue = 4.2f;
            so.FindProperty("_chaseSpeed").floatValue = 10f;
            so.FindProperty("_arenaEdgeMargin").floatValue = 14f;
            so.FindProperty("_minPointTravelDistance").floatValue = 12f;
            so.FindProperty("_minSeparationBetweenMonsters").floatValue = 14f;
            so.FindProperty("_minNavMeshEdgeClearance").floatValue = 2.2f;
            so.FindProperty("_skipLookingState").boolValue = true;
            SerializedProperty headAimProp = so.FindProperty("_headAimHeight");
            if (headAimProp != null) headAimProp.floatValue = headAimHeight;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireSprintInput(Scene scene)
        {
            ExamplePlayer player = FindInScene<ExamplePlayer>(scene);
            PlayerInput playerInput = FindInScene<PlayerInput>(scene);
            if (player == null || playerInput == null) return;

            Undo.RecordObject(playerInput, "Bind Sprint Action to ExamplePlayer.OnSprint");
            foreach (PlayerInput.ActionEvent evt in playerInput.actionEvents)
            {
                if (evt.actionId != SprintActionId &&
                    (string.IsNullOrEmpty(evt.actionName) || !evt.actionName.Contains("Sprint")))
                {
                    continue;
                }

                bool alreadyBound = false;
                int count = evt.GetPersistentEventCount();
                for (int i = 0; i < count; i++)
                {
                    if (evt.GetPersistentTarget(i) == player && evt.GetPersistentMethodName(i) == nameof(ExamplePlayer.OnSprint))
                    {
                        alreadyBound = true;
                        break;
                    }
                }

                if (!alreadyBound)
                {
                    UnityEventTools.AddPersistentListener(evt, player.OnSprint);
                }
                break;
            }

            EditorUtility.SetDirty(playerInput);
            PrefabUtility.RecordPrefabInstancePropertyModifications(playerInput);
        }

        private static void WireEnemySpawner(Scene scene, GameObject agent3Variant, GameObject demonFastVariant)
        {
            EnemySpawner spawner = FindInScene<EnemySpawner>(scene);
            if (spawner == null) return;

            Transform fifthSp = CreateOrUpdateFifthSpawnPoint(scene, spawner);

            GameObject agentBase = AssetDatabase.LoadAssetAtPath<GameObject>(AgentBasePath);
            GameObject agent1 = AssetDatabase.LoadAssetAtPath<GameObject>(Agent1Path);
            GameObject agent2 = AssetDatabase.LoadAssetAtPath<GameObject>(Agent2Path);

            Undo.RecordObject(spawner, "Configure 5 Enemies in EnemySpawner");
            var so = new SerializedObject(spawner);

            SerializedProperty enemiesProp = so.FindProperty("_enemies");
            enemiesProp.arraySize = 5;
            enemiesProp.GetArrayElementAtIndex(0).objectReferenceValue = agentBase;
            enemiesProp.GetArrayElementAtIndex(1).objectReferenceValue = agent1;
            enemiesProp.GetArrayElementAtIndex(2).objectReferenceValue = agent2;
            enemiesProp.GetArrayElementAtIndex(3).objectReferenceValue = agent3Variant;
            enemiesProp.GetArrayElementAtIndex(4).objectReferenceValue = demonFastVariant;

            SerializedProperty spProp = so.FindProperty("_spawnPoints");
            if (spProp.arraySize < 5) spProp.arraySize = 5;
            spProp.GetArrayElementAtIndex(4).objectReferenceValue = fifthSp;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(spawner);
        }

        private static Transform CreateOrUpdateFifthSpawnPoint(Scene scene, EnemySpawner spawner)
        {
            GameObject spRoot = FindRootByName(scene, "SpawnPoints");
            Transform parent = spRoot != null ? spRoot.transform : null;

            Transform existing = parent != null ? parent.Find("FifthEnemySP") : FindRootByName(scene, "FifthEnemySP")?.transform;
            GameObject spGo = existing != null ? existing.gameObject : new GameObject("FifthEnemySP");
            if (existing == null)
            {
                Undo.RegisterCreatedObjectUndo(spGo, "Create FifthEnemySP");
                if (parent != null) spGo.transform.SetParent(parent, true);
            }
            else
            {
                Undo.RecordObject(spGo.transform, "Update FifthEnemySP Position");
            }

            Vector3 pos = FindValidFifthSpawnPosition(scene, spawner);
            spGo.transform.position = pos;
            spGo.transform.rotation = Quaternion.identity;
            EditorUtility.SetDirty(spGo);
            return spGo.transform;
        }

        private static Vector3 FindValidFifthSpawnPosition(Scene scene, EnemySpawner spawner)
        {
            ArenaBounds arena = FindInScene<ArenaBounds>(scene);
            Bounds bounds = arena != null ? arena.Bounds : new Bounds(new Vector3(73f, 10f, 79f), new Vector3(260f, 30f, 260f));

            RegObj reg = FindInScene<RegObj>(scene);
            Vector3 playerPos = new Vector3(132.5f, 24.5f, 111.9f);
            if (reg != null)
            {
                Transform pt = new SerializedObject(reg).FindProperty("_player")?.objectReferenceValue as Transform;
                if (pt != null) playerPos = pt.position;
            }

            var existingSps = new List<Vector3>();
            var spawnerSo = new SerializedObject(spawner);
            SerializedProperty spProp = spawnerSo.FindProperty("_spawnPoints");
            for (int i = 0; i < Math.Min(4, spProp.arraySize); i++)
            {
                Transform t = spProp.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                if (t != null) existingSps.Add(t.position);
            }

            var forbiddenBounds = CollectExclusionZones(scene);
            Vector3 navCenter = bounds.center;
            if (NavMesh.SamplePosition(navCenter, out NavMeshHit centerHit, 40f, NavMesh.AllAreas))
            {
                navCenter = centerHit.position;
            }

            Vector3[] candidates =
            {
                new Vector3(5f, 10f, 45f),
                new Vector3(25f, 10f, 55f),
                new Vector3(-12f, 10f, 35f),
                new Vector3(50f, 10f, 42f)
            };

            NavMeshPath path = new NavMeshPath();
            foreach (Vector3 c in candidates)
            {
                if (TryValidateSpawnCandidate(c, bounds, playerPos, existingSps, forbiddenBounds, navCenter, path, out Vector3 valid))
                {
                    return valid;
                }
            }

            return new Vector3(5f, 3.38f, 45f);
        }

        private static bool TryValidateSpawnCandidate(
            Vector3 probe, Bounds arena, Vector3 playerPos, List<Vector3> otherSps,
            List<Bounds> forbiddenZones, Vector3 navCenter, NavMeshPath path, out Vector3 validPos)
        {
            validPos = probe;
            if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 18f, NavMesh.AllAreas)) return false;
            Vector3 p = hit.position;
            if (p.x < arena.min.x + 20f || p.x > arena.max.x - 20f || p.z < arena.min.z + 20f || p.z > arena.max.z - 20f) return false;
            if (HorizontalDist(p, playerPos) < 65f) return false;
            foreach (Vector3 sp in otherSps) if (HorizontalDist(p, sp) < 35f) return false;
            foreach (Bounds b in forbiddenZones) if (p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z) return false;
            if (NavMesh.FindClosestEdge(p, out NavMeshHit edge, NavMesh.AllAreas) && edge.distance < 3.5f) return false;
            if (!NavMesh.CalculatePath(p, navCenter, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            validPos = p;
            return true;
        }

        private static List<Bounds> CollectExclusionZones(Scene scene)
        {
            var list = new List<Bounds>();
            string[] names = { "MainHouse", "Shop", "BigHouses", "Water_Plane", "CutSceneLake" };
            foreach (string n in names)
            {
                GameObject go = FindRootByName(scene, n);
                if (go == null) continue;
                Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    Bounds b = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                    b.Expand(new Vector3(24f, 50f, 24f));
                    list.Add(b);
                }
            }
            return list;
        }

        private static float HorizontalDist(Vector3 a, Vector3 b) => Mathf.Sqrt((a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z));
        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }
        private static GameObject FindRootByName(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == name) return root;
            return null;
        }
        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }
    }
}
#endif
