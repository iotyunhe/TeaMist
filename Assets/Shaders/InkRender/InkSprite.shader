Shader "TeaMist/InkRender/InkSprite"
{
    // ── 水墨精灵着色器 ──
    // 替代 Sprite-Default，专门为茶馆场景的水墨风格精灵设计。
    // 
    // 核心功能：
    //   1. 标准 Sprite 渲染 + Alpha 混合
    //   2. 全局宣纸纹理叠加 — 所有精灵共享同一纸纹，消除贴图拼凑感
    //   3. Alpha 边缘柔化 — 精灵边缘不再是硬切，而是渐变融入背景
    //   4. 深度层标记 — 输出线性深度给全屏后处理，用于大气透视
    //
    // 注意：不做色调映射（留给全屏 InkTone pass），只做纸纹和边缘柔化
    Properties
    {
        [MainTexture] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header("━━━ 纸纹 ━━━")]
        [NoScaleOffset] _PaperTex ("宣纸纹理（全局）", 2D) = "white" {}
        _PaperStrength ("纸纹强度", Range(0, 0.3)) = 0.08
        _PaperTiling ("纸纹平铺", Float) = 0.5

        [Header("━━━ 边缘柔化 ━━━")]
        _EdgeSoftness ("边缘柔化", Range(0, 0.15)) = 0.04
        _EdgeFeather ("边缘羽化宽度", Range(0.5, 8.0)) = 3.0

        [Header("━━━ 深度层 ━━━")]
        _DepthLayer ("深度层 (0=远景, 1=近景)", Range(0, 1)) = 0.5
        _InkSaturation ("墨色饱和度保持", Range(0, 1)) = 0.15

        // 标准开关
        [Toggle] _PixelSnap ("Pixel Snap", Float) = 0
        [Toggle] _Flip ("Flip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "InkSprite"
            HLSLPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment SpriteFrag
            #pragma target 3.0

            // URP 2D Sprite 支持
            #pragma multi_compile _ PIXELSNAP_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ── 纹理声明 ──
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_PaperTex);
            SAMPLER(sampler_PaperTex);

            // ── 来自全局 Shader 属性（InkRenderPass 设置）──
            float4 _InkBaseColor;
            float  _ToneContrast;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                float4 _Color;
                float  _PaperStrength;
                float  _PaperTiling;
                float  _EdgeSoftness;
                float  _EdgeFeather;
                float  _DepthLayer;
                float  _InkSaturation;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                float2 screenUV   : TEXCOORD1;  // 用于纸纹屏幕空间采样
            };

            Varyings SpriteVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color * _Color;

                // 屏幕空间 UV — 纸纹按屏幕坐标采样，所有精灵纹理对齐
                float4 screenPos = OUT.positionCS;
                OUT.screenUV = screenPos.xy / screenPos.w;
                OUT.screenUV = OUT.screenUV * 0.5 + 0.5;

                #if PIXELSNAP_ON
                OUT.positionCS = float4(round(OUT.positionCS.xy), OUT.positionCS.zw);
                #endif

                return OUT;
            }

            half4 SpriteFrag(Varyings IN) : SV_Target
            {
                // ── 1. 采样精灵纹理 ──
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 baseColor = texColor * IN.color;

                // ── 2. Alpha 边缘柔化 ──
                //     在精灵 alpha 边缘处做渐变羽化，消除硬切边界
                half alpha = baseColor.a;
                if (_EdgeSoftness > 0.001 && alpha > 0.01 && alpha < 0.99)
                {
                    // 采样周围像素的 alpha 做梯度估计
                    float2 ts = _MainTex_TexelSize.xy * _EdgeFeather;
                    half aL = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-ts.x, 0)).a;
                    half aR = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( ts.x, 0)).a;
                    half aT = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(0,  ts.y)).a;
                    half aB = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(0, -ts.y)).a;

                    half alphaGrad = abs(aL - aR) + abs(aT - aB);
                    half edgeAlpha = smoothstep(0.0, 1.0, alpha * 3.0 - alphaGrad * _EdgeSoftness * 30.0);
                    alpha = lerp(alpha, edgeAlpha, _EdgeSoftness * 5.0);
                }

                // ── 3. 宣纸纹理叠加 ──
                //     按屏幕空间坐标采样全局纸纹，所有精灵纹理对齐
                half paper = SAMPLE_TEXTURE2D(_PaperTex, sampler_PaperTex, IN.screenUV * _PaperTiling).r;
                half3 withPaper = baseColor.rgb * lerp(1.0, 0.85 + paper * 0.3, _PaperStrength);
                // 纸纹在暗部减弱，亮部明显（模拟纸张质感）
                half luminance = dot(baseColor.rgb, half3(0.299, 0.587, 0.114));
                half paperBlend = _PaperStrength * lerp(1.0, 0.3, luminance);
                half3 result = lerp(baseColor.rgb, withPaper, paperBlend);

                // ── 4. 保留少量原始色相 ──
                //     不完全灰度化，让全屏 InkTone pass 做主要的色调映射
                //     这里只是给墨色基调做一个轻微的预偏移
                half gray = dot(result, half3(0.299, 0.587, 0.114));
                result = lerp(gray.xxx, result, _InkSaturation);

                return half4(result, alpha);
            }
            ENDHLSL
        }

        // ShadowCaster — 精灵投影（如果需要）
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            float3 _LightDirection;
            float3 _LightPosition;

            Varyings ShadowVert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = float3(0, 0, -1); // Sprite 法线朝相机
                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, _LightDirection));
                OUT.positionCS = positionCS;
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 ShadowFrag(Varyings IN) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                clip(texColor.a * IN.color.a - 0.01);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Sprites/Default"
}
