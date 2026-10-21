// Tia sáng dưới nước cho UI (uGUI Image), cộng sáng lên lớp nước (Blend SrcAlpha One).
// Texture là tia trắng trên nền đen; dùng kênh R làm cường độ. Tia lắc quanh _Pivot (mặc định giữa mép trên):
// lấy texture 2 lần với 2 góc lắc lệch pha / lệch tốc độ rồi trộn theo _LayerMix, để các tia trượt qua nhau thay vì cả quạt
// lắc cứng. Cường độ "thở" giữa _IntensityMin.._IntensityMax. Góc tính theo giây: sin(_Time.y * tốc độ) (rad/s).
Shader "FishSort/UI/Light Rays"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "black" {}
        _Color ("Tint (a = độ mạnh)", Color) = (1,1,1,1)

        _Pivot ("Pivot (UV xy)", Vector) = (0.5, 1, 0, 0)
        _Aspect ("Texture Aspect (w/h)", Float) = 0.6667
        _SwayAngle ("Sway Angle (độ)", Range(0, 15)) = 3
        _SwaySpeed ("Sway Speed (rad/s)", Range(0, 3)) = 0.35
        _LayerMix ("Second Layer Mix", Range(0, 1)) = 0.45
        _IntensityMin ("Intensity Min", Range(0, 2)) = 0.55
        _IntensityMax ("Intensity Max", Range(0, 2)) = 0.9
        _BreathSpeed ("Breath Speed (rad/s)", Range(0, 3)) = 0.6

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
        Blend SrcAlpha One
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

            float4 _Pivot;
            float _Aspect;
            float _SwayAngle;
            float _SwaySpeed;
            half _LayerMix;
            half _IntensityMin;
            half _IntensityMax;
            float _BreathSpeed;

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

            // Xoay UV quanh pivot trong không gian đã bù tỉ lệ khung, để góc xoay không bị méo theo chiều ngang.
            float2 RotateAround(float2 uv, float2 pivot, float angle)
            {
                float2 d = uv - pivot;
                d.x *= _Aspect;
                float s = sin(angle);
                float c = cos(angle);
                d = float2(c * d.x - s * d.y, s * d.x + c * d.y);
                d.x /= _Aspect;
                return pivot + d;
            }

            // 1 trong ô UV [0,1], 0 ngoài (không để texture clamp kéo dài mép ra).
            half Inside(float2 uv)
            {
                float2 s = step(0.0, uv) * step(uv, 1.0);
                return s.x * s.y;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float t = _Time.y;
                float amp = radians(_SwayAngle);
                float a1 = amp * sin(t * _SwaySpeed);
                float a2 = -amp * 1.3 * sin(t * _SwaySpeed * 0.77 + 1.7);

                float2 uv1 = RotateAround(IN.texcoord, _Pivot.xy, a1);
                float2 uv2 = RotateAround(IN.texcoord, _Pivot.xy, a2);
                half r1 = (tex2D(_MainTex, uv1).r + _TextureSampleAdd.r) * Inside(uv1);
                half r2 = (tex2D(_MainTex, uv2).r + _TextureSampleAdd.r) * Inside(uv2);
                half rays = lerp(r1, r2, _LayerMix);

                half breath = lerp(_IntensityMin, _IntensityMax, 0.5 + 0.5 * sin(t * _BreathSpeed));

                half4 color = IN.color;
                color.a *= saturate(rays * breath);

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
