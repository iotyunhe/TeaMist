Shader "TeaMist/InkRender/InkEdge"
{
    // ── 水墨边缘检测描边 ──
    // 基于深度法线做边缘检测，生成水墨笔触感的轮廓线
    // 不同于硬边勾线，水墨边缘有粗细变化和飞白
    Properties
    {
        _MainTex        ("主画面", 2D) = "white" {}
        _CameraDepthTex ("深度纹理", 2D) = "white" {}
        _CameraNormalsTex("法线纹理", 2D) = "white" {}

        _EdgeWidth      ("描边粗细", Range(0.5, 5.0)) = 1.5
        _DepthThreshold ("深度阈值", Range(0.01, 1.0)) = 0.08
        _NormalThreshold("法线阈值", Range(0.1, 2.0)) = 0.6
        _EdgeColor      ("描边墨色", Color) = (0.08, 0.06, 0.05, 1)
        _EdgeSoftness   ("边缘柔和度", Range(0, 1)) = 0.3
        _BrushVariation ("笔触粗细变化", Range(0, 1)) = 0.4
        _FlyWhite       ("飞白强度", Range(0, 1)) = 0.2  // 枯笔留白
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
            Name "InkEdge"
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
                float2 uv00       : TEXCOORD1;
                float2 uv10       : TEXCOORD2;
                float2 uv01       : TEXCOORD3;
                float2 uv11       : TEXCOORD4;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                float4 _EdgeColor;
                float  _EdgeWidth;
                float  _DepthThreshold;
                float  _NormalThreshold;
                float  _EdgeSoftness;
                float  _BrushVariation;
                float  _FlyWhite;
            CBUFFER_END

            // Roberts Cross 深度边缘检测
            half SampleDepth(float2 uv)
            {
                return SampleSceneDepth(uv);
            }

            half3 SampleNormal(float2 uv)
            {
                return SampleSceneNormals(uv);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;

                float2 texelSize = _MainTex_TexelSize.xy * _EdgeWidth;
                OUT.uv00 = IN.uv;               // center
                OUT.uv10 = IN.uv + float2( texelSize.x, 0);
                OUT.uv01 = IN.uv + float2(0, -texelSize.y);
                OUT.uv11 = IN.uv + float2( texelSize.x, -texelSize.y);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 1. Roberts Cross 深度边缘
                half d00 = SampleDepth(IN.uv00);
                half d10 = SampleDepth(IN.uv10);
                half d01 = SampleDepth(IN.uv01);
                half d11 = SampleDepth(IN.uv11);
                half depthEdge = abs(d00 - d11) + abs(d10 - d01);
                depthEdge = step(_DepthThreshold, depthEdge);

                // 2. 法线边缘检测
                half3 n00 = SampleNormal(IN.uv00);
                half3 n10 = SampleNormal(IN.uv10);
                half3 n01 = SampleNormal(IN.uv01);
                half3 n11 = SampleNormal(IN.uv11);
                half normalEdge = dot(abs(n00 - n11) + abs(n10 - n01), half3(0.333, 0.333, 0.333));
                normalEdge = step(_NormalThreshold, normalEdge);

                // 3. 合并深度和法线边缘
                half edge = max(depthEdge, normalEdge);

                // 4. 边缘柔和过渡
                edge = smoothstep(0.0, _EdgeSoftness, edge);

                // 5. 笔触粗细变化：用噪声调制边缘
                half noise = frac(sin(dot(IN.uv * 600.0, half2(127.1, 311.7))) * 43758.5453);
                half brushVar = lerp(1.0, noise, _BrushVariation);
                edge *= lerp(0.6, 1.4, brushVar - 0.5);

                // 6. 飞白效果：部分边缘像素随机留白
                half flyWhiteNoise = frac(sin(dot(IN.uv * 400.0, half2(89.3, 233.7))) * 31579.23);
                half flyWhiteMask = 1.0 - step(flyWhiteNoise, _FlyWhite);
                edge *= flyWhiteMask;

                // 7. 获取原始画面
                half4 original = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // 8. 边缘叠加到原画面
                half4 edgeOverlay = half4(lerp(original.rgb, _EdgeColor.rgb, edge), 1.0);

                return edgeOverlay;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
