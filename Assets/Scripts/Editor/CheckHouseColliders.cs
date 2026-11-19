#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoBA.Editor
{
    public static class CheckHouseColliders
    {
        [MenuItem("MoBA/Debug/Check House Wall Colliders")]
        public static void CheckWalls()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject mainHouse = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "MainHouse") { mainHouse = root; break; }
            }
            if (mainHouse == null)
            {
                Debug.LogError("MainHouse not found");
                return;
            }

            Debug.Log($"=== Inspecting MainHouse ({mainHouse.name}) ===");
            var colliders = mainHouse.GetComponentsInChildren<Collider>(true);
            Debug.Log($"Found {colliders.Length} colliders in MainHouse.");
            foreach (var col in colliders)
            {
                Debug.Log($"Col: {col.gameObject.name} ({col.GetType().Name}), layer={LayerMask.LayerToName(col.gameObject.layer)} ({col.gameObject.layer}), isTrigger={col.isTrigger}, bounds center={col.bounds.center}, size={col.bounds.size}", col);
            }
        }
    }
}
#endif
