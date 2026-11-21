#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MoBA.EditorTools
{
    /// <summary>
    /// Комплексный аудит и оптимизация сцены Level1, префабов и URP (FPS, Triangles, Vertices, Draw Calls).
    /// </summary>
    [InitializeOnLoad]
    public static class PerformanceOptimizer
    {
        private const string Level1Path = "Assets/Scenes/Level1.unity";
        private const string PipelineAssetPath = "Assets/Settings/PC_PipelineAsset.asset";
        private const string ForwardRendererPath = "Assets/Settings/PC_PipelineAsset_ForwardRenderer.asset";
        private const string AutoRunMarkerPath = "Library/MoBA_PerfOptimize_v3.done";

        static PerformanceOptimizer()
        {
            EditorApplication.delayCall += TryAutoRunOnce;
        }

        private static void TryAutoRunOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryAutoRunOnce;
                return;
            }
            if (File.Exists(AutoRunMarkerPath)) return;

            File.WriteAllText(AutoRunMarkerPath, DateTime.UtcNow.ToString("O"));
            OptimizeLevel1AndUrp();
        }

        [MenuItem("MoBA/Performance/Audit Scene Triangles & DrawCalls")]
        public static void AuditScene()
        {
            Scene scene = GetOrOpenLevel1();
            if (!scene.IsValid()) return;

            var meshStats = new Dictionary<string, (int count, long tris, long verts, int shadowCasters)>();
            long totalTris = 0;
            long totalVerts = 0;
            int totalRenderers = 0;
            int totalShadowCasters = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(false))
                {
                    if (mf == null || mf.sharedMesh == null) continue;
                    MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                    if (mr == null || !mr.enabled) continue;

                    int tris = mf.sharedMesh.triangles.Length / 3;
                    int verts = mf.sharedMesh.vertexCount;
                    bool castsShadow = mr.shadowCastingMode != ShadowCastingMode.Off;

                    totalTris += tris;
                    totalVerts += verts;
                    totalRenderers++;
                    if (castsShadow) totalShadowCasters++;

                    meshStats.TryGetValue(mf.sharedMesh.name, out var cur);
                    meshStats[mf.sharedMesh.name] = (cur.count + 1, cur.tris + tris, cur.verts + verts, cur.shadowCasters + (castsShadow ? 1 : 0));
                }

                foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(false))
                {
                    if (smr == null || smr.sharedMesh == null || !smr.enabled) continue;
                    int tris = smr.sharedMesh.triangles.Length / 3;
                    int verts = smr.sharedMesh.vertexCount;
                    bool castsShadow = smr.shadowCastingMode != ShadowCastingMode.Off;

                    totalTris += tris;
                    totalVerts += verts;
                    totalRenderers++;
                    if (castsShadow) totalShadowCasters++;

                    string key = "[Skinned] " + smr.sharedMesh.name;
                    meshStats.TryGetValue(key, out var cur);
                    meshStats[key] = (cur.count + 1, cur.tris + tris, cur.verts + verts, cur.shadowCasters + (castsShadow ? 1 : 0));
                }
            }

            Debug.Log($"[PerfAudit] Сцена '{scene.name}': активных MeshRenderer={totalRenderers}, ShadowCasters={totalShadowCasters}, Суммарно Triangles={totalTris:N0}, Vertices={totalVerts:N0}");
            foreach (var kv in meshStats.OrderByDescending(x => x.Value.tris).Take(15))
            {
                Debug.Log($"[PerfAudit]   {kv.Key}: x{kv.Value.count} | Tris={kv.Value.tris:N0} | Verts={kv.Value.verts:N0} | CastShadows={kv.Value.shadowCasters}");
            }
        }

        [MenuItem("MoBA/Performance/Optimize Level1 & URP (FPS Boost)")]
        public static void OptimizeLevel1AndUrp()
        {
            Scene scene = GetOrOpenLevel1();
            if (!scene.IsValid()) return;

            Debug.Log("[PerfAudit] === СОСТОЯНИЕ ДО ОПТИМИЗАЦИИ ===");
            AuditScene();

            OptimizeUrpSettings();
            OptimizeEnvironmentPrefabs();
            OptimizeCameraCinemachineAndTerrain(scene);
            int removedBushes = ThinRedundantBushes(scene);
            OptimizeScenePropsMaterialsAndShadows(scene);
            StaticOcclusionCulling.Compute();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PerformanceOptimizer] Оптимизация Level1 и URP завершена (удалено лишних кустов BushDry_A: {removedBushes}).");
            Debug.Log("[PerfAudit] === СОСТОЯНИЕ ПОСЛЕ ОПТИМИЗАЦИИ ===");
            AuditScene();
        }

        private static void OptimizeUrpSettings()
        {
            UnityEngine.Object pcAsset = AssetDatabase.LoadMainAssetAtPath(PipelineAssetPath);
            if (pcAsset != null)
            {
                var so = new SerializedObject(pcAsset);
                SetFloat(so, "m_ShadowDistance", 30f);
                SetInt(so, "m_MainLightShadowmapResolution", 1024);
                SetInt(so, "m_AdditionalLightsShadowmapResolution", 1024);
                SetInt(so, "m_SoftShadowQuality", 1);
                SetInt(so, "m_EnableLODCrossFade", 0);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(pcAsset);
            }

            UnityEngine.Object rendererAsset = AssetDatabase.LoadMainAssetAtPath(ForwardRendererPath);
            if (rendererAsset != null)
            {
                var so = new SerializedObject(rendererAsset);
                SetInt(so, "m_IntermediateTextureMode", 0);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(rendererAsset);
            }

            // Предотвращаем двойное удержание LOD_0 деревьев (было lodBias = 2)
            UnityEngine.Object qAsset = AssetDatabase.LoadMainAssetAtPath("ProjectSettings/QualitySettings.asset");
            if (qAsset != null)
            {
                var so = new SerializedObject(qAsset);
                SerializedProperty levels = so.FindProperty("m_QualitySettings");
                if (levels != null && levels.isArray)
                {
                    for (int i = 0; i < levels.arraySize; i++)
                    {
                        SerializedProperty lvl = levels.GetArrayElementAtIndex(i);
                        SerializedProperty bias = lvl.FindPropertyRelative("lodBias");
                        if (bias != null && bias.floatValue > 1.15f)
                        {
                            bias.floatValue = 1.0f;
                        }
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(qAsset);
                }
            }
        }

        private static void OptimizeEnvironmentPrefabs()
        {
            OptimizeBushPrefab("Assets/Packs/TerrainSampleAssets/Prefabs/BushDry_A.prefab");
            OptimizeTreePrefab("Assets/Packs/Tree_Packs/URP_Tree_Pack/Prefabs/URP_Tree_1.prefab");
            OptimizeTreePrefab("Assets/Packs/Tree_Packs/URP_Tree_Pack/Prefabs/URP_Tree_2.prefab");
            OptimizeTreePrefab("Assets/Packs/Tree_Packs/URP_Tree_Pack/Prefabs/URP_Tree_3.prefab");
            OptimizeEnemyPrefab("Assets/Prefabs/Agent.prefab");
            OptimizeEnemyPrefab("Assets/Prefabs/Agent (3) Variant.prefab");
            OptimizeEnemyPrefab("Assets/Prefabs/DemonFast Variant.prefab");
        }

        private static void OptimizeBushPrefab(string path)
        {
            GameObject root = LoadPrefabForEdit(path);
            if (root == null) return;

            root.layer = Mathf.Max(0, LayerMask.NameToLayer("Ground"));
            GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);

            Renderer[] rends = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in rends)
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                if (r.sharedMaterial != null) r.sharedMaterial.enableInstancing = true;
            }

            LODGroup lod = root.GetComponent<LODGroup>();
            if (lod == null) lod = root.AddComponent<LODGroup>();
            lod.fadeMode = LODFadeMode.None;
            lod.SetLODs(new[] { new LOD(0.045f, rends) });
            lod.RecalculateBounds();

            SaveAndUnloadPrefab(root, path);
        }

        private static void OptimizeTreePrefab(string path)
        {
            GameObject root = LoadPrefabForEdit(path);
            if (root == null) return;

            LODGroup lod = root.GetComponent<LODGroup>();
            if (lod != null)
            {
                LOD[] lods = lod.GetLODs();
                float[] heights = { 0.48f, 0.25f, 0.12f, 0.045f };
                for (int i = 0; i < lods.Length && i < heights.Length; i++)
                {
                    lods[i].screenRelativeTransitionHeight = heights[i];
                    if (i >= 2 && lods[i].renderers != null)
                    {
                        foreach (Renderer r in lods[i].renderers)
                        {
                            if (r != null) r.shadowCastingMode = ShadowCastingMode.Off;
                        }
                    }
                }
                lod.fadeMode = LODFadeMode.None;
                lod.SetLODs(lods);
            }

            foreach (MeshRenderer mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(mr.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                foreach (Material m in mr.sharedMaterials)
                {
                    if (m != null) m.enableInstancing = true;
                }
            }

            SaveAndUnloadPrefab(root, path);
        }

        private static void OptimizeEnemyPrefab(string path)
        {
            GameObject root = LoadPrefabForEdit(path);
            if (root == null) return;

            foreach (Animator a in root.GetComponentsInChildren<Animator>(true))
            {
                a.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            }
            foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.updateWhenOffscreen = false;
            }

            // Не даём LODGroup отсекать монстров в пределах тумана (было ~0.078 -> исчезали дальше ~16 м)
            foreach (LODGroup lod in root.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] lods = lod.GetLODs();
                if (lods != null && lods.Length > 0)
                {
                    int last = lods.Length - 1;
                    if (lods[last].screenRelativeTransitionHeight > 0.01f)
                    {
                        lods[last].screenRelativeTransitionHeight = 0.01f;
                        lod.SetLODs(lods);
                    }
                }
            }

            SaveAndUnloadPrefab(root, path);
        }

        private static void OptimizeCameraCinemachineAndTerrain(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Camera cam in root.GetComponentsInChildren<Camera>(true))
                {
                    if (cam == null) continue;
                    bool isWorldCam = (cam.cullingMask & 1) != 0;
                    if (isWorldCam && cam.farClipPlane > 135f)
                    {
                        Undo.RecordObject(cam, "Optimize Camera FarClip & Occlusion");
                        cam.farClipPlane = 130f;
                        cam.useOcclusionCulling = true;
                        EditorUtility.SetDirty(cam);
                    }
                    else if (!isWorldCam)
                    {
                        var uacd = cam.GetComponent("UniversalAdditionalCameraData");
                        if (uacd != null)
                        {
                            var uacdSo = new SerializedObject(uacd);
                            SetInt(uacdSo, "m_RenderShadows", 0);
                            uacdSo.ApplyModifiedPropertiesWithoutUndo();
                            EditorUtility.SetDirty(uacd);
                        }
                    }
                }

                // Cinemachine vcam переопределяет farClipPlane основной камеры каждый кадр
                foreach (Component comp in root.GetComponentsInChildren<Component>(true))
                {
                    if (comp == null) continue;
                    string typeName = comp.GetType().Name;
                    if (typeName == "CinemachineCamera" || typeName == "CinemachineVirtualCamera")
                    {
                        var so = new SerializedObject(comp);
                        SerializedProperty farProp = so.FindProperty("Lens.FarClipPlane") ?? so.FindProperty("m_Lens.FarClipPlane");
                        if (farProp != null && farProp.floatValue > 135f)
                        {
                            farProp.floatValue = 130f;
                            so.ApplyModifiedPropertiesWithoutUndo();
                            EditorUtility.SetDirty(comp);
                        }
                    }
                    else if (typeName == "MirrorReflection")
                    {
                        var so = new SerializedObject(comp);
                        SetInt(so, "_rtWidth", 256);
                        SetInt(so, "_rtHeight", 256);
                        SetFloat(so, "_maxRenderDistance", 12f);
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(comp);
                    }
                }

                foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
                {
                    if (terrain == null) continue;
                    Undo.RecordObject(terrain, "Optimize Terrain Settings");
                    terrain.heightmapPixelError = 12f;
                    terrain.treeDistance = 130f;
                    terrain.treeBillboardDistance = 40f;
                    terrain.treeMaximumFullLODCount = 16;
                    terrain.detailObjectDistance = 42f;
                    terrain.detailObjectDensity = 0.5f;
                    terrain.shadowCastingMode = ShadowCastingMode.Off;
                    EditorUtility.SetDirty(terrain);
                }
            }
        }

        private static int ThinRedundantBushes(Scene scene)
        {
            Bounds? playBounds = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                ArenaBounds ab = root.GetComponentInChildren<ArenaBounds>(true);
                if (ab != null)
                {
                    Bounds b = ab.Bounds;
                    b.Expand(new Vector3(36f, 200f, 36f)); // безопасный запас за забором
                    playBounds = b;
                    break;
                }
            }

            var occupiedCells = new HashSet<(int, int)>();
            var toRemove = new List<GameObject>();
            const float cellSize = 3.5f;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform tf in root.GetComponentsInChildren<Transform>(true))
                {
                    if (tf == null || tf.gameObject == null) continue;
                    if (!tf.name.StartsWith("BushDry_A", StringComparison.Ordinal) || tf.name == "BushDry_A_group") continue;
                    if (PrefabUtility.IsPartOfPrefabInstance(tf.gameObject) && !PrefabUtility.IsOutermostPrefabInstanceRoot(tf.gameObject)) continue;
                    if (tf.GetComponentInChildren<MeshRenderer>(true) == null) continue;

                    Vector3 p = tf.position;
                    if (playBounds.HasValue && !playBounds.Value.Contains(new Vector3(p.x, playBounds.Value.center.y, p.z)))
                    {
                        toRemove.Add(tf.gameObject);
                        continue;
                    }

                    var cell = (Mathf.FloorToInt(p.x / cellSize), Mathf.FloorToInt(p.z / cellSize));
                    if (!occupiedCells.Add(cell))
                    {
                        toRemove.Add(tf.gameObject);
                    }
                }
            }

            foreach (GameObject go in toRemove)
            {
                if (go != null) Undo.DestroyObjectImmediate(go);
            }
            return toRemove.Count;
        }

        private static void OptimizeScenePropsMaterialsAndShadows(Scene scene)
        {
            int groundLayer = Mathf.Max(0, LayerMask.NameToLayer("Ground"));
            var touchedMaterials = new HashSet<Material>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MeshRenderer mr in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (mr == null) continue;
                    GameObject go = mr.gameObject;
                    string n = go.name;

                    bool isFoliageOrSmall = n.Contains("BushDry") || n.Contains("stone") || n.Contains("Grass") ||
                                            n.Contains("Fern") || n.Contains("WoodenStake") || n.Contains("Grave") ||
                                            n.Contains("Cross") || mr.bounds.size.sqrMagnitude < 4f;
                    bool isTreeLod23 = n.Contains("LOD_2") || n.Contains("LOD_3");

                    if (isFoliageOrSmall || isTreeLod23)
                    {
                        if (mr.shadowCastingMode != ShadowCastingMode.Off)
                        {
                            Undo.RecordObject(mr, "Disable Small/Foliage Shadow");
                            mr.shadowCastingMode = ShadowCastingMode.Off;
                            EditorUtility.SetDirty(mr);
                        }
                    }

                    if (n.Contains("BushDry") && go.layer == 5)
                    {
                        go.layer = groundLayer;
                        EditorUtility.SetDirty(go);
                    }

                    bool isFoliage = n.Contains("BushDry") || n.Contains("Tree") || n.Contains("Grass") || n.Contains("Fern");
                    bool isSolidOccluder = n.Contains("Walllvl") || n.Contains("MainHouse") || n.Contains("BigHouse") ||
                                           n.Contains("OldHouse") || n.Contains("Building_Base") || n.Contains("Floor");

                    if (isFoliage)
                    {
                        SetCleanStaticFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                    }
                    else if (isSolidOccluder)
                    {
                        SetCleanStaticFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);
                    }
                    else if (n.Contains("Fence") || n.Contains("WoodenStake") || n.Contains("Grave"))
                    {
                        SetCleanStaticFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                    }

                    foreach (Material mat in mr.sharedMaterials)
                    {
                        if (mat == null || !touchedMaterials.Add(mat)) continue;
                        if (!mat.enableInstancing)
                        {
                            mat.enableInstancing = true;
                            EditorUtility.SetDirty(mat);
                        }
                    }
                }
            }
        }

        private static void SetCleanStaticFlags(GameObject go, StaticEditorFlags flags)
        {
            if (GameObjectUtility.GetStaticEditorFlags(go) != flags)
            {
                GameObjectUtility.SetStaticEditorFlags(go, flags);
                EditorUtility.SetDirty(go);
            }
        }

        private static GameObject LoadPrefabForEdit(string path)
        {
            if (!File.Exists(path)) return null;
            return PrefabUtility.LoadPrefabContents(path);
        }

        private static void SaveAndUnloadPrefab(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static Scene GetOrOpenLevel1()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && string.Equals(active.name, "Level1", StringComparison.OrdinalIgnoreCase))
            {
                return active;
            }
            return EditorSceneManager.OpenScene(Level1Path, OpenSceneMode.Single);
        }

        private static void SetInt(SerializedObject so, string propName, int value)
        {
            SerializedProperty p = so.FindProperty(propName);
            if (p != null) p.intValue = value;
        }

        private static void SetFloat(SerializedObject so, string propName, float value)
        {
            SerializedProperty p = so.FindProperty(propName);
            if (p != null) p.floatValue = value;
        }
    }
}
#endif
