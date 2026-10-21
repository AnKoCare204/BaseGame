// Shader bong bóng lấy màu trực tiếp từ ảnh bong bóng nhìn thẳng (kiểu matcap) cho SoftBubble (URP, Renderer2D): màu trong game
// đúng bằng màu ảnh vì texture được vẽ nguyên, chỉ premultiply theo alpha. Toạ độ lấy mẫu là pháp tuyến view-space (.xy) như
// SoftBubbleCartoon nên khi bóng mềm bị ép méo, dải viền vẫn bám đúng silhouette, phần trong giãn theo như hình in trên bóng cao su.
//
// Ảnh được tách sẵn thành 3 lớp cùng khung (T_SoftBubbleRingOuter / RingInner / Highlight) để chỉnh riêng: mỗi lớp có Tint, Opacity và
// Over Fish (1 = vẽ đè lên cá, 0 = vẽ sau cá). Thứ tự ghép: viền trong -> viền ngoài -> phần sáng trắng.
//
// Hai pass vì Renderer2D vẽ tối đa một pass cho mỗi tag, thứ tự SRPDefaultUnlit rồi Universal2D:
//   - Body (SRPDefaultUnlit, Cull Front): các lớp Over Fish = 0 vẽ trên MẶT SAU của bóng. Cá là URP Lit opaque có ghi depth và bóng
//     (queue 3000) vẽ sau cá (2000) nên mặt sau bị cá che: cá luôn nổi trên các lớp này.
//   - Overlay (Universal2D, Cull Back): các lớp Over Fish = 1 vẽ trên mặt trước, đè lên cá.
//
// Remap bán kính: bán kính trong ảnh nơi dải viền bắt đầu (TexRingStart) được kéo ra RingStart trên bóng, nên viền có thể mỏng / dày hơn
// ảnh gốc mà không phải sửa ảnh. Opacity từng lớp nhân alpha: ảnh mẫu vẽ trên nền trắng nên viền cyan chỉ đục ~45%, lên nền nước xanh
// phải đục hơn mới ra đúng màu.
Shader "FishSort/Soft Bubble Matcap"
{
    Properties
    {
        [Header(Vien xanh ngoai)]
        _OuterTex ("Outer Ring Texture (mep sang ngoai cung)", 2D) = "black" {}
        _OuterTint ("Outer Tint (alpha nhan them)", Color) = (1, 1, 1, 1)
        _OuterOpacity ("Outer Opacity (nhan alpha)", Range(0, 4)) = 2.0
        [ToggleUI] _OuterOverFish ("Outer Over Fish (1 = de len ca, 0 = sau ca)", Float) = 1

        [Header(Vien xanh trong)]
        _InnerTex ("Inner Ring Texture (dai xanh mem ben trong)", 2D) = "black" {}
        _InnerTint ("Inner Tint (alpha nhan them)", Color) = (1, 1, 1, 1)
        _InnerOpacity ("Inner Opacity (nhan alpha)", Range(0, 4)) = 1.5
        [ToggleUI] _InnerOverFish ("Inner Over Fish (1 = de len ca, 0 = sau ca)", Float) = 0

        [Header(Phan sang trang)]
        _HighlightTex ("Highlight Texture (vet sang trang)", 2D) = "black" {}
        _HighlightTint ("Highlight Tint (alpha nhan them)", Color) = (1, 1, 1, 1)
        _HighlightOpacity ("Highlight Opacity (nhan alpha)", Range(0, 4)) = 1.0
        [ToggleUI] _HighlightOverFish ("Highlight Over Fish (1 = de len ca, 0 = sau ca)", Float) = 1

        [Header(Chung)]
        _TexRadius ("Tex Radius (ban kinh bong trong anh theo nua canh anh; 1 = bong cham mep anh)", Range(0.5, 1.5)) = 1.0
        _Tint ("Tint (nhan len ca 3 lop)", Color) = (1, 1, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 1.0
        _EdgeFade ("Edge Fade (lam mem mep ngoai)", Range(0, 0.3)) = 0.0

        [Header(Do day dai vien)]
        _TexRingStart ("Tex Ring Start (ban kinh trong anh noi dai vien bat dau)", Range(0, 1)) = 0.7
        _RingStart ("Ring Start (ban kinh tren bong noi dai vien bat dau; lon hon Tex Ring Start = vien mong hon anh)", Range(0.05, 0.98)) = 0.85

        [Header(Dissolve)]
        _Seed ("Seed (per bubble)", Float) = 0
        _Dissolve ("Dissolve", Range(0, 1)) = 0
        _DissolveNoiseScale ("Dissolve Noise Scale", Float) = 3.0
        _DissolveEdgeWidth ("Dissolve Edge Width", Range(0.01, 0.5)) = 0.08
        [HDR] _DissolveEdgeColor ("Dissolve Edge Color", Color) = (1.5, 2.5, 4.0, 1.0)

        [Header(Bung ra)]
        _Inflate ("Inflate (no mesh theo phap tuyen, don vi object; BubbleView dat luc lop phu bung ra va tan)", Float) = 0
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

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_OuterTex);
        SAMPLER(sampler_OuterTex);
        TEXTURE2D(_InnerTex);
        SAMPLER(sampler_InnerTex);
        TEXTURE2D(_HighlightTex);
        SAMPLER(sampler_HighlightTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _OuterTex_ST;
            half4 _OuterTint;
            half _OuterOpacity;
            half _OuterOverFish;
            float4 _InnerTex_ST;
            half4 _InnerTint;
            half _InnerOpacity;
            half _InnerOverFish;
            float4 _HighlightTex_ST;
            half4 _HighlightTint;
            half _HighlightOpacity;
            half _HighlightOverFish;
            half _TexRadius;
            half4 _Tint;
            half _Alpha;
            half _EdgeFade;
            half _TexRingStart;
            half _RingStart;
            float _Seed;
            half _Dissolve;
            float _DissolveNoiseScale;
            half _DissolveEdgeWidth;
            half4 _DissolveEdgeColor;
            float _Inflate;
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
            float3 positionOS : TEXCOORD1;
        };

        float Hash31(float3 p)
        {
            p = frac(p * 0.1031);
            p += dot(p, p.zyx + 31.32);
            return frac((p.x + p.y) * p.z);
        }

        float ValueNoise3(float3 p)
        {
            float3 i = floor(p);
            float3 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float n000 = Hash31(i + float3(0, 0, 0));
            float n100 = Hash31(i + float3(1, 0, 0));
            float n010 = Hash31(i + float3(0, 1, 0));
            float n110 = Hash31(i + float3(1, 1, 0));
            float n001 = Hash31(i + float3(0, 0, 1));
            float n101 = Hash31(i + float3(1, 0, 1));
            float n011 = Hash31(i + float3(0, 1, 1));
            float n111 = Hash31(i + float3(1, 1, 1));
            float x00 = lerp(n000, n100, f.x);
            float x10 = lerp(n010, n110, f.x);
            float x01 = lerp(n001, n101, f.x);
            float x11 = lerp(n011, n111, f.x);
            float y0 = lerp(x00, x10, f.y);
            float y1 = lerp(x01, x11, f.y);
            return lerp(y0, y1, f.z);
        }

        // Ghép lớp (Lc, La) lên trên (C premultiplied, A).
        void Over(inout half3 c, inout half a, half3 layerColor, half layerAlpha)
        {
            c = layerColor * layerAlpha + c * (1.0 - layerAlpha);
            a = layerAlpha + a * (1.0 - layerAlpha);
        }

        Varyings vert(Attributes input)
        {
            Varyings output;
            // Bung ra: đẩy đỉnh theo pháp tuyến nên lớp phủ to hơn bóng nền mà vẫn lõm theo bóng mềm; noise dissolve vẫn lấy vị trí gốc.
            VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz + normalize(input.normalOS) * _Inflate);
            output.positionCS = vertexInput.positionCS;
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.positionOS = input.positionOS.xyz;
            return output;
        }

        // Phần chung của hai pass: dissolve, toạ độ matcap, rồi ghép 3 lớp ảnh theo trọng số của pass (overlay = 1: lớp Over Fish = 1;
        // overlay = 0: lớp Over Fish = 0). Trả về màu premultiplied và s = |p| để làm mềm mép.
        void SampleLayers(Varyings input, half overlay, out half3 c, out half a, out float s, out float dissolveEdge)
        {
            // Dissolve theo noise 3D trong không gian bóng.
            float dissolveNoise = ValueNoise3(input.positionOS * _DissolveNoiseScale + _Seed * 7.0);
            float threshold = _Dissolve * (1.0 + _DissolveEdgeWidth) - _DissolveEdgeWidth;
            float dissolveDelta = dissolveNoise - threshold;
            clip(dissolveDelta);
            dissolveEdge = 1.0 - saturate(dissolveDelta / _DissolveEdgeWidth);

            // Toạ độ "trên mặt bóng nhìn từ camera": p = pháp tuyến view-space .xy, s = |p| (0 tâm, 1 mép). Mặt sau có normal.z < 0
            // nhưng .xy trùng với mặt trước ở cùng điểm màn hình nên hai pass lấy mẫu cùng một chỗ trong ảnh.
            float3 normalVS = normalize(TransformWorldToViewDir(normalize(input.normalWS)));
            float2 p = normalVS.xy;
            s = saturate(length(p));

            // Remap bán kính: [0, RingStart] -> [0, TexRingStart], [RingStart, 1] -> [TexRingStart, 1].
            float ringStart = clamp(_RingStart, 0.05, 0.98);
            float texRing = saturate(_TexRingStart);
            float st = s < ringStart
                ? s * (texRing / ringStart)
                : texRing + (s - ringStart) * ((1.0 - texRing) / (1.0 - ringStart));
            float2 dir = p / max(s, 1e-4);
            float2 uv = 0.5 + 0.5 * dir * st * _TexRadius;

            // Ghép viền trong -> viền ngoài -> phần sáng trắng; lớp không thuộc pass này có trọng số 0.
            half wInner = lerp(1.0 - _InnerOverFish, _InnerOverFish, overlay);
            half wOuter = lerp(1.0 - _OuterOverFish, _OuterOverFish, overlay);
            half wHighlight = lerp(1.0 - _HighlightOverFish, _HighlightOverFish, overlay);
            c = 0;
            a = 0;

            half4 inner = SAMPLE_TEXTURE2D(_InnerTex, sampler_InnerTex, uv);
            Over(c, a, inner.rgb * _InnerTint.rgb, saturate(inner.a * _InnerTint.a * _InnerOpacity) * wInner);

            half4 outer = SAMPLE_TEXTURE2D(_OuterTex, sampler_OuterTex, uv);
            Over(c, a, outer.rgb * _OuterTint.rgb, saturate(outer.a * _OuterTint.a * _OuterOpacity) * wOuter);

            half4 highlight = SAMPLE_TEXTURE2D(_HighlightTex, sampler_HighlightTex, uv);
            Over(c, a, highlight.rgb * _HighlightTint.rgb, saturate(highlight.a * _HighlightTint.a * _HighlightOpacity) * wHighlight);

            c *= _Tint.rgb;
        }

        half4 Finish(half3 c, half a, float s, float dissolveEdge)
        {
            // Viền dissolve cộng thêm.
            c += _DissolveEdgeColor.rgb * dissolveEdge;

            // Làm mềm mép ngoài: mờ dần trong khoảng _EdgeFade cuối cùng của bán kính (0 = mép sắc như mesh).
            half edge = _Alpha * _Tint.a * (1.0 - smoothstep(1.0 - max(_EdgeFade, 1e-4), 1.0, s) * step(1e-4, _EdgeFade));
            c *= edge;
            a *= edge;
            return half4(c, a);
        }
        ENDHLSL

        Pass
        {
            // Các lớp Over Fish = 0, vẽ trên mặt sau: cá (opaque, ghi depth) ở trước mặt sau nên che được chúng.
            Name "SoftBubbleMatcapBody"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragBody

            half4 fragBody(Varyings input) : SV_Target
            {
                half3 c;
                half a;
                float s;
                float dissolveEdge;
                SampleLayers(input, 0.0, c, a, s, dissolveEdge);
                return Finish(c, a, s, dissolveEdge);
            }
            ENDHLSL
        }

        Pass
        {
            // Các lớp Over Fish = 1, vẽ trên mặt trước, đè lên cá.
            Name "SoftBubbleMatcapOverlay"
            Tags { "LightMode" = "Universal2D" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragOverlay

            half4 fragOverlay(Varyings input) : SV_Target
            {
                half3 c;
                half a;
                float s;
                float dissolveEdge;
                SampleLayers(input, 1.0, c, a, s, dissolveEdge);
                return Finish(c, a, s, dissolveEdge);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
