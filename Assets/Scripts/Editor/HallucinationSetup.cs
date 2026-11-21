#if UNITY_EDITOR
using System.Collections.Generic;
using MoBA.Hallucinations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MoBA.Editor
{
    /// <summary>
    /// Автоматическая настройка механики галлюцинаций на активной сцене:
    /// создаёт тёмный URP-материал, визуальный префаб монстра-тени и объект [HallucinationSystem].
    /// </summary>
    public static class HallucinationSetup
    {
        private const string SystemObjectName = "[HallucinationSystem]";
        private const string DarkMaterialPath = "Assets/Materials/HallucinationShadow_URP.mat";
        private const string ShadowPrefabPath = "Assets/Prefabs/HallucinationShadow.prefab";
        private const string SourceMonsterPrefabPath = "Assets/Prefabs/Agent.prefab";

        [MenuItem("MoBA/Setup Hallucinations")]
        public static void SetupHallucinations()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[HallucinationSetup] Нет открытой загруженной сцены.");
                return;
            }

            Material darkMat = EnsureDarkShadowMaterial();
            GameObject shadowPrefab = EnsureShadowMonsterPrefab(darkMat);
            MirrorReflection[] mirrors = FindAllInScene<MirrorReflection>(scene);

            GameObject systemGo = FindRootByName(scene, SystemObjectName);
            if (systemGo == null)
            {
                systemGo = new GameObject(SystemObjectName);
                Undo.RegisterCreatedObjectUndo(systemGo, "Create HallucinationSystem");
            }

            HallucinationShadowRunner runner = systemGo.GetComponent<HallucinationShadowRunner>();
            if (runner == null) runner = Undo.AddComponent<HallucinationShadowRunner>(systemGo);

            HallucinationDirector director = systemGo.GetComponent<HallucinationDirector>();
            if (director == null) director = Undo.AddComponent<HallucinationDirector>(systemGo);

            var runnerSo = new SerializedObject(runner);
            runnerSo.FindProperty("_shadowMonsterPrefab").objectReferenceValue = shadowPrefab;
            runnerSo.FindProperty("_darkShadowMaterial").objectReferenceValue = darkMat;
            runnerSo.FindProperty("_targetHeight").floatValue = 7.2f;
            runnerSo.FindProperty("_frontDistance").floatValue = 9.0f;
            runnerSo.FindProperty("_frontMinDistance").floatValue = 4.5f;
            runnerSo.FindProperty("_frontLateralSpan").floatValue = 22.0f;
            runnerSo.FindProperty("_frontRunDuration").floatValue = 0.65f;
            runnerSo.FindProperty("_mirrorBackDistance").floatValue = 4.8f;
            runnerSo.FindProperty("_mirrorMinBackDistance").floatValue = 2.5f;
            runnerSo.FindProperty("_mirrorLateralSpan").floatValue = 14.0f;
            runnerSo.FindProperty("_mirrorRunDuration").floatValue = 0.60f;
            runnerSo.ApplyModifiedPropertiesWithoutUndo();

            var directorSo = new SerializedObject(director);
            directorSo.FindProperty("_runner").objectReferenceValue = runner;
            directorSo.FindProperty("_mirrorMaxDistance").floatValue = 14.0f;
            directorSo.FindProperty("_mirrorMaxLookAngleDegrees").floatValue = 35.0f;
            SerializedProperty mirrorsProp = directorSo.FindProperty("_mirrors");
            mirrorsProp.arraySize = mirrors.Length;
            for (int i = 0; i < mirrors.Length; i++)
            {
                mirrorsProp.GetArrayElementAtIndex(i).objectReferenceValue = mirrors[i];
            }
            directorSo.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(systemGo);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"[HallucinationSetup] Галлюцинации настроены на сцене '{scene.name}'. Найдено зеркал: {mirrors.Length}, префаб: {(shadowPrefab != null ? shadowPrefab.name : "procedural fallback")}.");
        }

        [MenuItem("MoBA/Debug/Test Hallucination (Front)", true)]
        [MenuItem("MoBA/Debug/Test Hallucination (Mirror)", true)]
        private static bool ValidatePlayModeOnly()
        {
            return Application.isPlaying;
        }

        [MenuItem("MoBA/Debug/Test Hallucination (Front)")]
        private static void TestFrontHallucination()
        {
            HallucinationDirector director = Object.FindAnyObjectByType<HallucinationDirector>();
            if (director == null)
            {
                Debug.LogError("[HallucinationSetup] На сцене нет HallucinationDirector. Сначала запустите MoBA/Setup Hallucinations.");
                return;
            }
            director.DebugTriggerFrontHallucination();
        }

        [MenuItem("MoBA/Debug/Test Hallucination (Mirror)")]
        private static void TestMirrorHallucination()
        {
            HallucinationDirector director = Object.FindAnyObjectByType<HallucinationDirector>();
            if (director == null)
            {
                Debug.LogError("[HallucinationSetup] На сцене нет HallucinationDirector. Сначала запустите MoBA/Setup Hallucinations.");
                return;
            }
            director.DebugTriggerMirrorHallucination();
        }

        private static Material EnsureDarkShadowMaterial()
        {
            EnsureFolder("Assets/Materials");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(DarkMaterialPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");

            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, DarkMaterialPath);
            }
            else if (shader != null && mat.shader != shader)
            {
                mat.shader = shader;
            }

            Color veryDark = new Color(0.08f, 0.08f, 0.11f, 1f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", veryDark);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", veryDark);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.08f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject EnsureShadowMonsterPrefab(Material darkMat)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceMonsterPrefabPath);
            if (source == null)
            {
                GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(ShadowPrefabPath);
                if (existing != null) return existing;
                Debug.LogWarning($"[HallucinationSetup] Исходный префаб '{SourceMonsterPrefabPath}' не найден — будет использован процедурный силуэт.");
                return null;
            }

            GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                PrefabUtility.UnpackPrefabInstance(temp, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                temp.name = "HallucinationShadow";
                SetLayerRecursive(temp, 0);

                foreach (Collider col in temp.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
                foreach (Rigidbody rb in temp.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
                foreach (NavMeshAgent agent in temp.GetComponentsInChildren<NavMeshAgent>(true)) Object.DestroyImmediate(agent);
                foreach (EnemyAI ai in temp.GetComponentsInChildren<EnemyAI>(true)) Object.DestroyImmediate(ai);
                foreach (AudioSource audio in temp.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(audio);

                foreach (Renderer rend in temp.GetComponentsInChildren<Renderer>(true))
                {
                    rend.shadowCastingMode = ShadowCastingMode.Off;
                    rend.receiveShadows = false;
                    if (rend is SkinnedMeshRenderer smr)
                    {
                        smr.updateWhenOffscreen = true;
                    }
                    Material[] mats = new Material[Mathf.Max(1, rend.sharedMaterials.Length)];
                    for (int i = 0; i < mats.Length; i++) mats[i] = darkMat;
                    rend.sharedMaterials = mats;
                }

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(temp, ShadowPrefabPath);
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            string[] parts = assetPath.Split('/');
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

        private static GameObject FindRootByName(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
            }
            return null;
        }

        private static T[] FindAllInScene<T>(Scene scene) where T : Component
        {
            var list = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                list.AddRange(root.GetComponentsInChildren<T>(true));
            }
            return list.ToArray();
        }

        private static void SetLayerRecursive(GameObject obj, int layer)
        {
            obj.layer = layer;
            Transform tf = obj.transform;
            for (int i = 0; i < tf.childCount; i++)
            {
                SetLayerRecursive(tf.GetChild(i).gameObject, layer);
            }
        }
    }
}
#endif
