// Shader cho mọi model cá / prop trong game (material ở Graphics/Models/Fish Material): màu = texture × _BaseColor (không ánh sáng,
// ánh sáng đã vẽ sẵn trong texture, giống pass Universal2D của URP Lit mà Renderer2D vẫn dùng trước đây) + viền toon luôn bật.
// Hai pass vì Renderer2D vẽ tối đa một pass cho mỗi tag, thứ tự SRPDefaultUnlit rồi Universal2D:
//  1. Body (SRPDefaultUnlit): thân cá, ghi depth.
//  2. Outline (Universal2D): inverted hull. Đỉnh đẩy ra theo hướng viền trong kênh tangent (normal đã làm mượt lúc import bởi
//     FishSmoothNormalPostprocessor, nên cạnh cứng / seam UV không hở viền; SkinnedMeshRenderer skin cả tangent), đẩy trong không gian
//     màn hình nên bề dày tính theo pixel (chuẩn hoá theo màn cao 1280 như FishOutlineFeature), không đổi theo cỡ cá / khoảng cách.
//     Vẽ SAU thân nên phần vỏ nằm sau thân bị early-Z loại, chỉ tốn fill ở vành viền.
// Màu viền: lerp(_OutlineColor (màu phẳng), texture tại đỉnh × _OutlineTexTint (màu da làm tối), _OutlineTexMix). Texture lấy ở vertex
// shader (1 lần / đỉnh) nên fragment của viền chỉ trả hằng màu.
// Tối ưu mobile: không keyword (1 variant / pass), half cho màu, SRP Batcher (mọi thuộc tính trong UnityPerMaterial), không có pass
// ShadowCaster / DepthOnly (Renderer2D không dùng).
// Vây / tấm hở một lớp: nếu thấy viền phủ kín mặt sau của tấm, thử _OutlineCull = Off + _OutlineDepthPush nhỏ (đẩy vỏ ra sau theo độ sâu).
Shader "FishSort/Fish Toon Outline"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2

        [Header(Outline)]
        _OutlineWidth ("Width (px, screen height 1280)", Range(0, 12)) = 3
        _OutlineColor ("Flat Color", Color) = (0.08, 0.1, 0.2, 1)
        _OutlineTexTint ("Texture Tint", Color) = (0.45, 0.4, 0.5, 1)
        _OutlineTexMix ("Texture Mix (0 flat, 1 texture)", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _OutlineCull ("Cull", Float) = 1
        _OutlineDepthPush ("Depth Push (world units)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _OutlineColor;
            half4 _OutlineTexTint;
            half _OutlineTexMix;
            float _OutlineWidth;
            float _OutlineDepthPush;
            half _Cull;
            half _OutlineCull;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        ENDHLSL

        Pass
        {
            Name "Body"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex BodyVert
            #pragma fragment BodyFrag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings BodyVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 BodyFrag(Varyings input) : SV_Target
            {
                // Alpha giữ như pass Universal2D của URP Lit (texture.a × _BaseColor.a): RT của Home / ảnh chụp icon đọc kênh này.
                return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "Universal2D" }
            Cull [_OutlineCull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 tangentOS : TANGENT; // xyz = hướng viền (normal mượt), xem FishSmoothNormals
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 color : COLOR;
            };

            Varyings OutlineVert(Attributes input)
            {
                Varyings output;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 positionVS = TransformWorldToView(positionWS);
                positionVS.z -= _OutlineDepthPush; // view space nhìn về -Z: trừ = lùi xa camera
                float4 positionCS = TransformWViewToHClip(positionVS);

                // Hướng viền chiếu lên màn hình (đổi sang pixel trước khi chuẩn hoá để bề dày đều theo cả 2 trục khi màn không vuông).
                float3 directionWS = TransformObjectToWorldNormal(input.tangentOS.xyz);
                float2 directionPx = TransformWorldToHClipDir(directionWS).xy * _ScreenParams.xy;
                float lengthPx = length(directionPx);
                float widthPx = _OutlineWidth * _ScreenParams.y / 1280.0;
                float2 offsetPx = lengthPx > 1e-6 ? directionPx * (widthPx / lengthPx) : float2(0.0, 0.0);
                positionCS.xy += offsetPx * 2.0 / _ScreenParams.xy * positionCS.w;
                output.positionCS = positionCS;

                half3 texColor = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, TRANSFORM_TEX(input.uv, _BaseMap), 0).rgb;
                output.color = lerp(_OutlineColor.rgb, texColor * _OutlineTexTint.rgb, _OutlineTexMix);
                return output;
            }

            half4 OutlineFrag(Varyings input) : SV_Target
            {
                return half4(input.color, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
