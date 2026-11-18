#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DialogueEditor;
using MoBA.Interaction;
using MoBA.Narrative;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MoBA.Editor
{
    /// <summary>
    /// Автоматическая настройка квеста «Ключ от калитки» на активной сцене (Level1).
    /// </summary>
    public static class KeyQuestSceneSetup
    {
        private const string PrefabFolder = "Assets/Prefabs/Key";
        private const string KeyPrefabPath = PrefabFolder + "/Key.prefab";
        private const string KeyMaterialPath = PrefabFolder + "/Key_Mat.mat";
        private const string HouseTriggerName = "HouseZoneTrigger";
        private const string GateAnchorName = "GateGuidanceAnchor";
        private const string MonsterConvObjectName = "MonsterStubConversation";
        private static readonly Vector3 MonsterApproxPos = new Vector3(200f, 6f, 134f);

        [MenuItem("MoBA/Setup Key Quest")]
        public static void SetupKeyQuest()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                Debug.LogError("[KeyQuestSceneSetup] Нет открытой загруженной сцены.");
                return;
            }

            GameObject keyPrefab = EnsureKeyPrefabAsset();
            GameObject mainHouse = FindSceneObjectByName(activeScene, "MainHouse");
            if (mainHouse == null)
            {
                Debug.LogError("[KeyQuestSceneSetup] На сцене не найден объект 'MainHouse'. Откройте Level1.");
                return;
            }

            SetupHouseTrigger(mainHouse);
            PlaceKeyInMainHouseCupBoard(mainHouse, keyPrefab);

            Door gateDoor = FindGreyHouseGateDoor(activeScene);
            Transform gateAnchor = SetupGateDoor(gateDoor);

            GameObject monster = FindPinkMonster(activeScene);
            SetupMonsterTalk(activeScene, monster);

            SetupRegObj(activeScene, gateAnchor);

            EditorSceneManager.MarkSceneDirty(activeScene);
            AssetDatabase.SaveAssets();
            Debug.Log("[KeyQuestSceneSetup] Квест «Ключ от калитки» успешно настроен на сцене " + activeScene.name + ".");
        }

        private static GameObject EnsureKeyPrefabAsset()
        {
            string[] existingGuids = AssetDatabase.FindAssets("Key t:Model");
            foreach (string guid in existingGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path) && !path.StartsWith(PrefabFolder, StringComparison.OrdinalIgnoreCase))
                {
                    GameObject imported = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (imported != null)
                    {
                        return EnsureKeyPrefabFromModel(imported);
                    }
                }
            }

            EnsureFolder(PrefabFolder);
            Material keyMat = CreateOrUpdateKeyMaterial();

            GameObject root = new GameObject("Key");
            try
            {
                int interactLayer = LayerMask.NameToLayer("Interact");
                root.layer = interactLayer >= 0 ? interactLayer : 0;

                BoxCollider triggerCol = root.AddComponent<BoxCollider>();
                triggerCol.isTrigger = true;
                triggerCol.center = new Vector3(0f, 0.02f, 0.03f);
                triggerCol.size = new Vector3(0.24f, 0.09f, 0.34f);

                root.AddComponent<KeyPickup>();

                GameObject visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                visual.layer = root.layer;

                // Головка ключа (кольцо из 4 брусков) + стержень + бородка с зубцами
                AddPrimitivePart(visual.transform, PrimitiveType.Cube, "BowTop", new Vector3(0f, 0f, -0.105f), Vector3.zero, new Vector3(0.085f, 0.016f, 0.018f), keyMat);
                AddPrimitivePart(visual.transform, PrimitiveType.Cube, "BowBottom", new Vector3(0f, 0f, -0.025f), Vector3.zero, new Vector3(0.085f, 0.016f, 0.018f), keyMat);
                AddPrimitivePart(visual.transform, PrimitiveType.Cube, "BowLeft", new Vector3(-0.036f, 0f, -0.065f), Vector3.zero, new Vector3(0.018f, 0.016f, 0.085f), keyMat);
                AddPrimitivePart(visual.transform, PrimitiveType.Cube, "BowRight", new Vector3(0.036f, 0f, -0.065f), Vector3.zero, new Vector3(0.018f, 0.016f, 0.085f), keyMat);

                AddPrimitivePart(visual.transform, PrimitiveType.Cylinder, "Collar", new Vector3(0f, 0f, -0.008f), new Vector3(90f, 0f, 0f), new Vector3(0.032f, 0.01f, 0.032f), keyMat);
                AddPrimitivePart(visual.transform, PrimitiveType.Cylinder, "Shaft", new Vector3(0f, 0f, 0.075f), new Vector3(90f, 0f, 0f), new Vector3(0.022f, 0.085f, 0.022f), keyMat);
                AddPrimitivePart(visual.transform, PrimitiveType.Cube, "ToothA", new Vector3(0.024f, 0f, 0.125f), Vector3.zero, new Vector3(0.034f, 0.012f, 0.016f), keyMat);
                AddPrimitivePart(visual.transform, PrimitiveType.Cube, "ToothB", new Vector3(0.021f, 0f, 0.149f), Vector3.zero, new Vector3(0.028f, 0.012f, 0.014f), keyMat);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, KeyPrefabPath);
                AssetDatabase.Refresh();
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject EnsureKeyPrefabFromModel(GameObject modelAsset)
        {
            EnsureFolder(PrefabFolder);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            try
            {
                int interactLayer = LayerMask.NameToLayer("Interact");
                SetLayerRecursive(instance, interactLayer >= 0 ? interactLayer : 0);
                if (instance.GetComponent<Collider>() == null)
                {
                    BoxCollider col = instance.AddComponent<BoxCollider>();
                    col.isTrigger = true;
                }
                if (instance.GetComponent<KeyPickup>() == null)
                {
                    instance.AddComponent<KeyPickup>();
                }
                return PrefabUtility.SaveAsPrefabAsset(instance, KeyPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static Material CreateOrUpdateKeyMaterial()
        {
            Shader shader = ResolveActiveLitShader();
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(KeyMaterialPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, KeyMaterialPath);
            }
            else
            {
                mat.shader = shader;
            }

            Color brassColor = new Color(0.82f, 0.66f, 0.28f, 1f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", brassColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", brassColor);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.85f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.65f);
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

            Shader standard = Shader.Find("Standard");
            return standard != null ? standard : Shader.Find("Universal Render Pipeline/Lit");
        }

        private static void AddPrimitivePart(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 localEuler, Vector3 localScale, Material mat)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.layer = parent.gameObject.layer;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localRotation = Quaternion.Euler(localEuler);
            part.transform.localScale = localScale;

            Collider col = part.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);

            Renderer rend = part.GetComponent<Renderer>();
            if (rend != null && mat != null) rend.sharedMaterial = mat;
        }

        private static void SetupHouseTrigger(GameObject mainHouse)
        {
            Transform existing = mainHouse.transform.Find(HouseTriggerName);
            GameObject triggerGo = existing != null ? existing.gameObject : new GameObject(HouseTriggerName);
            if (existing == null)
            {
                Undo.RegisterCreatedObjectUndo(triggerGo, "Create HouseZoneTrigger");
                triggerGo.transform.SetParent(mainHouse.transform, false);
            }

            Undo.RecordObject(triggerGo, "Configure HouseZoneTrigger");
            int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            triggerGo.layer = ignoreRaycastLayer >= 0 ? ignoreRaycastLayer : 0;

            BoxCollider box = triggerGo.GetComponent<BoxCollider>();
            if (box == null) box = Undo.AddComponent<BoxCollider>(triggerGo);
            box.isTrigger = true;

            if (TryCalculateWorldBounds(mainHouse, out Bounds worldBounds))
            {
                // Небольшой отступ от наружных стен/крыльца, чтобы выход за порог сразу переключал подсказку на «Зайти в дом»
                Vector3 localCenter = mainHouse.transform.InverseTransformPoint(worldBounds.center);
                Vector3 localSize = new Vector3(
                    Mathf.Max(4f, worldBounds.size.x - 1.6f),
                    Mathf.Max(3.5f, worldBounds.size.y),
                    Mathf.Max(4f, worldBounds.size.z - 1.6f));
                triggerGo.transform.localPosition = localCenter;
                triggerGo.transform.localRotation = Quaternion.identity;
                triggerGo.transform.localScale = Vector3.one;
                box.center = Vector3.zero;
                box.size = localSize;
            }
            else
            {
                triggerGo.transform.localPosition = new Vector3(0f, 2.2f, 0f);
                box.center = Vector3.zero;
                box.size = new Vector3(14f, 4.5f, 14f);
            }

            if (triggerGo.GetComponent<HouseZoneTrigger>() == null)
            {
                Undo.AddComponent<HouseZoneTrigger>(triggerGo);
            }
            EditorUtility.SetDirty(triggerGo);
        }

        private static void PlaceKeyInMainHouseCupBoard(GameObject mainHouse, GameObject keyPrefab)
        {
            if (keyPrefab == null) return;

            foreach (KeyPickup oldKey in mainHouse.GetComponentsInChildren<KeyPickup>(true))
            {
                Undo.DestroyObjectImmediate(oldKey.gameObject);
            }

            CupBoard targetCupBoard = FindPreferredCupBoard(mainHouse);
            if (targetCupBoard == null)
            {
                Debug.LogError("[KeyQuestSceneSetup] Внутри MainHouse не найден компонент CupBoard.", mainHouse);
                return;
            }

            GameObject keyInstance = (GameObject)PrefabUtility.InstantiatePrefab(keyPrefab, targetCupBoard.transform);
            Undo.RegisterCreatedObjectUndo(keyInstance, "Place Key In CupBoard");

            Vector3 localPos = Vector3.zero;
            MeshFilter mf = targetCupBoard.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Bounds mb = mf.sharedMesh.bounds;
                localPos = new Vector3(mb.center.x, mb.min.y + Mathf.Min(0.035f, mb.size.y * 0.25f), mb.center.z);
            }

            keyInstance.transform.localPosition = localPos;
            keyInstance.transform.localRotation = Quaternion.Euler(0f, 28f, 0f);

            Vector3 lossy = targetCupBoard.transform.lossyScale;
            keyInstance.transform.localScale = new Vector3(
                lossy.x > 0.001f ? 1f / lossy.x : 1f,
                lossy.y > 0.001f ? 1f / lossy.y : 1f,
                lossy.z > 0.001f ? 1f / lossy.z : 1f);

            int interactLayer = LayerMask.NameToLayer("Interact");
            SetLayerRecursive(keyInstance, interactLayer >= 0 ? interactLayer : 0);
            EditorUtility.SetDirty(keyInstance);
        }

        private static CupBoard FindPreferredCupBoard(GameObject mainHouse)
        {
            CupBoard[] cupBoards = mainHouse.GetComponentsInChildren<CupBoard>(true);
            string[] preferredNames = { "draw05", "draw04", "draw03", "draw02", "draw01", "draw06", "draw07" };
            foreach (string preferred in preferredNames)
            {
                foreach (CupBoard cb in cupBoards)
                {
                    if (string.Equals(cb.gameObject.name, preferred, StringComparison.OrdinalIgnoreCase))
                    {
                        return cb;
                    }
                }
            }
            return cupBoards.Length > 0 ? cupBoards[0] : null;
        }

        private static Door FindGreyHouseGateDoor(Scene scene)
        {
            GameObject shop = FindSceneObjectByName(scene, "Shop");
            List<Door> candidates = new List<Door>();
            if (shop != null)
            {
                candidates.AddRange(shop.GetComponentsInChildren<Door>(true));
            }
            else
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    candidates.AddRange(root.GetComponentsInChildren<Door>(true));
                }
            }

            Door best = null;
            float bestScore = float.MaxValue;
            foreach (Door door in candidates)
            {
                SerializedObject so = new SerializedObject(door);
                bool alreadyKeyDoor = so.FindProperty("_requiresKey")?.boolValue ?? false;
                int doorId = so.FindProperty("_doorId")?.intValue ?? 0;

                float dist = Vector3.Distance(door.transform.position, MonsterApproxPos);
                float score = dist - (alreadyKeyDoor ? 1000f : 0f) - (doorId == 1 ? 200f : 0f);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = door;
                }
            }
            return best;
        }

        private static Transform SetupGateDoor(Door gateDoor)
        {
            if (gateDoor == null)
            {
                Debug.LogError("[KeyQuestSceneSetup] Не найдена калитка (Door) серого дома.");
                return null;
            }

            Undo.RecordObject(gateDoor, "Configure Gate Door");
            SerializedObject doorSo = new SerializedObject(gateDoor);
            SerializedProperty requiresKeyProp = doorSo.FindProperty("_requiresKey");
            SerializedProperty angleProp = doorSo.FindProperty("_angle");
            SerializedProperty lockedAngleProp = doorSo.FindProperty("_lockedAngle");

            if (requiresKeyProp != null) requiresKeyProp.boolValue = true;

            float closedYaw = gateDoor.transform.rotation.eulerAngles.y;
            if (angleProp != null && Mathf.Abs(Mathf.DeltaAngle(closedYaw, angleProp.floatValue)) < 5f)
            {
                angleProp.floatValue = closedYaw - 90f;
            }
            if (lockedAngleProp != null && Mathf.Abs(Mathf.DeltaAngle(closedYaw, lockedAngleProp.floatValue)) < 2f)
            {
                lockedAngleProp.floatValue = closedYaw + 13f;
            }
            doorSo.ApplyModifiedPropertiesWithoutUndo();

            int interactLayer = LayerMask.NameToLayer("Interact");
            if (interactLayer >= 0) gateDoor.gameObject.layer = interactLayer;
            EditorUtility.SetDirty(gateDoor);

            Transform anchor = gateDoor.transform.Find(GateAnchorName);
            GameObject anchorGo = anchor != null ? anchor.gameObject : new GameObject(GateAnchorName);
            if (anchor == null)
            {
                Undo.RegisterCreatedObjectUndo(anchorGo, "Create GateGuidanceAnchor");
                anchorGo.transform.SetParent(gateDoor.transform, false);
            }

            if (TryCalculateWorldBounds(gateDoor.gameObject, out Bounds b))
            {
                anchorGo.transform.position = b.center;
            }
            else
            {
                anchorGo.transform.localPosition = Vector3.zero;
            }

            EditorUtility.SetDirty(anchorGo);
            return anchorGo.transform;
        }

        private static GameObject FindPinkMonster(Scene scene)
        {
            GameObject best = null;
            float bestDist = float.MaxValue;

            // Сначала ищем по SkinnedMeshRenderer (розовый монстр со скелетом в углу ~200, 6, 134),
            // чтобы гарантированно не перепутать со статическими ящиками/стенами.
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (smr.GetComponentInParent<EnemyAI>() != null) continue;
                    if (string.Equals(smr.transform.root.name, "Shop", StringComparison.OrdinalIgnoreCase)) continue;

                    float dist = Vector3.Distance(smr.bounds.center, MonsterApproxPos);
                    if (dist < 25f && dist < bestDist)
                    {
                        bestDist = dist;
                        Animator anim = smr.GetComponentInParent<Animator>();
                        best = anim != null ? anim.gameObject : smr.transform.root.gameObject;
                    }
                }
            }

            if (best == null)
            {
                best = FindSceneObjectByName(scene, "Demon");
            }
            return best;
        }

        private static void SetupMonsterTalk(Scene scene, GameObject monster)
        {
            if (monster == null)
            {
                Debug.LogError("[KeyQuestSceneSetup] Не найден розовый монстр около (200, 6, 134).");
                return;
            }

            Undo.RecordObject(monster, "Configure MonsterTalkInteractable");
            int interactLayer = LayerMask.NameToLayer("Interact");
            if (interactLayer >= 0) monster.layer = interactLayer;

            Collider monsterCol = monster.GetComponent<Collider>();
            if (monsterCol == null)
            {
                BoxCollider box = Undo.AddComponent<BoxCollider>(monster);
                if (TryCalculateWorldBounds(monster, out Bounds worldBounds))
                {
                    Vector3 center = monster.transform.InverseTransformPoint(worldBounds.center);
                    Vector3 minLocal = monster.transform.InverseTransformPoint(worldBounds.min);
                    Vector3 maxLocal = monster.transform.InverseTransformPoint(worldBounds.max);
                    box.center = center;
                    box.size = new Vector3(
                        Mathf.Max(0.6f, Mathf.Abs(maxLocal.x - minLocal.x)),
                        Mathf.Max(1.6f, Mathf.Abs(maxLocal.y - minLocal.y)),
                        Mathf.Max(0.6f, Mathf.Abs(maxLocal.z - minLocal.z)));
                }
                else
                {
                    box.center = new Vector3(0f, 1.2f, 0f);
                    box.size = new Vector3(1.1f, 2.4f, 1.1f);
                }
            }

            MonsterTalkInteractable talk = monster.GetComponent<MonsterTalkInteractable>();
            if (talk == null) talk = Undo.AddComponent<MonsterTalkInteractable>(monster);

            Transform convChild = monster.transform.Find(MonsterConvObjectName);
            GameObject convGo = convChild != null ? convChild.gameObject : new GameObject(MonsterConvObjectName);
            if (convChild == null)
            {
                Undo.RegisterCreatedObjectUndo(convGo, "Create MonsterStubConversation");
                convGo.transform.SetParent(monster.transform, false);
            }

            NPCConversation stubConv = convGo.GetComponent<NPCConversation>();
            if (stubConv == null) stubConv = Undo.AddComponent<NPCConversation>(convGo);
            ConfigureStubConversation(scene, stubConv);

            SerializedObject talkSo = new SerializedObject(talk);
            SerializedProperty convProp = talkSo.FindProperty("_stubConversation");
            if (convProp != null) convProp.objectReferenceValue = stubConv;
            talkSo.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(convGo);
            EditorUtility.SetDirty(monster);
        }

        private static void ConfigureStubConversation(Scene scene, NPCConversation stubConv)
        {
            TMP_FontAsset font = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (NPCConversation existing in root.GetComponentsInChildren<NPCConversation>(true))
                {
                    if (existing != stubConv && existing.DefaultFont != null)
                    {
                        font = existing.DefaultFont;
                        break;
                    }
                }
                if (font != null) break;
            }
            if (font == null) font = TMP_Settings.defaultFontAsset;

            Undo.RecordObject(stubConv, "Configure Stub NPCConversation");
            stubConv.DefaultName = "...";
            stubConv.DefaultFont = font;

            var ec = new EditableConversation();
            var rootNode = new EditableSpeechNode
            {
                ID = 0,
                Name = "...",
                Text = "...",
                TMPFont = font,
                AdvanceDialogueAutomatically = false,
                AutoAdvanceShouldDisplayOption = false,
                TimeUntilAdvance = 0f,
                Volume = 1f,
                EditorInfo = new EditableConversationNode.EditorArgs { isRoot = true, xPos = 150f, yPos = 60f }
            };
            ec.SpeechNodes.Add(rootNode);
            rootNode.SerializeAssetData(stubConv);
            stubConv.Serialize(ec);
            EditorUtility.SetDirty(stubConv);
        }

        private static void SetupRegObj(Scene scene, Transform gateAnchor)
        {
            RegObj regObj = FindComponentInScene<RegObj>(scene);
            if (regObj == null)
            {
                Debug.LogError("[KeyQuestSceneSetup] На сцене не найден RegObj.");
                return;
            }

            Stone stone = FindComponentInScene<Stone>(scene);
            LineToSaya lineToSaya = FindComponentInScene<LineToSaya>(scene);
            PointerInmpulse pointerImpulse = FindComponentInScene<PointerInmpulse>(scene);
            TextRenaming textRename = FindComponentInScene<TextRenaming>(scene);
            CinemachineInputAxisController playerAxis = FindPlayerCameraAxisController(scene);

            Undo.RecordObject(regObj, "Wire Key Quest in RegObj");
            SerializedObject so = new SerializedObject(regObj);
            SetObjectProperty(so, "_stone", stone);
            SetObjectProperty(so, "_lineToSaya", lineToSaya);
            SetObjectProperty(so, "_pointerImpulse", pointerImpulse);
            SetObjectProperty(so, "_gateGuidanceTarget", gateAnchor);
            SetObjectProperty(so, "_cameraInputAxisController", playerAxis);
            if (textRename != null)
            {
                SetObjectProperty(so, "_textRename", textRename);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(regObj);
        }

        private static CinemachineInputAxisController FindPlayerCameraAxisController(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (CinemachineInputAxisController ctrl in root.GetComponentsInChildren<CinemachineInputAxisController>(true))
                {
                    if (ctrl.GetComponent<CinemachinePanTilt>() != null)
                    {
                        return ctrl;
                    }
                }
            }
            return FindComponentInScene<CinemachineInputAxisController>(scene);
        }

        private static void SetObjectProperty(SerializedObject so, string propName, UnityEngine.Object value)
        {
            SerializedProperty prop = so.FindProperty(propName);
            if (prop != null) prop.objectReferenceValue = value;
        }

        private static GameObject FindSceneObjectByName(Scene scene, string targetName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (string.Equals(t.name, targetName, StringComparison.Ordinal))
                    {
                        return t.gameObject;
                    }
                }
            }
            return null;
        }

        private static T FindComponentInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
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

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayerRecursive(child.gameObject, layer);
            }
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
