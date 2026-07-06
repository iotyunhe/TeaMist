Shader "TeaMist/InkRender/InkFullscreenBlit"
{
    // ── 水墨五层全屏后处理组合 Shader (v2 - 深度感知) ──
    // 供 InkRenderPass 的 Blitter.BlitCameraTexture 使用
    // 5 个 Pass，每个对应一层后处理效果
    //
    // v2 新增：
    //   - 深度感知距离雾气（大气透视，远淡近浓）
    //   - 宣纸纹理覆盖（统一所有元素到一张"画纸"上）
    //   - 飞白笔触强化（模拟毛笔枯笔效果）
    Properties
    {
        _MainTex ("Source Texture", 2D) = "white" {}
        [NoScaleOffset] _PaperTex ("宣纸纹理", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        Cull Off ZWrite Off ZTest Always

        // ═══════════════════════════════════════════════
        // Pass 0: InkTone — 灰度转墨色五阶 + 深度雾气
        // ═══════════════════════════════════════════════
        Pass
        {
            Name "InkTone"
            HLSLPROGRAM
            #pragma vertex FullscreenVert
            #pragma fragment fragTone
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _InkBaseColor;
            float  _ToneContrast;
            float4 _DistanceFogColor;
            float  _DistanceFogStart;
            float  _DistanceFogEnd;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings FullscreenVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = float4(IN.positionOS.xy, 0.0, 1.0);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 fragTone(Varyings IN) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // ── 1. 灰度化 ──
                half gray = dot(texColor.rgb, half3(0.299, 0.587, 0.114));

                // ── 2. 深度感知距离雾气 ──
                //     远处像素更淡、更接近纸色（大气透视）
                float depthRaw = SampleSceneDepth(IN.uv);
                float linearDepth = LinearEyeDepth(depthRaw, _ZBufferParams);
                float depthFog = smoothstep(_DistanceFogStart, _DistanceFogEnd, linearDepth);
                // 远景：减弱对比度，提亮
                float localContrast = lerp(_ToneContrast, _ToneContrast * 0.4, depthFog);
                half3 fogTint = lerp(half3(0.95, 0.93, 0.88), _DistanceFogColor.rgb, depthFog);

                // ── 3. 对比度 ──
                gray = (gray - 0.5) * localContrast + 0.5;
                gray = saturate(gray);

                // ── 4. 映射到墨色基调 ──
                //     深处：淡墨/纸色；近处：浓墨/焦墨
                half3 inkColor = lerp(half3(0.95, 0.93, 0.88), _InkBaseColor.rgb, 1.0 - gray);
                // 深度雾气叠加
                inkColor = lerp(inkColor, fogTint, depthFog * 0.6);

                return half4(inkColor, 1.0);
            }
            ENDHLSL
        }

        // ═══════════════════════════════════════════════
        // Pass 1: InkWash — 水墨晕染扩散 + 深度感知模糊
        // ═══════════════════════════════════════════════
        Pass
        {
            Name "InkWash"
            HLSLPROGRAM
            #pragma vertex FullscreenVert
            #pragma fragment fragWash
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            float _WashBlurRadius;
            float _WashIntensity;
            float _DistanceWashMult;
            float _DistanceBlurStart;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings FullscreenVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = float4(IN.positionOS.xy, 0.0, 1.0);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 fragWash(Varyings IN) : SV_Target
            {
                half4 center = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // 深度感知：远处晕染更强
                float depthRaw = SampleSceneDepth(IN.uv);
                float linearDepth = LinearEyeDepth(depthRaw, _ZBufferParams);
                float depthBlur = smoothstep(_DistanceBlurStart, _DistanceBlurStart + 3.0, linearDepth);
                float localBlur = _WashBlurRadius * lerp(1.0, _DistanceWashMult, depthBlur);

                // 8 方向盒式模糊（比原来 4 方向更平滑）
                float2 ts = _MainTex_TexelSize.xy * localBlur;
                half4 blur = half4(0,0,0,0);
                blur += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-ts.x,  ts.y));
                blur += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(    0,  ts.y));
                blur += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( ts.x,  ts.y));
                blur += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-ts.x,      0));
                blur += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( ts.x,      0));
                blur += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-ts.x, -ts.y));
                blur += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(    0, -ts.y));
                blur += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( ts.x, -ts.y));
                blur += center;
                blur /= 9.0;

                // 局部晕染强度
                float localIntensity = _WashIntensity * lerp(1.0, min(1.5, _DistanceWashMult), depthBlur);
                return lerp(center, blur, saturate(localIntensity));
            }
            ENDHLSL
        }

        // ═══════════════════════════════════════════════
        // Pass 2: SeasonTint — 季节染色
        // ═══════════════════════════════════════════════
        Pass
        {
            Name "SeasonTint"
            HLSLPROGRAM
            #pragma vertex FullscreenVert
            #pragma fragment fragTint
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _SeasonTintColor;
            float  _TintStrength;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings FullscreenVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = float4(IN.positionOS.xy, 0.0, 1.0);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 fragTint(Varyings IN) : SV_Target
            {
                half4 base = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                // 乘法叠加 + 保留 30% 原始墨色防止染色过重
                half3 tinted = base.rgb * _SeasonTintColor.rgb;
                half3 result = lerp(base.rgb, lerp(base.rgb, tinted, _TintStrength), 0.7);
                return half4(result, base.a);
            }
            ENDHLSL
        }

        // ═══════════════════════════════════════════════
        // Pass 3: InkEdge — 边缘墨线勾勒 + 深度感知
        // ═══════════════════════════════════════════════
        Pass
        {
            Name "InkEdge"
            HLSLPROGRAM
            #pragma vertex FullscreenVert
            #pragma fragment fragEdge
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            float _EdgeWidth;
            float _EdgeThreshold;
            float4 _EdgeColor;
            float _DistanceEdgeAtten;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings FullscreenVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = float4(IN.positionOS.xy, 0.0, 1.0);
                OUT.uv = IN.uv;
                return OUT;
            }

            half Luminance(half3 c) { return dot(c, half3(0.299, 0.587, 0.114)); }

            half Sobel(Varyings IN, float edgeWidth)
            {
                float2 ts = _MainTex_TexelSize.xy * edgeWidth;
                half tl = Luminance(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-ts.x,  ts.y)).rgb);
                half t  = Luminance(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( 0,     ts.y)).rgb);
                half tr = Luminance(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( ts.x,  ts.y)).rgb);
                half l  = Luminance(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-ts.x,  0)).rgb);
                half r  = Luminance(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( ts.x,  0)).rgb);
                half bl = Luminance(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-ts.x, -ts.y)).rgb);
                half b  = Luminance(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( 0,    -ts.y)).rgb);
                half br = Luminance(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( ts.x, -ts.y)).rgb);

                half gx = -tl - 2.0*l - bl + tr + 2.0*r + br;
                half gy = -tl - 2.0*t - tr + bl + 2.0*b + br;
                return sqrt(gx*gx + gy*gy);
            }

            half4 fragEdge(Varyings IN) : SV_Target
            {
                half4 base = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // 深度感知：远处减少边缘检测
                float depthRaw = SampleSceneDepth(IN.uv);
                float linearDepth = LinearEyeDepth(depthRaw, _ZBufferParams);
                float depthAtten = 1.0 - smoothstep(2.0, 8.0, linearDepth) * _DistanceEdgeAtten;
                float localThreshold = _EdgeThreshold + (1.0 - depthAtten) * _EdgeThreshold * 2.0;

                half edge = step(localThreshold, Sobel(IN, _EdgeWidth)) * depthAtten;

                // 飞白效果：边缘检测结果加随机噪点，模拟毛笔断续
                float noise = frac(sin(dot(IN.uv * 300.0, float2(12.9898, 78.233))) * 43758.5453);
                edge *= step(0.15, noise); // 15% 概率留白

                half3 result = lerp(base.rgb, _EdgeColor.rgb, edge * _EdgeColor.a);
                return half4(result, base.a);
            }
            ENDHLSL
        }

        // ═══════════════════════════════════════════════
        // Pass 4: InkVignette — 暗角 + 宣纸纹理 + 天气
        // ═══════════════════════════════════════════════
        Pass
        {
            Name "InkVignette"
            HLSLPROGRAM
            #pragma vertex FullscreenVert
            #pragma fragment fragVignette
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_PaperTex);
            SAMPLER(sampler_PaperTex);

            float4 _VignetteColor;
            float  _VignetteIntensity;
            float  _WeatherWetness;
            float  _FlyingWhiteStrength;
            float  _PaperOverlayStrength;
            float  _PaperTiling;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings FullscreenVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = float4(IN.positionOS.xy, 0.0, 1.0);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 fragVignette(Varyings IN) : SV_Target
            {
                half4 base = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // ── 1. 暗角：边缘渐暗 ──
                float2 uvCentered = IN.uv - 0.5;
                float vignette = 1.0 - dot(uvCentered, uvCentered) * 1.8;
                vignette = saturate(vignette);
                vignette = smoothstep(0.0, 1.0, vignette); // smoothstep 让暗角更自然
                vignette = lerp(1.0, vignette, _VignetteIntensity);

                // ── 2. 天气湿润度：雨/雾天画面更柔 ──
                float wetBlend = _WeatherWetness * 0.3;
                half3 wetTint = base.rgb * (1.0 - wetBlend * 0.2);

                // ── 3. 飞白：秋季降饱和度模拟笔触干枯 ──
                float flyingWhite = _FlyingWhiteStrength * 0.35;
                // 用噪声决定哪些区域"飞白"
                float noise = frac(sin(dot(IN.uv * 500.0, float2(43.789, 17.234))) * 65321.5432);
                float flyMask = step(flyingWhite, noise);
                half grayWet = dot(wetTint, half3(0.299, 0.587, 0.114));
                half3 desaturated = lerp(wetTint, grayWet.xxx, flyingWhite * flyMask);

                // ── 4. 宣纸纹理覆盖 ──
                //     全屏统一的纸纹，把所有元素"焊"在一张画纸上
                half paper = SAMPLE_TEXTURE2D(_PaperTex, sampler_PaperTex, IN.uv * _PaperTiling).r;
                half3 paperOverlay = desaturated * lerp(1.0, 0.88 + paper * 0.24, _PaperOverlayStrength);

                // ── 5. 暗角叠色 ──
                half3 result = paperOverlay * lerp(_VignetteColor.rgb, half3(1,1,1), vignette);

                return half4(result, base.a);
            }
            ENDHLSL
        }
    }
}
