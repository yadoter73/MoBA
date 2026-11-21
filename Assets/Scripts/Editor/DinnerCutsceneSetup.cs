#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using DialogueEditor;
using MoBA.Narrative;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoBA.EditorTools
{
    /// <summary>
    /// Настраивает DinnerChairInteractable на стуле в MainHouse:
    /// привязывает реальные префабы еды из магазина (Shop_Apple, Shop_Vodka, Shop_BreadQuest)
    /// и создаёт полноценный NPCConversation через DialogueEditor (DinnerAfterFoodConversation).
    /// </summary>
    [InitializeOnLoad]
    public static class DinnerCutsceneSetup
    {
        private const string Level1Path = "Assets/Scenes/Level1.unity";
        private const string ApplePrefabPath = "Assets/Prefabs/Shop/Shop_Apple.prefab";
        private const string VodkaPrefabPath = "Assets/Prefabs/Shop/Shop_Vodka.prefab";
        private const string BreadPrefabPath = "Assets/Prefabs/Shop/Shop_BreadQuest.prefab";
        private const string AutoRunMarkerPath = "Library/MoBA_DinnerCutscene_v4.done";

        static DinnerCutsceneSetup()
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
            SetupDinnerCutscene();
        }

        [MenuItem("MoBA/Setup Dinner Cutscene")]
        public static void SetupDinnerCutscene()
        {
            Scene scene = GetOrOpenLevel1();
            if (!scene.IsValid()) return;

            Stone stone = FindFirstInScene<Stone>(scene);
            if (stone == null)
            {
                Debug.LogError("[DinnerCutsceneSetup] На сцене Level1 не найден Stone.");
                return;
            }

            var stoneSo = new SerializedObject(stone);
            Transform houseTf = stoneSo.FindProperty("_houseTransform")?.objectReferenceValue as Transform
                ?? FindTransformByName(scene, "MainHouse")
                ?? FindTransformByName(scene, "PFB_Building_Full");
            GameObject sayaGo = stoneSo.FindProperty("_saya")?.objectReferenceValue as GameObject;
            Transform sayaChair = stoneSo.FindProperty("_targetChair")?.objectReferenceValue as Transform;
            Transform tableTf = stoneSo.FindProperty("_diningTable")?.objectReferenceValue as Transform;

            if (houseTf == null)
            {
                Transform tableFallback = FindTransformByName(scene, "PFB_DiningTable");
                if (tableFallback != null) houseTf = tableFallback.root;
            }

            if (houseTf == null)
            {
                Debug.LogError("[DinnerCutsceneSetup] На сцене Level1 не найден MainHouse.");
                return;
            }

            if (tableTf == null) tableTf = FindChildByName(houseTf, "PFB_DiningTable");
            if (sayaChair == null) sayaChair = FindChildByName(houseTf, "PFB_DiningChair (3)") ?? FindChildByName(houseTf, "PFB_DiningChair (1)");

            // Удаляем компонент с предыдущего неверного стула (если был)
            Transform chairTf = FindDinnerChair(houseTf, sayaChair, tableTf);
            foreach (DinnerChairInteractable oldComp in houseTf.GetComponentsInChildren<DinnerChairInteractable>(true))
            {
                if (oldComp != null && oldComp.transform != chairTf)
                {
                    Transform oldConv = oldComp.transform.Find("DinnerAfterFoodConversation");
                    if (oldConv != null) Undo.DestroyObjectImmediate(oldConv.gameObject);
                    Undo.DestroyObjectImmediate(oldComp);
                }
            }
            if (chairTf == null)
            {
                Debug.LogError("[DinnerCutsceneSetup] Не найден противоположный стул PFB_DiningChair в MainHouse.");
                return;
            }

            int interactLayer = LayerMask.NameToLayer("Interact");
            if (interactLayer >= 0 && chairTf.gameObject.layer != interactLayer)
            {
                Undo.RecordObject(chairTf.gameObject, "Set Dinner Chair Layer");
                chairTf.gameObject.layer = interactLayer;
            }

            if (chairTf.GetComponent<Collider>() == null)
            {
                BoxCollider box = Undo.AddComponent<BoxCollider>(chairTf.gameObject);
                box.center = new Vector3(0f, 0.45f, 0f);
                box.size = new Vector3(0.55f, 0.95f, 0.55f);
            }

            DinnerChairInteractable interactable = chairTf.GetComponent<DinnerChairInteractable>();
            if (interactable == null)
            {
                interactable = Undo.AddComponent<DinnerChairInteractable>(chairTf.gameObject);
            }

            TMP_FontAsset font = ResolveSharedFont(scene);
            NPCConversation dinnerConv = EnsureDinnerConversation(chairTf, font);

            GameObject applePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ApplePrefabPath);
            GameObject vodkaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VodkaPrefabPath);
            GameObject breadPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BreadPrefabPath);

            var so = new SerializedObject(interactable);
            SetObj(so, "_tableTransform", tableTf);
            SetObj(so, "_sayaTransform", sayaGo != null ? sayaGo.transform : null);
            SetObj(so, "_applePrefab", applePrefab);
            SetObj(so, "_vodkaPrefab", vodkaPrefab);
            SetObj(so, "_breadPrefab", breadPrefab);
            SetObj(so, "_dinnerConversation", dinnerConv);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(chairTf.gameObject);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log($"[DinnerCutsceneSetup] Настроен '{chairTf.name}': префабы магазина ({applePrefab?.name}, {vodkaPrefab?.name}, {breadPrefab?.name}) и диалог '{dinnerConv.gameObject.name}'.");
        }

        private static NPCConversation EnsureDinnerConversation(Transform parent, TMP_FontAsset font)
        {
            const string childName = "DinnerAfterFoodConversation";
            Transform existing = parent.Find(childName);
            GameObject go = existing != null ? existing.gameObject : new GameObject(childName);
            if (existing == null)
            {
                Undo.RegisterCreatedObjectUndo(go, "Create " + childName);
                go.transform.SetParent(parent, false);
            }

            NPCConversation conv = go.GetComponent<NPCConversation>() ?? Undo.AddComponent<NPCConversation>(go);
            Undo.RecordObject(conv, "Setup Dinner Conversation");
            conv.DefaultName = "You";
            conv.DefaultFont = font;
            conv.ContinueFont = font;
            conv.EndConversationFont = font;

            var lines = new (int id, string speaker, string text)[]
            {
                (401, "You", "Wait... why aren't you eating? You said you were starving."),
                (402, "Saya", "...I'm not hungry anymore. Finish your food — you need your strength."),
                (403, "You", "I risked my life out there for this. Why won't you touch a single bite?"),
                (404, "Saya", "...Don't ask questions you don't want answered. Just rest for a moment.")
            };

            var ec = new EditableConversation
            {
                Parameters = new List<EditableParameter>()
            };

            EditableSpeechNode prev = null;
            for (int i = 0; i < lines.Length; i++)
            {
                var spec = lines[i];
                var node = new EditableSpeechNode
                {
                    ID = spec.id,
                    Name = spec.speaker,
                    Text = spec.text,
                    TMPFont = font,
                    AdvanceDialogueAutomatically = true,
                    AutoAdvanceShouldDisplayOption = true,
                    TimeUntilAdvance = 5.0f,
                    Volume = 1f,
                    ParamActions = new List<EditableSetParamAction>(),
                    Connections = new List<EditableConnection>(),
                    EditorInfo = new EditableConversationNode.EditorArgs
                    {
                        isRoot = i == 0,
                        xPos = 160f + i * 260f,
                        yPos = 80f
                    }
                };

                ec.SpeechNodes.Add(node);
                if (prev != null) prev.AddSpeech(node);
                prev = node;
            }

            for (int i = 0; i < ec.SpeechNodes.Count; i++)
            {
                ec.SpeechNodes[i].RegisterUIDs();
                ec.SpeechNodes[i].SerializeAssetData(conv);
            }

            conv.CurrentIDCounter = 410;
            conv.Serialize(ec);
            EditorUtility.SetDirty(conv);
            EditorUtility.SetDirty(go);
            return conv;
        }

        private static Transform FindDinnerChair(Transform houseRoot, Transform sayaChair, Transform table)
        {
            // Самый левый и ближний к игроку/входу стул — PFB_DiningChair (по диагонали от Саи на PFB_DiningChair (3))
            Transform frontLeft = FindChildByName(houseRoot, "PFB_DiningChair");
            if (frontLeft != null && frontLeft != sayaChair)
            {
                return frontLeft;
            }

            Transform best = null;
            float bestScore = float.MinValue;
            Vector3 sayaPos = sayaChair != null ? sayaChair.position : Vector3.zero;
            Vector3 tablePos = table != null ? table.position : Vector3.zero;

            foreach (Transform tf in houseRoot.GetComponentsInChildren<Transform>(true))
            {
                if (!tf.name.StartsWith("PFB_DiningChair", StringComparison.Ordinal)) continue;
                if (tf == sayaChair) continue;

                float score = 0f;
                if (table != null)
                {
                    float distToTable = Vector3.Distance(tf.position, tablePos);
                    if (distToTable > 3.5f) continue;
                    score -= distToTable;
                }
                if (sayaChair != null)
                {
                    score += Vector3.Distance(tf.position, sayaPos) * 0.5f;
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    best = tf;
                }
            }
            return best;
        }

        private static TMP_FontAsset ResolveSharedFont(Scene scene)
        {
            ConversationManager cm = FindFirstInScene<ConversationManager>(scene);
            if (cm != null)
            {
                if (cm.DialogueText != null && cm.DialogueText.font != null) return cm.DialogueText.font;
                if (cm.NameText != null && cm.NameText.font != null) return cm.NameText.font;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (NPCConversation existing in root.GetComponentsInChildren<NPCConversation>(true))
                {
                    if (existing != null && existing.DefaultFont != null) return existing.DefaultFont;
                }
            }

            return TMP_Settings.defaultFontAsset;
        }

        private static Transform FindTransformByName(Scene scene, string targetName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = FindChildByName(root.transform, targetName);
                if (found != null) return found;
            }
            return null;
        }

        private static Transform FindChildByName(Transform root, string targetName)
        {
            if (root == null) return null;
            foreach (Transform tf in root.GetComponentsInChildren<Transform>(true))
            {
                if (tf != null && tf.name == targetName) return tf;
            }
            return null;
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

        private static void SetObj(SerializedObject so, string propName, UnityEngine.Object value)
        {
            SerializedProperty p = so.FindProperty(propName);
            if (p != null) p.objectReferenceValue = value;
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
