// Cá silhouette bơi xa trên BG gameplay (uGUI Image do BgFishSchool điều khiển).
// - Uốn thân: lệch UV dọc theo sóng chạy từ đầu ra đuôi, biên độ tăng dần về phía đuôi (_TailPower) nên đuôi quẫy mạnh nhất.
//   Sprite phải chừa lề trên dưới (bg_layers.py fish-split chừa 30%) để phần uốn không bị cắt.
// - Pha quẫy đuôi của từng con nằm ở kênh R của màu Image (BgFishSchool đặt lúc ra đàn), alpha là độ mờ theo độ xa;
//   màu cá lấy từ texture × _Color.
// - Không chồng lớp: stencil bit 128 — điểm ảnh đã có cá thì con sau không vẽ đè, nên chỗ hai con chồng nhau không đậm lên.
//   Con vẽ trước nằm trên (BgFishSchool xếp cá gần lên trước). Viền cắt theo _Cutoff nên sắc hơn ảnh gốc một chút.
//   Bit 128 không đụng các bit thấp mà UI Mask lồng nhau dùng; stencil được xóa mỗi frame khi camera clear.
Shader "FishSort/UI/Swim Silhouette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _HeadU ("Head U (0 = đầu ở mép trái sprite)", Range(0, 1)) = 0
        _WaveAmp ("Wave Amplitude (UV dọc)", Range(0, 0.2)) = 0.06
        _WaveFreq ("Tail Beat (Hz)", Range(0, 5)) = 1.8
        _WaveLength ("Waves Along Body", Range(0, 2)) = 0.7
        _TailPower ("Tail Weight Power", Range(0.5, 4)) = 1.8
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
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

        // Cố định (không theo thuộc tính _Stencil của UI Mask): vẽ mỗi điểm ảnh một lần.
        Stencil
        {
            Ref 128
            ReadMask 128
            WriteMask 128
            Comp NotEqual
            Pass Replace
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

            float _HeadU;
            float _WaveAmp;
            float _WaveFreq;
            float _WaveLength;
            float _TailPower;
            half _Cutoff;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                // 0 ở đầu, 1 ở đuôi; sóng chạy từ đầu ra đuôi, pha riêng từng con ở kênh R.
                float along = saturate(abs(uv.x - _HeadU));
                float weight = pow(along, _TailPower);
                float phase = IN.color.r;
                uv.y += _WaveAmp * weight * sin(6.2831853 * (_WaveFreq * _Time.y + phase - _WaveLength * along));

                half4 tex = tex2D(_MainTex, uv) + _TextureSampleAdd;
                clip(tex.a - _Cutoff);

                half4 color = half4(tex.rgb * _Color.rgb, IN.color.a * _Color.a);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
