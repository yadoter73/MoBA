Shader "Custom/PlanarMirrorURP"
{
    Properties
    {
        _MainTex ("Mirror RenderTexture", 2D) = "black" {}
        _TintColor ("Mirror Glass Tint", Color) = (0.76, 0.80, 0.78, 1)
        _EdgeVignette ("Frame Shadow", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+5"
        }

        Cull Back
        ZWrite On

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 mirrorClip  : TEXCOORD0;
                float2 uv          : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4x4 _MirrorVP;
                half4 _TintColor;
                half _EdgeVignette;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.mirrorClip = mul(_MirrorVP, float4(positionWS, 1.0));
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = (IN.mirrorClip.xy / max(IN.mirrorClip.w, 0.0001)) * 0.5 + 0.5;
                uv = saturate(uv);
                half3 refl = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;
                float2 edge = abs(IN.uv - 0.5) * 2.0;
                half vig = 1.0 - saturate(dot(edge, edge) * 0.35 * _EdgeVignette);
                return half4(refl * _TintColor.rgb * vig, 1.0);
            }
            ENDHLSL
        }
    }
}
