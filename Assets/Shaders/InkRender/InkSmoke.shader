Shader "TeaMist/InkRender/InkSmoke"
{
    // ── 茶烟/云雾粒子 ──
    // 模拟水墨画中茶烟袅袅、云雾缭绕的动态效果
    // 使用噪声叠加而非粒子系统，减少 Draw Call
    // 支持多个烟雾层，每层独立方向/速度/浓度
    Properties
    {
        _NoiseTex     ("噪声纹理", 2D) = "white" {}
        _GradientTex  ("渐变纹理", 2D) = "white" {}

        _SmokeColor   ("烟雾墨色", Color) = (0.35, 0.33, 0.31, 0.4)
        _SmokeOpacity ("烟雾总体浓度", Range(0, 1)) = 0.35
        _FlowSpeed    ("流动速度", Range(0, 2)) = 0.3
        _FlowDirection("流动方向", Vector) = (0, 0.3, 0, 0) // XY 方向

        _Turbulence   ("湍流强度", Range(0, 0.5)) = 0.08
        _SmokeScale   ("烟雾缩放", Range(0.5, 5)) = 2.0

        _LayerOffset1 ("层1偏移", Vector) = (0, 0, 0, 0)
        _LayerOffset2 ("层2偏移", Vector) = (0.5, 0.3, 0, 0)
        _LayerOffset3 ("层3偏移", Vector) = (-0.3, 0.7, 0, 0)

        _DistortionStrength ("扭曲强度", Range(0, 0.1)) = 0.02
        _RiseHeight    ("升腾高度衰减", Range(0, 3)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        LOD 100
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "InkSmoke"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_GradientTex);
            SAMPLER(sampler_GradientTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseTex_ST;
                float4 _SmokeColor;
                float4 _FlowDirection;
                float4 _LayerOffset1;
                float4 _LayerOffset2;
                float4 _LayerOffset3;
                float  _SmokeOpacity;
                float  _FlowSpeed;
                float  _Turbulence;
                float  _SmokeScale;
                float  _DistortionStrength;
                float  _RiseHeight;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _NoiseTex) * _SmokeScale;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half2 uv = IN.uv;

                // 时间流动
                half time = _Time.y * _FlowSpeed;

                // ── 三层噪声叠加 ──
                half smoke = 0;

                // 层 1：大尺度缓慢流动
                half2 uv1 = uv + _LayerOffset1.xy + _FlowDirection.xy * time * 0.5 +
                            half2(sin(uv.y * 3.0 + time), cos(uv.x * 2.0 + time * 0.7)) * _Turbulence;
                half n1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv1).r;
                smoke += n1 * 0.5;

                // 层 2：中等尺度，不同方向
                half2 uv2 = uv * 1.7 + _LayerOffset2.xy + _FlowDirection.xy * time * 0.8 -
                            half2(cos(uv.y * 2.5 + time * 1.2), sin(uv.x * 3.0 + time * 0.5)) * _Turbulence * 0.7;
                half n2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv2).r;
                smoke += n2 * 0.3;

                // 层 3：小尺度细节
                half2 uv3 = uv * 3.2 + _LayerOffset3.xy + _FlowDirection.xy * time * 1.3 +
                            half2(sin(uv.x * 5.0 + time * 1.5) * _Turbulence * 0.5, cos(uv.y * 4.0 - time)) * _Turbulence * 0.5;
                half n3 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv3).r;
                smoke += n3 * 0.2;

                // 归一化并应用渐变映射
                smoke = saturate(smoke * 0.8);
                half gradientU = smoke;
                half3 gradientColor = SAMPLE_TEXTURE2D(_GradientTex, sampler_GradientTex, half2(gradientU, 0.5)).rgb;

                // 升腾高度衰减：烟雾越往上越淡
                float heightFade = 1.0 - IN.uv.y * _RiseHeight;
                heightFade = saturate(heightFade);

                // 组合
                half3 finalColor = lerp(half3(1, 1, 1), gradientColor * _SmokeColor.rgb, smoke);
                half alpha = smoke * _SmokeOpacity * _SmokeColor.a * heightFade * IN.color.a;

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
