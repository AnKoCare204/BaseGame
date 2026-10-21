// Shader bể cá kiểu hoạt hình (URP), cùng cách làm với "FishSort/Soft Bubble Cartoon": mọi thứ tính theo pháp tuyến
// trong không gian camera nên không phụ thuộc đèn, chạy được trên cả 2D Renderer lẫn Universal Renderer.
// Màu nền là vùng sáng (CenterColor) ở giữa/dưới chuyển dần ra màu mép (EdgeColor), thêm bóng tối bám mép theo hướng
// ShadeDir; độ đục như kính: trong ở giữa (CenterOpacity) và đặc dần ra mép (EdgeOpacity) nên nhìn thấy cá bên trong;
// vẫn có dải viền sáng, nét viền, vệt sáng và chấm sáng ghép lên trên. Đặt cả hai Opacity = 1 và ZWrite On nếu muốn vẽ đặc.
// Bể có 3 submesh (lòng trong, thân, gờ) dùng 3 material: đặt Render Queue tăng dần (3000/3001/3002) để lòng trong vẽ trước.
// p = pháp tuyến view-space .xy (0 ở tâm, |p| = 1 ở mép); mọi bán kính trong property đều tính theo p.
Shader "FishSort/Fish Bowl Cartoon"
{
    Properties
    {
        [Header(Mau nen)]
        _CenterColor ("Center Color (vung sang)", Color) = (0.95, 1.0, 1.0, 1.0)
        _EdgeColor ("Edge Color (mau ve mep)", Color) = (0.25, 0.80, 1.0, 1.0)
        _CenterOffset ("Center Offset (x, y)", Vector) = (0, -0.35, 0, 0)
        _CenterRadius ("Center Radius", Range(0.1, 2)) = 1.0
        _CenterPower ("Center Power", Range(0.3, 4)) = 1.4

        [Header(Do duc nhu kinh)]
        _CenterOpacity ("Center Opacity (do dac o giua)", Range(0, 1)) = 0.22
        _EdgeOpacity ("Edge Opacity (do dac o mep)", Range(0, 1)) = 0.85
        _OpacityPower ("Opacity Curve", Range(0.5, 4)) = 2.0

        [Header(Bong toi bam mep)]
        _ShadeColor ("Shade Color", Color) = (0.08, 0.55, 0.95, 1.0)
        _ShadeDir ("Shade Direction (x, y)", Vector) = (0.3, -0.95, 0, 0)
        _ShadeStrength ("Shade Strength", Range(0, 1)) = 0.7
        _ShadePower ("Shade Edge Power (cang lon cang bam mep)", Range(0.5, 8)) = 3.0

        [Header(Dai vien sang)]
        _RimColor ("Rim Color", Color) = (0.62, 0.95, 1.0, 1.0)
        _RimStart ("Rim Start", Range(0, 1)) = 0.80
        _RimSoftness ("Rim Softness", Range(0.005, 0.5)) = 0.08
        _RimOpacity ("Rim Opacity", Range(0, 1)) = 0.6

        [Header(Net vien mep)]
        _OutlineColor ("Outline Color", Color) = (0.05, 0.45, 0.92, 1.0)
        _OutlineStart ("Outline Start", Range(0, 1)) = 0.94
        _OutlineSoftness ("Outline Softness", Range(0.005, 0.3)) = 0.03
        _OutlineOpacity ("Outline Opacity", Range(0, 1)) = 1.0

        [Header(Vet sang lon)]
        _HighlightColor ("Highlight Color", Color) = (1, 1, 1, 1)
        _HighlightDir ("Highlight Direction (x, y)", Vector) = (0.55, 0.80, 0, 0)
        _HighlightRadius ("Highlight Radius (vi tri tren ban kinh)", Range(0, 1)) = 0.62
        _HighlightLength ("Highlight Length (doc theo rim)", Range(0.01, 1)) = 0.30
        _HighlightWidth ("Highlight Width", Range(0.01, 1)) = 0.14
        _HighlightSoftness ("Highlight Softness", Range(0.01, 1)) = 0.2
        _HighlightCrescent ("Highlight Crescent (0 = bau duc)", Range(0, 0.5)) = 0.0
        _HighlightOpacity ("Highlight Opacity", Range(0, 1)) = 0.9

        [Header(Cham sang nho)]
        _SpotDir ("Spot Direction (x, y)", Vector) = (0.7, -0.55, 0, 0)
        _SpotRadius ("Spot Radius (vi tri tren ban kinh)", Range(0, 1)) = 0.72
        _SpotSize ("Spot Size", Range(0.005, 0.5)) = 0.07
        _SpotSoftness ("Spot Softness", Range(0.005, 0.5)) = 0.05
        _SpotOpacity ("Spot Opacity", Range(0, 1)) = 0.5

        [Header(Render)]
        _Alpha ("Alpha", Range(0, 1)) = 1.0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 0
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
            // Không đặt LightMode để pass được coi là SRPDefaultUnlit: cả 2D Renderer lẫn Universal Renderer đều vẽ.
            Name "FishBowlCartoon"
            Blend One OneMinusSrcAlpha   // premultiplied, các lớp được composite theo phép "over" trong shader
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CenterColor;
                half4 _EdgeColor;
                float4 _CenterOffset;
                half _CenterRadius;
                half _CenterPower;
                half _CenterOpacity;
                half _EdgeOpacity;
                half _OpacityPower;
                half4 _ShadeColor;
                float4 _ShadeDir;
                half _ShadeStrength;
                half _ShadePower;
                half4 _RimColor;
                half _RimStart;
                half _RimSoftness;
                half _RimOpacity;
                half4 _OutlineColor;
                half _OutlineStart;
                half _OutlineSoftness;
                half _OutlineOpacity;
                half4 _HighlightColor;
                float4 _HighlightDir;
                half _HighlightRadius;
                half _HighlightLength;
                half _HighlightWidth;
                half _HighlightSoftness;
                half _HighlightCrescent;
                half _HighlightOpacity;
                float4 _SpotDir;
                half _SpotRadius;
                half _SpotSize;
                half _SpotSoftness;
                half _SpotOpacity;
                half _Alpha;
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

            // Ghép lớp (Lc, La) lên trên (C premultiplied, A).
            void Over(inout half3 c, inout half a, half3 layerColor, half layerAlpha)
            {
                c = layerColor * layerAlpha + c * (1.0 - layerAlpha);
                a = layerAlpha + a * (1.0 - layerAlpha);
            }

            // Mặt nạ bầu dục mềm quanh tâm center, trục dài dọc theo tangent, trục ngắn dọc theo dir.
            half EllipseMask(float2 p, float2 center, float2 dir, float2 tangent, float halfLength, float halfWidth, float softness)
            {
                float2 d = p - center;
                float u = dot(d, tangent) / max(halfLength, 1e-4);
                float v = dot(d, dir) / max(halfWidth, 1e-4);
                float r = sqrt(u * u + v * v);
                return 1.0 - smoothstep(1.0 - softness, 1.0 + softness, r);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 normalVS = normalize(TransformWorldToViewDir(normalWS));
                float2 p = normalVS.xy;
                float s = saturate(length(p));

                // 1. Nền: vùng sáng quanh tâm (dịch theo CenterOffset) chuyển dần ra màu mép.
                float2 q = p - _CenterOffset.xy;
                half sc = saturate(length(q) / max(_CenterRadius, 1e-3));
                half3 fill = lerp(_CenterColor.rgb, _EdgeColor.rgb, pow(sc, _CenterPower));

                // 2. Bóng tối bám mép về phía ShadeDir.
                float2 shadeDir = normalize(_ShadeDir.xy + float2(1e-5, 0));
                half shade = saturate(dot(p, shadeDir)) * pow(s, _ShadePower) * _ShadeStrength;
                fill = lerp(fill, _ShadeColor.rgb, shade);

                // Độ đục như kính: trong ở giữa, đặc dần ra mép (premultiplied).
                half fillA = lerp(_CenterOpacity, _EdgeOpacity, pow(s, _OpacityPower));
                half3 c = fill * fillA;
                half a = fillA;

                // 3. Dải viền sáng dày.
                half rimMask = smoothstep(_RimStart - _RimSoftness, _RimStart + _RimSoftness, s);
                Over(c, a, _RimColor.rgb, rimMask * _RimOpacity);

                // 4. Nét viền mảnh đậm ở mép.
                half outlineMask = smoothstep(_OutlineStart - _OutlineSoftness, _OutlineStart + _OutlineSoftness, s);
                Over(c, a, _OutlineColor.rgb, outlineMask * _OutlineOpacity);

                // 5. Vệt sáng lớn: bầu dục ôm theo rìa ở góc HighlightDir, trừ bớt một bầu dục dịch vào trong để thành lưỡi liềm.
                float2 hdir = normalize(_HighlightDir.xy + float2(1e-5, 0));
                float2 htan = float2(-hdir.y, hdir.x);
                float2 hcenter = hdir * _HighlightRadius;
                half highlight = EllipseMask(p, hcenter, hdir, htan, _HighlightLength, _HighlightWidth, _HighlightSoftness);
                half cutout = EllipseMask(p, hcenter - hdir * _HighlightCrescent, hdir, htan, _HighlightLength * 0.9, _HighlightWidth, _HighlightSoftness);
                highlight = saturate(highlight - cutout * step(0.001, _HighlightCrescent));
                Over(c, a, _HighlightColor.rgb, highlight * _HighlightOpacity);

                // 6. Chấm sáng nhỏ.
                float2 sdir = normalize(_SpotDir.xy + float2(1e-5, 0));
                float spotDist = distance(p, sdir * _SpotRadius);
                half spot = 1.0 - smoothstep(_SpotSize - _SpotSoftness, _SpotSize + _SpotSoftness, spotDist);
                Over(c, a, _HighlightColor.rgb, spot * _SpotOpacity);

                c *= _Alpha;
                a *= _Alpha;
                return half4(c, a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
