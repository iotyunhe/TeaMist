Shader "TeaMist/PostProcess/InkMasterCombine"
{
    // ── 水墨渲染总管 ──
    // 将五层水墨效果集成到一个全屏 Pass 中
    // 顺序：SeasonTint → InkTone → InkWash → InkEdge → InkVignette
    // 作为 URP Renderer Feature 的最终后处理
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}

        // InkTone 参数
        _InkDark  ("焦墨", Color) = (0.08, 0.06, 0.05, 1)
        _InkDeep  ("浓墨", Color) = (0.15, 0.13, 0.12, 1)
        _InkMid   ("重墨", Color) = (0.28, 0.26, 0.24, 1)
        _InkLight ("淡墨", Color) = (0.55, 0.52, 0.50, 1)
        _InkClear ("清墨", Color) = (0.82, 0.80, 0.78, 1)
        _ToneIntensity ("墨调强度", Range(0, 1.5)) = 1.0

        // SeasonTint 参数
        _SeasonShift ("季节色调偏移", Range(-0.1, 0.1)) = 0.0
        _SeasonSat   ("季节饱和度", Range(0, 2)) = 0.8
        _SeasonWarm  ("季节暖色", Color) = (0.88, 0.92, 0.78, 0)
        _SeasonBlend ("季节影响度", Range(0, 1)) = 0.3

        // InkEdge 参数
        _EdgeWidth   ("描边粗细", Range(0.5, 5)) = 1.5
        _EdgeDepth   ("深度阈值", Range(0.01, 1)) = 0.08
        _EdgeNormal  ("法线阈值", Range(0.1, 2)) = 0.6
        _EdgeColor   ("描边色", Color) = (0.08, 0.06, 0.05, 1)
        _EdgeStrength ("描边强度", Range(0, 1)) = 1.0

        // InkVignette 参数
        _VigColor    ("暗角色", Color) = (0.12, 0.10, 0.08, 1)
        _VigIntensity("暗角强度", Range(0, 1)) = 0.5
        _VigSmooth   ("暗角羽化", Range(0.01, 1)) = 0.3

        // 纸纹
        _PaperColor  ("纸底色", Color) = (0.95, 0.93, 0.88, 1)
        _PaperAge    ("纸龄", Range(0, 1)) = 0.15
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "InkMaster"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                float4 _InkDark, _InkDeep, _InkMid, _InkLight, _InkClear;
                float4 _SeasonWarm, _EdgeColor, _VigColor, _PaperColor;
                float  _ToneIntensity, _SeasonShift, _SeasonSat, _SeasonBlend;
                float  _EdgeWidth, _EdgeDepth, _EdgeNormal, _EdgeStrength;
                float  _VigIntensity, _VigSmooth, _PaperAge;
            CBUFFER_END

            // ── RGB ↔ HSV ──
            half3 rgb2hsv(half3 c)
            {
                half4 K = half4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                half4 p = lerp(half4(c.bg, K.wz), half4(c.gb, K.xy), step(c.b, c.g));
                half4 q = lerp(half4(p.xyw, c.r), half4(c.r, p.yzx), step(p.x, c.r));
                half d = q.x - min(q.w, q.y);
                half e = 1.0e-10;
                return half3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }
            half3 hsv2rgb(half3 c)
            {
                half4 K = half4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                half3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // ── Step 1: SeasonTint ──
                half3 hsv = rgb2hsv(tex.rgb);
                hsv.r = frac(hsv.r + _SeasonShift);
                hsv.g = saturate(hsv.g * _SeasonSat);
                half3 seasonColor = hsv2rgb(hsv);
                seasonColor = lerp(seasonColor, seasonColor * _SeasonWarm.rgb, 0.3);
                half grayBase = dot(tex.rgb, half3(0.299, 0.587, 0.114));
                seasonColor = lerp(seasonColor, half3(grayBase, grayBase, grayBase), 0.25);
                half3 tinted = lerp(tex.rgb, seasonColor, _SeasonBlend);

                // ── Step 2: InkTone 五阶映射 ──
                half gray = dot(tinted, half3(0.299, 0.587, 0.114));
                half3 inkColor;
                if (gray < 0.2)      inkColor = lerp(_InkDark.rgb, _InkDeep.rgb, gray / 0.2);
                else if (gray < 0.4) inkColor = lerp(_InkDeep.rgb, _InkMid.rgb, (gray - 0.2) / 0.2);
                else if (gray < 0.6) inkColor = lerp(_InkMid.rgb, _InkLight.rgb, (gray - 0.4) / 0.2);
                else if (gray < 0.8) inkColor = lerp(_InkLight.rgb, _InkClear.rgb, (gray - 0.6) / 0.2);
                else                 inkColor = lerp(_InkClear.rgb, _PaperColor.rgb, (gray - 0.8) / 0.2);
                half3 toned = lerp(tinted, inkColor, _ToneIntensity);

                // ── Step 3: InkEdge ──
                float2 ts = _MainTex_TexelSize.xy * _EdgeWidth;
                half d00 = SampleSceneDepth(IN.uv);
                half d10 = SampleSceneDepth(IN.uv + float2(ts.x, 0));
                half d01 = SampleSceneDepth(IN.uv + float2(0, -ts.y));
                half d11 = SampleSceneDepth(IN.uv + float2(ts.x, -ts.y));
                half depthEdge = step(_EdgeDepth, abs(d00 - d11) + abs(d10 - d01));

                half3 n00 = SampleSceneNormals(IN.uv);
                half3 n10 = SampleSceneNormals(IN.uv + float2(ts.x, 0));
                half3 n01 = SampleSceneNormals(IN.uv + float2(0, -ts.y));
                half3 n11 = SampleSceneNormals(IN.uv + float2(ts.x, -ts.y));
                half normEdge = dot(abs(n00 - n11) + abs(n10 - n01), half3(0.333, 0.333, 0.333));
                normEdge = step(_EdgeNormal, normEdge);

                half edge = max(depthEdge, normEdge) * _EdgeStrength;
                half3 edged = lerp(toned, _EdgeColor.rgb, edge);

                // ── Step 4: InkVignette ──
                float2 cUV = IN.uv - 0.5;
                float dist = length(float2(cUV.x, cUV.y * 1.77)) / 0.707;
                float vignette = smoothstep(0.2, _VigSmooth + 0.3, dist) * _VigIntensity;
                half3 vignetted = lerp(edged, _VigColor.rgb, vignette);

                // ── Step 5: Paper Age ──
                half3 aged = lerp(vignetted, vignetted * half3(0.92, 0.86, 0.72), _PaperAge);

                return half4(aged, tex.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
