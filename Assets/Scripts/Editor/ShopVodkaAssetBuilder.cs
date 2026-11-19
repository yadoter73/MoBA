#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MoBA.Editor
{
    /// <summary>
    /// Генерирует и обновляет префабы квестовых товаров: Shop_Vodka и Shop_BreadQuest (URP Lit).
    /// </summary>
    public static class ShopVodkaAssetBuilder
    {
        public const string ShopPrefabsFolder = "Assets/Prefabs/Shop";
        public const string UrpFoodPrefabsFolder = "Assets/CrowAssets/Assets/Low Poly Stylized Food/URP/Prefabs";
        public const float ShopWorldScale = 4.2f;

        public static GameObject RebuildVodkaPrefab()
        {
            EnsureFolders();
            string path = $"{ShopPrefabsFolder}/Shop_Vodka.prefab";
            GameObject baseBottle = AssetDatabase.LoadAssetAtPath<GameObject>($"{UrpFoodPrefabsFolder}/Soda Bottle.prefab");
            GameObject root = baseBottle != null ? (GameObject)PrefabUtility.InstantiatePrefab(baseBottle) : new GameObject("Shop_Vodka");

            try
            {
                if (PrefabUtility.IsPartOfPrefabInstance(root))
                {
                    PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                }

                root.name = "Shop_Vodka";
                StripPhysicsAndInteractables(root);
                ScaleByHeightMeters(root, 0.30f);

                Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit");
                Material glassMat = EnsureGlassMaterial("Mat_VodkaGlass", urpLit, new Color(0.14f, 0.36f, 0.22f, 0.44f));
                Material labelMat = EnsureSimpleMaterial("Mat_VodkaLabel", urpLit, new Color(0.93f, 0.91f, 0.86f, 1f), 0.12f);
                Material bandMat = EnsureSimpleMaterial("Mat_VodkaBand", urpLit, new Color(0.10f, 0.10f, 0.12f, 1f), 0.25f);
                Material capMat = EnsureSimpleMaterial("Mat_VodkaCap", urpLit, new Color(0.14f, 0.14f, 0.16f, 1f), 0.55f);

                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = glassMat;
                    r.sharedMaterials = mats;
                }

                if (TryGetBounds(root, out Bounds b))
                {
                    float bodyRadius = Mathf.Max(0.05f, Mathf.Min(b.size.x, b.size.z) * 0.51f);
                    float labelY = b.min.y + b.size.y * 0.37f;
                    float labelHalfH = b.size.y * 0.14f;

                    AddCylinderPart(root.transform, "Vodka_Label",
                        new Vector3(b.center.x, labelY, b.center.z),
                        new Vector3(bodyRadius * 2.02f, labelHalfH, bodyRadius * 2.02f), labelMat);

                    AddCylinderPart(root.transform, "Vodka_Stripe",
                        new Vector3(b.center.x, labelY + b.size.y * 0.03f, b.center.z),
                        new Vector3(bodyRadius * 2.06f, b.size.y * 0.025f, bodyRadius * 2.06f), bandMat);

                    AddCylinderPart(root.transform, "Vodka_Cap",
                        new Vector3(b.center.x, b.max.y - b.size.y * 0.025f, b.center.z),
                        new Vector3(bodyRadius * 0.72f, b.size.y * 0.032f, bodyRadius * 0.72f), capMat);
                }

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                AssetDatabase.SaveAssets();
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public static GameObject RebuildQuestBreadPrefab()
        {
            EnsureFolders();
            string path = $"{ShopPrefabsFolder}/Shop_BreadQuest.prefab";
            GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>($"{UrpFoodPrefabsFolder}/BreadLoaf.prefab");
            if (src == null) return null;

            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(src);
            try
            {
                if (PrefabUtility.IsPartOfPrefabInstance(root))
                {
                    PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                }
                root.name = "Shop_BreadQuest";
                StripPhysicsAndInteractables(root);
                ScaleByHeightMeters(root, 0.12f);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                AssetDatabase.SaveAssets();
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public static void ScaleByHeightMeters(GameObject go, float targetHeightMeters, float maxHorizMeters = 0.28f)
        {
            Physics.SyncTransforms();
            if (!TryGetBounds(go, out Bounds b) || b.size.y < 0.001f) return;

            float targetH = targetHeightMeters * ShopWorldScale;
            float factor = targetH / b.size.y;

            float horiz = Mathf.Max(b.size.x, b.size.z);
            float maxHorizWorld = maxHorizMeters * ShopWorldScale;
            if (horiz * factor > maxHorizWorld && horiz > 0.001f)
            {
                factor = maxHorizWorld / horiz;
            }

            go.transform.localScale *= factor;
            Physics.SyncTransforms();
        }

        public static bool TryGetBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
            }
            return any;
        }

        public static void StripPhysicsAndInteractables(GameObject go)
        {
            foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(rb);
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
        }

        private static void AddCylinderPart(Transform parent, string name, Vector3 worldPos, Vector3 scale, Material mat)
        {
            GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cyl.name = name;
            UnityEngine.Object.DestroyImmediate(cyl.GetComponent<Collider>());
            cyl.transform.position = worldPos;
            cyl.transform.localScale = scale;
            cyl.transform.SetParent(parent, true);
            cyl.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static Material EnsureGlassMaterial(string matName, Shader shader, Color color)
        {
            Material mat = EnsureSimpleMaterial(matName, shader, color, 0.94f);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material EnsureSimpleMaterial(string matName, Shader shader, Color color, float smoothness)
        {
            string path = $"{ShopPrefabsFolder}/Materials/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            else if (shader != null && mat.shader != shader) mat.shader = shader;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(ShopPrefabsFolder)) AssetDatabase.CreateFolder("Assets/Prefabs", "Shop");
            if (!AssetDatabase.IsValidFolder(ShopPrefabsFolder + "/Materials")) AssetDatabase.CreateFolder(ShopPrefabsFolder, "Materials");
        }
    }
}
#endif
