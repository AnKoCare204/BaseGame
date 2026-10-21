using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// Test component bóng nước nền của BG gameplay. Chạy qua menu Tools/FishSort/Run Gameplay Bg Tests.
public class GameplayBgBubbleEmitterTests
{
    private const string BubblePath = "Assets/VFX/textures/water_bubble.png";

    private GameplayBgTestRig rig;

    [SetUp]
    public void SetUp() => Random.InitState(4321);

    [TearDown]
    public void TearDown()
    {
        rig?.Dispose();
        rig = null;
    }

    private BgBubbleEmitter Make(Vector2 root, Vector2 layer, bool withSprite = true)
    {
        rig = GameplayBgTestRig.Create(root, layer);
        var emitter = rig.Layer.gameObject.AddComponent<BgBubbleEmitter>();
        var so = new SerializedObject(emitter);
        so.FindProperty("<BubbleSprite>k__BackingField").objectReferenceValue = withSprite ? LoadBubbleSprite() : null;
        so.ApplyModifiedPropertiesWithoutUndo();
        emitter.Initialize();
        return emitter;
    }

    private static Sprite LoadBubbleSprite()
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(BubblePath))
        {
            if (asset is Sprite sprite) return sprite;
        }
        Assert.Fail($"{BubblePath} không có sub-asset Sprite (đang là Sprite Multiple, một lát water_bubble_0)");
        return null;
    }

    private static List<RectTransform> Active(BgBubbleEmitter emitter)
    {
        var list = new List<RectTransform>();
        foreach (Transform child in emitter.transform)
        {
            if (child.GetComponent<Image>().enabled) list.Add((RectTransform)child);
        }
        return list;
    }

    /// <summary>Độ đậm thật trên màn hình: màu Image nhân alpha của CanvasRenderer.</summary>
    private static float Alpha(Image image) => image.color.a * image.canvasRenderer.GetAlpha();

    [Test]
    public void FadingDoesNotChangeImageColor()
    {
        // Đổi Image.color mỗi frame làm uGUI dựng lại mesh từng bóng (đo được +0.16 ms canvas trong editor);
        // độ mờ phải đi qua CanvasRenderer.SetAlpha, màu Image giữ nguyên.
        BgBubbleEmitter emitter = Make(new Vector2(1080f, 2340f), new Vector2(1560f, 2340f));
        emitter.Simulate(0f);
        var colors = new Dictionary<Transform, Color>();
        foreach (RectTransform b in Active(emitter)) colors[b] = b.GetComponent<Image>().color;
        for (int i = 0; i < 20; i++) emitter.Simulate(0.05f);
        foreach (KeyValuePair<Transform, Color> pair in colors)
        {
            var image = pair.Key.GetComponent<Image>();
            if (!image.enabled) continue;
            Assert.AreEqual(pair.Value, image.color, $"{pair.Key.name}: Image.color đổi khi bóng mờ dần");
        }
    }

    [Test]
    public void FirstSimulate_PrewarmsInsideBand()
    {
        BgBubbleEmitter emitter = Make(new Vector2(1080f, 2340f), new Vector2(1560f, 2340f));
        Assert.AreEqual(0, emitter.ActiveCount, "chưa Simulate thì chưa có bóng");
        emitter.Simulate(0f);
        Assert.AreEqual(6, emitter.ActiveCount);
        foreach (RectTransform b in Active(emitter))
        {
            float ny = BgBubbleMath.NormalizedY(b.anchoredPosition.y, 1170f);
            Assert.That(ny, Is.InRange(0.05f - 1e-3f, 0.8f + 1e-3f));
            Assert.Greater(Alpha(b.GetComponent<Image>()), 0.25f, "bóng có sẵn đã hiện (ở ny = 0.8 alpha = 0.32)");
        }
    }

    [TestCase(1080f, 2340f, 1560f, 2340f)]
    [TestCase(1080f, 2520f, 1680f, 2520f)]
    [TestCase(1440f, 1920f, 1440f, 2160f)]
    public void Bubbles_SpawnInsideVisibleWidth(float rootW, float rootH, float layerW, float layerH)
    {
        BgBubbleEmitter emitter = Make(new Vector2(rootW, rootH), new Vector2(layerW, layerH));
        float limit = BgLayerMath.VisibleHalf(rootW, layerW) - 40f + 12f;
        for (int i = 0; i < 1500; i++)
        {
            emitter.Simulate(0.05f);
            foreach (RectTransform b in Active(emitter))
            {
                Assert.LessOrEqual(Mathf.Abs(b.anchoredPosition.x), limit + 1e-3f, $"bóng ở x={b.anchoredPosition.x} ra ngoài bề ngang nhìn thấy");
            }
        }
    }

    [Test]
    public void NeverExceedsMaxAlive_AndScreenNeverEmpty()
    {
        BgBubbleEmitter emitter = Make(new Vector2(1080f, 2340f), new Vector2(1560f, 2340f));
        for (int i = 0; i < 2000; i++)
        {
            emitter.Simulate(0.05f);
            Assert.LessOrEqual(emitter.ActiveCount, 10);
            Assert.GreaterOrEqual(emitter.ActiveCount, 1, $"màn hình trống ở bước {i}");
        }
    }

    [Test]
    public void Bubbles_RiseFadeAtTopAndLeave()
    {
        BgBubbleEmitter emitter = Make(new Vector2(1080f, 2340f), new Vector2(1560f, 2340f));
        emitter.Simulate(0f);
        var lastY = new Dictionary<Transform, float>();
        int left = 0;
        for (int i = 0; i < 1000; i++)
        {
            emitter.Simulate(0.05f);
            foreach (Transform child in emitter.transform)
            {
                var image = child.GetComponent<Image>();
                var rect = (RectTransform)child;
                if (!image.enabled)
                {
                    if (lastY.Remove(child)) left++;
                    continue;
                }
                float y = rect.anchoredPosition.y;
                if (lastY.TryGetValue(child, out float prev) && y < prev)
                {
                    // Cùng Image vừa trả về pool rồi được dùng lại cho bóng mới trong cùng một bước: bóng mới phải sinh dưới mép dưới.
                    Assert.LessOrEqual(y, -1170f, $"{child.name} đi xuống giữa chừng: {prev} -> {y}");
                    left++;
                }
                lastY[child] = y;
                if (y >= 1170f) Assert.LessOrEqual(Alpha(image), 1e-4f, "từ mép trên trở lên phải trong suốt");
            }
        }
        Assert.Greater(left, 10, "bóng phải lần lượt lên hết và trả về pool");
    }

    [Test]
    public void PausedFrames_NothingMoves()
    {
        BgBubbleEmitter emitter = Make(new Vector2(1080f, 2340f), new Vector2(1560f, 2340f));
        emitter.Simulate(0.5f);
        var before = new List<Vector2>();
        foreach (RectTransform b in Active(emitter)) before.Add(b.anchoredPosition);
        int count = emitter.ActiveCount;
        for (int i = 0; i < 50; i++) emitter.Simulate(0f);
        Assert.AreEqual(count, emitter.ActiveCount);
        List<RectTransform> after = Active(emitter);
        for (int i = 0; i < after.Count; i++)
        {
            Assert.IsFalse(float.IsNaN(after[i].anchoredPosition.x) || float.IsNaN(after[i].anchoredPosition.y));
            Assert.AreEqual(before[i], after[i].anchoredPosition);
        }
    }

    [Test]
    public void ZeroSizeLayout_WaitsBeforePrewarmAndSpawn()
    {
        // Prefab trong scene lưu kích thước 0 cho tới lần layout đầu (AspectRatioFitter chạy ở Canvas.willRenderCanvases, sau Update).
        BgBubbleEmitter emitter = Make(new Vector2(1080f, 2340f), Vector2.zero);
        emitter.Simulate(0f);
        emitter.Simulate(2f);
        Assert.AreEqual(0, emitter.ActiveCount, "chưa có kích thước lớp thì chưa sinh bóng (sẽ dồn hết vào giữa màn)");

        rig.Layer.sizeDelta = new Vector2(1560f, 2340f);
        emitter.Simulate(0f);
        Assert.AreEqual(6, emitter.ActiveCount, "có kích thước rồi thì prewarm như thường");
        foreach (RectTransform b in Active(emitter))
        {
            Assert.That(BgBubbleMath.NormalizedY(b.anchoredPosition.y, 1170f), Is.InRange(0.05f - 1e-3f, 0.8f + 1e-3f));
        }
    }

    [Test]
    public void MissingSprite_DisablesWithoutThrow()
    {
        BgBubbleEmitter emitter = Make(new Vector2(1080f, 2340f), new Vector2(1560f, 2340f), withSprite: false);
        Assert.IsFalse(emitter.enabled);
        emitter.Simulate(0.1f);
        Assert.AreEqual(0, emitter.transform.childCount);
    }

    [Test]
    public void Simulate_DoesNotAllocate()
    {
        BgBubbleEmitter emitter = Make(new Vector2(1080f, 2340f), new Vector2(1560f, 2340f));
        emitter.Simulate(0.016f);

        long probe = System.GC.GetAllocatedBytesForCurrentThread();
        var junk = new int[256];
        if (System.GC.GetAllocatedBytesForCurrentThread() - probe < 1000 || junk.Length == 0) Assert.Ignore("runtime không đo được byte cấp phát");

        long start = System.GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 400; i++) emitter.Simulate(0.016f);
        Assert.AreEqual(0L, System.GC.GetAllocatedBytesForCurrentThread() - start);
    }
}
