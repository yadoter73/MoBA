#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MoBA.EditorTools
{
    internal readonly struct HorrorTextureSpec
    {
        public readonly float Brightness;
        public readonly float Desaturate;
        public readonly float GrimeStrength;
        public readonly float WoodAgeStrength;
        public readonly float RustStrength;
        public readonly float BloodStrength;
        public readonly int Seed;

        public HorrorTextureSpec(
            float brightness,
            float desaturate,
            float grimeStrength,
            float woodAgeStrength,
            float rustStrength,
            float bloodStrength,
            int seed)
        {
            Brightness = brightness;
            Desaturate = desaturate;
            GrimeStrength = grimeStrength;
            WoodAgeStrength = woodAgeStrength;
            RustStrength = rustStrength;
            BloodStrength = bloodStrength;
            Seed = seed;
        }
    }

    internal enum BloodDecalKind
    {
        FloorPool = 0,
        WallDrips = 1,
        DragStreak = 2
    }

    /// <summary>
    /// Недеструктивный процедурный генератор состаренных, ржавых и окровавленных текстур для URP-материалов.
    /// </summary>
    internal static class HorrorTextureGenerator
    {
        private const int MaxProcessedSize = 768;

        public static Texture2D GenerateStyledTexture(string sourceAssetPath, string outputAssetPath, HorrorTextureSpec spec)
        {
            if (string.IsNullOrEmpty(sourceAssetPath) || !File.Exists(sourceAssetPath))
            {
                return null;
            }

            byte[] rawBytes = File.ReadAllBytes(sourceAssetPath);
            Texture2D src = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!ImageConversion.LoadImage(src, rawBytes, false))
            {
                UnityEngine.Object.DestroyImmediate(src);
                return null;
            }

            Texture2D working = src;
            int targetW = src.width;
            int targetH = src.height;
            if (targetW > MaxProcessedSize || targetH > MaxProcessedSize)
            {
                float scale = (float)MaxProcessedSize / Mathf.Max(targetW, targetH);
                targetW = Mathf.Max(64, Mathf.RoundToInt(targetW * scale));
                targetH = Mathf.Max(64, Mathf.RoundToInt(targetH * scale));
                working = ResizeTexture(src, targetW, targetH);
                UnityEngine.Object.DestroyImmediate(src);
            }

            Color32[] pixels = working.GetPixels32();
            ApplyHorrorWeathering(pixels, working.width, working.height, spec);

            Texture2D dst = new Texture2D(working.width, working.height, TextureFormat.RGBA32, true, false);
            dst.SetPixels32(pixels);
            dst.Apply(true, false);
            UnityEngine.Object.DestroyImmediate(working);

            EnsureParentDirectory(outputAssetPath);
            File.WriteAllBytes(outputAssetPath, dst.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(dst);

            AssetDatabase.ImportAsset(outputAssetPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outputAssetPath);
        }

        public static Texture2D GenerateBloodDecalTexture(string outputAssetPath, BloodDecalKind kind, int size = 512, int seed = 777)
        {
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = y / (float)(size - 1);
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1);
                    float alpha = SampleDecalAlpha(kind, u, v, seed, out float bright);

                    byte r = (byte)Mathf.RoundToInt(Mathf.Lerp(58f, 165f, bright));
                    byte g = (byte)Mathf.RoundToInt(Mathf.Lerp(3f, 12f, bright));
                    byte b = (byte)Mathf.RoundToInt(Mathf.Lerp(4f, 14f, bright));
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 238f);
                    pixels[y * size + x] = new Color32(r, g, b, a);
                }
            }

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true, false);
            tex.SetPixels32(pixels);
            tex.Apply(true, false);
            EnsureParentDirectory(outputAssetPath);
            File.WriteAllBytes(outputAssetPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(outputAssetPath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(outputAssetPath) as TextureImporter;
            if (importer != null)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outputAssetPath);
        }

        private static float SampleDecalAlpha(BloodDecalKind kind, float u, float v, int seed, out float bright)
        {
            float dx = (u - 0.5f) * 2f;
            float dy = (v - 0.5f) * 2f;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float n1 = Fbm(u * 6.5f + seed * 0.13f, v * 6.5f + seed * 0.29f, 3);
            float n2 = Fbm(u * 18f + seed * 0.41f, v * 18f + seed * 0.17f, 2);
            bright = Mathf.Clamp01(n1 * 0.7f + n2 * 0.3f);

            if (kind == BloodDecalKind.WallDrips)
            {
                float topCenterDist = Mathf.Sqrt(dx * dx * 1.6f + Mathf.Pow((v - 0.72f) * 2.4f, 2f));
                float blotch = 1f - Mathf.Clamp01((topCenterDist - (0.28f + (n1 - 0.5f) * 0.45f)) * 4.5f);
                float dripColumns = Fbm(u * 20f + seed * 0.31f, v * 1.4f + seed * 0.19f, 3);
                float dripHeightFade = v < 0.78f ? Mathf.SmoothStep(0.05f, 0.72f, v) : Mathf.SmoothStep(0.98f, 0.75f, v);
                float drips = Mathf.Clamp01((dripColumns - 0.50f) * 5.2f) * dripHeightFade * (1f - Mathf.Clamp01(Mathf.Abs(dx) * 1.15f));
                float spray = Mathf.Clamp01((n2 - 0.68f) * 7f) * Mathf.Clamp01(1.05f - dist);
                return Mathf.Clamp01(blotch + drips * 0.92f + spray * 0.75f);
            }

            if (kind == BloodDecalKind.DragStreak)
            {
                float streakNoise = Fbm(u * 3.2f + seed * 0.23f, v * 14f + seed * 0.47f, 3);
                float band = 1f - Mathf.Clamp01((Mathf.Abs(dy) - (0.14f + (streakNoise - 0.5f) * 0.36f)) * 5f);
                float ends = 1f - Mathf.Clamp01((Mathf.Abs(dx) - 0.62f) * 3.2f);
                float smear = Mathf.Clamp01((n2 - 0.44f) * 2.5f) * band * ends;
                float drops = Mathf.Clamp01((n2 - 0.66f) * 6.5f) * Mathf.Clamp01(1.1f - dist);
                return Mathf.Clamp01(smear * 1.1f + drops * 0.8f);
            }

            float pool = 1f - Mathf.Clamp01((dist - (0.34f + (n1 - 0.5f) * 0.58f)) * 4.0f);
            float droplets = Mathf.Clamp01((n2 - 0.64f) * 6.8f) * Mathf.Clamp01(1.18f - dist);
            return Mathf.Clamp01(pool + droplets * 0.85f);
        }

        private static void ApplyHorrorWeathering(Color32[] pixels, int w, int h, HorrorTextureSpec spec)
        {
            float sx = (spec.Seed * 0.371f) % 97f;
            float sy = (spec.Seed * 0.619f) % 89f;

            for (int y = 0; y < h; y++)
            {
                float v = y / (float)Mathf.Max(1, h - 1);
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)Mathf.Max(1, w - 1);
                    int idx = y * w + x;
                    Color32 c = pixels[idx];

                    float r = c.r / 255f;
                    float g = c.g / 255f;
                    float b = c.b / 255f;

                    float lum = r * 0.299f + g * 0.587f + b * 0.114f;
                    r = Mathf.Lerp(r, lum, spec.Desaturate) * spec.Brightness;
                    g = Mathf.Lerp(g, lum, spec.Desaturate) * spec.Brightness;
                    b = Mathf.Lerp(b, lum, spec.Desaturate) * spec.Brightness;

                    // Грязь, сырость и вертикальные дождевые потёки
                    if (spec.GrimeStrength > 0.001f)
                    {
                        float grime = Fbm(u * 7.5f + sx, v * 7.5f + sy, 3);
                        float streak = Fbm(u * 22f + sx * 1.3f, v * 2.4f + sy, 2);
                        float dirtMask = Mathf.Clamp01((grime * 0.65f + streak * 0.35f - 0.30f) * 2.2f) * spec.GrimeStrength;
                        r = Mathf.Lerp(r, r * 0.30f + 0.025f, dirtMask);
                        g = Mathf.Lerp(g, g * 0.28f + 0.022f, dirtMask);
                        b = Mathf.Lerp(b, b * 0.25f + 0.018f, dirtMask);
                    }

                    // Глубокое старение и потемнение древесины под стиль VillagePack
                    if (spec.WoodAgeStrength > 0.001f)
                    {
                        float grain = Fbm(u * 4.5f + sx * 0.7f, v * 36f + sy * 0.4f, 3);
                        float rot = Fbm(u * 11f + sx, v * 5.5f + sy, 2);
                        float cracks = Fbm(u * 28f + sx, v * 6f + sy, 2);
                        float ageMask = Mathf.Clamp01((grain * 0.55f + rot * 0.35f + cracks * 0.10f - 0.26f) * 2.1f) * spec.WoodAgeStrength;
                        r = Mathf.Lerp(r, 0.075f, ageMask * 0.85f);
                        g = Mathf.Lerp(g, 0.058f, ageMask * 0.85f);
                        b = Mathf.Lerp(b, 0.042f, ageMask * 0.85f);
                    }

                    // Густая ржавчина на металле и крыше
                    if (spec.RustStrength > 0.001f)
                    {
                        float rBase = Fbm(u * 8.5f + sx * 1.7f, v * 8.5f + sy * 1.1f, 3);
                        float rDetail = Fbm(u * 26f + sx, v * 12f + sy, 2);
                        float rustMask = Mathf.Clamp01((rBase * 0.68f + rDetail * 0.32f - 0.34f) * 3.2f) * spec.RustStrength;
                        float rustR = Mathf.Lerp(0.18f, 0.36f, rDetail);
                        float rustG = Mathf.Lerp(0.055f, 0.11f, rDetail);
                        float rustB = Mathf.Lerp(0.02f, 0.035f, rDetail);
                        r = Mathf.Lerp(r, rustR, rustMask);
                        g = Mathf.Lerp(g, rustG, rustMask);
                        b = Mathf.Lerp(b, rustB, rustMask);
                    }

                    // Выраженные тёмно-красные пятна, потёки и брызги крови
                    if (spec.BloodStrength > 0.001f)
                    {
                        float blotch = Fbm(u * 4.8f + sx * 2.1f, v * 4.8f + sy * 1.9f, 3);
                        float drips = Fbm(u * 18f + sx * 0.9f, v * 2.6f + sy * 2.3f, 2);
                        float splatter = Fbm(u * 26f + sx * 1.5f, v * 26f + sy * 0.8f, 2);
                        float rawBlood = blotch * 0.60f + drips * 0.26f + splatter * 0.14f;
                        float threshold = Mathf.Lerp(0.60f, 0.48f, Mathf.Clamp01(spec.BloodStrength));
                        float bloodMask = Mathf.Clamp01((rawBlood - threshold) * 9.0f) * Mathf.Clamp01(spec.BloodStrength);
                        if (bloodMask > 0.01f)
                        {
                            float bloodRed = Mathf.Lerp(0.24f, 0.56f, splatter);
                            r = Mathf.Lerp(r, bloodRed, bloodMask);
                            g = Mathf.Lerp(g, 0.012f, bloodMask);
                            b = Mathf.Lerp(b, 0.015f, bloodMask);
                        }
                    }

                    pixels[idx] = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt(r * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(g * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(b * 255f), 0, 255),
                        c.a);
                }
            }
        }

        private static Texture2D ResizeTexture(Texture2D source, int width, int height)
        {
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            RenderTexture prev = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            Texture2D resized = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            resized.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            resized.Apply(false, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return resized;
        }

        private static float Fbm(float x, float y, int octaves)
        {
            float sum = 0f;
            float amp = 0.5f;
            float freq = 1f;
            float norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Mathf.PerlinNoise(x * freq, y * freq) * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2.07f;
            }
            return norm > 0f ? sum / norm : 0f;
        }

        private static void EnsureParentDirectory(string assetPath)
        {
            string dir = Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
    }
}
#endif
