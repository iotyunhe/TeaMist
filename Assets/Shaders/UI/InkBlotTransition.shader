Shader "TeaMist/UI/InkBlotTransition"
{
    // 墨滴扩散转场 v4 — 自然洇墨版
    // 方向性形状 + 多尺度噪声 + 触须渗墨 + 三层浓淡 + 柔和飞溅
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        _InkDeep   ("浓墨色", Color) = (0.05, 0.04, 0.03,  1)
        _InkWash   ("淡墨晕色", Color) = (0.18, 0.16, 0.13, 1)

        _Center    ("墨滴中心 (UV)", Vector) = (0.5, 0.5,  0, 0)
        _Radius    ("扩散半径", Range(0, 1.5)) = 0

        _NoiseScale ("渗墨细度", Range(10, 120)) = 45
        _NoiseAmp   ("渗墨强度", Range(0, 0.5)) = 0.18
        _NoiseSpeed ("渗墨蠕动速度", Range(0, 0.08)) = 0.015

        _EdgeSoft   ("边缘羽化", Range(0.002, 0.25)) = 0.08
        _Tendril    ("触须渗墨", Range(0, 0.5)) = 0.18
        _Splatter   ("飞溅墨点强度", Range(0, 0.5)) = 0.12

        _TimeParam  ("动画时间", Float) = 0
        [HideInInspector] _Opacity ("总不透明度", Range(0, 1)) = 1

        // UI stencil
        _StencilComp  ("Stencil Comparison", Float) = 8
        _Stencil      ("Stencil ID", Float) = 0
        _StencilOp    ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask  ("Stencil Read Mask", Float) = 255
        _ColorMask    ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Overlay"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "InkBlot"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _InkDeep;
                float4 _InkWash;
                float4 _Center;
                float  _Radius;
                float  _NoiseScale;
                float  _NoiseAmp;
                float  _NoiseSpeed;
                float  _EdgeSoft;
                float  _Tendril;
                float  _Splatter;
                float  _TimeParam;
                float  _Opacity;
                float4 _MainTex_ST;
            CBUFFER_END

            // ─── 标准 hash ───
            float hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // ─── 标准 value noise（双线性插值） ───
            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = hash21(i + float2(0.0, 0.0));
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // ─── 3 octave FBM ───
            float fbm3(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int i = 0; i < 3; i++)
                {
                    v += a * valueNoise(p);
                    p *= 2.0;
                    a *= 0.5;
                }
                return v;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                float2 uv = IN.uv;

                float2 offset = uv - _Center.xy;
                float dist = length(offset);
                float angle = atan2(offset.y, offset.x);

                // ─── 1. 方向性形状偏移 ───
                // 模拟纸纤维方向：某些角度渗透更快，墨滴非完美圆形
                float shapeN = valueNoise(float2(angle * 1.5 + 3.7, _Center.x * 8.0 + 1.2));
                float dirBias = 0.85 + shapeN * 0.3;  // 0.85~1.15
                float radiusEff = _Radius * dirBias;

                // ─── 2. 多尺度边缘噪声 ───
                // 大尺度 FBM 控制宏观轮廓，小尺度 valueNoise 增加细节
                float2 nc1 = float2(angle * 2.5, dist * _NoiseScale * 0.015) + _TimeParam * _NoiseSpeed;
                float2 nc2 = float2(angle * 6.0, dist * _NoiseScale * 0.04) + _TimeParam * _NoiseSpeed * 1.3;
                float n1 = fbm3(nc1 * 2.5);
                float n2 = valueNoise(nc2 * 1.5);
                float edgeN = n1 * 0.65 + n2 * 0.35;

                float noiseOffset = (edgeN - 0.5) * 2.0 * _NoiseAmp;
                float edgeFactor = smoothstep(0.0, radiusEff * 0.25, dist);
                noiseOffset *= edgeFactor;

                float effDist = dist - noiseOffset;

                // ─── 3. 触须渗墨 ───
                // 边缘外侧的高频细长渗透线，模拟墨汁沿纸纤维扩散
                float tendril = 0.0;
                if (radiusEff > 0.05 && _Tendril > 0.001)
                {
                    float tN = valueNoise(float2(angle * 10.0, dist * 25.0) + _TimeParam * 0.015);
                    float tMask = smoothstep(radiusEff * 0.7, radiusEff * 1.0, dist)
                                * (1.0 - smoothstep(radiusEff * 1.0, radiusEff * 1.35, dist));
                    tendril = pow(tN, 5.0) * tMask * _Tendril;
                }

                // ─── 4. 三层浓淡 ───
                // 浓墨核(0~0.55R) → 渗墨带(0.55~0.82R) → 淡墨晕(0.82~1.0R)
                float coreR = radiusEff * 0.55;
                float midR  = radiusEff * 0.82;
                float washR = radiusEff;

                float softVar = _EdgeSoft * (0.6 + edgeN * 0.8);

                float core = 1.0 - smoothstep(coreR - 0.015, coreR + 0.015, effDist);
                float mid  = saturate((1.0 - smoothstep(midR - softVar * 0.5, midR + softVar * 0.5, effDist)) - core);
                float wash = saturate((1.0 - smoothstep(washR - softVar, washR + softVar, effDist)) - core - mid);
                wash = saturate(wash + tendril);

                // ─── 5. 飞溅墨点 ───
                // smoothstep 替代 step，墨点有柔和边缘
                float splat = 0.0;
                if (_Splatter > 0.001 && radiusEff > 0.05)
                {
                    float splatZone = smoothstep(washR + softVar, washR + softVar + 0.1, dist)
                                    * (1.0 - smoothstep(washR + softVar + 0.1, washR + softVar + 0.2, dist));
                    float splatHash = hash21(uv * 70.0 + _Center.xy * 17.0 + _TimeParam * 0.02);
                    float splatThresh = 0.88 + hash21(uv * 130.0 + _TimeParam * 0.01) * 0.08;
                    splat = smoothstep(splatThresh, splatThresh + 0.05, splatHash) * splatZone * _Splatter * 2.5;
                }

                // ─── 6. 合成 ───
                // 墨色：浓墨核用深色，渗墨带混合，淡墨晕用浅色
                float inkAlpha = saturate(core * 0.92 + mid * 0.6 + wash * 0.3 + splat * 0.5) * _Opacity;
                float inkMask  = saturate(core + mid + wash + splat);
                float3 inkColor = lerp(_InkWash.rgb, _InkDeep.rgb, core + mid * 0.5);

                half4 result;
                result.rgb = lerp(texColor.rgb, inkColor, inkMask);
                result.a   = texColor.a * inkAlpha * IN.color.a;

                return result;
            }
            ENDHLSL
        }
    }
    FallBack "UI/Default"
}
