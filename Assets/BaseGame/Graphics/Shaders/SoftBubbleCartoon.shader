// Shader bong bóng kiểu hoạt hình cho SoftBubble (URP): màu pastel phẳng, trong hơn ở giữa và đặc dần ra rìa,
// dải viền sáng dày, nét viền mảnh đậm hơn ở mép, vệt sáng trắng hình bầu dục hoặc lưỡi liềm ở góc trên trái,
// chấm sáng nhỏ ở góc dưới phải. Mọi thứ tính theo pháp tuyến trong không gian camera nên không phụ thuộc đèn,
// không cần Opaque Texture hay Depth Texture, chạy được trên cả 2D Renderer lẫn Universal Renderer.
// s = |pháp tuyến view-space .xy| = 0 ở tâm, 1 ở mép: mọi bán kính trong property đều tính theo s (tỉ lệ bán kính bóng).
Shader "FishSort/Soft Bubble Cartoon"
{
    Properties
    {
        [Header(Mau nen)]
        _CenterColor ("Center Color (tam, chuyen dan sang Fill Color ra mep)", Color) = (1, 1, 1, 1)
        _FillColor ("Fill Color (sang, goc tren trai)", Color) = (0.58, 0.88, 1.0, 1.0)
        _FillColorDark ("Fill Color Dark (goc duoi phai)", Color) = (0.30, 0.66, 0.98, 1.0)
        _GradientDir ("Gradient Direction (x, y)", Vector) = (-0.6, 0.8, 0, 0)
        _GradientSoftness ("Gradient Softness", Range(0.05, 2)) = 1.0
        _FillOpacityCenter ("Fill Opacity Center", Range(0, 1)) = 0.35
        _FillOpacityEdge ("Fill Opacity Edge", Range(0, 1)) = 0.78
        _FillOpacityPower ("Fill Opacity Curve (cang lon dai xanh cang dồn ra mep)", Range(0.5, 8)) = 2.0
        _EdgeFade ("Edge Fade (lam mem mep ngoai)", Range(0, 0.3)) = 0.0
        _Alpha ("Alpha", Range(0, 1)) = 1.0

        [Header(Dai vien sang)]
        _RimColor ("Rim Color", Color) = (0.84, 0.97, 1.0, 1.0)
        _RimStart ("Rim Start", Range(0, 1)) = 0.78
        _RimSoftness ("Rim Softness", Range(0.005, 0.5)) = 0.08
        _RimOpacity ("Rim Opacity", Range(0, 1)) = 0.9
        _RimShade ("Rim Shade (0 = deu; cang lon vanh cang mo dan ve phia nguoc Gradient Direction)", Range(0, 1)) = 0

        [Header(Net vien mep)]
        _OutlineColor ("Outline Color", Color) = (0.32, 0.68, 1.0, 1.0)
        _OutlineStart ("Outline Start", Range(0, 1)) = 0.94
        _OutlineSoftness ("Outline Softness", Range(0.005, 0.3)) = 0.03
        _OutlineOpacity ("Outline Opacity", Range(0, 1)) = 1.0

        [Header(Vet sang lon)]
        _HighlightColor ("Highlight Color", Color) = (1, 1, 1, 1)
        _HighlightDir ("Highlight Direction (x, y)", Vector) = (-0.62, 0.78, 0, 0)
        _HighlightRadius ("Highlight Radius (vi tri tren ban kinh)", Range(0, 1)) = 0.62
        _HighlightLength ("Highlight Length (doc theo rim)", Range(0.01, 1)) = 0.36
        _HighlightWidth ("Highlight Width", Range(0.01, 1)) = 0.16
        _HighlightSoftness ("Highlight Softness", Range(0.01, 1)) = 0.15
        _HighlightCrescent ("Highlight Crescent (0 = bau duc)", Range(0, 0.5)) = 0.1
        _HighlightOpacity ("Highlight Opacity", Range(0, 1)) = 0.95
        _HighlightArc ("Highlight Arc (1 = dai cong dong tam voi bong thay vi bau duc thang)", Range(0, 1)) = 0

        [Header(Cham sang nho)]
        _SpotDir ("Spot Direction (x, y)", Vector) = (0.6, -0.62, 0, 0)
        _SpotRadius ("Spot Radius (vi tri tren ban kinh)", Range(0, 1)) = 0.7
        _SpotSize ("Spot Size", Range(0.005, 0.5)) = 0.09
        _SpotSoftness ("Spot Softness", Range(0.005, 0.5)) = 0.05
        _SpotOpacity ("Spot Opacity", Range(0, 1)) = 0.85
        _SpotColor ("Spot Color", Color) = (1, 1, 1, 1)

        [Header(Vien cau vong)]
        _RimIridescent ("Rim Iridescent (0 = tat; 1 = vien va net mep doi mau cau vong theo goc quanh bong, kieu bong xa phong)", Range(0, 1)) = 0
        _RimHueCycles ("Rim Hue Cycles (so vong mau quanh bong, lam tron thanh so nguyen de mau lien vong)", Float) = 2
        _RimHueOffset ("Rim Hue Offset", Range(0, 1)) = 0
        _RimHueSpeed ("Rim Hue Speed (vong / giay, mau troi cham quanh bong)", Float) = 0
        _RimIridescentSaturation ("Rim Iridescent Saturation (0 = trang, 1 = mau dam)", Range(0, 1)) = 0.55

        [Header(Khoi mo)]
        _Matte ("Matte (0 = tat; khoi cau mo: toi dan theo Lambert ve phia xa den)", Range(0, 1)) = 0
        _MatteLightDir ("Matte Light Direction (view-space, +z ve phia camera)", Vector) = (-0.5, 0.6, 0.65, 0)
        _MatteShadowColor ("Matte Shadow Color (mau nen nhan o vung toi)", Color) = (0.55, 0.12, 0.42, 1)
        _MatteWrap ("Matte Wrap (0 = Lambert chuan, cang lon vung sang cang lan sang phia toi)", Range(0, 1)) = 0.3

        [Header(Suong gia)]
        [Toggle(_FROST_ON)] _Frost ("Frost (lop suong gia / tinh the bang quanh ria, dung cho vo bang BubbleHp)", Float) = 0
        _FrostColor ("Frost Color", Color) = (1, 1, 1, 1)
        _FrostAmount ("Frost Amount", Range(0, 1)) = 0.8
        _FrostStart ("Frost Start (ban kinh bat dau co suong, day dan ra mep)", Range(0, 1)) = 0.5
        _FrostScale ("Frost Noise Scale (cang lon tinh the cang nho)", Float) = 8.0

        [Header(Bong tuyet)]
        [Toggle(_SNOWFLAKE_ON)] _Snowflake ("Snowflake (bong tuyet 6 canh ve thu tuc tren mat bong, moi o luoi mot bong; dung cho vo bang BubbleHp)", Float) = 0
        _SnowflakeColor ("Snowflake Color", Color) = (1, 1, 1, 1)
        _SnowflakeOpacity ("Snowflake Opacity", Range(0, 1)) = 0.9
        _SnowflakeCells ("Snowflake Cells (so o luoi tren duong kinh bong; cang lon bong cang nho va nhieu)", Float) = 3.0
        _SnowflakeSize ("Snowflake Size (ban kinh bong so voi o luoi)", Range(0.1, 0.6)) = 0.34
        _SnowflakeWidth ("Snowflake Arm Width (so voi ban kinh bong)", Range(0.02, 0.3)) = 0.09
        _SnowflakeSoftness ("Snowflake Edge Softness (so voi ban kinh bong)", Range(0.005, 0.2)) = 0.04
        _SnowflakeSpin ("Snowflake Spin (vong / giay, bong xoay cham, chieu ngau nhien)", Float) = 0.03
        _SnowflakeDensity ("Snowflake Density (ti le o luoi co bong; 1 = o nao cung co)", Range(0, 1)) = 0.6
        _SnowflakeStart ("Snowflake Start (ban kinh tu do moi co bong: bong chi bam quanh vien; 0 = ca mat bong)", Range(0, 1)) = 0.55
        _SnowflakeOpacityMin ("Snowflake Opacity Min (moi bong dam nhat ngau nhien tu Min toi 1, nhan Opacity)", Range(0, 1)) = 0.35

        [Header(Khoi)]
        [Toggle(_SMOKE_ON)] _Smoke ("Smoke (khoi dac lap day bong, mat khoi nghieng gon song, ranh gioi mo; dung cho bong khoi isIce)", Float) = 0
        _SmokeColor ("Smoke Color (than khoi phia truoc / sang)", Color) = (0.55, 0.78, 1.0, 1)
        _SmokeShadow ("Smoke Shadow (than khoi phia sau / sau / mep)", Color) = (0.36, 0.5, 0.95, 1)
        _SmokeHighlight ("Smoke Highlight (mu sang tren mat khoi)", Color) = (0.9, 0.97, 1.0, 1)
        _SmokeLevel ("Smoke Level (do cao mat khoi luc day: -1 = day bong, 1 = dinh bong)", Range(-1, 1)) = 0.55
        _SmokeTilt ("Smoke Tilt (nhin xuong mat khoi bao nhieu: 0 = ngang, lon = thay mat khoi rong hon)", Range(0, 0.6)) = 0.25
        _SmokeSoft ("Smoke Soft (do mo cua ranh gioi mat khoi)", Range(0.01, 0.4)) = 0.12
        _SmokeDensity ("Smoke Density (do dac: cang lon khoi cang mau che kin)", Range(1, 20)) = 7
        _SmokeWaveAmp ("Wave Amp (bien do song mat khoi, don vi ban kinh bong)", Range(0, 0.3)) = 0.06
        _SmokeWaveLen ("Wave Length (so song ngang qua mat bong)", Range(0.5, 6)) = 2.0
        _SmokeSpeed ("Smoke Speed (toc do song chay, rad / giay)", Float) = 1.2
        _SmokeGapAlpha ("Gap Alpha (do duc cua phan ho phia tren mat khoi)", Range(0, 1)) = 0.35
        _SmokeSurfaceGlow ("Surface Glow (do sang cua mu ngay tren mat khoi)", Range(0, 1)) = 0.6
        _SmokeDissolveScale ("Dissolve Scale (co noise cua lo thung khi tan, khong gian object; cang lon lo cang nho)", Float) = 1.3
        _SmokeFluff ("Dissolve Fluff (khi tan, mep bong thung som hon bao nhieu)", Range(0, 0.6)) = 0.35
        _SmokeHoleLow ("Hole Noise Low (noise duoi muc nay coi nhu 0; ha xuong = lo dau tien xuat hien muon hon)", Range(0, 0.6)) = 0.22
        _SmokeHoleHigh ("Hole Noise High (noise tren muc nay coi nhu 1; nang len = chom khoi cuoi tan muon hon)", Range(0.4, 1)) = 0.82
        _SmokeThin ("Smoke Thin (0 = day, 1 = tan het; BubbleView dat theo thoi gian bong cham dinh)", Range(0, 1)) = 0

        [Header(Dissolve)]
        _Seed ("Seed (per bubble)", Float) = 0
        _Dissolve ("Dissolve", Range(0, 1)) = 0
        _DissolveNoiseScale ("Dissolve Noise Scale", Float) = 3.0
        _DissolveEdgeWidth ("Dissolve Edge Width", Range(0.01, 0.5)) = 0.08
        [HDR] _DissolveEdgeColor ("Dissolve Edge Color", Color) = (1.5, 2.5, 4.0, 1.0)

        [Header(Bung ra)]
        _Inflate ("Inflate (no mesh theo phap tuyen, don vi object; BubbleView dat luc lop bang bung ra va tan)", Float) = 0
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
            Name "SoftBubbleCartoon"
            Blend One OneMinusSrcAlpha   // premultiplied, các lớp được composite theo phép "over" trong shader
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local_fragment _FROST_ON
            #pragma shader_feature_local_fragment _SNOWFLAKE_ON
            #pragma shader_feature_local_fragment _SMOKE_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CenterColor;
                half4 _FillColor;
                half4 _FillColorDark;
                float4 _GradientDir;
                half _GradientSoftness;
                half _FillOpacityCenter;
                half _FillOpacityEdge;
                half _FillOpacityPower;
                half _EdgeFade;
                half _Alpha;
                half4 _RimColor;
                half _RimStart;
                half _RimSoftness;
                half _RimOpacity;
                half _RimShade;
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
                half _HighlightArc;
                float4 _SpotDir;
                half _SpotRadius;
                half _SpotSize;
                half _SpotSoftness;
                half _SpotOpacity;
                half4 _SpotColor;
                half _RimIridescent;
                float _RimHueCycles;
                float _RimHueOffset;
                float _RimHueSpeed;
                half _RimIridescentSaturation;
                half _Matte;
                float4 _MatteLightDir;
                half4 _MatteShadowColor;
                half _MatteWrap;
                half4 _FrostColor;
                half _FrostAmount;
                half _FrostStart;
                float _FrostScale;
                half4 _SnowflakeColor;
                half _SnowflakeOpacity;
                float _SnowflakeCells;
                float _SnowflakeSize;
                float _SnowflakeWidth;
                float _SnowflakeSoftness;
                float _SnowflakeSpin;
                half _SnowflakeDensity;
                half _SnowflakeStart;
                half _SnowflakeOpacityMin;
                half4 _SmokeColor;
                half4 _SmokeShadow;
                half4 _SmokeHighlight;
                half _SmokeLevel;
                half _SmokeTilt;
                half _SmokeSoft;
                half _SmokeDensity;
                half _SmokeWaveAmp;
                float _SmokeWaveLen;
                float _SmokeSpeed;
                half _SmokeGapAlpha;
                half _SmokeSurfaceGlow;
                float _SmokeDissolveScale;
                half _SmokeFluff;
                half _SmokeHoleLow;
                half _SmokeHoleHigh;
                half _SmokeThin;
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
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
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

            float SegDist(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a;
                float2 ba = b - a;
                float h = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-6));
                return length(pa - ba * h);
            }

            // Bông tuyết 6 cánh: gập góc về một nửa cánh [0, 30°] rồi đo khoảng cách tới cánh chính (đoạn 0..R trên trục x), hai nhánh
            // phụ xiên 60° và chấm tâm; phủ mềm quanh bề dày w. q tính từ tâm bông, R = bán kính bông (cùng đơn vị).
            float SnowflakeMask(float2 q, float R, float w, float soft)
            {
                float r = length(q);
                if (r > R * 1.1) return 0.0;
                float sector = 6.2831853 / 6.0;
                float ang = abs(fmod(abs(atan2(q.y, q.x)) + sector * 0.5, sector) - sector * 0.5);
                float2 f = r * float2(cos(ang), sin(ang));
                float2 branch = float2(0.5, 0.8660254);
                float d = SegDist(f, float2(0.0, 0.0), float2(R, 0.0));
                d = min(d, SegDist(f, float2(0.42 * R, 0.0), float2(0.42 * R, 0.0) + branch * 0.32 * R));
                d = min(d, SegDist(f, float2(0.7 * R, 0.0), float2(0.7 * R, 0.0) + branch * 0.2 * R));
                d = min(d, r - 0.14 * R);
                return 1.0 - smoothstep(w - soft, w + soft, d);
            }

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
                // Bung ra: đẩy đỉnh theo pháp tuyến nên lớp phủ to hơn bóng nền mà vẫn lõm theo bóng mềm; noise dissolve vẫn lấy vị trí gốc.
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz + normalize(input.normalOS) * _Inflate);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);

                // Dissolve theo noise 3D trong không gian bóng.
                float dissolveNoise = ValueNoise3(input.positionOS * _DissolveNoiseScale + _Seed * 7.0);
                float threshold = _Dissolve * (1.0 + _DissolveEdgeWidth) - _DissolveEdgeWidth;
                float dissolveDelta = dissolveNoise - threshold;
                clip(dissolveDelta);
                float dissolveEdge = 1.0 - saturate(dissolveDelta / _DissolveEdgeWidth);

                // Tọa độ "trên mặt bóng nhìn từ camera": p = pháp tuyến view-space .xy, s = |p| (0 tâm, 1 mép).
                float3 normalVS = normalize(TransformWorldToViewDir(normalWS));
                float2 p = normalVS.xy;
                float s = saturate(length(p));

                // 1. Nền pastel: sáng ở hướng GradientDir, tối dần về hướng ngược lại; trong ở giữa, đặc dần ra mép.
                float2 gradDir = normalize(_GradientDir.xy + float2(1e-5, 0));
                float g = saturate(0.5 + 0.5 * dot(p, gradDir) / max(_GradientSoftness, 1e-3));
                half radial = pow(s, _FillOpacityPower);
                half3 fill = lerp(_CenterColor.rgb, lerp(_FillColorDark.rgb, _FillColor.rgb, g), radial);
                half smokeAlpha = 1.0;

                // 1a. Khói đặc (bóng khói isIce), tính theo thể tích: mỗi điểm ảnh lấy 8 mẫu dọc tia nhìn xuyên qua quả cầu (z từ mặt
                // trước tới mặt sau, chiều dài dây cung 2*sqrt(1 - s^2)). Khói lấp phần dưới một mặt phẳng hơi nghiêng (Tilt: phía sau
                // cao hơn phía trước, như nhìn xuống mặt khói) có sóng chạy theo cả ngang (x) lẫn chiều sâu (z), mật độ chuyển mềm
                // quanh mặt (Soft) nên ranh giới là sương chứ không phải nét; ghép trước-ra-sau với độ tắt Density. Màu: mẫu càng sâu
                // vào trong / càng ra sau càng tối (Shadow), mẫu sát mặt khói phủ mù sáng (Highlight x SurfaceGlow) nên thấy được mặt
                // khói lùi vào trong bóng = cảm giác 3D. Phần hở phía trên vẽ nền pastel của bóng (mục 1) với alpha GapAlpha. Tan
                // (_SmokeThin, theo thời gian bóng chạm đỉnh rồi BreakIce kéo tới 1) giữ kiểu bản đầu: khói thủng theo noise 3D
                // (không gian object, xoay chậm) từ mép bóng vào thành vệt rồi mờ hẳn, mặt khói không hạ, không bung. Sau keyword
                // nên material không bật thì không tốn gì.
                #if defined(_SMOKE_ON)
                {
                    float thin = saturate(_SmokeThin);
                    float t = _Time.y * _SmokeSpeed + _Seed * 2.0;
                    float level = _SmokeLevel;
                    float halfChord = sqrt(saturate(1.0 - s * s));
                    float wx = p.x * _SmokeWaveLen * 3.1416;
                    float waveX = sin(wx + t) * 0.6 + sin(wx * 1.7 - t * 1.4 + _Seed) * 0.4;
                    float stepLen = 2.0 * halfChord / 8.0;
                    float trans = 1.0;
                    half3 acc = half3(0.0, 0.0, 0.0);
                    [unroll]
                    for (int i = 0; i < 8; i++)
                    {
                        float z = lerp(-halfChord, halfChord, ((float)i + 0.5) / 8.0);
                        float wz = z * _SmokeWaveLen * 2.2;
                        float wave = waveX + sin(wz + t * 0.8 + _Seed * 0.5) * 0.5;
                        float ySurf = level + _SmokeTilt * z + wave * _SmokeWaveAmp;
                        float dRel = p.y - ySurf;
                        float dens = 1.0 - smoothstep(-_SmokeSoft, _SmokeSoft, dRel);
                        float a = 1.0 - exp(-dens * stepLen * _SmokeDensity);
                        float depthMix = saturate(0.5 + 0.5 * z / max(halfChord, 1e-3)) * 0.6 + saturate(-dRel * 1.5) * 0.4;
                        half3 c = lerp(_SmokeColor.rgb, _SmokeShadow.rgb, depthMix);
                        float mist = exp(-abs(dRel) * 5.0) * _SmokeSurfaceGlow;
                        c = lerp(c, _SmokeHighlight.rgb, mist);
                        acc += trans * a * c;
                        trans *= 1.0 - a;
                    }
                    float smokeCover = 1.0 - trans;
                    half3 smokeCol = acc / max(smokeCover, 1e-4);
                    smokeCol = lerp(smokeCol, _SmokeShadow.rgb * 0.85, smoothstep(0.5, 1.0, s) * 0.5);
                    // Tan kiểu bản đầu: hai lớp noise 3D trong không gian object xoay chậm (lớp sau bị lớp trước kéo), chỗ noise thấp
                    // thủng trước, mép bóng thủng sớm hơn (Fluff) nên khói rã từ ngoài vào thành vệt; mép lỗ tô tối một nét mảnh; lúc
                    // đầy (thin = 0) không thủng. Lỗ áp lên cả thân khói lẫn phần hở nên cá lộ qua lỗ trước khi tất cả mờ hẳn.
                    float tn = _Time.y * 0.12 + _Seed;
                    float3 q = input.positionOS * _SmokeDissolveScale;
                    float cn = cos(tn);
                    float sn2 = sin(tn);
                    float3 q1 = float3(cn * q.x - sn2 * q.y, sn2 * q.x + cn * q.y, q.z + tn * 0.3);
                    float n1 = ValueNoise3(q1 + _Seed * 5.7);
                    float3 q2 = float3(cn * q.x + sn2 * q.y, -sn2 * q.x + cn * q.y, q.z - tn * 0.2) * 2.3 + (n1 - 0.5) * 1.2;
                    float n2 = ValueNoise3(q2 + _Seed * 2.3);
                    float f = n1 * 0.7 + n2 * 0.3;
                    // Value noise dồn quanh 0.55 (p10 ≈ 0.37, p90 ≈ 0.72): so thẳng f với thin thì gần nửa đầu không thủng gì rồi
                    // thủng ào trong một quãng ngắn quanh trung vị. Ép về phân bố đều: u = smoothstep(Low, High, f) ≈ đều trên [0, 1],
                    // thủng chỗ u dưới ngưỡng chạy tuyến tính theo thin → diện tích khói giảm gần đều từ lúc bắt đầu chạm đỉnh tới lúc
                    // tan hết. Ngưỡng xuất phát −0.06 (dưới mép mềm 0.05) để thin = 0 kín hoàn toàn; mép bóng chạy trước (Fluff).
                    float rimNeed = smoothstep(0.5, 1.0, s) * _SmokeFluff;
                    float u = smoothstep(_SmokeHoleLow, max(_SmokeHoleHigh, _SmokeHoleLow + 0.05), f);
                    float dens = u - lerp(-0.06, 1.0, saturate(thin * (1.0 + rimNeed * 1.25)));
                    float holeCover = smoothstep(0.0, 0.05, dens);
                    float edgeLine = smoothstep(0.0, 0.05, dens) - smoothstep(0.05, 0.14, dens);
                    smokeCol = lerp(smokeCol, _SmokeShadow.rgb, edgeLine * 0.7);
                    fill = lerp(fill, smokeCol, smokeCover);
                    // Độ đục tổng chỉ mờ thêm ở đoạn cuối (thin 0.7 → 1) cho mấy chỏm khói cuối không cắt cụt; phần giảm đều do lỗ lo.
                    smokeAlpha = lerp(_SmokeGapAlpha, 1.0, smokeCover) * holeCover * (1.0 - smoothstep(0.7, 1.0, thin));
                }
                #endif

                // 1b. Khối mờ (gum): tối dần theo Lambert với đèn cố định trong view-space, wrap để vùng sáng lan mềm; _Matte = 0 thì bỏ qua.
                if (_Matte > 0.0)
                {
                    float3 lightDir = normalize(_MatteLightDir.xyz + float3(0, 0, 1e-5));
                    half lambert = saturate((dot(normalVS, lightDir) + _MatteWrap) / (1.0 + _MatteWrap));
                    lambert = lambert * lambert * (3.0 - 2.0 * lambert);
                    fill = lerp(fill, fill * _MatteShadowColor.rgb, (1.0 - lambert) * _Matte);
                }

                half fillA = lerp(_FillOpacityCenter, _FillOpacityEdge, radial) * smokeAlpha;
                half3 c = fill * fillA;
                half a = fillA;

                // 2. Dải viền sáng dày. Viền cầu vồng: hue chạy theo góc quanh bóng (thêm chút theo bán kính và seed để mỗi bóng lệch
                // nhau), trôi chậm theo thời gian; pha với trắng theo Saturation cho ra tông pastel của bong bóng xà phòng.
                half3 rimColor = _RimColor.rgb;
                half3 outlineColor = _OutlineColor.rgb;
                if (_RimIridescent > 0.0)
                {
                    float angle = atan2(p.y, p.x) / 6.2831853;
                    // Số vòng màu làm tròn thành số nguyên: atan2 nhảy từ 0.5 về -0.5 ở góc 180°, số lẻ sẽ để lộ một vạch nối màu ở mép trái.
                    float cycles = max(1.0, round(_RimHueCycles));
                    float hue = frac(angle * cycles + _RimHueOffset + _Time.y * _RimHueSpeed + _Seed * 0.37 + s * 0.25);
                    half3 rainbow = saturate(abs(frac(hue + float3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0);
                    rimColor = lerp(rimColor, lerp(half3(1, 1, 1), rainbow, _RimIridescentSaturation), _RimIridescent);
                    outlineColor = lerp(outlineColor, lerp(half3(1, 1, 1), rainbow, saturate(_RimIridescentSaturation * 1.4)), _RimIridescent);
                }

                half rimMask = smoothstep(_RimStart - _RimSoftness, _RimStart + _RimSoftness, s);
                // Góc sáng / góc tối: vành đủ đậm ở phía Gradient Direction, mờ dần về phía đối diện để lộ màu fill tối hơn (_RimShade = 0 thì đều).
                half rimShade = lerp(1.0 - _RimShade, 1.0, g);
                Over(c, a, rimColor, rimMask * _RimOpacity * rimShade);

                // 3. Nét viền mảnh đậm ở mép.
                half outlineMask = smoothstep(_OutlineStart - _OutlineSoftness, _OutlineStart + _OutlineSoftness, s);
                Over(c, a, outlineColor, outlineMask * _OutlineOpacity);

                // 3b. Sương giá (vỏ băng): gân tinh thể + đốm trắng theo noise 3D bám trên mặt bóng (không gian object, lõm và xoay theo
                // bóng), dày dần từ FrostStart ra mép. Sau keyword nên material không bật thì không tốn thêm noise.
                #if defined(_FROST_ON)
                {
                    float3 fp = input.positionOS * _FrostScale + _Seed * 3.1;
                    float n1 = ValueNoise3(fp);
                    float n2 = ValueNoise3(fp * 2.7 + 11.3);
                    float veins = smoothstep(0.84, 0.99, 1.0 - abs(2.0 * n1 - 1.0));   // gân mảnh dọc theo đường n1 = 0.5
                    float specks = smoothstep(0.62, 0.9, n2);                          // đốm tinh thể
                    half band = smoothstep(_FrostStart, 1.0, s);
                    band *= sqrt(band);                                                // dồn sương ra sát rìa, giữa bóng để trong
                    half frost = saturate((veins * 0.9 + specks * 0.8) * band + band * band * 0.4) * _FrostAmount;
                    Over(c, a, _FrostColor.rgb, frost);
                }
                #endif

                // 3c. Bông tuyết (vỏ băng kiểu bóng tuyết): lưới ô trong toạ độ đĩa p, mỗi ô một bông lệch tâm / xoay / cỡ / đậm nhạt ngẫu
                // nhiên theo ô và seed, chỉ một phần ô có bông (Density) và chỉ bông có tâm ngoài bán kính Start mới hiện (bám quanh viền);
                // xoay chậm theo thời gian; duyệt 3×3 ô lân cận để bông lệch sang ô bên không bị cắt. Sau keyword nên material không bật thì
                // không tốn gì.
                #if defined(_SNOWFLAKE_ON)
                {
                    float2 gp = p * (_SnowflakeCells * 0.5) + 100.0;
                    float2 cell = floor(gp);
                    float flake = 0.0;
                    for (int oy = -1; oy <= 1; oy++)
                    {
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            float2 cid = cell + float2(ox, oy);
                            float h4 = Hash31(float3(cid, _Seed * 0.13 + 13.7));
                            if (h4 > _SnowflakeDensity) continue;
                            float h1 = Hash31(float3(cid, _Seed * 0.13 + 1.7));
                            float h2 = Hash31(float3(cid, _Seed * 0.13 + 5.3));
                            float h3 = Hash31(float3(cid, _Seed * 0.13 + 9.1));
                            float h5 = Hash31(float3(cid, _Seed * 0.13 + 17.9));
                            float2 center = cid + 0.5 + (float2(h1, h2) - 0.5) * 0.5;
                            float ring = smoothstep(_SnowflakeStart - 0.12, _SnowflakeStart + 0.08, length((center - 100.0) / (_SnowflakeCells * 0.5)));
                            if (ring <= 0.0) continue;
                            float2 q = gp - center;
                            float size = _SnowflakeSize * (0.7 + 0.6 * h3);
                            float rot = h1 * 6.2831853 + _Time.y * _SnowflakeSpin * 6.2831853 * (h2 > 0.5 ? 1.0 : -1.0);
                            float cs = cos(rot);
                            float sn = sin(rot);
                            q = float2(cs * q.x - sn * q.y, sn * q.x + cs * q.y);
                            float alpha = lerp(_SnowflakeOpacityMin, 1.0, h5) * ring;
                            flake = max(flake, SnowflakeMask(q, size, _SnowflakeWidth * size, _SnowflakeSoftness * size) * alpha);
                        }
                    }

                    Over(c, a, _SnowflakeColor.rgb, flake * _SnowflakeOpacity);
                }
                #endif

                // 4. Vệt sáng lớn: bầu dục ôm theo rìa ở góc HighlightDir, trừ bớt một bầu dục dịch vào trong để thành lưỡi liềm.
                //    _HighlightArc > 0: thay bằng dải cong đồng tâm với bóng (quả cầu bóng): tâm dải ở bán kính HighlightRadius, nửa bề dày
                //    HighlightWidth theo bán kính, nửa chiều dài HighlightLength đo dọc theo cung (cùng đơn vị bán kính), mềm theo HighlightSoftness.
                float2 hdir = normalize(_HighlightDir.xy + float2(1e-5, 0));
                float2 htan = float2(-hdir.y, hdir.x);
                float2 hcenter = hdir * _HighlightRadius;
                half highlight;
                if (_HighlightArc > 0.0)
                {
                    float2 pn = p / max(s, 1e-4);
                    float arcAng = acos(clamp(dot(pn, hdir), -1.0, 1.0));
                    float arcU = arcAng * _HighlightRadius / max(_HighlightLength, 1e-4);
                    float arcV = (s - _HighlightRadius) / max(_HighlightWidth, 1e-4);
                    float arcR = sqrt(arcU * arcU + arcV * arcV);
                    highlight = 1.0 - smoothstep(1.0 - _HighlightSoftness, 1.0 + _HighlightSoftness, arcR);
                }
                else
                {
                    highlight = EllipseMask(p, hcenter, hdir, htan, _HighlightLength, _HighlightWidth, _HighlightSoftness);
                    half cutout = EllipseMask(p, hcenter - hdir * _HighlightCrescent, hdir, htan, _HighlightLength * 0.9, _HighlightWidth, _HighlightSoftness);
                    highlight = saturate(highlight - cutout * step(0.001, _HighlightCrescent));
                }
                Over(c, a, _HighlightColor.rgb, highlight * _HighlightOpacity);

                // 5. Chấm sáng nhỏ.
                float2 sdir = normalize(_SpotDir.xy + float2(1e-5, 0));
                float spotDist = distance(p, sdir * _SpotRadius);
                half spot = 1.0 - smoothstep(_SpotSize - _SpotSoftness, _SpotSize + _SpotSoftness, spotDist);
                Over(c, a, _SpotColor.rgb, spot * _SpotOpacity);

                // 6. Viền dissolve cộng thêm.
                c += _DissolveEdgeColor.rgb * dissolveEdge;

                // 7. Làm mềm mép ngoài: mờ dần trong khoảng _EdgeFade cuối cùng của bán kính (0 = mép sắc như mesh).
                half edge = _Alpha * (1.0 - smoothstep(1.0 - max(_EdgeFade, 1e-4), 1.0, s) * step(1e-4, _EdgeFade));
                c *= edge;
                a *= edge;
                return half4(c, a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
