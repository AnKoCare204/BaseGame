using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Renderer Feature (gắn trên Renderer2D.asset) vẽ viền màn hình quanh silhouette cá đang highlight, kiểu Match Factory: bao trọn cả
/// vây / đuôi mỏng, bề dày đều theo pixel, đè lên bóng và cá phía trước. Cá được chọn bằng rendering layer RenderingLayer
/// (Fish.SetHighlighted bật bit này trên Renderer của model). Chỉ chạy khi Fish.HighlightedCount > 0 nên lúc không đè tay không tốn gì.
/// Các bước (shader FishSort/FishOutlineScreen, texture nửa độ phân giải theo ResolutionScale): mask silhouette -> khoảng cách ngang ->
/// khoảng cách Euclid + coverage vành -> blend màu lên màn hình sau transparent (trước post-processing).
/// Nhóm thứ hai, stroke icon: cá mẫu trên bể (Fish.SetAsIconPreview) mang bit IconRenderingLayer, cùng các bước trên nhưng màu / bề dày
/// riêng (IconSettings, mặc định đen mảnh) để nổi trên nền sáng. Chỉ chạy khi Fish.IconPreviewCount > 0, vẽ trước viền highlight.
/// </summary>
public class FishOutlineFeature : ScriptableRendererFeature
{
    /// <summary>Bit rendering layer đánh dấu renderer cần viền (Project Settings > Tags and Layers > Rendering Layers: "FishOutline").</summary>
    public const uint RenderingLayer = 1u << 1;

    /// <summary>Bit rendering layer của cá mẫu cần stroke icon (Rendering Layers: "FishIconStroke").</summary>
    public const uint IconRenderingLayer = 1u << 2;

    /// <summary>Rendering Layer "FishOutlineDeny" (bit 3): viền đỏ "không đủ chỗ" của đàn FishSchoolBubble (Fish.SetDenied), cùng các bước như highlight.</summary>
    public const uint DenyRenderingLayer = 1u << 3;

    private const string ShaderName = "FishSort/FishOutlineScreen";

    /// <summary>
    /// Số đỉnh mỗi pass HDist / Coverage / Composite: MỘT tam giác của VertRect. Không vẽ quad 2 tam giác: trên GPU Mali (Pixel 6 Pro, Vulkan)
    /// hàng texel dọc cạnh chéo chung ra sai (texture không clear nên là rác) -> sọc màu viền chạy chéo màn hình.
    /// </summary>
    public const int RectVertexCount = 3;

    /// <summary>Chiều cao màn hình quy chiếu cho Width (px).</summary>
    private const float ReferenceScreenHeight = 1280f;

    [Serializable]
    public class Settings
    {
        [Tooltip("Màu viền")]
        public Color Color = new(0.9f, 1f, 0.24f, 1f);

        [Min(0f), Tooltip("Bề dày viền tính bằng pixel ở màn hình cao 1280 px, tự nhân theo chiều cao màn hình thật")]
        public float Width = 9f;

        [Range(0.25f, 1f), Tooltip("Độ phân giải của mask / khoảng cách so với màn hình. 0.5 = một nửa: đủ mịn và rẻ gấp 4")]
        public float ResolutionScale = 0.5f;
    }

    [SerializeField, Tooltip("Viền highlight quanh cá đang đè tay / tutorial")]
    private Settings settings = new();

    [SerializeField, Tooltip("Stroke quanh cá mẫu trên bể (Fish.SetAsIconPreview). Chỉ xử lý vùng bao các icon nên ResolutionScale 1 vẫn rẻ; " +
                             "dưới 1 thì mép trong của stroke mảnh bị răng cưa")]
    private Settings iconSettings = new() { Color = Color.black, Width = 3f, ResolutionScale = 1f };

    [SerializeField, Tooltip("Viền đỏ quanh cả đàn FishSchoolBubble khi đè tay mà bể + ô chờ không đủ chỗ (Fish.SetDenied)")]
    private Settings denySettings = new() { Color = new Color(1f, 0.25f, 0.2f, 1f), Width = 7f, ResolutionScale = 1f };

    [SerializeField, HideInInspector, Tooltip("Shader FishSort/FishOutlineScreen; giữ tham chiếu để build không strip")]
    private Shader shader;

    private Material material;
    private FishOutlinePass pass;
    private FishOutlinePass iconPass;
    private FishOutlinePass denyPass;

    public override void Create()
    {
        if (shader == null) shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError($"FishOutlineFeature: không tìm thấy shader {ShaderName}.");
            return;
        }

