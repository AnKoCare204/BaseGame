// Quả cầu token (spawner / chìa trong bóng): dán một ảnh vẽ sẵn (quả cầu bóng đã có sáng tối) lên mesh sphere theo pháp tuyến
// view-space thay vì UV của mesh. Điểm trên mặt cầu nhìn về hướng nào thì lấy pixel tương ứng trên ảnh, nên ảnh luôn hướng thẳng camera
// dù thân token xoay theo hướng bơi, và mép ảnh trùng khít mép silhouette của quả cầu. Ảnh phải là hình vuông, quả cầu nằm giữa và chạm mép.
// Vẫn là MeshRenderer nên SetSilhouette (đổi material thành bóng đen) hoạt động như cũ.
Shader "FishSort/Token Sprite Sphere"
{
    Properties
    {
        _MainTex ("Sprite (hinh vuong, qua cau o giua cham mep)", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Zoom ("Zoom (1 = anh vua khit silhouette; <1 thu anh vao trong, >1 phong to)", Range(0.8, 1.5)) = 1.0
        _EdgeFade ("Edge Fade (lam mem mep ngoai)", Range(0, 0.3)) = 0.04
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            // Không đặt LightMode để pass được coi là SRPDefaultUnlit: cả 2D Renderer lẫn Universal Renderer đều vẽ (giống Soft Bubble Cartoon).
            Name "TokenSprite"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Zoom;
                half _EdgeFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Pháp tuyến view-space: xy = vị trí trên đĩa nhìn từ camera (tâm 0, mép 1), y hướng lên.
                float3 normalVS = normalize(TransformWorldToViewDir(input.normalWS));
                float2 p = normalVS.xy;
                float s = saturate(length(p));
                float2 uv = p * (0.5 / max(_Zoom, 1e-3)) + 0.5;
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * _Color;
                // Mép mềm: mờ dần trong khoảng _EdgeFade cuối của bán kính để silhouette không răng cưa.
                half edge = 1.0 - smoothstep(1.0 - max(_EdgeFade, 1e-4), 1.0, s) * step(1e-4, _EdgeFade);
                return half4(tex.rgb, tex.a * edge);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
