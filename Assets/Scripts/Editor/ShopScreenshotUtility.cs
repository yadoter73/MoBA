#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoBA.Editor
{
    /// <summary>
    /// Сэмплер верхних граней мешей магазина (Woods.008 / Woods.010) и утилита скриншота прилавка.
    /// </summary>
    public static class ShopScreenshotUtility
    {
        public const string ScreenshotAssetPath = "Assets/Screenshots/Shop_Counter.png";

        public struct SurfaceTriangle
        {
            public Vector3 A;
            public Vector3 B;
            public Vector3 C;
            public float MinX;
            public float MaxX;
            public float MinZ;
            public float MaxZ;
        }

        public static List<SurfaceTriangle> ExtractUpwardTriangles(GameObject shop, string partName, float minY, float maxY)
        {
            var result = new List<SurfaceTriangle>();
            if (shop == null) return result;

            Transform target = null;
            foreach (Transform t in shop.GetComponentsInChildren<Transform>(true))
            {
                if (string.Equals(t.name, partName, StringComparison.Ordinal))
                {
                    target = t;
                    break;
                }
            }
            if (target == null) return result;

            MeshFilter mf = target.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return result;

            Vector3[] verts = mf.sharedMesh.vertices;
            int[] tris = mf.sharedMesh.triangles;
            Matrix4x4 ltw = target.localToWorldMatrix;

            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = ltw.MultiplyPoint3x4(verts[tris[i]]);
                Vector3 b = ltw.MultiplyPoint3x4(verts[tris[i + 1]]);
                Vector3 c = ltw.MultiplyPoint3x4(verts[tris[i + 2]]);
                if (Vector3.Cross(b - a, c - a).normalized.y < 0.90f) continue;

                float avgY = (a.y + b.y + c.y) / 3f;
                if (avgY < minY || avgY > maxY) continue;

                result.Add(new SurfaceTriangle
                {
                    A = a,
                    B = b,
                    C = c,
                    MinX = Mathf.Min(a.x, Mathf.Min(b.x, c.x)),
                    MaxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x)),
                    MinZ = Mathf.Min(a.z, Mathf.Min(b.z, c.z)),
                    MaxZ = Mathf.Max(a.z, Mathf.Max(b.z, c.z))
                });
            }

            return result;
        }

        public static bool TrySampleSurfaceY(List<SurfaceTriangle> tris, float x, float z, float targetY, float toleranceY, out float y)
        {
            y = targetY;
            float bestDiff = float.MaxValue;
            bool found = false;

            for (int i = 0; i < tris.Count; i++)
            {
                SurfaceTriangle t = tris[i];
                if (x < t.MinX - 0.02f || x > t.MaxX + 0.02f || z < t.MinZ - 0.02f || z > t.MaxZ + 0.02f) continue;
                if (!PointInTriangleXZ(x, z, t.A, t.B, t.C, out float u, out float v, out float w)) continue;

                float sampleY = u * t.A.y + v * t.B.y + w * t.C.y;
                float diff = Mathf.Abs(sampleY - targetY);
                if (diff <= toleranceY && diff < bestDiff)
                {
                    bestDiff = diff;
                    y = sampleY;
                    found = true;
                }
            }
            return found;
        }

        private static bool PointInTriangleXZ(float px, float pz, Vector3 a, Vector3 b, Vector3 c, out float u, out float v, out float w)
        {
            float v0x = c.x - a.x, v0z = c.z - a.z;
            float v1x = b.x - a.x, v1z = b.z - a.z;
            float v2x = px - a.x, v2z = pz - a.z;

            float dot00 = v0x * v0x + v0z * v0z;
            float dot01 = v0x * v1x + v0z * v1z;
            float dot02 = v0x * v2x + v0z * v2z;
            float dot11 = v1x * v1x + v1z * v1z;
            float dot12 = v1x * v2x + v1z * v2z;

            float denom = dot00 * dot11 - dot01 * dot01;
            if (Mathf.Abs(denom) < 1e-6f)
            {
                u = v = w = 0f;
                return false;
            }

            float inv = 1f / denom;
            w = (dot11 * dot02 - dot01 * dot12) * inv;
            v = (dot00 * dot12 - dot01 * dot02) * inv;
            u = 1f - v - w;
            return u >= -0.02f && v >= -0.02f && w >= -0.02f;
        }

        [MenuItem("MoBA/Capture Shop Counter Screenshot")]
        public static void CaptureShopCounterMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || !string.Equals(scene.name, "Level1", StringComparison.OrdinalIgnoreCase))
            {
                scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Level1.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            }
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[ShopScreenshot] Нет открытой активной сцены.");
                return;
            }

            CaptureCounterScreenshot(scene);
            AssetDatabase.Refresh();
        }

        public static void CaptureCounterScreenshot(Scene scene)
        {
            const int width = 1600;
            const int height = 900;

            string dir = Path.GetDirectoryName(ScreenshotAssetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var camGo = new GameObject("__TempShopPreviewCam");
            var savedMats = new List<(Renderer r, Material[] mats)>();
            var tempMats = new List<Material>();
            var disabledCanvases = new List<Canvas>();
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);

            RenderTexture prevActive = RenderTexture.active;
            bool prevAllowAsync = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");

                foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
                {
                    if (c.enabled) { c.enabled = false; disabledCanvases.Add(c); }
                }

                // Для надёжного превью без артефактов URP-освещения в -batchmode временно подменяем материалы в памяти
                if (unlitShader != null)
                {
                    foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
                    {
                        if (r.bounds.size == Vector3.zero) continue;
                        if (Vector3.Distance(r.bounds.center, new Vector3(195f, 11f, 140f)) > 45f) continue;

                        Material[] orig = r.sharedMaterials;
                        savedMats.Add((r, orig));
                        var preview = new Material[orig.Length];
                        for (int i = 0; i < orig.Length; i++)
                        {
                            Material src = orig[i];
                            var m = new Material(unlitShader);
                            tempMats.Add(m);
                            if (src != null)
                            {
                                Texture baseMap = src.HasProperty("_BaseMap") ? src.GetTexture("_BaseMap") : null;
                                if (baseMap == null && src.HasProperty("_MainTex")) baseMap = src.GetTexture("_MainTex");
                                Color baseCol = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : (src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white);
                                float shade = r.gameObject.name.Contains("Wall") || r.gameObject.name.Contains("Woods") ? 0.65f : 1.0f;
                                if (r.gameObject.name == "Woods.008") shade = 0.85f;
                                Color tint = new Color(baseCol.r * shade, baseCol.g * shade, baseCol.b * shade, baseCol.a);

                                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", baseMap);
                                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", baseMap);
                                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
                                if (m.HasProperty("_Color")) m.SetColor("_Color", tint);

                                bool isTrans = src.HasProperty("_Surface") && src.GetFloat("_Surface") > 0.5f;
                                if (isTrans && m.HasProperty("_Surface"))
                                {
                                    m.SetFloat("_Surface", 1f);
                                    m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                                    m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                                    m.SetFloat("_ZWrite", 0f);
                                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                                    m.renderQueue = 3000;
                                }
                            }
                            preview[i] = m;
                        }
                        r.sharedMaterials = preview;
                    }
                }

                Camera mainCam = Camera.main;
                var brain = mainCam != null ? mainCam.GetComponent("CinemachineBrain") as Behaviour : null;
                bool prevBrain = brain != null && brain.enabled;
                Vector3 prevCamPos = mainCam != null ? mainCam.transform.position : Vector3.zero;
                Quaternion prevCamRot = mainCam != null ? mainCam.transform.rotation : Quaternion.identity;
                float prevFov = mainCam != null ? mainCam.fieldOfView : 60f;
                RenderTexture prevTarget = mainCam != null ? mainCam.targetTexture : null;
                try
                {
                    Camera renderCam = mainCam != null ? mainCam : camGo.AddComponent<Camera>();
                    if (brain != null) brain.enabled = false;
                    renderCam.fieldOfView = 58f;
                    renderCam.nearClipPlane = 0.05f;
                    renderCam.farClipPlane = 150f;
                    renderCam.transform.position = new Vector3(191.5f, 14.0f, 151.8f);
                    renderCam.transform.LookAt(new Vector3(196.2f, 11.6f, 140.5f));
                    renderCam.targetTexture = rt;
                    renderCam.Render();
                    renderCam.targetTexture = prevTarget;
                }
                finally
                {
                    if (mainCam != null)
                    {
                        mainCam.transform.position = prevCamPos;
                        mainCam.transform.rotation = prevCamRot;
                        mainCam.fieldOfView = prevFov;
                        mainCam.targetTexture = prevTarget;
                        if (brain != null) brain.enabled = prevBrain;
                    }
                }

                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();

                File.WriteAllBytes(ScreenshotAssetPath, tex.EncodeToPNG());
                Debug.Log($"[ShopScreenshot] Скриншот сохранён: {ScreenshotAssetPath}");
            }
            finally
            {
                for (int i = 0; i < savedMats.Count; i++)
                {
                    if (savedMats[i].r != null) savedMats[i].r.sharedMaterials = savedMats[i].mats;
                }
                for (int i = 0; i < tempMats.Count; i++)
                {
                    if (tempMats[i] != null) UnityEngine.Object.DestroyImmediate(tempMats[i]);
                }
                foreach (var c in disabledCanvases) if (c != null) c.enabled = true;

                ShaderUtil.allowAsyncCompilation = prevAllowAsync;
                RenderTexture.active = prevActive;
                UnityEngine.Object.DestroyImmediate(tex);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(camGo);
            }
        }
    }
}
#endif
