#if UNITY_EDITOR
using System.Text;
using DialogueEditor;
using MoBA.Interaction;
using MoBA.Narrative;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoBA.Editor
{
    public static class FixWallAndGateIssues
    {
        [MenuItem("MoBA/Diagnose And Fix Wall, Gate, Saya, Doors")]
        public static void RunDiagnosticAndFix()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene("Assets/Scenes/Level1.unity", OpenSceneMode.Single);
            }

            int interactLayer = LayerMask.NameToLayer("Interact");
            if (interactLayer < 0) interactLayer = 9;

            var sb = new StringBuilder();
            sb.AppendLine("=== DIAGNOSE AND FIX REPORT ===");

            // 1. Фикс дверей MainHouse (leftDoor, rightDoor)
            GameObject mainHouse = FindObjectByName(scene, "MainHouse");
            if (mainHouse != null)
            {
                FixMainHouseDoors(mainHouse, interactLayer, sb);
                FixMainHouseInteriorWallColliders(mainHouse, sb);
            }
            else
            {
                sb.AppendLine("[-] MainHouse не найден на сцене!");
            }

            // 2. Фикс калитки (Shop gate door)
            GameObject shop = FindObjectByName(scene, "Shop");
            if (shop != null)
            {
                FixShopGate(shop, interactLayer, sb);
            }
            else
            {
                sb.AppendLine("[-] Shop не найден на сцене!");
            }

            // 3. Фикс диалогов Саи и монстра (включая RepeatConversation для Саи)
            StoryQuestDialogueBuilder.FixAllDialoguesAutoEndMenu();
            sb.AppendLine("[+] Выполнен StoryQuestDialogueBuilder.FixAllDialoguesAutoEndMenu");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();

            Debug.Log(sb.ToString());
        }

        private static void FixMainHouseDoors(GameObject mainHouse, int interactLayer, StringBuilder sb)
        {
            Transform leftDoorTf = FindDeepChild(mainHouse.transform, "leftDoor");
            Transform rightDoorTf = FindDeepChild(mainHouse.transform, "rightDoor");

            if (leftDoorTf != null)
            {
                SetupSingleHouseDoor(leftDoorTf, 88f, interactLayer, sb, "leftDoor");
            }
            if (rightDoorTf != null)
            {
                SetupSingleHouseDoor(rightDoorTf, -88f, interactLayer, sb, "rightDoor");
            }
        }

        private static void SetupSingleHouseDoor(Transform doorTf, float openAngle, int interactLayer, StringBuilder sb, string name)
        {
            doorTf.gameObject.layer = interactLayer;

            BoxCollider box = doorTf.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = Undo.AddComponent<BoxCollider>(doorTf.gameObject);
                box.isTrigger = false;
                box.center = new Vector3(0.42f, 0f, 0f);
                box.size = new Vector3(0.85f, 2.1f, 0.12f);
                sb.AppendLine($"[+] {name}: добавлен BoxCollider");
            }
            else
            {
                box.enabled = true;
                box.isTrigger = false;
            }

            Door doorComp = doorTf.GetComponent<Door>();
            if (doorComp == null)
            {
                doorComp = Undo.AddComponent<Door>(doorTf.gameObject);
                sb.AppendLine($"[+] {name}: добавлен компонент Door");
            }

            var so = new SerializedObject(doorComp);
            so.FindProperty("_doorId").intValue = 0;
            so.FindProperty("_requiresKey").boolValue = false;
            so.FindProperty("_angle").floatValue = openAngle;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(doorTf.gameObject);
            sb.AppendLine($"[OK] {name}: настроен Door (angle={openAngle}) на слое Interact ({interactLayer})");
        }

        private static void FixMainHouseInteriorWallColliders(GameObject mainHouse, StringBuilder sb)
        {
            // Проверяем наличие BoxCollider на разделительных внутренних стенах между гостиной и спальней/ванной
            const string interiorWallName = "InteriorWallObstacleCollider";
            Transform existing = mainHouse.transform.Find(interiorWallName);
            GameObject wallGo = existing != null ? existing.gameObject : new GameObject(interiorWallName);
            if (existing == null)
            {
                Undo.RegisterCreatedObjectUndo(wallGo, "Create InteriorWallObstacleCollider");
                wallGo.transform.SetParent(mainHouse.transform, false);
            }

            wallGo.transform.localPosition = new Vector3(0.85f, 1.8f, 1.2f);
            wallGo.transform.localRotation = Quaternion.identity;
            wallGo.layer = 0; // Default layer

            BoxCollider box = wallGo.GetComponent<BoxCollider>();
            if (box == null) box = Undo.AddComponent<BoxCollider>(wallGo);
            box.isTrigger = false;
            // Стена от пола до потолка, разделяющая гостиную с камином от задних комнат
            box.size = new Vector3(0.25f, 3.4f, 7.5f);
            box.center = Vector3.zero;

            EditorUtility.SetDirty(wallGo);
            sb.AppendLine($"[OK] Разделительный коллайдер внутренней стены {interiorWallName} установлен.");
        }

        private static void FixShopGate(GameObject shop, int interactLayer, StringBuilder sb)
        {
            Door[] doors = shop.GetComponentsInChildren<Door>(true);
            sb.AppendLine($"Внутри Shop найдено {doors.Length} компонентов Door.");

            Door gateDoor = null;
            foreach (Door d in doors)
            {
                var so = new SerializedObject(d);
                if (so.FindProperty("_requiresKey")?.boolValue == true)
                {
                    gateDoor = d;
                    break;
                }
            }
            if (gateDoor == null && doors.Length > 0)
            {
                gateDoor = doors[0];
            }

            if (gateDoor != null)
            {
                GameObject gateGo = gateDoor.gameObject;
                gateGo.layer = interactLayer;

                // Убеждаемся, что у калитки есть удобный BoxCollider для взаимодействия с обеих сторон
                BoxCollider box = gateGo.GetComponent<BoxCollider>();
                if (box == null)
                {
                    box = Undo.AddComponent<BoxCollider>(gateGo);
                    sb.AppendLine("[+] На калитку Shop добавлен BoxCollider");
                }
                box.enabled = true;
                box.isTrigger = false;

                // Если уже был MeshCollider, делаем его невыпуклым или оставляем оба
                MeshCollider mc = gateGo.GetComponent<MeshCollider>();
                if (mc != null)
                {
                    box.center = mc.bounds.center - gateGo.transform.position;
                    box.size = new Vector3(Mathf.Max(mc.bounds.size.x, 0.4f), Mathf.Max(mc.bounds.size.y, 0.9f), Mathf.Max(mc.bounds.size.z, 0.15f));
                }
                else
                {
                    box.size = new Vector3(1.2f, 1.0f, 0.2f);
                    box.center = new Vector3(0.6f, 0.5f, 0f);
                }

                // Гарантируем настройки Door
                var so = new SerializedObject(gateDoor);
                so.FindProperty("_requiresKey").boolValue = true;
                so.FindProperty("_doorId").intValue = 0;
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(gateGo);
                sb.AppendLine($"[OK] Калитка Shop {gateGo.name} настроена на слое Interact с BoxCollider.");
            }
            else
            {
                sb.AppendLine("[-] Калитка Door в Shop не найдена!");
            }
        }

        private static Transform FindDeepChild(Transform parent, string targetName)
        {
            if (parent == null) return null;
            if (parent.name == targetName) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDeepChild(parent.GetChild(i), targetName);
                if (found != null) return found;
            }
            return null;
        }

        private static GameObject FindObjectByName(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
                Transform t = FindDeepChild(root.transform, name);
                if (t != null) return t.gameObject;
            }
            return null;
        }
    }
}
#endif
