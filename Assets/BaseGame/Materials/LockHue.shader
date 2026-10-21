Shader "UI/LockHue"
{
    /****************************************************
     * PROPERTIES
     * Các biến hiển thị trong Inspector
     ****************************************************/
    Properties
    {
        // Texture chính của Sprite / UI Image
        _MainTex ("Sprite Texture", 2D) = "white" {}

        // Màu tint (giống Color trong Image)
        _Color ("Tint", Color) = (1,1,1,1)

        // Mức độ chuyển sang grayscale
        // 0 = giữ nguyên màu
        // 1 = chuyển hoàn toàn sang xám
        _GrayAmount ("Gray Amount", Range(0,1)) = 1
    }

    SubShader
    {
        /****************************************************
         * TAGS - Phục vụ render cho UI / Sprite
         ****************************************************/
        Tags
        {
            "Queue"="Transparent"             // Render sau Opaque
            "IgnoreProjector"="True"          // Không bị projector ảnh hưởng
            "RenderType"="Transparent"        // Shader trong nhóm Transparent
            "PreviewType"="Plane"             // Preview trong Inspector
            "CanUseSpriteAtlas"="True"        // Hỗ trợ Sprite Atlas
        }

        /****************************************************
         * RENDER STATE - Chuẩn UI
         ****************************************************/
        Cull Off                               // Không cull mặt sau (UI 2D)
        Lighting Off                           // Không dùng lighting
        ZWrite Off                             // Không ghi depth
        Blend SrcAlpha OneMinusSrcAlpha        // Alpha blend chuẩn UI

        Pass
        {
            CGPROGRAM

            /************************************************
             * PRAGMA
             ************************************************/
            #pragma vertex vert               // Vertex shader
            #pragma fragment frag             // Fragment shader
            #include "UnityCG.cginc"           // Thư viện Unity

            /************************************************
             * INPUT STRUCT - Dữ liệu từ Mesh/UI
             ************************************************/
            struct appdata_t
            {
                float4 vertex   : POSITION;   // Vị trí đỉnh
                float2 texcoord : TEXCOORD0;  // UV
                float4 color    : COLOR;      // Vertex color (Image color)
            };

            /************************************************
             * OUTPUT STRUCT - Vertex -> Fragment
             ************************************************/
            struct v2f
            {
                float4 vertex   : SV_POSITION; // Vị trí trên màn hình
                float2 texcoord : TEXCOORD0;   // UV
                float4 color    : COLOR;       // Màu đã nhân tint
            };

            /************************************************
             * UNIFORM VARIABLES
             ************************************************/
            sampler2D _MainTex;                // Texture chính
            float4 _MainTex_ST;                // Tiling + Offset
            float4 _Color;                     // Tint
            float  _GrayAmount;                // Độ xám

            /************************************************
             * VERTEX SHADER
             ************************************************/
            v2f vert (appdata_t IN)
            {
                v2f OUT;

                // Chuyển vertex từ Object space -> Clip space
                OUT.vertex = UnityObjectToClipPos(IN.vertex);

                // Áp dụng tiling & offset cho UV
                OUT.texcoord = TRANSFORM_TEX(IN.texcoord, _MainTex);

                // Nhân vertex color với tint color
                OUT.color = IN.color * _Color;

                return OUT;
            }

            /************************************************
             * FRAGMENT SHADER
             ************************************************/
            fixed4 frag (v2f IN) : SV_Target
            {
                // Lấy màu từ texture và nhân với color
                fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;

                // Chuyển RGB sang grayscale
                // Công thức chuẩn theo mắt người
                // R = 0.299, G = 0.587, B = 0.114
                float gray = dot(c.rgb, float3(0.299, 0.587, 0.114));

                // Nội suy giữa màu gốc và màu xám
                // _GrayAmount = 0 -> giữ màu
                // _GrayAmount = 1 -> xám hoàn toàn
                c.rgb = lerp(c.rgb, gray.xxx, _GrayAmount);

                // Trả màu cuối cùng (giữ nguyên alpha)
                return c;
            }

            ENDCG
        }
    }
}
