using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// FishOutlineFeature chạy trên mọi camera Game không render vào RenderTexture, kể cả camera Overlay: camera Tut chỉ vẽ layer TutMesh,
/// mà Fish.SetTut chuyển cá tutorial sang layer đó rồi bật highlight, nên viền tutorial phải do camera Tut vẽ. Camera cá Home (RT) bị bỏ.
/// Stroke icon trong Renderer2D.asset giữ ResolutionScale 1: dưới 1 mép trong bị răng cưa, vùng xử lý chỉ là AABB icon nên full-res vẫn rẻ.
public class PerfFishOutlineFeatureTests
{
    [Test]
    public void RendersFor_OverlayGameCamera_IsTrue()
    {
        Assert.IsTrue(FishOutlineFeature.RendersFor(CameraType.Game, CameraRenderType.Overlay, hasTargetTexture: false));
    }

    [Test]
    public void RendersFor_BaseGameCamera_IsTrue()
    {
        Assert.IsTrue(FishOutlineFeature.RendersFor(CameraType.Game, CameraRenderType.Base, hasTargetTexture: false));
    }

    [Test]
    public void RendersFor_RenderTextureCamera_IsFalse()
    {
        Assert.IsFalse(FishOutlineFeature.RendersFor(CameraType.Game, CameraRenderType.Base, hasTargetTexture: true));
    }

    [Test]
    public void RendersFor_NonGameCamera_IsFalse()
    {
        Assert.IsFalse(FishOutlineFeature.RendersFor(CameraType.SceneView, CameraRenderType.Base, hasTargetTexture: false));
    }

    [Test]
    public void Create_PassesDeclareCameraColorInput()
    {
        // Không HDR / depth / opaque / sorting-layer texture thì Renderer2D vẽ thẳng ra backbuffer; các pass outline chỉ đúng khi camera color
        // là texture (mask lệch, stroke icon mất khi vẽ backbuffer). Khai báo cần camera color để URP luôn tạo intermediate texture khi có outline.
        FishOutlineFeature feature = ScriptableObject.CreateInstance<FishOutlineFeature>();
        try
        {
            feature.Create();
            Assert.IsTrue(feature.PassesRequireCameraColor, "3 pass phải ConfigureInput(ScriptableRenderPassInput.Color)");
        }
        finally
        {
            Object.DestroyImmediate(feature);
        }
    }

    [Test]
    public void RectPasses_DrawOneTriangle()
    {
        // Quad 2 tam giác để lại sọc chéo trên Mali (Pixel 6 Pro, Vulkan) dọc cạnh chung; VertRect chỉ dựng 3 đỉnh.
        Assert.AreEqual(3, FishOutlineFeature.RectVertexCount);
    }

    [Test]
    public void HDistPass_FullScreen_WritesEveryTexel()
    {
        float[] r = RenderHDist(bounded: false);
        for (int i = 0; i < r.Length; i++)
        {
            if (Mathf.Abs(r[i] - 1f) > 0.02f) Assert.Fail($"texel ({i % Size}, {i / Size}) = {r[i]}, phải 1 (pass toàn màn hình ghi mọi texel)");
        }
    }

    [Test]
    public void HDistPass_Bounded_WritesOnlyInsideRect()
    {
        // AABB [-0.5, 0.5]² với VP đơn vị -> vùng chiếu là texel 16..47 (đối xứng nên y-flip không đổi kết quả).
        float[] r = RenderHDist(bounded: true);
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float v = r[y * Size + x];
            bool inside = x >= 17 && x <= 46 && y >= 17 && y <= 46;
            bool outside = x <= 14 || x >= 49 || y <= 14 || y >= 49;
            if (inside && Mathf.Abs(v - 1f) > 0.02f) Assert.Fail($"texel ({x}, {y}) trong vùng = {v}, phải 1");
            if (outside && Mathf.Abs(v - Sentinel) > 0.02f) Assert.Fail($"texel ({x}, {y}) ngoài vùng = {v}, phải giữ {Sentinel}");
        }
    }

    private const int Size = 64;
    private const float Sentinel = 0.5f;

    /// <summary>Chạy pass HDist (mask đen -> mọi texel được ghi ra 1) vào RT đã tô Sentinel, trả kênh R.</summary>
    private static float[] RenderHDist(bool bounded)
    {
        Material material = CoreUtils.CreateEngineMaterial(Shader.Find("FishSort/FishOutlineScreen"));
        RenderTexture rt = new RenderTexture(Size, Size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Texture2D read = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true);
        CommandBuffer cmd = new CommandBuffer();
        RenderTexture previous = RenderTexture.active;
        try
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetTexture("_BlitTexture", Texture2D.blackTexture);
            block.SetVector("_BlitScaleBias", new Vector4(1f, 1f, 0f, 0f));
            block.SetVector("_FishOutlineParams", new Vector4(2f, 1f, 1f / Size, 1f / Size));
            block.SetVector("_FishOutlineBoundsMin", new Vector4(-0.5f, -0.5f, 0f, bounded ? 1f : 0f));
            block.SetVector("_FishOutlineBoundsMax", new Vector4(0.5f, 0.5f, 0f, 0f));

            cmd.SetRenderTarget(rt);
            cmd.ClearRenderTarget(false, true, new Color(Sentinel, Sentinel, Sentinel, Sentinel));
            cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
            cmd.DrawProcedural(Matrix4x4.identity, material, 1, MeshTopology.Triangles, FishOutlineFeature.RectVertexCount, 1, block);
            Graphics.ExecuteCommandBuffer(cmd);

            RenderTexture.active = rt;
            read.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            read.Apply();
            Color[] pixels = read.GetPixels();
            float[] r = new float[pixels.Length];
            for (int i = 0; i < pixels.Length; i++) r[i] = pixels[i].r;
            return r;
        }
        finally
        {
            RenderTexture.active = previous;
            cmd.Release();
            Object.DestroyImmediate(read);
            rt.Release();
            Object.DestroyImmediate(rt);
            CoreUtils.Destroy(material);
        }
    }

    [Test]
    public void Renderer2D_IconStrokeFullRes_HighlightHalfRes()
    {
        FishOutlineFeature feature = null;
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath("Assets/Settings/Renderer2D.asset"))
        {
            if (o is FishOutlineFeature f) { feature = f; break; }
        }
        Assert.IsNotNull(feature, "Renderer2D.asset phải có FishOutlineFeature");
        SerializedObject so = new SerializedObject(feature);
        Assert.AreEqual(1f, so.FindProperty("iconSettings.ResolutionScale").floatValue, 1e-4f, "stroke icon phải full-res");
        Assert.AreEqual(0.5f, so.FindProperty("settings.ResolutionScale").floatValue, 1e-4f, "viền highlight nửa độ phân giải");
    }
}
