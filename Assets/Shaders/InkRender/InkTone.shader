Shader "TeaMist/InkRender/InkTone"
{
    // ── 墨色基调染色 ──
    // 将纹理灰度化后再映射到中国传统墨色五阶
    // 0% → 焦墨 │ 25% → 浓墨 │ 50% → 重墨 │ 75% → 淡墨 │ 100% → 清墨
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _InkDark ("焦墨 (最浓)", Color) = (0.08, 0.06, 0.05, 1)
        _InkDeep ("浓墨", Color) = (0.15, 0.13, 0.12, 1)
        _InkMid  ("重墨", Color) = (0.28, 0.26, 0.24, 1)
        _InkLight ("淡墨", Color) = (0.55, 0.52, 0.50, 1)
        _InkClear ("清墨 (最淡)", Color) = (0.82, 0.80, 0.78, 1)

        _Threshold1 ("焦/浓边界", Range(0, 1)) = 0.2
        _Threshold2 ("浓/重边界", Range(0, 1)) = 0.4
        _Threshold3 ("重/淡边界", Range(0, 1)) = 0.6
        _Threshold4 ("淡/清边界", Range(0, 1)) = 0.8

        _PaperColor ("纸底色", Color) = (0.95, 0.93, 0.88, 1)
        _InkIntensity ("墨色强度", Range(0, 1.5)) = 1.0
        _BrushGrain ("笔触纹理", 2D) = "white" {}
        _BrushGrainStrength ("笔触噪点强度", Range(0, 0.3)) = 0.05
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Name "InkTone"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_BrushGrain);
            SAMPLER(sampler_BrushGrain);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _InkDark;
                float4 _InkDeep;
                float4 _InkMid;
                float4 _InkLight;
                float4 _InkClear;
                float4 _PaperColor;
                float  _Threshold1;
                float  _Threshold2;
                float  _Threshold3;
                float  _Threshold4;
                float  _InkIntensity;
                float  _BrushGrainStrength;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 1. 采样主纹理并灰度化
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half gray = dot(texColor.rgb, half3(0.299, 0.587, 0.114));

                // 2. 笔触纹理叠加噪点 (模拟水墨宣纸渗透)
                half2 grainUV = IN.uv * 4.0;
                half grain = SAMPLE_TEXTURE2D(_BrushGrain, sampler_BrushGrain, grainUV).r;
                gray += (grain - 0.5) * _BrushGrainStrength * 0.5;
                gray = saturate(gray);

                // 3. 墨分五色：根据灰度值映射到五阶墨色
                half3 inkColor;
                if (gray < _Threshold1)
                    inkColor = lerp(_InkDark.rgb, _InkDeep.rgb, gray / _Threshold1);
                else if (gray < _Threshold2)
                    inkColor = lerp(_InkDeep.rgb, _InkMid.rgb, (gray - _Threshold1) / (_Threshold2 - _Threshold1));
                else if (gray < _Threshold3)
                    inkColor = lerp(_InkMid.rgb, _InkLight.rgb, (gray - _Threshold2) / (_Threshold3 - _Threshold2));
                else if (gray < _Threshold4)
                    inkColor = lerp(_InkLight.rgb, _InkClear.rgb, (gray - _Threshold3) / (_Threshold4 - _Threshold3));
                else
                    inkColor = lerp(_InkClear.rgb, _PaperColor.rgb, (gray - _Threshold4) / (1.0 - _Threshold4));

                // 4. 墨色强度
                inkColor = lerp(_PaperColor.rgb, inkColor, _InkIntensity);

                // 5. 基础光照 (让墨色有微妙的明暗层次)
                Light mainLight = GetMainLight();
                half NdotL = dot(normalize(IN.normalWS), mainLight.direction) * 0.5 + 0.5;
                inkColor *= lerp(0.7, 1.0, NdotL);

                half4 finalColor = half4(inkColor, texColor.a);
                return finalColor;
            }
            ENDHLSL
        }

        // ShadowCaster Pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowVert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                OUT.positionCS = positionCS;
                return OUT;
            }

            half4 ShadowFrag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