        material = CoreUtils.CreateEngineMaterial(shader);
        pass = new FishOutlinePass(material, RenderingLayer, "FishOutline") { renderPassEvent = RenderPassEvent.AfterRenderingTransparents };
        iconPass = new FishOutlinePass(material, IconRenderingLayer, "FishIconStroke") { renderPassEvent = RenderPassEvent.AfterRenderingTransparents };
        denyPass = new FishOutlinePass(material, DenyRenderingLayer, "FishOutlineDeny") { renderPassEvent = RenderPassEvent.AfterRenderingTransparents };
        // Không HDR / depth / opaque / sorting-layer texture thì Renderer2D vẽ thẳng ra backbuffer, và các pass này chỉ đúng khi camera color là
        // texture (vẽ backbuffer: mask viền lệch vị trí, stroke icon mất). Khai báo cần camera color để URP tạo intermediate texture ở frame có outline.
        pass.ConfigureInput(ScriptableRenderPassInput.Color);
        iconPass.ConfigureInput(ScriptableRenderPassInput.Color);
        denyPass.ConfigureInput(ScriptableRenderPassInput.Color);
    }

    /// <summary>Cả 3 pass đã khai báo cần camera color (xem Create).</summary>
    public bool PassesRequireCameraColor =>
        pass != null && iconPass != null && denyPass != null
        && (pass.input & ScriptableRenderPassInput.Color) != 0
        && (iconPass.input & ScriptableRenderPassInput.Color) != 0
        && (denyPass.input & ScriptableRenderPassInput.Color) != 0;

    /// <summary>
    /// Feature chạy trên mọi camera Game không render vào RenderTexture: Main Camera và camera Tut. Camera Tut là Overlay chỉ vẽ layer TutMesh,
    /// mà Fish.SetTut chuyển cá tutorial sang layer đó rồi bật highlight, nên viền tutorial phải do camera Tut vẽ (Main Camera không thấy layer này).
    /// Camera cá Home render vào RenderTexture nên bỏ.
    /// </summary>
    public static bool RendersFor(CameraType cameraType, CameraRenderType renderType, bool hasTargetTexture)
    {
        return cameraType == CameraType.Game && !hasTargetTexture;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (pass == null || iconPass == null || denyPass == null || material == null) return;
        if (!RendersFor(renderingData.cameraData.cameraType, renderingData.cameraData.renderType, renderingData.cameraData.camera.targetTexture != null)) return;

        // Stroke icon trước để viền highlight (nếu trùng chỗ) đè lên trên.
        if (Fish.IconPreviewCount > 0 && Fish.TryGetIconPreviewBounds(out Bounds iconBounds))
        {
            iconPass.Setup(iconSettings, iconBounds);
            renderer.EnqueuePass(iconPass);
        }

        if (Fish.HighlightedCount > 0)
        {
            pass.Setup(settings, null);
            renderer.EnqueuePass(pass);
        }

        if (Fish.DeniedCount > 0)
        {
            denyPass.Setup(denySettings, null);
            renderer.EnqueuePass(denyPass);
        }
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
        material = null;
        pass = null;
        iconPass = null;
        denyPass = null;
    }

    private class FishOutlinePass : ScriptableRenderPass
    {
        private const int MaskPass = 0;
        private const int HDistPass = 1;
        private const int CoveragePass = 2;
        private const int CompositePass = 3;

        /// <summary>Renderer có một trong các pass này (URP Lit / Unlit / sprite...) mới được đưa vào mask; pass vẽ thật là override material.</summary>
        private static readonly ShaderTagId[] ShaderTags =
        {
            new("Universal2D"), new("UniversalForward"), new("UniversalForwardOnly"), new("SRPDefaultUnlit"),
        };

        private static readonly int ParamsId = Shader.PropertyToID("_FishOutlineParams");
        private static readonly int ColorId = Shader.PropertyToID("_FishOutlineColor");
        private static readonly int BoundsMinId = Shader.PropertyToID("_FishOutlineBoundsMin");
        private static readonly int BoundsMaxId = Shader.PropertyToID("_FishOutlineBoundsMax");
        private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
        private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
        private static readonly MaterialPropertyBlock PropertyBlock = new();

        private readonly Material material;
        private readonly uint renderingLayer;
        private Settings settings;
        private Bounds? worldBounds;

        private class MaskData
        {
            public RendererListHandle List;
        }

        private class BlitData
        {
            public TextureHandle Source;
            public Material Material;
            public int Pass;
            public Vector4 Params;
            public Color Color;
            public Vector4 BoundsMin;
            public Vector4 BoundsMax;
        }

        public FishOutlinePass(Material material, uint renderingLayer, string samplerName)
        {
            this.material = material;
            this.renderingLayer = renderingLayer;
            profilingSampler = new ProfilingSampler(samplerName);
        }

        /// <summary>worldBounds khác null: các pass sau mask chỉ xử lý vùng chiếu của AABB này (cộng lề theo bán kính quét) thay vì toàn màn hình.</summary>
        public void Setup(Settings settings, Bounds? worldBounds)
        {
            this.settings = settings;
            this.worldBounds = worldBounds;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resources = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            if (settings == null || !resources.activeColorTexture.IsValid()) return;

            RenderTextureDescriptor cameraDesc = cameraData.cameraTargetDescriptor;
            float scale = Mathf.Clamp(settings.ResolutionScale, 0.25f, 1f);
            int width = Mathf.Max(1, Mathf.RoundToInt(cameraDesc.width * scale));
            int height = Mathf.Max(1, Mathf.RoundToInt(cameraDesc.height * scale));
            float widthTexels = settings.Width * cameraDesc.height / ReferenceScreenHeight * scale;
            if (widthTexels < 0.01f) return;
            int radius = Mathf.Clamp(Mathf.CeilToInt(widthTexels) + 1, 1, 64);
            Vector4 outlineParams = new(radius, widthTexels, 1f / width, 1f / height);

            RenderTextureDescriptor desc = cameraDesc;
            desc.width = width;
            desc.height = height;
            desc.graphicsFormat = GraphicsFormat.R8_UNorm;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            desc.useMipMap = false;
            desc.autoGenerateMips = false;
            desc.bindMS = false;
            TextureHandle mask = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_FishOutlineMask", true, FilterMode.Point);
            TextureHandle hDist = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_FishOutlineHDist", false, FilterMode.Point);
            TextureHandle coverage = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_FishOutlineCoverage", false, FilterMode.Bilinear);

            using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("FishOutline Mask", out MaskData data, profilingSampler))
            {
                RendererListDesc listDesc = new(ShaderTags, renderingData.cullResults, cameraData.camera)
                {
                    renderQueueRange = RenderQueueRange.all,
                    sortingCriteria = SortingCriteria.None,
                    overrideMaterial = material,
                    overrideMaterialPassIndex = MaskPass,
                    layerMask = -1,
                    renderingLayerMask = renderingLayer,
                };
                data.List = renderGraph.CreateRendererList(listDesc);
                builder.UseRendererList(data.List);
                builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (MaskData d, RasterGraphContext context) => context.cmd.DrawRendererList(d.List));
            }

            // Lề (texel) quanh vùng chiếu của worldBounds: mỗi pass phải phủ hết vùng mà pass sau đọc (hDist và coverage không clear,
            // ngoài vùng vẽ là rác). Composite: vành viền R; Coverage: thêm texel lân cận của bilinear; HDist: thêm R hàng mà Coverage quét.
            AddBlitPass(renderGraph, "FishOutline HDist", mask, hDist, HDistPass, outlineParams, 2 * radius + 4, AccessFlags.Write);
            AddBlitPass(renderGraph, "FishOutline Coverage", hDist, coverage, CoveragePass, outlineParams, radius + 3, AccessFlags.Write);
            AddBlitPass(renderGraph, "FishOutline Composite", coverage, resources.activeColorTexture, CompositePass, outlineParams, radius + 1, AccessFlags.ReadWrite);
        }

        private void AddBlitPass(RenderGraph renderGraph, string name, TextureHandle source, TextureHandle destination, int passIndex,
            Vector4 outlineParams, int padTexels, AccessFlags destinationAccess)
        {
            using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(name, out BlitData data, profilingSampler);
            data.Source = source;
            data.Material = material;
            data.Pass = passIndex;
            data.Params = outlineParams;
            data.Color = settings.Color;
            Vector3 boundsMin = worldBounds?.min ?? Vector3.zero;
            Vector3 boundsMax = worldBounds?.max ?? Vector3.zero;
            data.BoundsMin = new Vector4(boundsMin.x, boundsMin.y, boundsMin.z, worldBounds.HasValue ? 1f : 0f);
            data.BoundsMax = new Vector4(boundsMax.x, boundsMax.y, boundsMax.z, padTexels);
            builder.UseTexture(source);
            builder.SetRenderAttachment(destination, 0, destinationAccess);
            if (passIndex == CompositePass) builder.AllowPassCulling(false);
            builder.SetRenderFunc(static (BlitData d, RasterGraphContext context) =>
            {
                PropertyBlock.Clear();
                PropertyBlock.SetTexture(BlitTextureId, d.Source);
                PropertyBlock.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
                PropertyBlock.SetVector(ParamsId, d.Params);
                PropertyBlock.SetColor(ColorId, d.Color);
                PropertyBlock.SetVector(BoundsMinId, d.BoundsMin);
                PropertyBlock.SetVector(BoundsMaxId, d.BoundsMax);
                context.cmd.DrawProcedural(Matrix4x4.identity, d.Material, d.Pass, MeshTopology.Triangles, RectVertexCount, 1, PropertyBlock);
            });
        }
    }
}
