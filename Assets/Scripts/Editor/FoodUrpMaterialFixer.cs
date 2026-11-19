#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MoBA.Editor
{
    /// <summary>
    /// Исправляет URP-материалы еды в Assets/CrowAssets/Assets/Low Poly Stylized Food/URP/Materials:
    /// переводит с HDRP-шейдера на URP Lit, переносит _MainTex -> _BaseMap, _Color -> _BaseColor
    /// и настраивает прозрачность для Glass.mat.
    /// </summary>
    public static class FoodUrpMaterialFixer
    {
        public const string UrpMaterialsFolder = "Assets/CrowAssets/Assets/Low Poly Stylized Food/URP/Materials";
        public const string FoodTexturesFolder = "Assets/CrowAssets/Assets/Low Poly Stylized Food/Textures";

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        private static readonly int BlendId = Shader.PropertyToID("_Blend");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        private static readonly int MetallicGlossMapId = Shader.PropertyToID("_MetallicGlossMap");
        private static readonly int OcclusionMapId = Shader.PropertyToID("_OcclusionMap");

        [MenuItem("MoBA/Fix Food URP Materials")]
        public static void FixFoodUrpMaterials()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                urpLit = Shader.Find("Universal Render Pipeline/Simple Lit");
            }
            if (urpLit == null)
            {
                Debug.LogError("[FixFoodUrpMaterials] Не найден шейдер Universal Render Pipeline/Lit.");
                return;
            }

            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { UrpMaterialsFolder });
            int fixedCount = 0;
            int missingTexCount = 0;

            foreach (string guid in matGuids)
            {
                string matPath = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null) continue;

                Undo.RecordObject(mat, "Fix Food URP Material");

                Texture mainTex = mat.HasProperty(MainTexId) ? mat.GetTexture(MainTexId) : null;
                Texture baseTex = mat.HasProperty(BaseMapId) ? mat.GetTexture(BaseMapId) : null;
                Texture normalTex = mat.HasProperty(BumpMapId) ? mat.GetTexture(BumpMapId) : null;
                Texture metallicTex = mat.HasProperty(MetallicGlossMapId) ? mat.GetTexture(MetallicGlossMapId) : null;
                Texture occlusionTex = mat.HasProperty(OcclusionMapId) ? mat.GetTexture(OcclusionMapId) : null;
                Color oldColor = mat.HasProperty(ColorId) ? mat.GetColor(ColorId) : Color.white;
                Color oldBaseColor = mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId) : Color.white;

                Texture targetAlbedo = baseTex != null ? baseTex : mainTex;
                bool isGlass = string.Equals(mat.name, "Glass", StringComparison.OrdinalIgnoreCase);

                if (targetAlbedo == null && !isGlass && !string.Equals(mat.name, "Plane", StringComparison.OrdinalIgnoreCase))
                {
                    targetAlbedo = FindTextureByMaterialName(mat.name);
                }

                mat.shader = urpLit;

                if (targetAlbedo != null)
                {
                    mat.SetTexture(BaseMapId, targetAlbedo);
                    mat.SetTexture(MainTexId, targetAlbedo);
                }
                if (normalTex != null) mat.SetTexture(BumpMapId, normalTex);
                if (metallicTex != null) mat.SetTexture(MetallicGlossMapId, metallicTex);
                if (occlusionTex != null) mat.SetTexture(OcclusionMapId, occlusionTex);

                // Очищаем устаревшие HDRP/лишние ключевые слова и отключаем свечение
                mat.shaderKeywords = Array.Empty<string>();
                mat.DisableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);

                if (isGlass)
                {
                    ConfigureTransparentGlass(mat);
                }
                else
                {
                    ConfigureOpaqueFood(mat, oldColor, oldBaseColor, mat.name);
                    if (targetAlbedo == null && !string.Equals(mat.name, "Plane", StringComparison.OrdinalIgnoreCase))
                    {
                        missingTexCount++;
                        Debug.LogWarning($"[FixFoodUrpMaterials] Для {mat.name} не найдена альбедо-текстура.", mat);
                    }
                }

                EditorUtility.SetDirty(mat);
                fixedCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[FixFoodUrpMaterials] Обновлено материалов: {fixedCount}, без текстуры: {missingTexCount}.");
        }

        private static void ConfigureOpaqueFood(Material mat, Color oldColor, Color oldBaseColor, string matName)
        {
            if (mat.HasProperty(SurfaceId)) mat.SetFloat(SurfaceId, 0f);
            if (mat.HasProperty(BlendId)) mat.SetFloat(BlendId, 0f);
            if (mat.HasProperty(SrcBlendId)) mat.SetFloat(SrcBlendId, (float)BlendMode.One);
            if (mat.HasProperty(DstBlendId)) mat.SetFloat(DstBlendId, (float)BlendMode.Zero);
            if (mat.HasProperty(ZWriteId)) mat.SetFloat(ZWriteId, 1f);
            mat.SetOverrideTag("RenderType", "Opaque");
            mat.renderQueue = -1;

            Color chosen = oldColor.a > 0.05f ? oldColor : oldBaseColor;
            if (chosen.maxColorComponent < 0.05f) chosen = Color.white;
            chosen.a = 1f;

            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, chosen);
            if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, chosen);

            bool isMetallic = matName.IndexOf("Can", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              matName.IndexOf("Sardines", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              matName.IndexOf("Soda", StringComparison.OrdinalIgnoreCase) >= 0;
            if (mat.HasProperty(MetallicId)) mat.SetFloat(MetallicId, isMetallic ? 0.35f : 0f);
            if (mat.HasProperty(SmoothnessId)) mat.SetFloat(SmoothnessId, isMetallic ? 0.55f : 0.32f);
        }

        private static void ConfigureTransparentGlass(Material mat)
        {
            if (mat.HasProperty(SurfaceId)) mat.SetFloat(SurfaceId, 1f);
            if (mat.HasProperty(BlendId)) mat.SetFloat(BlendId, 0f);
            if (mat.HasProperty(SrcBlendId)) mat.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
            if (mat.HasProperty(DstBlendId)) mat.SetFloat(DstBlendId, (float)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty(ZWriteId)) mat.SetFloat(ZWriteId, 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;

            var glassColor = new Color(0.86f, 0.94f, 0.92f, 0.32f);
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, glassColor);
            if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, glassColor);
            if (mat.HasProperty(MetallicId)) mat.SetFloat(MetallicId, 0.05f);
            if (mat.HasProperty(SmoothnessId)) mat.SetFloat(SmoothnessId, 0.92f);
        }

        private static Texture2D FindTextureByMaterialName(string matName)
        {
            string normalizedMat = NormalizeName(matName);
            string[] aliases = GetAliases(normalizedMat);

            string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { FoodTexturesFolder });
            Texture2D fallback = null;

            foreach (string guid in texGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileNameWithoutExtension(path);
                if (IsUtilityMap(file)) continue;

                string folder = Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty);
                string normFile = NormalizeName(file);
                string normFolder = NormalizeName(folder);

                foreach (string alias in aliases)
                {
                    if (normFile.Contains(alias, StringComparison.OrdinalIgnoreCase) ||
                        normFolder.Contains(alias, StringComparison.OrdinalIgnoreCase))
                    {
                        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                        if (tex == null) continue;
                        if (file.IndexOf("Albedo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            file.IndexOf("Base", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            file.IndexOf("Color", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            file.IndexOf("Diffuse", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return tex;
                        }
                        if (fallback == null) fallback = tex;
                    }
                }
            }
            return fallback;
        }

        private static string[] GetAliases(string normMat)
        {
            var list = new List<string> { normMat };
            if (normMat.Contains("egg")) list.Add("egg");
            if (normMat.Contains("sodabottle")) list.Add("sodabottle");
            if (normMat.Contains("sodacan")) list.Add("sodacan");
            if (normMat.Contains("can01open")) list.Add("can01open");
            if (normMat.Contains("cheese")) list.Add("cheese");
            return list.ToArray();
        }

        private static bool IsUtilityMap(string name)
        {
            return name.IndexOf("Normal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Roughness", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Metallic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Mask", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("AO", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Ambient", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Height", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string NormalizeName(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var chars = new List<char>(s.Length);
            foreach (char c in s)
            {
                if (char.IsLetterOrDigit(c)) chars.Add(char.ToLowerInvariant(c));
            }
            return new string(chars.ToArray());
        }
    }
}
#endif
