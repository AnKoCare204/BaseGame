Shader "Particles/WaterStylizedStreak_UI"
{
    Properties
    {
        _WaterDarkColor ("Màu Nước Tối (Có Alpha)", Color) = (0, 0.4, 0.8, 0.6)
        _WaterLightColor ("Màu Nước Sáng (Có Alpha)", Color) = (0.5, 0.9, 1, 0.9)
        
        _WaveTex ("Wave Streak Texture (Grayscale)", 2D) = "white" {}
        _WaveRotation ("Góc Xoay Wave (Degrees)", Range(0, 360)) = 0
        _WaveContrast ("Độ Gắt Giữa Vệt Sáng/Tối", Range(0.1, 5)) = 1.5

        [Header(Distortion Noise)]
        _DistortTex ("Noise Làm Biến Dạng UV", 2D) = "bump" {}
        _DistortSpeed ("Tốc Độ Sóng Sánh (X, Y)", Vector) = (0.05, 0.05, 0, 0)
        _DistortAmount ("Cường Độ Nhấp Nhô", Range(0, 0.2)) = 0.03
        
        [Header(Main Flow Speed)]
        _Speed ("Hướng & Tốc Độ Trôi Chính (X, Y)", Vector) = (-0.05, -0.02, 0, 0)
    }

    SubShader
    {
        Tags 
        { 
            "Queue" = "Transparent" 
            "IgnoreProjector" = "True" 
            "RenderType" = "Transparent" 
            "PreviewType" = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR; // Nhận màu/Alpha từ Particle System
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _WaveTex;
            float4 _WaveTex_ST;
            sampler2D _DistortTex;
            float4 _DistortTex_ST;

            fixed4 _WaterDarkColor;
            fixed4 _WaterLightColor;
            float _WaveRotation;
            float _WaveContrast;
            
            float2 _DistortSpeed;
            float _DistortAmount;
            float2 _Speed;

            float2 RotateUV(float2 uv, float rotationDegrees)
            {
                float rad = rotationDegrees * 0.01745329251;
                float s = sin(rad);
                float c = cos(rad);
                uv -= 0.5;
                float2 rotatedUV = float2(
                    uv.x * c - uv.y * s,
                    uv.x * s + uv.y * c
                );
                return rotatedUV + 0.5;
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color; // Truyền màu hạt (Particle Color) sang Fragment
                o.uv = v.texcoord;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 1. Biến dạng UV (Sóng sánh)
                float2 distortUV = i.uv * _DistortTex_ST.xy + _DistortSpeed * _Time.y;
                float2 distortion = (tex2D(_DistortTex, distortUV).rg - 0.5) * _DistortAmount;

                // 2. UV Trôi chính + Xoay
                float2 scrolledUV = i.uv * _WaveTex_ST.xy + _Speed * _Time.y;
                float2 distortedUV = scrolledUV + distortion;
                float2 finalUV = RotateUV(distortedUV, _WaveRotation);

                // 3. Đọc Texture vệt sóng
                float waveVal = tex2D(_WaveTex, finalUV).r;
                float streakMask = smoothstep(0.3, 0.7, pow(waveVal, _WaveContrast));

                // 4. Blend màu nước + Nhân trực tiếp với Color/Alpha over Lifetime của Particle
                fixed4 color = lerp(_WaterDarkColor, _WaterLightColor, streakMask) * i.color;

                return color;
            }
            ENDCG
        }
    }
}