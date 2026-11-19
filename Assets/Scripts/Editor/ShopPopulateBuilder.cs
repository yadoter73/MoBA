#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using MoBA.Interaction;
using MoBA.Narrative;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoBA.Editor
{
    /// <summary>
    /// Наполняет прилавок (Woods.008) и полки слева (Woods.010) магазина URP-товарами
    /// с реальными масштабами через Renderer.bounds, группами 2–4 шт и квестовым набором.
    /// </summary>
    public static class ShopPopulateBuilder
    {
        private const string PropsRootName = "[Shop_Props]";
        private const string QuestGoodsRootName = "[Shop_QuestGoods]";
        private const string ShopPrefabsFolder = ShopVodkaAssetBuilder.ShopPrefabsFolder;
        private const string CrowUrpFolder = ShopVodkaAssetBuilder.UrpFoodPrefabsFolder;

        private readonly struct FoodSpec
        {
            public readonly string AssetPath;
            public readonly float TargetHeightMeters;
            public readonly float MaxHorizMeters;
            public readonly bool CanTip;

            public FoodSpec(string path, float heightM, float maxHorizM = 0.26f, bool canTip = true)
            {
                AssetPath = path;
                TargetHeightMeters = heightM;
                MaxHorizMeters = maxHorizM;
                CanTip = canTip;
            }
        }

        public static void PopulateShopAndQuestGoods(Scene scene, GameObject shop)
        {
            FoodUrpMaterialFixer.FixFoodUrpMaterials();
            GameObject vodkaPrefab = ShopVodkaAssetBuilder.RebuildVodkaPrefab();
            GameObject breadPrefab = ShopVodkaAssetBuilder.RebuildQuestBreadPrefab();
            GameObject applePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ShopPrefabsFolder}/Shop_Apple.prefab");

            ClearChild(shop.transform, PropsRootName);
            ClearChild(shop.transform, QuestGoodsRootName);

            GameObject propsRoot = CreateChild(shop.transform, PropsRootName, "Create Shop Props");
            GameObject questRoot = CreateChild(shop.transform, QuestGoodsRootName, "Create Shop Quest Goods");

            var counterTris = ShopScreenshotUtility.ExtractUpwardTriangles(shop, "Woods.008", 11.1f, 11.8f);
            var shelfTris = ShopScreenshotUtility.ExtractUpwardTriangles(shop, "Woods.010", 11.0f, 16.5f);

            var placedBounds = new List<Bounds>();
            var questPickups = PlaceFiveQuestGoods(questRoot.transform, counterTris, placedBounds, vodkaPrefab, applePrefab, breadPrefab);
            WirePresenter(shop, questPickups);

            var rng = new System.Random(133742);
            int decorCount = 0;
            decorCount += PopulateCounterZones(propsRoot.transform, counterTris, placedBounds, rng);
            decorCount += PopulateShelfLevels(propsRoot.transform, shelfTris, placedBounds, rng);
            decorCount += PopulateFloorCrates(propsRoot.transform, placedBounds, rng);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            ShopScreenshotUtility.CaptureCounterScreenshot(scene);
            AssetDatabase.Refresh();
            Debug.Log($"[PopulateShop] Магазин заполнен по категориям: декор {decorCount}, квестовых товаров на прилавке {questPickups.Count}.");
        }

        private static List<GroceryPickup> PlaceFiveQuestGoods(
            Transform questParent,
            List<ShopScreenshotUtility.SurfaceTriangle> counterTris,
            List<Bounds> placedBounds,
            GameObject vodkaPrefab,
            GameObject applePrefab,
            GameObject breadPrefab)
        {
            var result = new List<GroceryPickup>();
            int interactLayer = LayerMask.NameToLayer("Interact");
            int layer = interactLayer >= 0 ? interactLayer : 0;

            (GameObject prefab, GroceryItemType type, string name, float x, float z, float yaw, float targetH)[] specs =
            {
                (vodkaPrefab, GroceryItemType.Vodka, "Quest_Vodka", 193.35f, 143.45f, 18f, 0.30f),
                (applePrefab, GroceryItemType.Apple, "Quest_Apple_1", 194.45f, 143.95f, 35f, 0.085f),
                (applePrefab, GroceryItemType.Apple, "Quest_Apple_2", 195.05f, 143.25f, 145f, 0.085f),
                (applePrefab, GroceryItemType.Apple, "Quest_Apple_3", 195.60f, 144.00f, 250f, 0.085f),
                (breadPrefab, GroceryItemType.Bread, "Quest_BreadLoaf", 196.75f, 143.55f, -22f, 0.12f)
            };

            for (int i = 0; i < specs.Length; i++)
            {
                var s = specs[i];
                if (s.prefab == null) continue;

                float surfaceY = SampleY(counterTris, s.x, s.z, 11.45f);
                GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(s.prefab, questParent);
                Undo.RegisterCreatedObjectUndo(go, "Place Quest Good");
                go.name = s.name;
                go.transform.rotation = Quaternion.Euler(0f, s.yaw, 0f);
                ShopVodkaAssetBuilder.ScaleByHeightMeters(go, s.targetH, 0.28f);
                go.transform.position = new Vector3(s.x, surfaceY, s.z);
                GroundByBottomBounds(go, surfaceY);

                SetLayerAndStripStatic(go, layer);
                BoxCollider box = GetOrAdd<BoxCollider>(go);
                foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
                {
                    if (c != box) c.enabled = false;
                }
                box.enabled = true;
                box.isTrigger = false;

                if (ShopVodkaAssetBuilder.TryGetBounds(go, out Bounds b))
                {
                    Vector3 localCenter = go.transform.InverseTransformPoint(b.center);
                    Vector3 lossy = go.transform.lossyScale;
                    box.center = localCenter;
                    box.size = new Vector3(
                        Mathf.Max(b.size.x, 0.36f) / Mathf.Max(0.01f, Mathf.Abs(lossy.x)),
                        Mathf.Max(b.size.y, 0.36f) / Mathf.Max(0.01f, Mathf.Abs(lossy.y)),
                        Mathf.Max(b.size.z, 0.36f) / Mathf.Max(0.01f, Mathf.Abs(lossy.z)));

                    Bounds reserve = b;
                    reserve.Expand(0.16f);
                    placedBounds.Add(reserve);
                }

                GroceryPickup pickup = GetOrAdd<GroceryPickup>(go);
                var so = new SerializedObject(pickup);
                so.FindProperty("_itemType").enumValueIndex = (int)s.type;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(go);
                result.Add(pickup);
            }

            return result;
        }

        private static int PopulateCounterZones(
            Transform parent,
            List<ShopScreenshotUtility.SurfaceTriangle> counterTris,
            List<Bounds> placedBounds,
            System.Random rng)
        {
            FoodSpec[] counterMix = BuildCounterSpecs();
            int count = 0;

            // Передний прилавок: левый крыло (185.0..191.6) и правый конец перед калиткой (198.2..204.8)
            count += PopulateAnchorZone(parent, counterTris, 11.45f, 185.1f, 191.6f, 142.3f, 144.6f, 1.35f, 6, 4, counterMix, placedBounds, rng, allowTip: true);
            count += PopulateAnchorZone(parent, counterTris, 11.45f, 198.2f, 204.8f, 142.3f, 144.6f, 1.35f, 6, 4, counterMix, placedBounds, rng, allowTip: true);
            // Боковое крыло Г-прилавка (-X сторона, не блокируя проход монстра)
            count += PopulateAnchorZone(parent, counterTris, 11.45f, 185.0f, 187.5f, 129.2f, 141.4f, 1.45f, 8, 4, counterMix, placedBounds, rng, allowTip: true);
            return count;
        }

        private static int PopulateShelfLevels(
            Transform parent,
            List<ShopScreenshotUtility.SurfaceTriangle> shelfTris,
            List<Bounds> placedBounds,
            System.Random rng)
        {
            FoodSpec[] bottomShelf = BuildProduceAndBakerySpecs();
            FoodSpec[] midShelf = BuildCansAndCondimentsSpecs();
            FoodSpec[] topShelf = BuildDrinksSpecs();

            int count = 0;
            count += PopulateAnchorZone(parent, shelfTris, 11.55f, 211.45f, 212.55f, 131.6f, 143.2f, 1.40f, 8, 4, bottomShelf, placedBounds, rng, allowTip: true);
            count += PopulateAnchorZone(parent, shelfTris, 13.90f, 211.45f, 212.55f, 131.6f, 143.2f, 1.35f, 9, 4, midShelf, placedBounds, rng, allowTip: true);
            count += PopulateAnchorZone(parent, shelfTris, 16.25f, 211.45f, 212.55f, 131.6f, 143.2f, 1.35f, 8, 4, topShelf, placedBounds, rng, allowTip: false);
            return count;
        }

        private static int PopulateFloorCrates(Transform parent, List<Bounds> placedBounds, System.Random rng)
        {
            FoodSpec[] crates =
            {
                new FoodSpec($"{ShopPrefabsFolder}/Shop_CrateFilled.prefab", 0.36f, 0.55f, canTip: false),
                new FoodSpec($"{ShopPrefabsFolder}/Shop_CrateEmpty.prefab", 0.34f, 0.55f, canTip: false)
            };

            (float x, float z, float yaw)[] spots =
            {
                (209.65f, 134.2f, 14f),
                (209.55f, 137.4f, -11f)
            };

            int count = 0;
            for (int i = 0; i < spots.Length; i++)
            {
                FoodSpec spec = crates[i % crates.Length];
                if (TrySpawnProp(parent, spec, new Vector3(spots[i].x, 6.46f, spots[i].z), spots[i].yaw, tip: false, placedBounds))
                {
                    count++;
                }
            }
            return count;
        }

        private static int PopulateAnchorZone(
            Transform parent,
            List<ShopScreenshotUtility.SurfaceTriangle> surfaceTris,
            float targetY,
            float minX,
            float maxX,
            float minZ,
            float maxZ,
            float clusterSpacing,
            int maxClusters,
            int maxPerCluster,
            FoodSpec[] pool,
            List<Bounds> placedBounds,
            System.Random rng,
            bool allowTip)
        {
            int count = 0;
            int clusters = 0;
            bool alongX = (maxX - minX) >= (maxZ - minZ);
            float primaryMin = alongX ? minX : minZ;
            float primaryMax = alongX ? maxX : maxZ;

            for (float p = primaryMin + 0.35f; p <= primaryMax - 0.35f && clusters < maxClusters; p += clusterSpacing)
            {
                clusters++;
                float cx = alongX ? p + (float)(rng.NextDouble() - 0.5) * 0.25f : Mathf.Lerp(minX, maxX, (float)rng.NextDouble() * 0.6f + 0.2f);
                float cz = alongX ? Mathf.Lerp(minZ, maxZ, (float)rng.NextDouble() * 0.6f + 0.2f) : p + (float)(rng.NextDouble() - 0.5) * 0.25f;

                int groupSize = rng.Next(2, maxPerCluster + 1);
                for (int k = 0; k < groupSize; k++)
                {
                    FoodSpec spec = pool[rng.Next(pool.Length)];
                    float ox = cx + (float)(rng.NextDouble() - 0.5) * 0.85f;
                    float oz = cz + (float)(rng.NextDouble() - 0.5) * 0.70f;
                    ox = Mathf.Clamp(ox, minX, maxX);
                    oz = Mathf.Clamp(oz, minZ, maxZ);

                    float surfaceY = SampleY(surfaceTris, ox, oz, targetY);
                    float yaw = (float)(rng.NextDouble() * 360.0);
                    bool tip = allowTip && spec.CanTip && rng.NextDouble() < 0.14;
                    if (TrySpawnProp(parent, spec, new Vector3(ox, surfaceY, oz), yaw, tip, placedBounds))
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        private static bool TrySpawnProp(
            Transform parent,
            FoodSpec spec,
            Vector3 pos,
            float yaw,
            bool tip,
            List<Bounds> placedBounds)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.AssetPath);
            if (prefab == null) return false;

            GameObject item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (item == null) return false;

            float pitch = tip ? 84f : 0f;
            item.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            ShopVodkaAssetBuilder.ScaleByHeightMeters(item, spec.TargetHeightMeters, spec.MaxHorizMeters);
            item.transform.position = pos;
            GroundByBottomBounds(item, pos.y);

            if (ShopVodkaAssetBuilder.TryGetBounds(item, out Bounds b))
            {
                if (OverlapsAny3D(b, placedBounds, 0.10f))
                {
                    UnityEngine.Object.DestroyImmediate(item);
                    return false;
                }
                placedBounds.Add(b);
            }

            Undo.RegisterCreatedObjectUndo(item, "Spawn Shop Prop");
            MarkStaticNonInteractable(item);
            return true;
        }

        private static FoodSpec[] BuildCounterSpecs() => new[]
        {
            new FoodSpec($"{CrowUrpFolder}/Milk.prefab", 0.25f),
            new FoodSpec($"{CrowUrpFolder}/Juice Box.prefab", 0.20f),
            new FoodSpec($"{CrowUrpFolder}/Mustard.prefab", 0.20f),
            new FoodSpec($"{CrowUrpFolder}/Ketchup.prefab", 0.20f),
            new FoodSpec($"{CrowUrpFolder}/SodaCan_01.prefab", 0.12f),
            new FoodSpec($"{CrowUrpFolder}/SodaCan_02.prefab", 0.12f),
            new FoodSpec($"{CrowUrpFolder}/SodaCan_03.prefab", 0.12f),
            new FoodSpec($"{CrowUrpFolder}/Can_01.prefab", 0.12f),
            new FoodSpec($"{CrowUrpFolder}/Sardines.prefab", 0.03f, 0.14f),
            new FoodSpec($"{CrowUrpFolder}/BreadLoaf.prefab", 0.12f, 0.22f),
            new FoodSpec($"{CrowUrpFolder}/Bread.prefab", 0.025f, 0.13f, false),
            new FoodSpec($"{CrowUrpFolder}/Chips.prefab", 0.08f, 0.22f, false),
            new FoodSpec($"{CrowUrpFolder}/CandyBar.prefab", 0.02f, 0.14f, false),
            new FoodSpec($"{CrowUrpFolder}/Cheese Wedge.prefab", 0.055f, 0.14f, false),
            new FoodSpec($"{CrowUrpFolder}/Sandwich.prefab", 0.08f, 0.16f, false),
            new FoodSpec($"{CrowUrpFolder}/Egg.prefab", 0.065f, 0.09f),
            new FoodSpec($"{CrowUrpFolder}/Jar.prefab", 0.22f),
            new FoodSpec($"{CrowUrpFolder}/Jelly.prefab", 0.16f),
            new FoodSpec($"{CrowUrpFolder}/Peanut Butter.prefab", 0.16f),
            new FoodSpec($"{CrowUrpFolder}/Soda Bottle.prefab", 0.30f),
            new FoodSpec($"{CrowUrpFolder}/Lettuce.prefab", 0.025f, 0.14f, false),
            new FoodSpec($"{CrowUrpFolder}/Tomato Slice.prefab", 0.02f, 0.09f, false),
            new FoodSpec($"{ShopPrefabsFolder}/Shop_AppleRotten.prefab", 0.085f)
        };

        private static FoodSpec[] BuildDrinksSpecs() => new[]
        {
            new FoodSpec($"{CrowUrpFolder}/Soda Bottle.prefab", 0.30f),
            new FoodSpec($"{CrowUrpFolder}/Milk.prefab", 0.25f),
            new FoodSpec($"{CrowUrpFolder}/Juice Box.prefab", 0.20f),
            new FoodSpec($"{CrowUrpFolder}/SodaCan_01.prefab", 0.12f),
            new FoodSpec($"{CrowUrpFolder}/SodaCan_02.prefab", 0.12f),
            new FoodSpec($"{CrowUrpFolder}/SodaCan_03.prefab", 0.12f)
        };

        private static FoodSpec[] BuildCansAndCondimentsSpecs() => new[]
        {
            new FoodSpec($"{CrowUrpFolder}/Can_01.prefab", 0.12f),
            new FoodSpec($"{CrowUrpFolder}/Can_01_Open.prefab", 0.14f),
            new FoodSpec($"{CrowUrpFolder}/Jar.prefab", 0.22f),
            new FoodSpec($"{CrowUrpFolder}/Jelly.prefab", 0.16f),
            new FoodSpec($"{CrowUrpFolder}/Peanut Butter.prefab", 0.16f),
            new FoodSpec($"{CrowUrpFolder}/Ketchup.prefab", 0.20f),
            new FoodSpec($"{CrowUrpFolder}/Mustard.prefab", 0.20f),
            new FoodSpec($"{CrowUrpFolder}/Sardines.prefab", 0.03f, 0.14f),
            new FoodSpec($"{CrowUrpFolder}/Can Opener.prefab", 0.03f, 0.18f, false)
        };

        private static FoodSpec[] BuildProduceAndBakerySpecs() => new[]
        {
            new FoodSpec($"{CrowUrpFolder}/BreadLoaf.prefab", 0.12f, 0.22f),
            new FoodSpec($"{CrowUrpFolder}/Chips.prefab", 0.08f, 0.22f, false),
            new FoodSpec($"{CrowUrpFolder}/Sandwich.prefab", 0.08f, 0.16f, false),
            new FoodSpec($"{CrowUrpFolder}/Cheese Wedge.prefab", 0.055f, 0.14f, false),
            new FoodSpec($"{CrowUrpFolder}/Egg.prefab", 0.065f, 0.09f),
            new FoodSpec($"{CrowUrpFolder}/CrackedEgg.prefab", 0.08f, 0.09f, false),
            new FoodSpec($"{CrowUrpFolder}/Lettuce.prefab", 0.025f, 0.14f, false),
            new FoodSpec($"{ShopPrefabsFolder}/Shop_Apple.prefab", 0.085f),
            new FoodSpec($"{ShopPrefabsFolder}/Shop_Orange.prefab", 0.085f),
            new FoodSpec($"{ShopPrefabsFolder}/Shop_Carrot.prefab", 0.06f, 0.20f, false),
            new FoodSpec($"{ShopPrefabsFolder}/Shop_Tomato.prefab", 0.08f)
        };

        private static float SampleY(List<ShopScreenshotUtility.SurfaceTriangle> tris, float x, float z, float fallbackY)
        {
            return ShopScreenshotUtility.TrySampleSurfaceY(tris, x, z, fallbackY, 0.45f, out float y) ? y : fallbackY;
        }

        private static void GroundByBottomBounds(GameObject go, float targetMinY)
        {
            Physics.SyncTransforms();
            if (ShopVodkaAssetBuilder.TryGetBounds(go, out Bounds b))
            {
                go.transform.position += new Vector3(0f, targetMinY - b.min.y, 0f);
                Physics.SyncTransforms();
            }
        }

        private static bool OverlapsAny3D(Bounds b, List<Bounds> list, float pad)
        {
            Bounds expanded = b;
            expanded.Expand(pad);
            for (int i = 0; i < list.Count; i++) if (expanded.Intersects(list[i])) return true;
            return false;
        }

        private static void WirePresenter(GameObject shop, List<GroceryPickup> goods)
        {
            ShopCounterGoodsPresenter presenter = GetOrAdd<ShopCounterGoodsPresenter>(shop);
            var so = new SerializedObject(presenter);
            SerializedProperty prop = so.FindProperty("_counterGoods");
            if (prop != null)
            {
                prop.arraySize = goods.Count;
                for (int i = 0; i < goods.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = goods[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(shop);
            }
        }

        private static void MarkStaticNonInteractable(GameObject go)
        {
            StaticEditorFlags flags = StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
                t.gameObject.layer = 0;
            }
        }

        private static void SetLayerAndStripStatic(GameObject go, int layer)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                t.gameObject.layer = layer;
            }
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T existing = go.GetComponent<T>();
            return existing != null ? existing : Undo.AddComponent<T>(go);
        }

        private static void ClearChild(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
        }

        private static GameObject CreateChild(Transform parent, string name, string undo)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, undo);
            go.transform.SetParent(parent, false);
            return go;
        }
    }
}
#endif
