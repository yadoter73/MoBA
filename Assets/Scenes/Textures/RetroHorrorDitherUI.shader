Shader "Custom/RetroHorrorDitherUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Render Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        [Header(Readability and Tone)]
        _BrightnessBoost ("Brightness Boost", Range(0.5, 2.0)) = 1.12
        _ShadowLift ("Shadow Visibility Lift", Range(0.0, 0.25)) = 0.035

        [Header(Retro Dithering and Quantization)]
        _DitherSpread ("Dither Spread", Range(0.0, 2.0)) = 0.85
        _DitherStrength ("Dither Strength", Range(0.0, 1.5)) = 0.75
        _ColorSteps ("Color Steps Per Channel", Range(8.0, 64.0)) = 40.0

        [Header(Dark Crimson Atmosphere)]
        _ShadowThreshold ("Shadow Tint Range", Range(0.0, 0.5)) = 0.22
        _DarkCrimsonTint ("Shadow Crimson Tint", Color) = (0.35, 0.06, 0.08, 0.14)

        [Header(Dynamic Horror Effects)]
        _AberrationOffset ("Chromatic Aberration (Texels)", Range(0.0, 6.0)) = 0.0
        _VignetteIntensity ("Vignette Intensity", Range(0.0, 1.5)) = 0.18
        _VignetteRoundness ("Vignette Roundness", Range(0.5, 4.0)) = 2.1
        _NoiseAmount ("Static Noise Amount", Range(0.0, 0.2)) = 0.0

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "RetroHorrorDitherUI"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4  color       : COLOR;
                float2 uv          : TEXCOORD0;
                float4 localPos    : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                half4  _Color;
                float4 _ClipRect;

                half   _BrightnessBoost;
                half   _ShadowLift;

                half   _DitherSpread;
                half   _DitherStrength;
                half   _ColorSteps;

                half   _ShadowThreshold;
                half4  _DarkCrimsonTint;

                half   _AberrationOffset;
                half   _VignetteIntensity;
                half   _VignetteRoundness;
                half   _NoiseAmount;
            CBUFFER_END

            // Нормализованная матрица Байера 8x8 в диапазоне [-0.5, +0.5)
            static const float kBayer8x8[64] =
            {
                 0.0/64.0 - 0.5, 32.0/64.0 - 0.5,  8.0/64.0 - 0.5, 40.0/64.0 - 0.5,  2.0/64.0 - 0.5, 34.0/64.0 - 0.5, 10.0/64.0 - 0.5, 42.0/64.0 - 0.5,
                48.0/64.0 - 0.5, 16.0/64.0 - 0.5, 56.0/64.0 - 0.5, 24.0/64.0 - 0.5, 50.0/64.0 - 0.5, 18.0/64.0 - 0.5, 58.0/64.0 - 0.5, 26.0/64.0 - 0.5,
                12.0/64.0 - 0.5, 44.0/64.0 - 0.5,  4.0/64.0 - 0.5, 36.0/64.0 - 0.5, 14.0/64.0 - 0.5, 46.0/64.0 - 0.5,  6.0/64.0 - 0.5, 38.0/64.0 - 0.5,
                60.0/64.0 - 0.5, 28.0/64.0 - 0.5, 52.0/64.0 - 0.5, 20.0/64.0 - 0.5, 62.0/64.0 - 0.5, 30.0/64.0 - 0.5, 54.0/64.0 - 0.5, 22.0/64.0 - 0.5,
                 3.0/64.0 - 0.5, 35.0/64.0 - 0.5, 11.0/64.0 - 0.5, 43.0/64.0 - 0.5,  1.0/64.0 - 0.5, 33.0/64.0 - 0.5,  9.0/64.0 - 0.5, 41.0/64.0 - 0.5,
                51.0/64.0 - 0.5, 19.0/64.0 - 0.5, 59.0/64.0 - 0.5, 27.0/64.0 - 0.5, 49.0/64.0 - 0.5, 17.0/64.0 - 0.5, 57.0/64.0 - 0.5, 25.0/64.0 - 0.5,
                15.0/64.0 - 0.5, 47.0/64.0 - 0.5,  7.0/64.0 - 0.5, 39.0/64.0 - 0.5, 13.0/64.0 - 0.5, 45.0/64.0 - 0.5,  5.0/64.0 - 0.5, 37.0/64.0 - 0.5,
                63.0/64.0 - 0.5, 31.0/64.0 - 0.5, 55.0/64.0 - 0.5, 23.0/64.0 - 0.5, 61.0/64.0 - 0.5, 29.0/64.0 - 0.5, 53.0/64.0 - 0.5, 21.0/64.0 - 0.5
            };

            float HashTexel(float2 texelCoord, float timeStep)
            {
                float3 p = frac(float3(texelCoord.xyx) * 0.1031 + timeStep * 0.0713);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z) - 0.5;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.localPos = IN.positionOS;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = half4(IN.color) * _Color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 texDims = max(_MainTex_TexelSize.zw, float2(1.0, 1.0));
                float2 texelSize = rcp(texDims);

                // Привязка 1:1 к целочисленным текселям входной RenderTexture
                float2 maxTexel = texDims - 1.0;
                float2 clampedUV = saturate(IN.uv);
                float2 texelF = clamp(floor(clampedUV * texDims), float2(0.0, 0.0), maxTexel);
                int2 texelI = int2(texelF);
                float2 snappedUV = (texelF + 0.5) * texelSize;

                // 1. Хроматическая аберрация по целым текселям (появляется ближе к краям и усиливается при страхе)
                float2 centered = snappedUV - 0.5;
                float edgeFactor = saturate(dot(centered, centered) * 2.6);
                float2 shiftTexels = round(centered * ((float)_AberrationOffset * edgeFactor * 2.0));
                float2 uvOffset = shiftTexels * texelSize;

                float2 minUV = texelSize * 0.5;
                float2 maxUV = 1.0 - minUV;
                float2 uvR = clamp(snappedUV + uvOffset, minUV, maxUV);
                float2 uvB = clamp(snappedUV - uvOffset, minUV, maxUV);

                float r = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvR).r;
                float g = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, snappedUV).g;
                float b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvB).b;
                float3 linearCol = max(0.0, float3(r, g, b)) * IN.color.rgb;

                // 2. Переходим в перцептивное sRGB-пространство, чтобы не проваливать тёмные участки сцены
                float3 col = LinearToSRGB(saturate(linearCol));

                // Мягкий подъём читаемости в тенях (чистый чёрный 0 остаётся чёрным)
                float luma = dot(col, float3(0.299, 0.587, 0.114));
                col = saturate(col * (float)_BrightnessBoost + (float)_ShadowLift * (1.0 - col) * smoothstep(0.008, 0.14, luma));
                luma = dot(col, float3(0.299, 0.587, 0.114));

                // 3. Деликатный тёмно-бордовый оттенок теней без потери яркости силуэтов
                float threshold = max(0.01, (float)_ShadowThreshold);
                float tintMask = (1.0 - smoothstep(0.0, threshold, luma)) * smoothstep(0.01, 0.08, luma);
                float3 tintedShadow = max(col, col * 0.72 + _DarkCrimsonTint.rgb * (luma * 1.15));
                col = lerp(col, tintedShadow, tintMask * _DarkCrimsonTint.a);

                // 4. Мягкая виньетка ДО дизеринга (чтобы затухание на углах не создавало полос)
                float2 vigCoord = centered * 2.0;
                float vigMetric = pow(pow(abs(vigCoord.x), (float)_VignetteRoundness) + pow(abs(vigCoord.y), (float)_VignetteRoundness), 1.0 / max(0.25, (float)_VignetteRoundness));
                float vignette = 1.0 - saturate(pow(saturate(vigMetric * 0.65), 2.4) * (float)_VignetteIntensity);
                col *= vignette;
                luma = dot(col, float3(0.299, 0.587, 0.114));

                // 5. Опциональный плёночный шум только на видимых участках (по умолчанию 0 — никаких летающих точек)
                float noiseMask = smoothstep(0.05, 0.22, luma);
                float grain = HashTexel(texelF, floor(_Time.y * 24.0));
                col = saturate(col + grain * ((float)_NoiseAmount * noiseMask));

                // 6. Ordered Bayer 8x8 Dithering strictly 1:1 к текселям
                // Затухает на чистом чёрном (< 0.025), чтобы в темноте не появлялись паразитные светящиеся точки
                uint bx = uint(texelI.x) & 7u;
                uint by = uint(texelI.y) & 7u;
                float bayer = kBayer8x8[by * 8u + bx];

                float steps = max(8.0, (float)_ColorSteps);
                float ditherMask = smoothstep(0.005, 0.045, luma);
                float ditherAmplitude = (((float)_DitherSpread * (float)_DitherStrength) / (steps - 1.0)) * ditherMask;
                col = saturate(col + bayer * ditherAmplitude);
                col = floor(col * (steps - 1.0) + 0.5) / (steps - 1.0);

                // Возвращаем в Linear для корректного вывода в URP (m_ActiveColorSpace = Linear)
                float3 finalLinear = SRGBToLinear(col);

                half alpha = IN.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                    half2 inside = step(_ClipRect.xy, IN.localPos.xy) * step(IN.localPos.xy, _ClipRect.zw);
                    alpha *= inside.x * inside.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                    clip(alpha - 0.001h);
                #endif

                return half4(half3(finalLinear), alpha);
            }
            ENDHLSL
        }
    }
}
