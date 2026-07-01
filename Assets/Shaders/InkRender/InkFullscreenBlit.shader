Shader "TeaMist/InkRender/InkFullscreenBlit"
{
    // ── 水墨五层全屏后处理组合 Shader ──
    // 供 InkRenderPass 的 Blitter.BlitCameraTexture 使用
    // 5 个 Pass，每个对应一层后处理效果
    Properties
    {
        // ── 共享纹理 ──
        _MainTex ("Source Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        Cull Off ZWrite Off ZTest Always

        // ═══════════════════════════════════════════════
        // Pass 0: InkTone — 灰度转墨色五阶
        // ═══════════════════════════════════════════════
        Pass
        {
            Name "InkTone"
            HLSLPROGRAM
            #pragma vertex FullscreenVert
            #pragma fragment fragTone
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _InkBaseColor;
            float  _ToneContrast;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

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
                // 灰度化
                half gray = dot(texColor.rgb, half3(0.299, 0.587, 0.114));
                // 对比度
                gray = (gray - 0.5) * _ToneContrast + 0.5;
                gray = saturate(gray);
                // 映射到墨色基调
                half3 inkColor = lerp(half3(0.95, 0.93, 0.88), _InkBaseColor.rgb, 1.0 - gray);
                return half4(inkColor, 1.0);
            }
            ENDHLSL
        }

        // ═══════════════════════════════════════════════
        // Pass 1: InkWash — 水墨晕染扩散
        // ═══════════════════════════════════════════════
        Pass
        {
            Name "InkWash"
            HLSLPROGRAM
            #pragma vertex FullscreenVert
            #pragma fragment fragWash
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            float _WashBlurRadius;
            float _WashIntensity;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

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
                // 简易盒式模糊模拟水墨晕染
                float2 texelSize = _MainTex_TexelSize.xy * _WashBlurRadius;
                half4 blur = half4(0,0,0,0);
                blur += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-texelSize.x,  texelSize.y));
                blur += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( texelSize.x,  texelSize.y));
                blur += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-texelSize.x, -texelSize.y));
                blur += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( texelSize.x, -texelSize.y));
                blur += center;
                blur /= 5.0;
                return lerp(center, blur, _WashIntensity);
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _SeasonTintColor;
            float  _TintStrength;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

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
                // 季节着色：乘法叠加
                half3 tinted = lerp(base.rgb, base.rgb * _SeasonTintColor.rgb, _TintStrength);
                return half4(tinted, base.a);
            }
            ENDHLSL
        }

        // ═══════════════════════════════════════════════
        // Pass 3: InkEdge — 边缘墨线勾勒
        // ═══════════════════════════════════════════════
        Pass
        {
            Name "InkEdge"
            HLSLPROGRAM
            #pragma vertex FullscreenVert
            #pragma fragment fragEdge
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            float _EdgeWidth;
            float _EdgeThreshold;
            float4 _EdgeColor;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings FullscreenVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = float4(IN.positionOS.xy, 0.0, 1.0);
                OUT.uv = IN.uv;
                return OUT;
            }

            half Luminance(half3 c) { return dot(c, half3(0.299, 0.587, 0.114)); }

            half Sobel(Varyings IN)
            {
                float2 ts = _MainTex_TexelSize.xy * _EdgeWidth;
                half topLeft     = Luminance(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-ts.x,  ts.y)).rgb);
                half top         = Luminance(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( 0,     ts.y)).rgb);
                half topRight    = Luminance(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( ts.x,  ts.y)).rgb);
                half left        = Luminance(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-ts.x,  0)).rgb);
                half right       = Luminance(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( ts.x,  0)).rgb);
                half bottomLeft  = Luminance(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-ts.x, -ts.y)).rgb);
                half bottom      = Luminance(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( 0,    -ts.y)).rgb);
                half bottomRight = Luminance(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( ts.x, -ts.y)).rgb);

                half gx = -topLeft  - 2.0*left - bottomLeft + topRight + 2.0*right + bottomRight;
                half gy = -topLeft  - 2.0*top  - topRight   + bottomLeft + 2.0*bottom + bottomRight;
                return sqrt(gx*gx + gy*gy);
            }

            half4 fragEdge(Varyings IN) : SV_Target
            {
                half4 base = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half edge = step(_EdgeThreshold, Sobel(IN));
                half3 result = lerp(base.rgb, _EdgeColor.rgb, edge * _EdgeColor.a);
                return half4(result, base.a);
            }
            ENDHLSL
        }

        // ═══════════════════════════════════════════════
        // Pass 4: InkVignette — 暗角/画框感
        // ═══════════════════════════════════════════════
        Pass
        {
            Name "InkVignette"
            HLSLPROGRAM
            #pragma vertex FullscreenVert
            #pragma fragment fragVignette
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _VignetteColor;
            float  _VignetteIntensity;
            float  _WeatherWetness;
            float  _FlyingWhiteStrength;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

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

                // 暗角：边缘渐暗
                float2 uvCentered = IN.uv - 0.5;
                float vignette = 1.0 - dot(uvCentered, uvCentered) * 1.5;
                vignette = saturate(vignette);
                vignette = lerp(1.0, vignette, _VignetteIntensity);

                // 天气湿润度：雨/雾天画面更柔
                float wetBlend = _WeatherWetness * 0.3;
                half3 wetTint = lerp(base.rgb, base.rgb * 0.85, wetBlend);

                // 飞白：秋天随机降饱和度模拟笔触干枯
                float flyingWhite = _FlyingWhiteStrength * 0.4;
                half3 desaturated = lerp(wetTint, dot(wetTint, half3(0.299, 0.587, 0.114)), flyingWhite);

                half3 result = desaturated * lerp(_VignetteColor.rgb, half3(1,1,1), vignette);
                return half4(result, base.a);
            }
            ENDHLSL
        }
    }
}
