// Ảnh UI (uGUI Image / RawImage) có gợn sóng nước kiểu FishDom. Vòng KHÔNG được vẽ đè bằng màu riêng: mỗi gợn là 2 đỉnh sóng
// đồng tâm nở ra, độ dốc của sóng (a) lệch UV để khúc xạ ảnh bên dưới và (b) làm sáng / tối ảnh theo hướng ánh sáng, nên vòng
// "trong suốt", màu là màu nền bị biến dạng; chỉ cộng thêm một ánh nhẹ ở đỉnh sóng (_ShineColor) và tint nhân rất nhẹ (_TintColor).
// Dữ liệu gợn do UIWaterRipple.cs đẩy lên mỗi frame (đã tính sẵn bán kính / độ mờ). Tọa độ gợn và bán kính tính bằng pixel màn hình,
// nên nhiều lớp UI (nền + lớp cá) dùng chung một bộ dữ liệu vẫn khớp nhau; ở lớp có pixel trong suốt (RawImage cá) phần sáng / tối
// là phép nhân nên vùng trong suốt không hiện gì, để lộ lớp nền bên dưới cũng đang gợn.
Shader "FishSort/UI/Water Ripple"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _LightDir ("Light Dir (xy)", Vector) = (-0.4, 0.9, 0, 0)
        _ShadeStrength ("Shade Strength", Range(0, 2)) = 0.45
        _TintColor ("Crest Tint (a = amount)", Color) = (1, 0.9, 0.65, 0.3)
        _ShineColor ("Crest Shine (a = amount)", Color) = (1, 0.96, 0.8, 0.25)
        _InnerRingRatio ("Inner Ring Ratio", Range(0, 1)) = 0.62
        _InnerRingStrength ("Inner Ring Strength", Range(0, 1)) = 0.7

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
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
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #define MAX_RIPPLES 48

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
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            float4 _LightDir;
            half _ShadeStrength;
            half4 _TintColor;
            half4 _ShineColor;
            half _InnerRingRatio;
            half _InnerRingStrength;

            // _RippleUVRect: xy = UV góc trái dưới của vùng vẽ, zw = kích thước UV.
            // _RippleScreenRect: xy = góc trái dưới vùng vẽ (pixel màn hình), zw = kích thước (pixel màn hình).
            float4 _RippleUVRect;
            float4 _RippleScreenRect;
            int _RippleCount;
            // A = (tâm x, tâm y, bán kính hiện tại, độ đậm 0..1) - pixel màn hình; B = (độ dày đỉnh sóng, biên độ khúc xạ, 0, 0).
            float4 _RippleA[MAX_RIPPLES];
            float4 _RippleB[MAX_RIPPLES];

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            // Một đỉnh sóng tại bán kính r, nửa rộng w: trả về chiều cao (bump, 0..1) và độ dốc theo hướng tâm (slope, ±1.54 max).
            void Crest(float d, float r, float w, out float bump, out float slope)
            {
                float x = (d - r) / w;
                float q = saturate(1.0 - x * x);
                bump = q * q;
                slope = -4.0 * x * q;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float2 uvSize = max(_RippleUVRect.zw, 1e-5);
                float2 screenSize = max(_RippleScreenRect.zw, 1e-3);
                float2 pos = _RippleScreenRect.xy + (uv - _RippleUVRect.xy) / uvSize * screenSize;
                float2 lightDir = normalize(_LightDir.xy + float2(1e-4, 0));

                float2 offset = 0;
                float shade = 0; // độ dốc chiếu lên hướng sáng: + sáng, - tối
                float crest = 0; // chiều cao đỉnh sóng, cho tint / ánh

                for (int i = 0; i < _RippleCount; i++)
                {
                    float4 a = _RippleA[i];
                    float4 b = _RippleB[i];
                    float2 delta = pos - a.xy;
                    float d = length(delta);
                    float r = a.z;
                    float w = b.x;
                    if (d > r + w) continue;

                    float2 dir = delta / max(d, 1e-3);
                    float fade = a.w;

                    float bump1, slope1, bump2, slope2;
                    Crest(d, r, w, bump1, slope1);
                    Crest(d, r * _InnerRingRatio, w * 0.85, bump2, slope2);
                    float bump = (bump1 + _InnerRingStrength * bump2) * fade;
                    float slope = (slope1 + _InnerRingStrength * slope2) * fade;

                    offset += dir * slope * b.y; // khúc xạ: một bên kéo ra, một bên dồn vào
                    shade += slope * dot(dir, lightDir);
                    crest += bump;
                }

                uv += offset / screenSize * uvSize;
                half4 color = (tex2D(_MainTex, uv) + _TextureSampleAdd) * IN.color;

                // Sáng / tối theo độ dốc: vòng hiện lên từ chính màu nền, không vẽ màu riêng.
                color.rgb *= max(1.0 + shade * _ShadeStrength, 0.0);
                half c = saturate(crest);
                color.rgb *= lerp(1.0, _TintColor.rgb, c * _TintColor.a);
                color.rgb += _ShineColor.rgb * (c * c * c) * _ShineColor.a * color.a;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
