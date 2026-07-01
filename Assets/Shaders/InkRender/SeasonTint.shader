Shader "TeaMist/InkRender/SeasonTint"
{
    // ── 四季全局调色 ──
    // 根据季节参数对全域画面应用中国传统色彩偏移
    // 春：青灰回暖 │ 夏：浓绿润泽 │ 秋：赭褐温暖 │ 冬：枯墨萧瑟
    Properties
    {
        _MainTex ("主画面", 2D) = "white" {}

        [KeywordEnum(Spring, Summer, Autumn, Winter)]
        _SEASON ("当前季节", Float) = 0

        // 春季调色
        _SpringShift ("春·色调偏移 (Hue)", Range(-0.1, 0.1)) = 0.03
        _SpringSat   ("春·饱和度", Range(0, 2)) = 0.7
        _SpringWarm  ("春·暖色调", Color) = (0.88, 0.92, 0.78, 0)

        // 夏季调色
        _SummerShift ("夏·色调偏移", Range(-0.1, 0.1)) = -0.02
        _SummerSat   ("夏·饱和度", Range(0, 2)) = 1.1
        _SummerWarm  ("夏·暖色调", Color) = (0.75, 0.88, 0.72, 0)

        // 秋季调色
        _AutumnShift ("秋·色调偏移", Range(-0.1, 0.1)) = 0.05
        _AutumnSat   ("秋·饱和度", Range(0, 2)) = 0.85
        _AutumnWarm  ("秋·暖色调", Color) = (0.82, 0.72, 0.58, 0)

        // 冬季调色
        _WinterShift ("冬·色调偏移", Range(-0.1, 0.1)) = -0.05
        _WinterSat   ("冬·饱和度", Range(0, 2)) = 0.4
        _WinterWarm  ("冬·暖色调", Color) = (0.72, 0.74, 0.78, 0)

        _BlendFactor  ("季节过渡 (0=上季, 1=当季)", Range(0, 1)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "SeasonTint"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature _SEASON_SPRING _SEASON_SUMMER _SEASON_AUTUMN _SEASON_WINTER

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
                float4 _SpringWarm;
                float4 _SummerWarm;
                float4 _AutumnWarm;
                float4 _WinterWarm;
                float  _SpringShift;
                float  _SpringSat;
                float  _SummerShift;
                float  _SummerSat;
                float  _AutumnShift;
                float  _AutumnSat;
                float  _WinterShift;
                float  _WinterSat;
                float  _BlendFactor;
            CBUFFER_END

            // RGB → HSV
            half3 rgb2hsv(half3 c)
            {
                half4 K = half4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                half4 p = lerp(half4(c.bg, K.wz), half4(c.gb, K.xy), step(c.b, c.g));
                half4 q = lerp(half4(p.xyw, c.r), half4(c.r, p.yzx), step(p.x, c.r));
                half d = q.x - min(q.w, q.y);
                half e = 1.0e-10;
                return half3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }

            // HSV → RGB
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
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // 根据当前季节选择调色参数
                half hueShift, saturation;
                half3 warmColor;

                #if defined(_SEASON_SPRING)
                    hueShift = _SpringShift; saturation = _SpringSat; warmColor = _SpringWarm.rgb;
                #elif defined(_SEASON_SUMMER)
                    hueShift = _SummerShift; saturation = _SummerSat; warmColor = _SummerWarm.rgb;
                #elif defined(_SEASON_AUTUMN)
                    hueShift = _AutumnShift; saturation = _AutumnSat; warmColor = _AutumnWarm.rgb;
                #else // _SEASON_WINTER
                    hueShift = _WinterShift; saturation = _WinterSat; warmColor = _WinterWarm.rgb;
                #endif

                // 转 HSV 调整色相和饱和度
                half3 hsv = rgb2hsv(tex.rgb);
                hsv.r = frac(hsv.r + hueShift);
                hsv.g = saturate(hsv.g * saturation);
                half3 adjusted = hsv2rgb(hsv);

                // 暖色叠加
                adjusted = lerp(adjusted, adjusted * warmColor, 0.3);

                // 灰度：保留 Chinese Ink 质感
                half gray = dot(tex.rgb, half3(0.299, 0.587, 0.114));
                adjusted = lerp(adjusted, half3(gray, gray, gray), 0.25);

                return half4(adjusted, tex.a);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
