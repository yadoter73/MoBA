#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using DialogueEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MoBA.EditorTools
{
    /// <summary>
    /// Стилизация MainHouse и Shop под мрачную атмосферу VillagePack (тёмное старое дерево, ржавый металл, кровь) + скорость диалогов.
    /// </summary>
    [InitializeOnLoad]
    public static class BuildingHorrorStyleSetup
    {
        private const string Level1Path = "Assets/Scenes/Level1.unity";
        private const string GenTexDir = "Assets/Generated/HorrorHouses/Textures";
        private const string GenMatDir = "Assets/Generated/HorrorHouses/Materials";
        private const string AutoRunMarkerPath = "Library/MoBA_HorrorStyle_v3.done";

        private readonly struct MaterialStyleRule
        {
            public readonly string SourceAlbedoPath;
            public readonly string NormalPath;
            public readonly string MetallicPath;
            public readonly string OcclusionPath;
            public readonly HorrorTextureSpec TextureSpec;
            public readonly Color BaseTint;
            public readonly float SmoothnessScale;
            public readonly float MetallicScale;

            public MaterialStyleRule(
                string sourceAlbedoPath,
                string normalPath,
                string metallicPath,
                string occlusionPath,
                HorrorTextureSpec textureSpec,
                Color baseTint,
                float smoothnessScale,
                float metallicScale = 1f)
            {
                SourceAlbedoPath = sourceAlbedoPath;
                NormalPath = normalPath;
                MetallicPath = metallicPath;
                OcclusionPath = occlusionPath;
                TextureSpec = textureSpec;
                BaseTint = baseTint;
                SmoothnessScale = smoothnessScale;
                MetallicScale = metallicScale;
            }
        }

        static BuildingHorrorStyleSetup()
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
            RestyleMainHouseAndShop();
        }

        [MenuItem("MoBA/Style/Restyle MainHouse & Shop (Horror)")]
        public static void RestyleMainHouseAndShop()
        {
            Scene scene = GetOrOpenLevel1();
            if (!scene.IsValid()) return;

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("[BuildingHorrorStyleSetup] Не найден шейдер 'Universal Render Pipeline/Lit'.");
                return;
            }

            CleanupWrongNavMeshDecals(scene);

            Dictionary<string, Material> mainHouseMats = BuildMainHouseMaterials(urpLit);
            Dictionary<string, Material> shopMats = BuildShopMaterials(urpLit);
            Material[] decalMats = BuildBloodDecalMaterials(urpLit);

            int updatedRenderers = 0;
            Transform mainHouseRoot = null;
            Transform shopRoot = null;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MeshRenderer mr in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (mr == null || IsExcludedPropOrNpc(mr.transform)) continue;

                    if (TryReplaceMaterials(mr, mainHouseMats, "MainHouse_"))
                    {
                        updatedRenderers++;
                        mainHouseRoot ??= FindBuildingRoot(mr.transform);
                    }
                    else if (TryReplaceMaterials(mr, shopMats, "Shop_"))
                    {
                        updatedRenderers++;
                        shopRoot ??= FindBuildingRoot(mr.transform);
                    }
                }

                foreach (ConversationManager cm in root.GetComponentsInChildren<ConversationManager>(true))
                {
                    if (cm != null && cm.ScrollSpeed > 0.85f)
                    {
                        Undo.RecordObject(cm, "Speed Up Dialogue Scroll");
                        cm.ScrollSpeed = 0.85f;
                        EditorUtility.SetDirty(cm);
                    }
                }
            }

            int mainDecals = mainHouseRoot != null
                ? HorrorDecalPlacer.PlaceBloodDecals(mainHouseRoot, "[HorrorDecals_MainHouse]", decalMats, isShop: false)
                : 0;
            int shopDecals = shopRoot != null
                ? HorrorDecalPlacer.PlaceBloodDecals(shopRoot, "[HorrorDecals_Shop]", decalMats, isShop: true)
                : 0;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[BuildingHorrorStyleSetup] Готово: рендеров обновлено={updatedRenderers}, пятен крови MainHouse={mainDecals} (root='{mainHouseRoot?.name}'), Shop={shopDecals} (root='{shopRoot?.name}').");
        }

        private static bool TryReplaceMaterials(MeshRenderer mr, Dictionary<string, Material> matMap, string generatedPrefix)
        {
            Material[] mats = mr.sharedMaterials;
            bool changed = false;

            for (int i = 0; i < mats.Length; i++)
            {
                Material cur = mats[i];
                if (cur == null) continue;

                string lookup = cur.name.Replace(" (Instance)", "");
                if (lookup.StartsWith(generatedPrefix, StringComparison.Ordinal))
                {
                    foreach (var kv in matMap)
                    {
                        if (lookup.Contains(kv.Key))
                        {
                            mats[i] = kv.Value;
                            changed = true;
                            break;
                        }
                    }
                }
                else if (matMap.TryGetValue(lookup, out Material replacement))
                {
                    mats[i] = replacement;
                    changed = true;
                }
            }

            if (changed)
            {
                Undo.RecordObject(mr, "Apply Horror Building Materials");
                mr.sharedMaterials = mats;
                EditorUtility.SetDirty(mr);
            }
            return changed;
        }

        private static Transform FindBuildingRoot(Transform child)
        {
            GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(child.gameObject);
            if (prefabRoot != null) return prefabRoot.transform;

            Transform cur = child;
            while (cur.parent != null && cur.parent.parent != null)
            {
                cur = cur.parent;
            }
            return cur;
        }

        private static void CleanupWrongNavMeshDecals(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform tf in root.GetComponentsInChildren<Transform>(true))
                {
                    if (tf == null) continue;
                    if ((tf.name == "[HorrorDecals_MainHouse]" || tf.name == "[HorrorDecals_Shop]") &&
                        tf.parent != null && tf.parent.name.StartsWith("BuildingNoNav_", StringComparison.Ordinal))
                    {
                        Undo.DestroyObjectImmediate(tf.gameObject);
                    }
                }
            }
        }

        private static Dictionary<string, Material> BuildMainHouseMaterials(Shader urpLit)
        {
            const string t = "Assets/FurnishedCabin/Textures";
            var darkWoodTint = new Color(0.68f, 0.64f, 0.60f, 1f);
            var interiorTint = new Color(0.62f, 0.58f, 0.54f, 1f);
            var applianceTint = new Color(0.56f, 0.52f, 0.48f, 1f);

            var rules = new Dictionary<string, MaterialStyleRule>(StringComparer.OrdinalIgnoreCase)
            {
                ["BuildingA_MAT"] = new MaterialStyleRule(
                    $"{t}/BuildingA_MAT_AlbedoTransparency.png", $"{t}/BuildingA_MAT_Normal.png", $"{t}/BuildingA_MAT_MetallicSmoothness.png", null,
                    new HorrorTextureSpec(0.32f, 0.45f, 0.72f, 0.88f, 0.0f, 0.92f, 101), darkWoodTint, 0.06f, 0.10f),
                ["BuildingB_MAT"] = new MaterialStyleRule(
                    $"{t}/BuildingB_MAT_AlbedoTransparency.png", $"{t}/BuildingB_MAT_Normal.png", $"{t}/BuildingB_MAT_MetallicSmoothness.png", null,
                    new HorrorTextureSpec(0.30f, 0.44f, 0.75f, 0.90f, 0.0f, 0.95f, 102), darkWoodTint, 0.05f, 0.08f),
                ["Doors_TVStand_MAT"] = new MaterialStyleRule(
                    $"{t}/Doors_TVStand_MAT_AlbedoTransparency.png", $"{t}/Doors_TVStand_MAT_Normal.png", $"{t}/Doors_TVStand_MAT_MetallicSmoothness.png", null,
                    new HorrorTextureSpec(0.28f, 0.42f, 0.72f, 0.90f, 0.25f, 0.95f, 103), darkWoodTint, 0.06f, 0.15f),
                ["Fireplace_KitchenBench_MAT"] = new MaterialStyleRule(
                    $"{t}/Fireplace_KitchenBench_AlbedoTransparency.png", $"{t}/Fireplace_KitchenBench_Normal.png", $"{t}/Fireplace_KitchenBench_MetallicSmoothness.png", $"{t}/Fireplace_KitchenBench_AmbientOcclusion.png",
                    new HorrorTextureSpec(0.24f, 0.55f, 0.85f, 0.55f, 0.55f, 0.92f, 104), interiorTint, 0.06f, 0.12f),
                ["Rug_Curtains_MAT"] = new MaterialStyleRule(
                    $"{t}/Rug_Curtains_MAT_AlbedoTransparency.png", $"{t}/Rug_Curtains_MAT_Normal.png", $"{t}/Rug_Curtains_MAT_MetallicSmoothness.png", $"{t}/Rug_Curtains_MAT_AmbientOcclusion.png",
                    new HorrorTextureSpec(0.32f, 0.50f, 0.78f, 0.0f, 0.0f, 0.95f, 105), interiorTint, 0.04f, 0.0f),
                ["Chairs_MAT"] = new MaterialStyleRule(
                    $"{t}/Chairs_AlbedoTransparency.png", $"{t}/Chairs_Normal.png", $"{t}/Chairs_MetallicSmoothness.png", null,
                    new HorrorTextureSpec(0.30f, 0.45f, 0.70f, 0.85f, 0.0f, 0.88f, 106), darkWoodTint, 0.06f, 0.05f),
                ["BedroomFurniture_MAT"] = new MaterialStyleRule(
                    $"{t}/BedroomFurniture_AlbedoTransparency.png", $"{t}/BedroomFurniture_Normal.png", $"{t}/BedroomFurniture_MetallicSmoothness.png", null,
                    new HorrorTextureSpec(0.30f, 0.45f, 0.72f, 0.85f, 0.0f, 0.75f, 107), darkWoodTint, 0.06f, 0.05f),
                ["Bed_Sink_MAT"] = new MaterialStyleRule(
                    $"{t}/Bed_Sink_MAT_AlbedoTransparency.png", $"{t}/Bed_Sink_MAT_Normal.png", $"{t}/Bed_Sink_MAT_MetallicSmoothness.png", null,
                    new HorrorTextureSpec(0.24f, 0.55f, 0.82f, 0.35f, 0.65f, 0.92f, 108), applianceTint, 0.06f, 0.12f),
                ["Bathroom_Oven_MAT"] = new MaterialStyleRule(
                    $"{t}/Bathroom_Oven_MAT_AlbedoTransparency.png", $"{t}/Bathroom_Oven_MAT_Normal.png", $"{t}/Bathroom_Oven_MAT_MetallicSmoothness.png", $"{t}/Bathroom_Oven_MAT_AmbientOcclusion.png",
                    new HorrorTextureSpec(0.21f, 0.65f, 0.88f, 0.0f, 0.90f, 0.95f, 109), applianceTint, 0.06f, 0.18f),
                ["TV_Lamp_Table_MAT"] = new MaterialStyleRule(
                    $"{t}/TV_Lamp_Table_MAT_AlbedoTransparency.png", $"{t}/TV_Lamp_Table_MAT_Normal.png", $"{t}/TV_Lamp_Table_MAT_MetallicSmoothness.png", null,
                    new HorrorTextureSpec(0.26f, 0.48f, 0.78f, 0.85f, 0.15f, 0.88f, 110), darkWoodTint, 0.06f, 0.08f),
                ["Lights_towel_MAT"] = new MaterialStyleRule(
                    $"{t}/Lights_towel_MAT_AlbedoTransparency.png", $"{t}/Lights_towel_MAT_Normal.png", $"{t}/Lights_towel_MAT_MetallicSmoothness.png", null,
                    new HorrorTextureSpec(0.23f, 0.60f, 0.88f, 0.0f, 0.45f, 0.95f, 111), applianceTint, 0.04f, 0.05f),
                ["Plastic_Steel_Handle_MAT"] = new MaterialStyleRule(
                    $"{t}/PlasticSteelHandle_AlbedoTransparency.png", $"{t}/PlasticSteelHandle_Normal.png", $"{t}/PlasticSteelHandle_MetallicSmoothness.png", null,
                    new HorrorTextureSpec(0.22f, 0.70f, 0.85f, 0.0f, 0.92f, 0.75f, 112), applianceTint, 0.08f, 0.35f),
            };
            return CreateStyledMaterials("MainHouse", rules, urpLit);
        }

        private static Dictionary<string, Material> BuildShopMaterials(Shader urpLit)
        {
            const string t = "Assets/Packs/abandoned-building-shop-old-house/textures";
            var darkShopTint = new Color(0.68f, 0.65f, 0.62f, 1f);

            var rules = new Dictionary<string, MaterialStyleRule>(StringComparer.OrdinalIgnoreCase)
            {
                ["wall_painted_diffuse"] = new MaterialStyleRule(
                    $"{t}/wall_painted_diffuse.png", $"{t}/wall_normal.png", null, $"{t}/wall_ao.png",
                    new HorrorTextureSpec(0.28f, 0.45f, 0.78f, 0.65f, 0.12f, 0.90f, 201), darkShopTint, 0.05f, 0.0f),
                ["woods_painted_diffuse"] = new MaterialStyleRule(
                    $"{t}/woods_painted_diffuse.png", $"{t}/woods_normal.png", $"{t}/woods_metalness.png", null,
                    new HorrorTextureSpec(0.25f, 0.48f, 0.75f, 0.95f, 0.0f, 0.88f, 202), darkShopTint, 0.04f, 0.0f),
                ["woods_roughness"] = new MaterialStyleRule(
                    $"{t}/woods_painted_diffuse.png", $"{t}/woods_normal.png", $"{t}/woods_metalness.png", null,
                    new HorrorTextureSpec(0.25f, 0.48f, 0.75f, 0.95f, 0.0f, 0.88f, 202), darkShopTint, 0.04f, 0.0f),
                ["irons_painted_diffuse"] = new MaterialStyleRule(
                    $"{t}/irons_painted_diffuse.png", $"{t}/irons_normal.png", $"{t}/irons_metalness.png", $"{t}/irons_ao.png",
                    new HorrorTextureSpec(0.28f, 0.35f, 0.60f, 0.0f, 0.96f, 0.78f, 203), darkShopTint, 0.14f, 0.35f),
                ["roof_painted_diffuse"] = new MaterialStyleRule(
                    $"{t}/roof_painted_diffuse.png", $"{t}/roof_normal.png", $"{t}/roof_metalness.png", $"{t}/roof_ao.png",
                    new HorrorTextureSpec(0.26f, 0.38f, 0.65f, 0.0f, 0.95f, 0.55f, 204), darkShopTint, 0.12f, 0.35f),
                ["roof_metalness"] = new MaterialStyleRule(
                    $"{t}/roof_painted_diffuse.png", $"{t}/roof_normal.png", $"{t}/roof_metalness.png", $"{t}/roof_ao.png",
                    new HorrorTextureSpec(0.26f, 0.38f, 0.65f, 0.0f, 0.95f, 0.55f, 204), darkShopTint, 0.12f, 0.35f),
                ["roof_roughness"] = new MaterialStyleRule(
                    $"{t}/roof_painted_diffuse.png", $"{t}/roof_normal.png", $"{t}/roof_metalness.png", $"{t}/roof_ao.png",
                    new HorrorTextureSpec(0.26f, 0.38f, 0.65f, 0.0f, 0.95f, 0.55f, 204), darkShopTint, 0.12f, 0.35f),
                ["environments_painted_diffuse"] = new MaterialStyleRule(
                    $"{t}/environments_painted_diffuse.png", $"{t}/environments_normal.png", $"{t}/environments_metalness.png", $"{t}/environments_ao.png",
                    new HorrorTextureSpec(0.28f, 0.58f, 0.72f, 0.65f, 0.85f, 0.82f, 205), darkShopTint, 0.08f, 0.20f),
                ["tiles_diffuse"] = new MaterialStyleRule(
                    $"{t}/tiles_diffuse.png", $"{t}/tiles_normal.png", null, null,
                    new HorrorTextureSpec(0.32f, 0.42f, 0.75f, 0.0f, 0.0f, 0.95f, 206), darkShopTint, 0.08f, 0.0f),
                ["ground_diffuse"] = new MaterialStyleRule(
                    $"{t}/ground_diffuse.png", null, null, null,
                    new HorrorTextureSpec(0.35f, 0.40f, 0.72f, 0.0f, 0.0f, 0.65f, 207), darkShopTint, 0.04f, 0.0f),
            };
            return CreateStyledMaterials("Shop", rules, urpLit);
        }

        private static Dictionary<string, Material> CreateStyledMaterials(string prefix, Dictionary<string, MaterialStyleRule> rules, Shader urpLit)
        {
            Directory.CreateDirectory(GenTexDir);
            Directory.CreateDirectory(GenMatDir);
            var result = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);

            foreach (var kv in rules)
            {
                MaterialStyleRule rule = kv.Value;
                string texPath = $"{GenTexDir}/{prefix}_{kv.Key}_Horror.png";
                Texture2D albedoTex = HorrorTextureGenerator.GenerateStyledTexture(rule.SourceAlbedoPath, texPath, rule.TextureSpec);
                if (albedoTex == null) continue;

                string matPath = $"{GenMatDir}/{prefix}_{kv.Key}_Horror.mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath) ?? new Material(urpLit) { name = $"{prefix}_{kv.Key}_Horror" };
                if (!AssetDatabase.Contains(mat)) AssetDatabase.CreateAsset(mat, matPath);

                mat.shader = urpLit;
                mat.SetTexture("_BaseMap", albedoTex);
                mat.SetColor("_BaseColor", rule.BaseTint);
                mat.SetFloat("_Smoothness", rule.SmoothnessScale);
                mat.SetFloat("_Metallic", rule.MetallicScale);
                mat.enableInstancing = true;

                AssignOptionalMap(mat, "_BumpMap", rule.NormalPath, "_NORMALMAP", "_BumpScale", 1.25f);
                AssignOptionalMap(mat, "_MetallicGlossMap", rule.MetallicPath, "_METALLICSPECGLOSSMAP", null, 0f);
                AssignOptionalMap(mat, "_OcclusionMap", rule.OcclusionPath, null, "_OcclusionStrength", 0.9f);

                EditorUtility.SetDirty(mat);
                result[kv.Key] = mat;
            }
            return result;
        }

        private static void AssignOptionalMap(Material mat, string texProp, string assetPath, string keyword, string floatProp, float floatVal)
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null) return;
            mat.SetTexture(texProp, tex);
            if (!string.IsNullOrEmpty(keyword)) mat.EnableKeyword(keyword);
            if (!string.IsNullOrEmpty(floatProp)) mat.SetFloat(floatProp, floatVal);
        }

        private static Material[] BuildBloodDecalMaterials(Shader urpLit)
        {
            Directory.CreateDirectory(GenTexDir);
            Directory.CreateDirectory(GenMatDir);

            var specs = new[]
            {
                (kind: BloodDecalKind.FloorPool, name: "BloodPoolSplatter", seed: 777),
                (kind: BloodDecalKind.WallDrips, name: "BloodWallDrips", seed: 888),
                (kind: BloodDecalKind.DragStreak, name: "BloodDragStreak", seed: 999)
            };

            var result = new Material[specs.Length];
            for (int i = 0; i < specs.Length; i++)
            {
                Texture2D bloodTex = HorrorTextureGenerator.GenerateBloodDecalTexture($"{GenTexDir}/{specs[i].name}.png", specs[i].kind, 512, specs[i].seed);
                string matPath = $"{GenMatDir}/{specs[i].name}_Horror.mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath) ?? new Material(urpLit) { name = $"{specs[i].name}_Horror" };
                if (!AssetDatabase.Contains(mat)) AssetDatabase.CreateAsset(mat, matPath);

                mat.shader = urpLit;
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_ZWrite", 0f);
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.renderQueue = (int)RenderQueue.Transparent;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetTexture("_BaseMap", bloodTex);
                mat.SetColor("_BaseColor", new Color(0.42f, 0.36f, 0.36f, 0.92f));
                mat.SetFloat("_Smoothness", 0.18f);
                mat.SetFloat("_Metallic", 0.02f);
                mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);
                result[i] = mat;
            }
            return result;
        }

        private static bool IsExcludedPropOrNpc(Transform t)
        {
            Transform cur = t;
            while (cur != null)
            {
                string n = cur.name;
                if (n.Contains("Saya") || n.Contains("Demon") || n.Contains("Shop_QuestGoods") ||
                    n.Contains("Mirror") || n.StartsWith("[HorrorDecals_", StringComparison.Ordinal))
                {
                    return true;
                }
                cur = cur.parent;
            }
            return false;
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
    }
}
#endif
