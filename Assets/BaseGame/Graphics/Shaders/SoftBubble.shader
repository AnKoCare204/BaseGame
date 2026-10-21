// Shader bong bóng cho SoftBubble (URP, viết tay theo hướng video "Bubble Shield" của A Bit Of Game Dev):
// khúc xạ nền có lắc theo noise (twirl), viền fresnel, ánh cầu vồng mỏng (iridescence), điểm sáng specular giả,
// dissolve theo noise 3D, và tuỳ chọn sáng chỗ giao cắt theo depth (chỉ khi dùng Universal Renderer 3D).
//
// Project đang dùng 2D Renderer nên nền được lấy từ _CameraSortingLayerTexture: bật "Camera Sorting Layer Texture"
// trong Renderer2D, Foremost Sorting Layer = Default, và đặt MeshRenderer của bóng ở sorting layer "Bubble".
// Nếu chuyển sang Universal Renderer 3D thì bật toggle "Use Opaque Texture" và "Depth Intersection" trên material.
Shader "FishSort/Soft Bubble"
{
    Properties
    {
        [Header(Mau)]
        _Tint ("Tint", Color) = (0.62, 0.86, 1.0, 1.0)
        _TintStrength ("Tint Strength", Range(0, 1)) = 0.12
        _Alpha ("Alpha", Range(0, 1)) = 0.9
        _Seed ("Seed (per bubble)", Float) = 0

        [Header(Khuc xa nen)]
        _RefractionStrength ("Refraction Strength", Range(0, 0.3)) = 0.06
        _WobbleStrength ("Wobble Strength", Range(0, 0.1)) = 0.012
        _WobbleScale ("Wobble Scale", Float) = 2.5
        _WobbleSpeed ("Wobble Speed", Float) = 1.2

        [Header(Vien fresnel)]
        [HDR] _RimColor ("Rim Color", Color) = (0.75, 0.95, 1.0, 1.0)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3.0
        _RimIntensity ("Rim Intensity", Range(0, 5)) = 1.3

        [Header(Anh cau vong)]
        _IridescenceStrength ("Iridescence Strength", Range(0, 1)) = 0.35
        _IridescenceScale ("Iridescence Scale", Float) = 2.0
        _IridescenceSpeed ("Iridescence Speed", Float) = 0.15

        [Header(Diem sang)]
        _LightDir ("Light Direction", Vector) = (-0.45, 0.8, -0.4, 0)
        _SpecColor ("Specular Color", Color) = (1, 1, 1, 1)
        _SpecPower ("Specular Power", Range(4, 512)) = 90
        _SpecIntensity ("Specular Intensity", Range(0, 3)) = 0.9

        [Header(Dissolve)]
        _Dissolve ("Dissolve", Range(0, 1)) = 0
        _DissolveNoiseScale ("Dissolve Noise Scale", Float) = 3.0
        _DissolveEdgeWidth ("Dissolve Edge Width", Range(0.01, 0.5)) = 0.08
        [HDR] _DissolveEdgeColor ("Dissolve Edge Color", Color) = (1.5, 2.5, 4.0, 1.0)

        [Header(Chi cho Universal Renderer 3D)]
        [Toggle(_SCENE_COLOR_OPAQUE)] _SceneColorOpaque ("Use Opaque Texture", Float) = 0
        [Toggle(_DEPTH_INTERSECTION)] _DepthIntersectionOn ("Depth Intersection", Float) = 0
        [HDR] _IntersectionColor ("Intersection Color", Color) = (1.2, 2.0, 2.6, 1.0)
        _IntersectionDistance ("Intersection Distance", Range(0.01, 2)) = 0.25
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
            Name "SoftBubble"
            Blend One OneMinusSrcAlpha   // premultiplied: phần rim, specular, cầu vồng cộng thêm không bị alpha làm mờ
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _SCENE_COLOR_OPAQUE
            #pragma shader_feature_local _DEPTH_INTERSECTION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            #if defined(_SCENE_COLOR_OPAQUE)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #else
                TEXTURE2D_X(_CameraSortingLayerTexture);
                SAMPLER(sampler_CameraSortingLayerTexture);
            #endif

            #if defined(_DEPTH_INTERSECTION)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #endif

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _TintStrength;
                half _Alpha;
                float _Seed;
                half _RefractionStrength;
                half _WobbleStrength;
                float _WobbleScale;
                float _WobbleSpeed;
                half4 _RimColor;
                half _RimPower;
                half _RimIntensity;
                half _IridescenceStrength;
                float _IridescenceScale;
                float _IridescenceSpeed;
                float4 _LightDir;
                half4 _SpecColor;
                half _SpecPower;
                half _SpecIntensity;
                half _Dissolve;
                float _DissolveNoiseScale;
                half _DissolveEdgeWidth;
                half4 _DissolveEdgeColor;
                half4 _IntersectionColor;
                half _IntersectionDistance;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 positionNDC : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
            };

            // Value noise 3D không cần texture, kết quả 0..1.
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

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.positionNDC = vertexInput.positionNDC;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                float ndv = saturate(dot(normalWS, viewDirWS));
                float fresnel = pow(1.0 - ndv, _RimPower);
                float time = _Time.y;

                // Dissolve theo noise 3D trong không gian bóng (mesh đã là offset từ tâm nên đi theo bóng).
                float dissolveNoise = ValueNoise3(input.positionOS * _DissolveNoiseScale + _Seed * 7.0);
                float threshold = _Dissolve * (1.0 + _DissolveEdgeWidth) - _DissolveEdgeWidth;
                float dissolveDelta = dissolveNoise - threshold;
                clip(dissolveDelta);
                float dissolveEdge = 1.0 - saturate(dissolveDelta / _DissolveEdgeWidth);

                // Khúc xạ: lệch UV màn hình theo pháp tuyến view-space, cộng lắc theo noise chuyển động (thay cho twirl).
                float2 screenUV = input.positionNDC.xy / input.positionNDC.w;
                float3 normalVS = TransformWorldToViewDir(normalWS);
                float3 wobbleP = input.positionOS * _WobbleScale + float3(_Seed, time * _WobbleSpeed, -time * _WobbleSpeed * 0.7);
                float2 wobble = float2(ValueNoise3(wobbleP), ValueNoise3(wobbleP + 19.19)) - 0.5;
                float2 offset = normalVS.xy * _RefractionStrength * (0.3 + 0.7 * (1.0 - ndv)) + wobble * _WobbleStrength;
                float2 refractUV = saturate(screenUV + offset);

                #if defined(_SCENE_COLOR_OPAQUE)
                    half3 scene = SampleSceneColor(refractUV);
                #else
                    half3 scene = SAMPLE_TEXTURE2D_X(_CameraSortingLayerTexture, sampler_CameraSortingLayerTexture,
                        UnityStereoTransformScreenSpaceTex(refractUV)).rgb;
                #endif

                half3 baseColor = lerp(scene, _Tint.rgb, _TintStrength);

                // Ánh cầu vồng kiểu màng mỏng: hue chạy theo góc nhìn, rõ ở gần rìa.
                float hue = frac(ndv * _IridescenceScale + time * _IridescenceSpeed + _Seed * 0.37);
                half3 iridescence = HsvToRgb(float3(hue, 0.75, 1.0));
                half iridescenceMask = _IridescenceStrength * (0.25 + 0.75 * saturate(fresnel * 2.0));
                baseColor = lerp(baseColor, baseColor * iridescence * 1.6, iridescenceMask);

                half3 rim = _RimColor.rgb * fresnel * _RimIntensity;

                float3 lightDir = normalize(_LightDir.xyz);
                float3 halfDir = normalize(lightDir + viewDirWS);
                half spec = pow(saturate(dot(normalWS, halfDir)), _SpecPower) * _SpecIntensity;
                half3 specular = _SpecColor.rgb * spec;

                half3 additive = rim + specular + _DissolveEdgeColor.rgb * dissolveEdge;

                #if defined(_DEPTH_INTERSECTION)
                    float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                    float fragDepth = input.positionNDC.w;
                    float intersection = 1.0 - saturate((sceneDepth - fragDepth) / _IntersectionDistance);
                    additive += _IntersectionColor.rgb * intersection;
                #endif

                half alpha = saturate(_Alpha);
                half3 color = baseColor * alpha + additive;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
