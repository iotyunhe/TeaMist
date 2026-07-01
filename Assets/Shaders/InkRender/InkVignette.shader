Shader "TeaMist/InkRender/InkVignette"
{
    // ── 画面暗角 ──
    // 水墨画的天然"聚气"效果：画面中心清透，四周渐暗
    // 不是纯黑遮罩，而是纸质自然泛黄 + 边缘墨色渗透
    Properties
    {
        _MainTex      ("主画面", 2D) = "white" {}
        _VignetteColor("暗角墨色", Color) = (0.12, 0.10, 0.08, 1)
        _VignetteIntensity ("暗角强度", Range(0, 1)) = 0.5
        _VignetteSmoothness("暗角羽化", Range(0.01, 1)) = 0.3
        _VignetteRoundness ("暗角圆度", Range(0, 1)) = 0.5
        _CenterX       ("中心X偏移", Range(-0.5, 0.5)) = 0.0
        _CenterY       ("中心Y偏移", Range(-0.5, 0.5)) = 0.0

        // 纸质泛黄效果
        _PaperAge      ("纸龄泛黄", Range(0, 1)) = 0.2
        _AgeColor      ("泛黄色调", Color) = (0.92, 0.86, 0.72, 0)
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
            Name "InkVignette"
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
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _VignetteColor;
                float4 _AgeColor;
                float  _VignetteIntensity;
                float  _VignetteSmoothness;
                float  _VignetteRoundness;
                float  _CenterX;
                float  _CenterY;
                float  _PaperAge;
            CBUFFER_END

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

                // 1. 计算到中心的距离
                float2 centeredUV = IN.uv - float2(0.5 + _CenterX, 0.5 + _CenterY);
                // 考虑圆度：拉伸垂直方向
                float aspectCorrected = length(float2(centeredUV.x, centeredUV.y * lerp(1.0, 1.77, _VignetteRoundness)));
                // 归一化到 0~1
                float dist = aspectCorrected / 0.707; // 对角线一半

                // 2. 暗角计算（smoothstep 羽化）
                float vignette = smoothstep(0.2, _VignetteSmoothness + 0.3, dist);
                vignette = saturate(vignette * _VignetteIntensity);

                // 3. 边缘墨色渗透（不只是暗，是带色的墨）
                float3 vignetteColor = lerp(tex.rgb, _VignetteColor.rgb, vignette);

                // 4. 纸质泛黄
                float ageFactor = _PaperAge;
                vignetteColor = lerp(vignetteColor, vignetteColor * _AgeColor.rgb, ageFactor);

                return half4(vignetteColor, tex.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
