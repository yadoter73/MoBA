#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using DialogueEditor;
using KinematicCharacterController.Examples;
using MoBA.Interaction;
using MoBA.Narrative;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MoBA.Editor
{
    /// <summary>
    /// Утилиты валидации дверей/шкафов, разметки NPC (Talk) и наполнения магазина в сером доме (Блоки A, B, C).
    /// </summary>
    public static class QuestAndShopSetup
    {
        private const string ShopPrefabsFolder = "Assets/Prefabs/Shop";
        private const string CrowFoodPrefabsFolder = "Assets/CrowAssets/Assets/Low Poly Stylized Food/URP/Prefabs";
        private const string ShopPropsRootName = "[Shop_Props]";
        private static readonly Vector3 MonsterApproxPos = new Vector3(200f, 6f, 134f);

        private enum ShelfCategory
        {
            DryFoodOrDrink = 0,
            Produce = 1,
            Crate = 2
        }

        [MenuItem("MoBA/Validate Doors")]
        public static void ValidateDoors()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[ValidateDoors] Нет активной загруженной сцены.");
                return;
            }

            int interactMask = ResolveInteractionLayerMask(scene);
            int targetLayer = ResolveFirstLayerFromMask(interactMask);
            Door gateDoor = FindGreyHouseGateDoor(scene);

            var report = new StringBuilder();
            report.AppendLine("=== MoBA Validate Doors & Interactables ===");
            report.AppendLine("Имя | Путь | Слой | Коллайдер | requiresKey | doorId | _angle | Результат");

            int fixedCount = 0;

            foreach (Door door in FindAllInScene<Door>(scene))
            {
                bool isGate = door == gateDoor;
                fixedCount += ValidateAndFixSingleDoor(door, isGate, interactMask, targetLayer, report);
            }

            foreach (CupBoard cup in FindAllInScene<CupBoard>(scene))
            {
                fixedCount += ValidateAndFixInteractable(cup, interactMask, targetLayer, "CupBoard", report);
            }

            foreach (KeyPickup key in FindAllInScene<KeyPickup>(scene))
            {
                fixedCount += ValidateAndFixInteractable(key, interactMask, targetLayer, "KeyPickup", report);
            }

            int talkTagged = TagExistingDialogueNpcs(scene, targetLayer);
            EditorSceneManager.MarkSceneDirty(scene);
            report.AppendLine($"Итого автоисправлений дверей/шкафов: {fixedCount}, NPC помечено Talk: {talkTagged}.");
            Debug.Log(report.ToString());
        }

        private static int ValidateAndFixSingleDoor(Door door, bool isGate, int interactMask, int targetLayer, StringBuilder report)
        {
            int fixes = 0;
            var issues = new List<string>();
            GameObject go = door.gameObject;

            if (((1 << go.layer) & interactMask) == 0)
            {
                issues.Add($"слой {LayerMask.LayerToName(go.layer)}->{LayerMask.LayerToName(targetLayer)}");
                Undo.RecordObject(go, "Fix Door Layer");
                go.layer = targetLayer;
                fixes++;
            }

            Collider col = go.GetComponent<Collider>();
            if (col == null)
            {
                issues.Add("нет коллайдера->добавлен BoxCollider");
                col = Undo.AddComponent<BoxCollider>(go);
                fixes++;
            }
            else if (!col.enabled)
            {
                issues.Add("коллайдер выключен->включён");
                Undo.RecordObject(col, "Enable Door Collider");
                col.enabled = true;
                fixes++;
            }

            Undo.RecordObject(door, "Validate Door Fields");
            var so = new SerializedObject(door);
            SerializedProperty reqKeyProp = so.FindProperty("_requiresKey");
            SerializedProperty doorIdProp = so.FindProperty("_doorId");
            SerializedProperty angleProp = so.FindProperty("_angle");
            SerializedProperty lockedProp = so.FindProperty("_lockedAngle");

            bool reqKey = reqKeyProp != null && reqKeyProp.boolValue;
            int doorId = doorIdProp != null ? doorIdProp.intValue : 0;
            float angle = angleProp != null ? angleProp.floatValue : 0f;

            if (reqKeyProp != null && reqKey != isGate)
            {
                issues.Add($"requiresKey:{reqKey}->{isGate}");
                reqKeyProp.boolValue = isGate;
                reqKey = isGate;
                fixes++;
            }

            if (isGate && doorIdProp != null && doorId == 1)
            {
                issues.Add("gate doorId:1->0 (управляется requiresKey)");
                doorIdProp.intValue = 0;
                doorId = 0;
                fixes++;
            }
            else if (!isGate && doorId == 1)
            {
                issues.Add("намеренно заперта (doorId==1)");
            }

            float closedYaw = door.transform.rotation.eulerAngles.y;
            float swingDelta = Mathf.Abs(Mathf.DeltaAngle(closedYaw, angle));
            bool intentionallyLocked = !isGate && doorId == 1;
            bool wallBlocked = !intentionallyLocked && WouldDoorClipWall(door, col, angle);

            if (!intentionallyLocked && (swingDelta < 20f || wallBlocked))
            {
                float betterAngle = ChooseBestOpenAngle(door, col, closedYaw);
                issues.Add($"_angle:{angle:F0}°->{betterAngle:F0}°{(wallBlocked ? " (утыкалась в стену)" : " (малый ход)")}");
                if (angleProp != null) angleProp.floatValue = betterAngle;
                angle = betterAngle;
                fixes++;
            }

            if (isGate && lockedProp != null && Mathf.Abs(Mathf.DeltaAngle(closedYaw, lockedProp.floatValue)) < 2f)
            {
                lockedProp.floatValue = closedYaw + 13f;
                fixes++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(door);
            EditorUtility.SetDirty(go);

            string status = issues.Count == 0 ? "OK" : string.Join("; ", issues);
            report.AppendLine($"{go.name} | {GetHierarchyPath(go.transform)} | {LayerMask.LayerToName(go.layer)} | {(col != null ? col.GetType().Name : "None")} | {reqKey} | {doorId} | {angle:F0}° | {status}");
            return fixes;
        }

        private static int ValidateAndFixInteractable(Component comp, int interactMask, int targetLayer, string kind, StringBuilder report)
        {
            int fixes = 0;
            var issues = new List<string>();
            GameObject go = comp.gameObject;

            if (((1 << go.layer) & interactMask) == 0)
            {
                issues.Add($"слой {LayerMask.LayerToName(go.layer)}->{LayerMask.LayerToName(targetLayer)}");
                Undo.RecordObject(go, "Fix Interactable Layer");
                go.layer = targetLayer;
                fixes++;
            }

            Collider col = go.GetComponent<Collider>();
            if (col == null)
            {
                issues.Add("нет коллайдера->добавлен BoxCollider");
                col = Undo.AddComponent<BoxCollider>(go);
                fixes++;
            }
            else if (!col.enabled)
            {
                issues.Add("коллайдер выключен->включён");
                Undo.RecordObject(col, "Enable Interactable Collider");
                col.enabled = true;
                fixes++;
            }

            EditorUtility.SetDirty(go);
            string status = issues.Count == 0 ? "OK" : string.Join("; ", issues);
            report.AppendLine($"{go.name} ({kind}) | {GetHierarchyPath(go.transform)} | {LayerMask.LayerToName(go.layer)} | {col.GetType().Name} | - | - | - | {status}");
            return fixes;
        }

        private static int TagExistingDialogueNpcs(Scene scene, int interactLayer)
        {
            int count = 0;
            Stone stone = FindFirstInScene<Stone>(scene);
            if (stone != null)
            {
                var stoneSo = new SerializedObject(stone);
                count += EnsureTalkPrompt(stoneSo.FindProperty("_saya")?.objectReferenceValue as GameObject, interactLayer);
                count += EnsureTalkPrompt(stoneSo.FindProperty("_sayaRoped")?.objectReferenceValue as GameObject, interactLayer);
            }

            foreach (MonsterTalkInteractable monster in FindAllInScene<MonsterTalkInteractable>(scene))
            {
                count += EnsureTalkPrompt(monster.gameObject, interactLayer);
            }

            foreach (NPCConversation conv in FindAllInScene<NPCConversation>(scene))
            {
                if (conv.GetComponent<RegObj>() != null || conv.GetComponent<Stone>() != null) continue;
                Transform target = conv.transform.parent != null && conv.GetComponent<Collider>() == null
                    ? conv.transform.parent
                    : conv.transform;
                count += EnsureTalkPrompt(target.gameObject, interactLayer);
            }

            return count;
        }

        private static int EnsureTalkPrompt(GameObject npcGo, int interactLayer)
        {
            if (npcGo == null) return 0;
            bool changed = false;

            if (npcGo.GetComponent<IInteractionPrompt>() == null)
            {
                Undo.AddComponent<NpcDialoguePrompt>(npcGo);
                changed = true;
            }

            if (npcGo.GetComponent<Collider>() == null && npcGo.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
            {
                CapsuleCollider cap = Undo.AddComponent<CapsuleCollider>(npcGo);
                cap.center = new Vector3(0f, 0.86f, 0f);
                cap.radius = 0.28f;
                cap.height = 1.72f;
                changed = true;
            }

            if (npcGo.GetComponent<Collider>() != null && npcGo.layer != interactLayer)
            {
                Undo.RecordObject(npcGo, "Set NPC Talk Layer");
                npcGo.layer = interactLayer;
                changed = true;
            }

            if (changed) EditorUtility.SetDirty(npcGo);
            return changed ? 1 : 0;
        }

        [MenuItem("MoBA/Setup Story Quest & Shop")]
        public static void SetupStoryQuestAndShop()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !string.Equals(scene.name, "Level1", StringComparison.OrdinalIgnoreCase))
            {
                scene = EditorSceneManager.OpenScene("Assets/Scenes/Level1.unity", OpenSceneMode.Single);
            }
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[SetupStoryQuestAndShop] Не удалось загрузить Assets/Scenes/Level1.unity.");
                return;
            }

            StoryQuestDialogueBuilder.SetupOnScene(scene);
            ValidateDoors();
            PopulateShop();
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("MoBA/Populate Shop")]
        public static void PopulateShop()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[PopulateShop] Нет активной загруженной сцены.");
                return;
            }

            GameObject shop = FindSceneObjectByName(scene, "Shop");
            if (shop == null)
            {
                Debug.LogError("[PopulateShop] На сцене не найден объект 'Shop'.");
                return;
            }

            ShopPopulateBuilder.PopulateShopAndQuestGoods(scene, shop);
            EditorSceneManager.SaveOpenScenes();
        }

        private static int PopulateShopByRaycastGrid(
            GameObject shop,
            Transform parent,
            List<Vector2> avoidZones,
            List<Vector3> placedPositions,
            List<GameObject> dryPrefabs,
            List<GameObject> producePrefabs,
            List<GameObject> cratePrefabs,
            System.Random rng)
        {
            if (!TryCalculateWorldBounds(shop, out Bounds shopBounds)) return 0;

            float floorY = shopBounds.min.y;
            float[] sliceHeights = { floorY + 2.35f, floorY + 1.85f, floorY + 1.40f, floorY + 0.98f };
            const float step = 0.44f;
            const int maxProps = 90;
            int placed = 0;

            foreach (float rayY in sliceHeights)
            {
                if (placed >= maxProps) break;
                for (float x = shopBounds.min.x + 0.45f; x <= shopBounds.max.x - 0.45f; x += step)
                {
                    if (placed >= maxProps) break;
                    for (float z = shopBounds.min.z + 0.45f; z <= shopBounds.max.z - 0.45f; z += step)
                    {
                        if (placed >= maxProps) break;
                        float px = x + (float)(rng.NextDouble() * 0.08 - 0.04);
                        float pz = z + (float)(rng.NextDouble() * 0.08 - 0.04);
                        if (IsNearAvoidZone(px, pz, avoidZones, 1.2f)) continue;

                        if (!Physics.Raycast(new Vector3(px, rayY, pz), Vector3.down, out RaycastHit hit, 0.50f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                        {
                            continue;
                        }
                        if (!hit.collider.transform.IsChildOf(shop.transform)) continue;
                        if (hit.collider.GetComponent<Door>() != null) continue;
                        if (hit.normal.y < 0.95f) continue;

                        float hFromFloor = hit.point.y - floorY;
                        if (hFromFloor < 0.65f || hFromFloor > 2.30f) continue;

                        // Требуем свободное пространство сверху >= 0.30 м И наличие крыши Shop выше (чтобы не спавнить снаружи)
                        if (Physics.Raycast(hit.point + Vector3.up * 0.03f, Vector3.up, 0.30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                        {
                            continue;
                        }
                        if (!Physics.Raycast(hit.point + Vector3.up * 0.32f, Vector3.up, out RaycastHit roofHit, 5.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                            !roofHit.collider.transform.IsChildOf(shop.transform))
                        {
                            continue;
                        }

                        if (IsTooCloseIn3D(hit.point, placedPositions, 0.36f)) continue;
                        if (rng.NextDouble() > 0.55) continue;

                        double roll = rng.NextDouble();
                        GameObject prefab;
                        bool tipSide = false;
                        if (roll < 0.16 && cratePrefabs.Count > 0)
                        {
                            prefab = cratePrefabs[rng.Next(cratePrefabs.Count)];
                        }
                        else if (roll < 0.50 && producePrefabs.Count > 0)
                        {
                            prefab = producePrefabs[rng.Next(producePrefabs.Count)];
                        }
                        else
                        {
                            prefab = dryPrefabs[rng.Next(dryPrefabs.Count)];
                            tipSide = rng.NextDouble() < 0.28;
                        }

                        if (prefab == null) continue;
                        GameObject item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                        Undo.RegisterCreatedObjectUndo(item, "Spawn Shop Prop");

                        float yaw = (float)(rng.NextDouble() * 360.0);
                        float pitch = tipSide ? (rng.NextDouble() < 0.5 ? 88f : -88f) : 0f;
                        item.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
                        item.transform.position = hit.point;
                        Physics.SyncTransforms();

                        if (TryCalculateWorldBounds(item, out Bounds itemBounds))
                        {
                            float lift = hit.point.y - itemBounds.min.y;
                            item.transform.position += new Vector3(0f, lift, 0f);
                        }

                        MarkStaticProp(item);
                        placedPositions.Add(hit.point);
                        placed++;
                    }
                }
            }

            return placed;
        }

        private static bool IsTooCloseIn3D(Vector3 candidate, List<Vector3> placed, float minDist)
        {
            float minSq = minDist * minDist;
            for (int i = 0; i < placed.Count; i++)
            {
                if ((placed[i] - candidate).sqrMagnitude < minSq) return true;
            }
            return false;
        }

        private static List<Vector2> CollectShopAvoidPositions(GameObject shop)
        {
            var zones = new List<Vector2>();
            foreach (Door d in shop.GetComponentsInChildren<Door>(true))
            {
                zones.Add(new Vector2(d.transform.position.x, d.transform.position.z));
            }
            MonsterTalkInteractable monster = UnityEngine.Object.FindAnyObjectByType<MonsterTalkInteractable>();
            if (monster != null)
            {
                zones.Add(new Vector2(monster.transform.position.x, monster.transform.position.z));
            }
            else
            {
                zones.Add(new Vector2(MonsterApproxPos.x, MonsterApproxPos.z));
            }
            return zones;
        }

        private static bool IsNearAvoidZone(float x, float z, List<Vector2> zones, float minDist)
        {
            float minSqr = minDist * minDist;
            for (int i = 0; i < zones.Count; i++)
            {
                float dx = x - zones[i].x;
                float dz = z - zones[i].y;
                if (dx * dx + dz * dz < minSqr) return true;
            }
            return false;
        }

        private static List<GameObject> EnsureShopPrefabs(ShelfCategory category)
        {
            EnsureFolder(ShopPrefabsFolder);
            EnsureFolder(ShopPrefabsFolder + "/Materials");
            var list = new List<GameObject>();

            if (category == ShelfCategory.DryFoodOrDrink)
            {
                string[] crowNames = { "Soda Bottle.prefab", "Jar.prefab", "Can_01.prefab", "Can_01_Open.prefab", "Juice Box.prefab", "Milk.prefab" };
                foreach (string file in crowNames)
                {
                    GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>($"{CrowFoodPrefabsFolder}/{file}");
                    if (src != null)
                    {
                        string outName = "Shop_" + file.Replace(" ", "");
                        list.Add(CreateStaticVariantPrefab(src, $"{ShopPrefabsFolder}/{outName}"));
                    }
                }

                list.Add(CreatePrimitivePrefab("Shop_Bottle", root =>
                {
                    Material glass = EnsureShopMaterial("Mat_GlassGreen", new Color(0.18f, 0.34f, 0.21f), 0.1f, 0.85f);
                    Material cork = EnsureShopMaterial("Mat_Cork", new Color(0.42f, 0.30f, 0.18f), 0f, 0.25f);
                    AddPart(root.transform, PrimitiveType.Cylinder, "Body", new Vector3(0f, 0.09f, 0f), new Vector3(0.08f, 0.09f, 0.08f), glass);
                    AddPart(root.transform, PrimitiveType.Cylinder, "Neck", new Vector3(0f, 0.21f, 0f), new Vector3(0.032f, 0.045f, 0.032f), glass);
                    AddPart(root.transform, PrimitiveType.Cylinder, "Cap", new Vector3(0f, 0.26f, 0f), new Vector3(0.036f, 0.012f, 0.036f), cork);
                }));
            }
            else if (category == ShelfCategory.Produce)
            {
                Material appleMat = EnsureShopMaterial("Mat_Apple", new Color(0.68f, 0.16f, 0.12f), 0f, 0.45f);
                Material rottenMat = EnsureShopMaterial("Mat_RottenFruit", new Color(0.20f, 0.16f, 0.10f), 0f, 0.15f);
                Material tomatoMat = EnsureShopMaterial("Mat_Tomato", new Color(0.76f, 0.14f, 0.09f), 0f, 0.55f);
                Material orangeMat = EnsureShopMaterial("Mat_Orange", new Color(0.84f, 0.42f, 0.08f), 0f, 0.38f);
                Material carrotMat = EnsureShopMaterial("Mat_Carrot", new Color(0.80f, 0.34f, 0.06f), 0f, 0.30f);
                Material stemMat = EnsureShopMaterial("Mat_Stem", new Color(0.24f, 0.30f, 0.12f), 0f, 0.20f);

                list.Add(CreateSphereFruit("Shop_Apple", 0.09f, appleMat, stemMat));
                list.Add(CreateSphereFruit("Shop_AppleRotten", 0.085f, rottenMat, rottenMat));
                list.Add(CreateSphereFruit("Shop_Tomato", 0.08f, tomatoMat, stemMat));
                list.Add(CreateSphereFruit("Shop_Orange", 0.095f, orangeMat, stemMat));
                list.Add(CreatePrimitivePrefab("Shop_Carrot", root =>
                {
                    GameObject body = AddPart(root.transform, PrimitiveType.Cylinder, "CarrotBody", new Vector3(0f, 0.09f, 0f), new Vector3(0.038f, 0.08f, 0.038f), carrotMat);
                    body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    AddPart(root.transform, PrimitiveType.Cylinder, "Leaf", new Vector3(0f, 0.09f, 0.095f), new Vector3(0.015f, 0.025f, 0.015f), stemMat);
                }));
            }
            else
            {
                Material woodMat = EnsureShopMaterial("Mat_CrateWood", new Color(0.32f, 0.24f, 0.16f), 0f, 0.22f);
                list.Add(CreateCratePrefab("Shop_CrateEmpty", woodMat, filled: false));
                list.Add(CreateCratePrefab("Shop_CrateFilled", woodMat, filled: true));
            }

            return list;
        }

        private static GameObject CreateStaticVariantPrefab(GameObject sourcePrefab, string targetPath)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(targetPath);
            if (existing != null) return existing;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab);
            try
            {
                foreach (Rigidbody rb in instance.GetComponentsInChildren<Rigidbody>(true))
                {
                    UnityEngine.Object.DestroyImmediate(rb);
                }
                foreach (Collider c in instance.GetComponentsInChildren<Collider>(true))
                {
                    if (c is MeshCollider mc) mc.convex = true;
                }
                if (instance.GetComponentInChildren<Collider>(true) == null)
                {
                    instance.AddComponent<BoxCollider>();
                }
                MarkStaticProp(instance);
                return PrefabUtility.SaveAsPrefabAsset(instance, targetPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static GameObject CreateSphereFruit(string prefabName, float diameter, Material fruitMat, Material stemMat)
        {
            return CreatePrimitivePrefab(prefabName, root =>
            {
                AddPart(root.transform, PrimitiveType.Sphere, "Fruit", new Vector3(0f, diameter * 0.5f, 0f), Vector3.one * diameter, fruitMat);
                AddPart(root.transform, PrimitiveType.Cylinder, "Stem", new Vector3(0f, diameter + 0.012f, 0f), new Vector3(0.01f, 0.015f, 0.01f), stemMat);
            });
        }

        private static GameObject CreateCratePrefab(string prefabName, Material woodMat, bool filled)
        {
            return CreatePrimitivePrefab(prefabName, root =>
            {
                AddPart(root.transform, PrimitiveType.Cube, "Bottom", new Vector3(0f, 0.015f, 0f), new Vector3(0.34f, 0.03f, 0.26f), woodMat);
                AddPart(root.transform, PrimitiveType.Cube, "WallN", new Vector3(0f, 0.10f, 0.12f), new Vector3(0.34f, 0.16f, 0.022f), woodMat);
                AddPart(root.transform, PrimitiveType.Cube, "WallS", new Vector3(0f, 0.10f, -0.12f), new Vector3(0.34f, 0.16f, 0.022f), woodMat);
                AddPart(root.transform, PrimitiveType.Cube, "WallE", new Vector3(0.16f, 0.10f, 0f), new Vector3(0.022f, 0.16f, 0.22f), woodMat);
                AddPart(root.transform, PrimitiveType.Cube, "WallW", new Vector3(-0.16f, 0.10f, 0f), new Vector3(0.022f, 0.16f, 0.22f), woodMat);

                if (filled)
                {
                    Material rotten = EnsureShopMaterial("Mat_RottenFruit", new Color(0.20f, 0.16f, 0.10f), 0f, 0.15f);
                    Material apple = EnsureShopMaterial("Mat_Apple", new Color(0.68f, 0.16f, 0.12f), 0f, 0.45f);
                    AddPart(root.transform, PrimitiveType.Sphere, "Item1", new Vector3(-0.07f, 0.075f, 0.03f), Vector3.one * 0.09f, apple);
                    AddPart(root.transform, PrimitiveType.Sphere, "Item2", new Vector3(0.05f, 0.075f, -0.02f), Vector3.one * 0.085f, rotten);
                    AddPart(root.transform, PrimitiveType.Sphere, "Item3", new Vector3(0.01f, 0.075f, 0.05f), Vector3.one * 0.08f, apple);
                }
            });
        }

        private static GameObject CreatePrimitivePrefab(string prefabName, Action<GameObject> buildVisual)
        {
            string path = $"{ShopPrefabsFolder}/{prefabName}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            GameObject root = new GameObject(prefabName);
            try
            {
                buildVisual(root);
                if (TryCalculateWorldBounds(root, out Bounds b))
                {
                    BoxCollider col = root.AddComponent<BoxCollider>();
                    col.center = b.center;
                    col.size = b.size;
                }
                MarkStaticProp(root);
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject AddPart(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = pos;
            part.transform.localScale = scale;
            Collider col = part.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);
            Renderer r = part.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
            return part;
        }

        private static Material EnsureShopMaterial(string matName, Color color, float metallic, float smoothness)
        {
            string path = $"{ShopPrefabsFolder}/Materials/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = ResolveActiveLitShader();
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Shader ResolveActiveLitShader()
        {
            RenderPipelineAsset rp = GraphicsSettings.currentRenderPipeline != null
                ? GraphicsSettings.currentRenderPipeline
                : GraphicsSettings.defaultRenderPipeline;
            if (rp != null)
            {
                string typeName = rp.GetType().FullName ?? string.Empty;
                if (typeName.IndexOf("Universal", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
                    if (urpLit != null) return urpLit;
                }
                if (typeName.IndexOf("HDRenderPipeline", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Shader hdrpLit = Shader.Find("HDRP/Lit");
                    if (hdrpLit != null) return hdrpLit;
                }
            }
            return Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
        }

        private static void MarkStaticProp(GameObject go)
        {
            StaticEditorFlags flags = StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
                t.gameObject.layer = 0;
            }
        }

        private static bool WouldDoorClipWall(Door door, Collider doorCollider, float targetYaw)
        {
            if (doorCollider == null) return false;
            Quaternion originalRot = door.transform.rotation;
            door.transform.rotation = Quaternion.Euler(originalRot.eulerAngles.x, targetYaw, originalRot.eulerAngles.z);
            Physics.SyncTransforms();

            Bounds b = doorCollider.bounds;
            Vector3 hingeXZ = new Vector3(door.transform.position.x, b.center.y, door.transform.position.z);
            Vector3 leafOffset = b.center - hingeXZ;
            Vector3 probeCenter = leafOffset.sqrMagnitude > 0.0025f
                ? hingeXZ + leafOffset * 1.15f + Vector3.up * 0.1f
                : b.center + Vector3.up * 0.1f;
            Vector3 halfExtents = new Vector3(0.12f, Mathf.Max(0.2f, b.extents.y * 0.45f), 0.12f);
            Collider[] hits = Physics.OverlapBox(probeCenter, halfExtents, Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            door.transform.rotation = originalRot;
            Physics.SyncTransforms();

            foreach (Collider hit in hits)
            {
                if (hit == null || hit == doorCollider || hit.transform.IsChildOf(door.transform)) continue;
                if (hit is TerrainCollider || hit.GetComponent<Door>() != null) continue;
                if (hit.bounds.max.y <= b.min.y + 0.12f) continue;
                return true;
            }
            return false;
        }

        private static float ChooseBestOpenAngle(Door door, Collider doorCollider, float closedYaw)
        {
            float candidateA = Mathf.Repeat(closedYaw + 85f, 360f);
            float candidateB = Mathf.Repeat(closedYaw - 85f, 360f);
            bool clipA = WouldDoorClipWall(door, doorCollider, candidateA);
            bool clipB = WouldDoorClipWall(door, doorCollider, candidateB);
            if (!clipA && clipB) return candidateA;
            if (!clipB && clipA) return candidateB;
            return candidateB;
        }

        private static int ResolveInteractionLayerMask(Scene scene)
        {
            InteractionManager im = FindFirstInScene<InteractionManager>(scene);
            if (im != null)
            {
                var so = new SerializedObject(im);
                SerializedProperty maskProp = so.FindProperty("_layerMask");
                if (maskProp != null && maskProp.intValue != 0) return maskProp.intValue;
            }
            int layer = LayerMask.NameToLayer("Interact");
            return layer >= 0 ? (1 << layer) : 1;
        }

        private static int ResolveFirstLayerFromMask(int mask)
        {
            int interact = LayerMask.NameToLayer("Interact");
            if (interact >= 0 && ((1 << interact) & mask) != 0) return interact;
            for (int i = 0; i < 32; i++)
            {
                if (((1 << i) & mask) != 0) return i;
            }
            return 0;
        }

        private static Door FindGreyHouseGateDoor(Scene scene)
        {
            GameObject shop = FindSceneObjectByName(scene, "Shop");
            if (shop == null) return null;

            Door best = null;
            float bestScore = float.MaxValue;
            foreach (Door door in shop.GetComponentsInChildren<Door>(true))
            {
                var so = new SerializedObject(door);
                bool reqKey = so.FindProperty("_requiresKey")?.boolValue ?? false;
                int doorId = so.FindProperty("_doorId")?.intValue ?? 0;
                float dist = Vector3.Distance(door.transform.position, MonsterApproxPos);
                float score = dist - (reqKey ? 1000f : 0f) - (doorId == 1 ? 200f : 0f);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = door;
                }
            }
            return best;
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

        private static bool TryCalculateWorldBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return hasBounds;
        }

        private static string GetHierarchyPath(Transform t)
        {
            var sb = new StringBuilder(t.name);
            Transform p = t.parent;
            while (p != null)
            {
                sb.Insert(0, p.name + "/");
                p = p.parent;
            }
            return sb.ToString();
        }

        private static void EnsureFolder(string assetFolderPath)
        {
            string[] parts = assetFolderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
#endif
