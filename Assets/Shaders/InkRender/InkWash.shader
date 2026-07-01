Shader "TeaMist/InkRender/InkWash"
{
    // ── 墨色晕染混合 ──
    // 多层纹理之间的水墨式混合：干笔叠加 + 湿笔渗透 + 泼墨融合
    // 用于场景背景的多层叠合 (远山/中景/近景)
    Properties
    {
        _Layer1 ("远山层", 2D) = "white" {}
        _Layer2 ("中景层", 2D) = "white" {}
        _Layer3 ("近景层", 2D) = "white" {}
        _MaskTex ("融合遮罩", 2D) = "white" {}

        _Layer1Opacity ("远山浓度", Range(0, 1)) = 0.4
        _Layer2Opacity ("中景浓度", Range(0, 1)) = 0.7
        _Layer3Opacity ("近景浓度", Range(0, 1)) = 1.0

        _Wetness ("湿笔渗透度", Range(0, 1)) = 0.3
        _BleedRadius ("墨色洇散半径", Range(0, 0.05)) = 0.008
        _DryStroke ("枯笔飞白", Range(0, 1)) = 0.15

        _PaperColor ("纸底色", Color) = (0.95, 0.93, 0.88, 1)
        _PaperTexture ("宣纸纹理", 2D) = "white" {}
        _PaperStrength ("纸纹强度", Range(0, 0.15)) = 0.06
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Name "InkWash"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS

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

            TEXTURE2D(_Layer1);       SAMPLER(sampler_Layer1);
            TEXTURE2D(_Layer2);       SAMPLER(sampler_Layer2);
            TEXTURE2D(_Layer3);       SAMPLER(sampler_Layer3);
            TEXTURE2D(_MaskTex);      SAMPLER(sampler_MaskTex);
            TEXTURE2D(_PaperTexture); SAMPLER(sampler_PaperTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _Layer1_ST;
                float4 _Layer2_ST;
                float4 _Layer3_ST;
                float4 _PaperColor;
                float  _Layer1Opacity;
                float  _Layer2Opacity;
                float  _Layer3Opacity;
                float  _Wetness;
                float  _BleedRadius;
                float  _DryStroke;
                float  _PaperStrength;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half2 uv = IN.uv;
                half4 paper = SAMPLE_TEXTURE2D(_PaperTexture, sampler_PaperTexture, uv * 2.0);

                // 1. 掩码采样：控制每层可见区域
                half mask1 = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, uv).r;
                half mask2 = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, uv).g;
                half mask3 = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, uv).b;

                // 2. 三层分别采样并灰度化
                half4 l1 = SAMPLE_TEXTURE2D(_Layer1, sampler_Layer1, uv);
                half4 l2 = SAMPLE_TEXTURE2D(_Layer2, sampler_Layer2, uv);
                half4 l3 = SAMPLE_TEXTURE2D(_Layer3, sampler_Layer3, uv);
                half g1 = dot(l1.rgb, half3(0.299, 0.587, 0.114));
                half g2 = dot(l2.rgb, half3(0.299, 0.587, 0.114));
                half g3 = dot(l3.rgb, half3(0.299, 0.587, 0.114));

                // 3. 枯笔飞白效果：暗部区域随机留白
                half dry1 = step(_DryStroke, frac(sin(dot(uv * 300.0, half2(12.9898, 78.233))) * 43758.5453));
                half dry2 = step(_DryStroke, frac(sin(dot(uv * 250.0, half2(39.346, 21.791))) * 28521.1647));
                half dry3 = step(_DryStroke, frac(sin(dot(uv * 350.0, half2(67.123, 45.678))) * 52139.8562));

                // 4. 湿笔渗透：对浓墨区域做微偏移
                half2 bleedOffset1 = half2(
                    frac(sin(dot(uv * 100.0, half2(23.456, 78.901))) * _BleedRadius),
                    frac(sin(dot(uv * 100.0, half2(45.678, 12.345))) * _BleedRadius)
                );
                half g1_bleed = dot(SAMPLE_TEXTURE2D(_Layer1, sampler_Layer1, uv + bleedOffset1 * _Wetness).rgb, half3(0.299, 0.587, 0.114));

                // 5. 融合：湿笔渗透优先，干笔飞白叠加
                half ink1 = g1 * mask1 * (1.0 - _Wetness) + g1_bleed * mask1 * _Wetness;
                ink1 *= dry1;
                half ink2 = g2 * mask2 * dry2;
                half ink3 = g3 * mask3 * dry3;

                // 6. 从远到近叠加
                half totalInk = ink1 * _Layer1Opacity;
                totalInk = max(totalInk, ink2 * _Layer2Opacity);
                totalInk = max(totalInk, ink3 * _Layer3Opacity);

                // 7. 映射到宣纸底色
                half3 finalColor = lerp(_PaperColor.rgb, half3(0.08, 0.06, 0.05), totalInk);

                // 8. 宣纸纹理
                finalColor = lerp(finalColor, finalColor * (0.85 + paper.r * 0.3), _PaperStrength);

                // 9. 基础光照
                Light mainLight = GetMainLight();
                half NdotL = dot(normalize(IN.normalWS), mainLight.direction) * 0.3 + 0.7;
                finalColor *= NdotL;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
