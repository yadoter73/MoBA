#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DialogueEditor;
using MoBA.Interaction;
using MoBA.Narrative;
using MoBA.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoBA.Editor
{
    /// <summary>
    /// Настраивает диалоги Саи и монстра-продавца (NPCConversation с понятными ID нод) и HUD-счётчик продуктов.
    /// </summary>
    public static class StoryQuestDialogueBuilder
    {
        public readonly struct DialogueLineSpec
        {
            public readonly int Id;
            public readonly string Speaker;
            public readonly string Text;

            public DialogueLineSpec(int id, string speaker, string text)
            {
                Id = id;
                Speaker = speaker;
                Text = text;
            }
        }

        public static readonly DialogueLineSpec[] SayaIntroLines =
        {
            new DialogueLineSpec(101, "Saya", "You... you came back for me. Thank you... I thought the dark out there had taken you too."),
            new DialogueLineSpec(102, "Saya", "Catch your breath for a second... My legs are still shaking after what dragged me toward the lake."),
            new DialogueLineSpec(103, "Saya", "Look around... there is not a scrap of food or clean water left in this house."),
            new DialogueLineSpec(104, "Saya", "I cannot walk back out into those woods alone... If we stay hungry like this, we will not last the night."),
            new DialogueLineSpec(105, "Saya", "Past the fence, down by the lake, there is an old gray building... an abandoned grocery store."),
            new DialogueLineSpec(106, "Saya", "Please, go there and bring back whatever food is still untouched — something to keep us going."),
            new DialogueLineSpec(107, "Saya", "Wait... the iron gate outside is locked. Nobody has used that shop since the mist swallowed the road."),
            new DialogueLineSpec(108, "Saya", "I hid the spare gate key inside the chest of drawers right here in this house. Check the drawers carefully."),
            new DialogueLineSpec(109, "Saya", "Keep your voice down once you step outside. Do not wander near the shore, and do not let \"them\" hear you."),
            new DialogueLineSpec(110, "Saya", "Find the key in the drawer, reach that gray store, and hurry back to me. Please.")
        };

        public static readonly DialogueLineSpec[] MonsterOrderLines =
        {
            new DialogueLineSpec(201, "Shopkeeper", "...Oh. A customer... How wonderfully... unexpected."),
            new DialogueLineSpec(202, "Shopkeeper", "The gate hinges screamed... yet you still stepped inside. Nobody has crossed my threshold in... oh, so many winters..."),
            new DialogueLineSpec(203, "Shopkeeper", "Do not mind the dust... or the quiet. Tell me, traveler... what brings a living voice to my counter?"),
            new DialogueLineSpec(204, "You", "We're trapped in the house across the woods. Saya needs supplies — vodka, three apples, and a loaf of bread."),
            new DialogueLineSpec(205, "Shopkeeper", "...Vodka. Three apples. Bread... Such ordinary, fragile little things for a night like this..."),
            new DialogueLineSpec(206, "Shopkeeper", "She is waiting for you, isn't she...? Listening at the floorboards... counting every breath until the door opens..."),
            new DialogueLineSpec(207, "You", "How much do we owe you? I don't have any money left on me."),
            new DialogueLineSpec(208, "Shopkeeper", "Money...? No, no... Paper and copper mean very little when the road outside forgets where it leads..."),
            new DialogueLineSpec(209, "Shopkeeper", "...Consider it free for now... You will pay me another night... with the story of how long you both lasted."),
            new DialogueLineSpec(210, "Shopkeeper", "Wait right there... One bottle of vodka, kept cold behind the counter... Three apples, still firm enough to bite..."),
            new DialogueLineSpec(211, "Shopkeeper", "...And a loaf of bread. Let me set them out on the counter for you..."),
            new DialogueLineSpec(212, "Shopkeeper", "Take everything from the counter, traveler... Every last piece... Before the mist grows curious about us both."),
            new DialogueLineSpec(213, "Shopkeeper", "...Go on back to her now. And do mind the dark... I would hate to lose my favorite guest...")
        };

        public static readonly DialogueLineSpec[] MonsterRepeatLines =
        {
            new DialogueLineSpec(251, "Shopkeeper", "...You already have my wares... Go on, hurry back to her... Come again soon...")
        };

        public static readonly DialogueLineSpec[] SayaRepeatLines =
        {
            new DialogueLineSpec(151, "Saya", "Did you find the key in the chest of drawers? Please, hurry to that gray store past the gate... and be careful out there.")
        };

        public static readonly DialogueLineSpec[] SayaFinalLines =
        {
            new DialogueLineSpec(301, "Saya", "You made it back... Every time the wind hit the porch I feared the worst."),
            new DialogueLineSpec(302, "Saya", "Vodka, apples, bread... Thank you. You actually went through all of that for us."),
            new DialogueLineSpec(303, "Saya", "Leave them by the table and rest a moment... At least tonight, we are still alive.")
        };

        [MenuItem("MoBA/Fix All Dialogues Auto-End")]
        public static void FixAllDialoguesAutoEndMenu()
        {
            Scene active = SceneManager.GetActiveScene();
            Scene scene = active.IsValid() && string.Equals(active.name, "Level1", StringComparison.OrdinalIgnoreCase)
                ? active
                : EditorSceneManager.OpenScene("Assets/Scenes/Level1.unity", OpenSceneMode.Single);

            int fixedNodes = EnsureAllConversationsAutoAdvance(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log($"[StoryQuestDialogueBuilder] Исправлено нод диалогов (AdvanceDialogueAutomatically=true): {fixedNodes}");
        }

        public static int EnsureAllConversationsAutoAdvance(Scene scene)
        {
            int fixedCount = 0;
            foreach (NPCConversation conv in FindAllInScene<NPCConversation>(scene))
            {
                if (conv == null) continue;
                EditableConversation ec = conv.DeserializeForEditor();
                if (ec == null || ec.SpeechNodes == null || ec.SpeechNodes.Count == 0) continue;

                bool changed = false;
                for (int i = 0; i < ec.SpeechNodes.Count; i++)
                {
                    EditableSpeechNode node = ec.SpeechNodes[i];
                    if (node == null) continue;

                    bool isLeafNode = node.Connections == null || node.Connections.Count == 0;
                    if (!node.AdvanceDialogueAutomatically)
                    {
                        node.AdvanceDialogueAutomatically = true;
                        changed = true;
                        fixedCount++;
                    }
                    if (!Mathf.Approximately(node.TimeUntilAdvance, 5.0f))
                    {
                        node.TimeUntilAdvance = 5.0f;
                        changed = true;
                    }
                }

                if (changed)
                {
                    Undo.RecordObject(conv, "Enable Auto-Advance on All Dialogue Nodes");
                    conv.Serialize(ec);
                    EditorUtility.SetDirty(conv);
                    Debug.Log($"[StoryQuestDialogueBuilder] Обновлён диалог '{conv.gameObject.name}' ({ec.SpeechNodes.Count} реплик, автозавершение включено).");
                }
            }

            return fixedCount;
        }

        [MenuItem("MoBA/Fix Saya Talk")]
        public static void FixSayaAndMonsterTalkMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !string.Equals(scene.name, "Level1", StringComparison.OrdinalIgnoreCase))
            {
                scene = EditorSceneManager.OpenScene("Assets/Scenes/Level1.unity", OpenSceneMode.Single);
            }
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[FixSayaTalk] Нет активной загруженной сцены Level1.");
                return;
            }

            SetupOnScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[FixSayaTalk] Коллайдер, слой Interact, InteractionManager._interactRange и ссылки NPC обновлены.");
        }

        [MenuItem("MoBA/Setup Story Quest & Dialogues")]
        public static void SetupStoryQuestAndDialoguesMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[StoryQuest] Нет активной загруженной сцены.");
                return;
            }

            SetupOnScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
        }

        public static void SetupOnScene(Scene scene)
        {
            KeyQuestSceneSetup.SetupKeyQuest();
            ConfigureInteractionManager(scene);

            TMP_FontAsset sharedFont = ResolveSharedFont(scene);
            Stone stone = FindFirstInScene<Stone>(scene);
            GameObject saya = null;
            if (stone != null)
            {
                var stoneSo = new SerializedObject(stone);
                saya = stoneSo.FindProperty("_saya")?.objectReferenceValue as GameObject;
            }

            if (saya != null)
            {
                SetupSayaInteractable(saya, sharedFont);
                WireSayaGuidanceOnRegObj(scene, saya.transform);
            }
            else
            {
                Debug.LogError("[StoryQuest] На Stone не найдена ссылка _saya.");
            }

            MonsterTalkInteractable monster = FindFirstInScene<MonsterTalkInteractable>(scene);
            if (monster != null)
            {
                SetupMonsterConversations(monster, sharedFont);
            }
            else
            {
                Debug.LogError("[StoryQuest] На сцене не найден MonsterTalkInteractable.");
            }

            EnsureGroceryHud(scene, sharedFont);
            Debug.Log("[StoryQuest] Сюжетные диалоги (Saya Intro/Final, Monster Order/Repeat), RegObj и GroceryHudView настроены.");
        }

        private static void ConfigureInteractionManager(Scene scene)
        {
            InteractionManager im = FindFirstInScene<InteractionManager>(scene);
            if (im == null) return;

            int interactLayer = LayerMask.NameToLayer("Interact");
            var so = new SerializedObject(im);
            SerializedProperty maskProp = so.FindProperty("_layerMask");
            if (maskProp != null && interactLayer >= 0)
            {
                maskProp.intValue |= 1 << interactLayer;
            }

            SerializedProperty rangeProp = so.FindProperty("_interactRange");
            if (rangeProp != null && rangeProp.floatValue < 11.5f)
            {
                rangeProp.floatValue = 11.5f;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(im);
        }

        private static void SetupSayaInteractable(GameObject saya, TMP_FontAsset font)
        {
            int interactLayer = LayerMask.NameToLayer("Interact");
            if (interactLayer >= 0 && saya.layer != interactLayer)
            {
                Undo.RecordObject(saya, "Set Saya Interact Layer");
                saya.layer = interactLayer;
            }

            CapsuleCollider cap = saya.GetComponent<CapsuleCollider>();
            if (cap == null)
            {
                cap = Undo.AddComponent<CapsuleCollider>(saya);
                cap.center = new Vector3(0f, 0.86f, 0f);
                cap.radius = 0.34f;
                cap.height = 1.72f;
            }

            // Отдельный хитбокс разговора, чтобы EnsureSayaScaleAndCollision(true) в StoneToCutScene не урезал область наведения
            const string hitboxName = "SayaTalkHitbox";
            Transform existingHitbox = saya.transform.Find(hitboxName);
            GameObject hitboxGo = existingHitbox != null ? existingHitbox.gameObject : new GameObject(hitboxName);
            if (existingHitbox == null)
            {
                Undo.RegisterCreatedObjectUndo(hitboxGo, "Create SayaTalkHitbox");
                hitboxGo.transform.SetParent(saya.transform, false);
            }
            hitboxGo.transform.localPosition = Vector3.zero;
            hitboxGo.transform.localRotation = Quaternion.identity;
            hitboxGo.transform.localScale = Vector3.one;
            if (interactLayer >= 0) hitboxGo.layer = interactLayer;

            BoxCollider talkBox = GetOrAdd<BoxCollider>(hitboxGo);
            talkBox.enabled = true;
            talkBox.isTrigger = false;
            talkBox.center = new Vector3(0f, 1.15f, 0.06f);
            talkBox.size = new Vector3(1.15f, 2.35f, 1.15f);
            EditorUtility.SetDirty(hitboxGo);

            // Удаляем лишний NpcDialoguePrompt: SayaTalkInteractable сам реализует IConditionalInteractionPrompt
            NpcDialoguePrompt legacyPrompt = saya.GetComponent<NpcDialoguePrompt>();
            if (legacyPrompt != null)
            {
                Undo.DestroyObjectImmediate(legacyPrompt);
            }

            SayaTalkInteractable talk = GetOrAdd<SayaTalkInteractable>(saya);
            NPCConversation introConv = EnsureChildConversation(saya.transform, "SayaIntroConversation", "Saya", SayaIntroLines, font);
            NPCConversation repeatConv = EnsureChildConversation(saya.transform, "SayaRepeatConversation", "Saya", SayaRepeatLines, font);
            NPCConversation finalConv = EnsureChildConversation(saya.transform, "SayaFinalConversation", "Saya", SayaFinalLines, font);

            var so = new SerializedObject(talk);
            SerializedProperty introProp = so.FindProperty("_introConversation");
            SerializedProperty repeatProp = so.FindProperty("_repeatConversation");
            SerializedProperty finalProp = so.FindProperty("_finalConversation");
            if (introProp != null) introProp.objectReferenceValue = introConv;
            if (repeatProp != null) repeatProp.objectReferenceValue = repeatConv;
            if (finalProp != null) finalProp.objectReferenceValue = finalConv;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(saya);
        }

        private static void SetupMonsterConversations(MonsterTalkInteractable monster, TMP_FontAsset font)
        {
            int interactLayer = LayerMask.NameToLayer("Interact");
            if (interactLayer >= 0 && monster.gameObject.layer != interactLayer)
            {
                Undo.RecordObject(monster.gameObject, "Set Monster Interact Layer");
                monster.gameObject.layer = interactLayer;
            }

            NpcDialoguePrompt legacyPrompt = monster.GetComponent<NpcDialoguePrompt>();
            if (legacyPrompt != null)
            {
                Undo.DestroyObjectImmediate(legacyPrompt);
            }

            NPCConversation orderConv = EnsureChildConversation(monster.transform, "MonsterStubConversation", "Shopkeeper", MonsterOrderLines, font);
            NPCConversation repeatConv = EnsureChildConversation(monster.transform, "MonsterRepeatConversation", "Shopkeeper", MonsterRepeatLines, font);

            var so = new SerializedObject(monster);
            SerializedProperty orderProp = so.FindProperty("_stubConversation");
            SerializedProperty repeatProp = so.FindProperty("_repeatConversation");
            if (orderProp != null) orderProp.objectReferenceValue = orderConv;
            if (repeatProp != null) repeatProp.objectReferenceValue = repeatConv;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(monster.gameObject);
        }

        private static NPCConversation EnsureChildConversation(
            Transform parent,
            string childName,
            string defaultSpeaker,
            DialogueLineSpec[] lines,
            TMP_FontAsset font)
        {
            Transform child = parent.Find(childName);
            GameObject go = child != null ? child.gameObject : new GameObject(childName);
            if (child == null)
            {
                Undo.RegisterCreatedObjectUndo(go, "Create " + childName);
                go.transform.SetParent(parent, false);
            }

            NPCConversation conv = GetOrAdd<NPCConversation>(go);
            Undo.RecordObject(conv, "Populate " + childName);
            conv.DefaultName = defaultSpeaker;
            conv.DefaultFont = font;
            conv.ContinueFont = font;
            conv.EndConversationFont = font;

            var ec = new EditableConversation
            {
                Parameters = new List<EditableParameter>()
            };
            EditableSpeechNode prevNode = null;
            int maxId = 1;

            for (int i = 0; i < lines.Length; i++)
            {
                DialogueLineSpec spec = lines[i];
                if (spec.Id >= maxId) maxId = spec.Id + 1;

                var node = new EditableSpeechNode
                {
                    ID = spec.Id,
                    Name = spec.Speaker,
                    Text = spec.Text,
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
                if (prevNode != null)
                {
                    prevNode.AddSpeech(node);
                }
                prevNode = node;
            }

            for (int i = 0; i < ec.SpeechNodes.Count; i++)
            {
                ec.SpeechNodes[i].RegisterUIDs();
                ec.SpeechNodes[i].SerializeAssetData(conv);
            }

            conv.CurrentIDCounter = maxId;
            conv.Serialize(ec);
            EditorUtility.SetDirty(conv);
            EditorUtility.SetDirty(go);
            return conv;
        }

        private static void WireSayaGuidanceOnRegObj(Scene scene, Transform sayaTransform)
        {
            RegObj regObj = FindFirstInScene<RegObj>(scene);
            if (regObj == null) return;

            var so = new SerializedObject(regObj);
            SerializedProperty prop = so.FindProperty("_sayaGuidanceTarget");
            if (prop != null)
            {
                prop.objectReferenceValue = sayaTransform;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(regObj);
            }
        }

        private static void EnsureGroceryHud(Scene scene, TMP_FontAsset font)
        {
            TextRenaming objectiveText = FindFirstInScene<TextRenaming>(scene);
            Canvas targetCanvas = objectiveText != null ? objectiveText.GetComponentInParent<Canvas>() : FindFirstInScene<Canvas>(scene);
            if (targetCanvas == null) return;

            Transform existing = targetCanvas.transform.Find("GroceryHudCounter");
            GameObject hudGo = existing != null ? existing.gameObject : new GameObject("GroceryHudCounter");
            if (existing == null)
            {
                Undo.RegisterCreatedObjectUndo(hudGo, "Create GroceryHudCounter");
                hudGo.transform.SetParent(targetCanvas.transform, false);
            }

            RectTransform rt = GetOrAdd<RectTransform>(hudGo);
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -56f);
            rt.sizeDelta = new Vector2(340f, 36f);

            CanvasGroup cg = GetOrAdd<CanvasGroup>(hudGo);
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;

            TextMeshProUGUI tmp = GetOrAdd<TextMeshProUGUI>(hudGo);
            tmp.font = font;
            tmp.fontSize = 22f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.92f, 0.87f, 0.78f, 1f);
            tmp.text = "Продукты: 0/5";
            tmp.raycastTarget = false;

            GroceryHudView view = GetOrAdd<GroceryHudView>(hudGo);
            var so = new SerializedObject(view);
            SerializedProperty textProp = so.FindProperty("_counterText");
            if (textProp != null) textProp.objectReferenceValue = tmp;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hudGo);
        }

        private static TMP_FontAsset ResolveSharedFont(Scene scene)
        {
            TextRenaming textRenaming = FindFirstInScene<TextRenaming>(scene);
            if (textRenaming != null)
            {
                TMP_Text t = textRenaming.GetComponentInChildren<TMP_Text>(true);
                if (t != null && t.font != null) return t.font;
            }

            foreach (NPCConversation existing in FindAllInScene<NPCConversation>(scene))
            {
                if (existing.DefaultFont != null) return existing.DefaultFont;
            }
            return TMP_Settings.defaultFontAsset;
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

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T existing = go.GetComponent<T>();
            return existing != null ? existing : Undo.AddComponent<T>(go);
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
    }
}
#endif
