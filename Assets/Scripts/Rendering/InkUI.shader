Shader "TeaMist/UI/InkUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        _PaperTex ("宣纸纹理", 2D) = "white" {}
        _PaperStrength ("纸纹强度", Range(0, 0.5)) = 0.12
        _PaperTiling ("纸纹平铺", Range(0.1, 4)) = 1.0
        _EdgeSoftness ("边缘晕染", Range(0, 0.5)) = 0.08
        _EdgeNoise ("边缘不规则", Range(0, 0.3)) = 0.1
        _InkDarken ("墨色加深", Range(0, 1)) = 0.25
        _InkBleed ("墨晕扩散", Range(0, 0.5)) = 0.05

        // UI 系统必需 stencil 属性
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255

        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
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
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _MainTex_TexelSize;
            float4 _ClipRect;

            sampler2D _PaperTex;
            float4 _PaperTex_ST;
            float _PaperStrength;
            float _PaperTiling;
            float _EdgeSoftness;
            float _EdgeNoise;
            float _InkDarken;
            float _InkBleed;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.color = v.color * _Color;
                OUT.texcoord = v.texcoord;
                OUT.worldPosition = v.vertex;
                return OUT;
            }

            // 简易 value noise
            float hash2(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float noise2d(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash2(i);
                float b = hash2(i + float2(1,0));
                float c = hash2(i + float2(0,1));
                float d = hash2(i + float2(1,1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int i = 0; i < 3; i++)
                {
                    v += a * noise2d(p);
                    p *= 2.0;
                    a *= 0.5;
                }
                return v;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 texColor = tex2D(_MainTex, IN.texcoord);
                half4 col = texColor * IN.color;

                // 纸纹采样（随 rect 尺寸自适应平铺）
                float2 paperUV = IN.texcoord * _PaperTiling;
                float paper = tex2D(_PaperTex, paperUV).r;
                float paperNoise = 0.9 + (paper - 0.5) * _PaperStrength;

                // 边缘不规则晕染：用 FBM 扰动 UV 距离
                float2 edgeUV = IN.texcoord * 2.0 - 1.0;
                float edgeDist = 1.0 - max(abs(edgeUV.x), abs(edgeUV.y));
                float edgeNoise = fbm(IN.texcoord * 12.0 + float2(_SinTime.w, _CosTime.w) * 0.0) * _EdgeNoise;
                float edgeAlpha = smoothstep(0.0, _EdgeSoftness + edgeNoise * 0.1, edgeDist);

                // 墨晕：暗角 + 颜色加深
                float vignette = 1.0 - (1.0 - edgeAlpha) * _InkBleed;
                col.rgb = lerp(col.rgb, col.rgb * (1.0 - _InkDarken), (1.0 - edgeAlpha));
                col.rgb *= paperNoise;
                col.a *= edgeAlpha;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
