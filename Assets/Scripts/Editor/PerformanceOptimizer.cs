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
    /// Аудит и комплексная оптимизация Level1 и URP для резкого снижения Vertices/Triangles/DrawCalls и роста FPS.
    /// </summary>
    [InitializeOnLoad]
    public static class PerformanceOptimizer
    {
        private const string Level1Path = "Assets/Scenes/Level1.unity";
        private const string PipelineAssetPath = "Assets/Settings/PC_PipelineAsset.asset";
        private const string ForwardRendererPath = "Assets/Settings/PC_PipelineAsset_ForwardRenderer.asset";
        private const string AutoRunMarkerPath = "Library/MoBA_PerfOptimize_v1.done";

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

                    string key = mf.sharedMesh.name;
                    meshStats.TryGetValue(key, out var cur);
                    meshStats[key] = (cur.count + 1, cur.tris + tris, cur.verts + verts, cur.shadowCasters + (castsShadow ? 1 : 0));
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

            OptimizeUrpSettings();
            OptimizeCameraAndTerrain(scene);
            OptimizeScenePropsAndMaterials(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[PerformanceOptimizer] Оптимизация Level1 и URP успешно выполнена и сохранена.");
            AuditScene();
        }

        private static void OptimizeUrpSettings()
        {
            UnityEngine.Object pcAsset = AssetDatabase.LoadMainAssetAtPath(PipelineAssetPath);
            if (pcAsset != null)
            {
                var so = new SerializedObject(pcAsset);
                SetFloat(so, "m_ShadowDistance", 32f);
                SetInt(so, "m_AdditionalLightsShadowmapResolution", 1024);
                SetInt(so, "m_SoftShadowQuality", 1); // Medium вместо High
                SetInt(so, "m_EnableLODCrossFade", 0);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(pcAsset);
            }

            UnityEngine.Object rendererAsset = AssetDatabase.LoadMainAssetAtPath(ForwardRendererPath);
            if (rendererAsset != null)
            {
                var so = new SerializedObject(rendererAsset);
                SetInt(so, "m_IntermediateTextureMode", 0); // Auto вместо Always
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(rendererAsset);
            }
        }

        private static void OptimizeCameraAndTerrain(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Camera cam in root.GetComponentsInChildren<Camera>(true))
                {
                    if (cam == null) continue;
                    // При чёрном тумане FogDensity = 0.041 всё дальше ~110 м уже полностью скрыто в тумане
                    if (cam.farClipPlane > 140f && (cam.cullingMask & 1) != 0)
                    {
                        Undo.RecordObject(cam, "Optimize Camera FarClip & Occlusion");
                        cam.farClipPlane = 135f;
                        cam.useOcclusionCulling = true;
                        EditorUtility.SetDirty(cam);
                    }
                }

                foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
                {
                    if (terrain == null) continue;
                    Undo.RecordObject(terrain, "Optimize Terrain Settings");
                    terrain.heightmapPixelError = 12f;
                    terrain.treeDistance = 135f;
                    terrain.treeBillboardDistance = 45f;
                    terrain.treeMaximumFullLODCount = 20;
                    terrain.detailObjectDistance = 45f;
                    terrain.detailObjectDensity = 0.55f;
                    terrain.shadowCastingMode = ShadowCastingMode.Off;
                    EditorUtility.SetDirty(terrain);
                }
            }
        }

        private static void OptimizeScenePropsAndMaterials(Scene scene)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            int disabledBushShadows = 0;
            int instancedMaterials = 0;
            var touchedMaterials = new HashSet<Material>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MeshRenderer mr in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (mr == null) continue;
                    GameObject go = mr.gameObject;
                    string n = go.name;

                    bool isBushOrSmallProp = n.Contains("BushDry") || n.Contains("stone") || n.Contains("Grass") || n.Contains("Fern");
                    bool isRepeatedEnv = isBushOrSmallProp || n.Contains("Tree") || n.Contains("Walllvl0") || n.Contains("Fence");

                    if (isBushOrSmallProp)
                    {
                        if (mr.shadowCastingMode != ShadowCastingMode.Off)
                        {
                            Undo.RecordObject(mr, "Disable Bush Shadows");
                            mr.shadowCastingMode = ShadowCastingMode.Off;
                            EditorUtility.SetDirty(mr);
                            disabledBushShadows++;
                        }

                        // Исправляем ошибочный слой UI (5) на Ground (6), если стоит UI
                        if (groundLayer >= 0 && go.layer == 5)
                        {
                            Undo.RecordObject(go, "Fix Bush Layer");
                            go.layer = groundLayer;
                            EditorUtility.SetDirty(go);
                        }
                    }

                    if (isRepeatedEnv)
                    {
                        // Включаем BatchingStatic и OccludeeStatic для группировки и отсечения
                        StaticEditorFlags desiredFlags = GameObjectUtility.GetStaticEditorFlags(go)
                                                         | StaticEditorFlags.BatchingStatic
                                                         | StaticEditorFlags.OccludeeStatic;
                        if (GameObjectUtility.GetStaticEditorFlags(go) != desiredFlags)
                        {
                            GameObjectUtility.SetStaticEditorFlags(go, desiredFlags);
                            EditorUtility.SetDirty(go);
                        }

                        foreach (Material mat in mr.sharedMaterials)
                        {
                            if (mat == null || !touchedMaterials.Add(mat)) continue;
                            if (!mat.enableInstancing)
                            {
                                mat.enableInstancing = true;
                                EditorUtility.SetDirty(mat);
                                instancedMaterials++;
                            }
                        }
                    }
                }
            }

            Debug.Log($"[PerformanceOptimizer] Отключены тени у {disabledBushShadows} кустов/камней; включён GPU Instancing на {instancedMaterials} материалах.");
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
