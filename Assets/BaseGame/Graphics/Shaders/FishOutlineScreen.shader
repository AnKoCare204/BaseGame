// Shader cho FishOutlineFeature (viền màn hình quanh silhouette cá đang highlight / stroke cá mẫu trên bể, kể cả vây / đuôi mỏng).
// Pass 0 Mask:      vẽ renderer có rendering layer của nhóm viền bằng override material -> mask trắng (ZTest Always: lấy trọn silhouette
//                   kể cả phần bị cá khác che).
// Pass 1 HDist:     quét ngang ±R texel trên mask, ghi khoảng cách ngang tới điểm mask gần nhất (chuẩn hoá theo R+1).
// Pass 2 Coverage:  quét dọc ±R hàng của HDist, d = min sqrt(dx*dx + dy*dy) = khoảng cách Euclid chính xác trong bán kính R;
//                   ra 1 ở vành 0 < d <= W (mềm 1 texel ở mép ngoài), 0 trong silhouette.
// Pass 3 Composite: blend màu viền lên màn hình theo coverage (nội suy bilinear khi texture nhỏ hơn màn hình).
// Pass 1-3 xử lý một vùng chữ nhật (VertRect): toàn màn hình, hoặc chỉ vùng chiếu của AABB thế giới _FishOutlineBoundsMin/Max (stroke
// icon: vài icon nhỏ nên chạy full-res vẫn rẻ). Vùng chiếu tính bằng UNITY_MATRIX_VP, cùng ma trận với pass Mask nên luôn khớp mask (kể cả y-flip).
// Vùng này vẽ bằng MỘT tam giác (như full-screen triangle của Blit.hlsl), không dùng quad 2 tam giác: trên GPU Mali (Pixel 6 Pro, Vulkan)
// hàng texel dọc cạnh chéo chung của quad ra sai, texture đích không clear nên thành rác -> sọc màu viền chạy chéo màn hình.
// Mọi tham số truyền qua MaterialPropertyBlock từ FishOutlineFeature: _FishOutlineParams = (R, W, 1/width, 1/height), _FishOutlineColor.
Shader "FishSort/FishOutlineScreen"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float4 _FishOutlineParams;
        float4 _FishOutlineBoundsMin; // xyz: AABB min (world); w: 1 = chỉ vẽ trong vùng chiếu của AABB, 0 = toàn màn hình
        float4 _FishOutlineBoundsMax; // xyz: AABB max (world); w: lề thêm quanh vùng chiếu (texel của texture đích) cho pass này

        struct RectVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 texcoord   : TEXCOORD0;
            nointerpolation float4 uvRect : TEXCOORD1; // xy: uv min, zw: uv max của vùng xử lý; ngoài vùng thì discard
            UNITY_VERTEX_OUTPUT_STEREO
        };

        // Một tam giác (3 đỉnh, DrawProcedural) vuông góc ở góc dưới trái của vùng nới thêm 1 texel, hai cạnh góc vuông dài gấp đôi:
        // vùng nằm trọn bên trong, mọi cạnh tam giác ở ngoài vùng nên không còn cạnh chéo nào cắt qua. Vùng toàn màn hình thì tam giác
        // gần như full-screen triangle của Blit.hlsl. texcoord khớp texel với Blit.hlsl.
        RectVaryings VertRect(Attributes input)
        {
            RectVaryings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            float4 rect = float4(-1.0, -1.0, 1.0, 1.0);
            if (_FishOutlineBoundsMin.w > 0.5)
            {
                float2 lo = 1e9;
                float2 hi = -1e9;
                bool valid = true;
                for (int i = 0; i < 8; i++)
                {
                    float3 corner = float3((i & 1) ? _FishOutlineBoundsMax.x : _FishOutlineBoundsMin.x,
                                           (i & 2) ? _FishOutlineBoundsMax.y : _FishOutlineBoundsMin.y,
                                           (i & 4) ? _FishOutlineBoundsMax.z : _FishOutlineBoundsMin.z);
                    float4 cornerCS = mul(UNITY_MATRIX_VP, float4(corner, 1.0));
                    if (cornerCS.w <= 0.0) valid = false; // góc sau camera: bỏ giới hạn, vẽ toàn màn hình
                    float2 ndc = cornerCS.xy / max(cornerCS.w, 1e-5);
                    lo = min(lo, ndc);
                    hi = max(hi, ndc);
                }

                float2 pad = _FishOutlineBoundsMax.w * 2.0 * _FishOutlineParams.zw;
                if (valid) rect = float4(max(lo - pad, -1.0), min(hi + pad, 1.0));
            }

            float2 margin = 2.0 * _FishOutlineParams.zw; // 1 texel (NDC)
            float2 triMin = rect.xy - margin;
            float2 triSize = rect.zw + margin - triMin;
            uint id = input.vertexID;
            float2 ndcPos = triMin + float2(id == 1, id == 2) * 2.0 * triSize;
            float2 uv = ndcPos * 0.5 + 0.5;
            float4 uvRect = rect * 0.5 + 0.5;
            #if UNITY_UV_STARTS_AT_TOP
            uv.y = 1.0 - uv.y;
            uvRect = float4(uvRect.x, 1.0 - uvRect.w, uvRect.z, 1.0 - uvRect.y);
            #endif

            output.positionCS = float4(ndcPos, UNITY_NEAR_CLIP_VALUE, 1.0);
            #ifdef UNITY_PRETRANSFORM_TO_DISPLAY_ORIENTATION
            output.positionCS = ApplyPretransformRotation(output.positionCS);
            #endif
            output.texcoord = uv;
            output.uvRect = uvRect;
            return output;
        }

        // Bỏ điểm ảnh ngoài vùng xử lý (tam giác phủ rộng hơn vùng). Vùng toàn màn hình thì không bỏ gì.
        void ClipToRect(RectVaryings input)
        {
            float2 inside = step(input.uvRect.xy, input.texcoord) * step(input.texcoord, input.uvRect.zw);
            clip(inside.x * inside.y - 0.5);
        }
        ENDHLSL

        Pass
        {
            Name "FishOutlineMask"

            HLSLPROGRAM
            #pragma vertex MaskVert
            #pragma fragment MaskFrag

            struct MaskAttributes
            {
                float4 positionOS : POSITION;
            };

            struct MaskVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            MaskVaryings MaskVert(MaskAttributes input)
            {
                MaskVaryings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 MaskFrag(MaskVaryings input) : SV_Target
            {
                return 1;
            }
            ENDHLSL
        }

        Pass
        {
            Name "FishOutlineHDist"

            HLSLPROGRAM
            #pragma vertex VertRect
            #pragma fragment Frag

            half4 Frag(RectVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                ClipToRect(input);
                int radius = (int)_FishOutlineParams.x;
                float texel = _FishOutlineParams.z;
                float best = _FishOutlineParams.x + 1.0;
                // Quét từ tâm ra hai phía, gặp mask là dừng: khoảng cách ngang nhỏ nhất.
                [loop]
                for (int t = 0; t <= radius; t++)
                {
                    float2 offset = float2(t * texel, 0.0);
                    float m = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, input.texcoord + offset, 0).r
                            + SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, input.texcoord - offset, 0).r;
                    if (m > 0.5)
                    {
                        best = t;
                        break;
                    }
                }
                return best / (_FishOutlineParams.x + 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "FishOutlineCoverage"

            HLSLPROGRAM
            #pragma vertex VertRect
            #pragma fragment Frag

            half4 Frag(RectVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                ClipToRect(input);
                int radius = (int)_FishOutlineParams.x;
                float scale = _FishOutlineParams.x + 1.0;
                float width = _FishOutlineParams.y;
                float texel = _FishOutlineParams.w;
                float d = scale;
                [loop]
                for (int t = -radius; t <= radius; t++)
                {
                    float dx = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, input.texcoord + float2(0.0, t * texel), 0).r * scale;
                    d = min(d, sqrt(dx * dx + (float)(t * t)));
                }
                float inside = step(d, 0.5);
                float ring = 1.0 - smoothstep(width - 0.5, width + 0.5, d);
                return ring * (1.0 - inside);
            }
            ENDHLSL
        }

        Pass
        {
            Name "FishOutlineComposite"
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex VertRect
            #pragma fragment Frag

            half4 _FishOutlineColor;

            half4 Frag(RectVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                ClipToRect(input);
                half coverage = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, input.texcoord, 0).r;
                return half4(_FishOutlineColor.rgb, coverage * _FishOutlineColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
