using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// Test component đàn cá xa của BG gameplay. Chạy qua menu Tools/FishSort/Run Gameplay Bg Tests.
public class GameplayBgFishSchoolTests
{
    private const float FishWidth = 100f;

    private GameplayBgTestRig rig;
    private Sprite testSprite;

    [SetUp]
    public void SetUp()
    {
        Random.InitState(12345);
        // Sprite tạo tại chỗ: test không phụ thuộc file ảnh cá (máy khác có thể chưa có), bề ngang cá lấy từ FishWidth.
        testSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
    }

    [TearDown]
    public void TearDown()
    {
        rig?.Dispose();
        rig = null;
        if (testSprite != null) Object.DestroyImmediate(testSprite);
        foreach (Object extra in extras) if (extra != null) Object.DestroyImmediate(extra);
        extras.Clear();
    }

    private readonly List<Object> extras = new List<Object>();

    private Sprite MakeSprite(float w, float h)
    {
        Sprite sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f));
        extras.Add(sprite);
        return sprite;
    }

    private BgFishSchool Make(Vector2 root, Vector2 layer, float edgeMargin = 80f, int pool = 10, bool withSprite = true,
        Sprite[] sprites = null, Material material = null)
    {
        rig = GameplayBgTestRig.Create(root, layer);
        var fish = rig.Layer.gameObject.AddComponent<BgFishSchool>();
        var so = new SerializedObject(fish);
        Sprite[] list = sprites ?? (withSprite ? new[] { testSprite } : new Sprite[0]);
        SerializedProperty spritesProp = so.FindProperty("<FishSprites>k__BackingField");
        spritesProp.arraySize = list.Length;
        for (int i = 0; i < list.Length; i++) spritesProp.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
        so.FindProperty("<FishMaterial>k__BackingField").objectReferenceValue = material;
        so.FindProperty("<FirstDelay>k__BackingField").vector2Value = new Vector2(1000f, 1000f);
        so.FindProperty("<Interval>k__BackingField").vector2Value = new Vector2(1000f, 1000f);
        so.FindProperty("<EdgeMargin>k__BackingField").floatValue = edgeMargin;
        so.FindProperty("<PoolSize>k__BackingField").intValue = pool;
        so.ApplyModifiedPropertiesWithoutUndo();
        fish.Initialize();
        return fish;
    }

    private static List<RectTransform> Active(BgFishSchool fish)
    {
        var list = new List<RectTransform>();
        foreach (Transform child in fish.transform)
        {
            if (child.GetComponent<Image>().enabled) list.Add((RectTransform)child);
        }
        return list;
    }

    private static void AssertFullyOutside(List<RectTransform> fish, float halfWidth)
    {
        foreach (RectTransform f in fish)
        {
            float x = f.anchoredPosition.x;
            float halfBody = FishWidth * Mathf.Abs(f.localScale.x) * 0.5f;
            Assert.GreaterOrEqual(Mathf.Abs(x) - halfBody, halfWidth - 1e-3f, $"cá ở x={x} còn ló vào màn hình (nửa màn {halfWidth})");
            Assert.Greater(x * f.localScale.x, 0f, "cá phải quay đầu vào trong màn hình");
        }
    }

    [TestCase(1080f, 2340f, 1560f, 2340f)]
    [TestCase(1080f, 2520f, 1680f, 2520f)]
    [TestCase(1440f, 1920f, 1440f, 2160f)]
    public void SpawnedFish_StartFullyOutsideScreen(float rootW, float rootH, float layerW, float layerH)
    {
        BgFishSchool fish = Make(new Vector2(rootW, rootH), new Vector2(layerW, layerH));
        float half = BgLayerMath.VisibleHalf(rootW, layerW);
        bool sawLeft = false, sawRight = false;
        for (int round = 0; round < 12; round++)
        {
            Assert.IsTrue(fish.TrySpawnSchool());
            List<RectTransform> school = Active(fish);
            Assert.That(school.Count, Is.InRange(3, 5));
            AssertFullyOutside(school, half);
            if (school[0].anchoredPosition.x < 0f) sawRight = true; else sawLeft = true;
            for (int i = 0; i < 600 && fish.ActiveCount > 0; i++) fish.Simulate(0.1f);
            Assert.AreEqual(0, fish.ActiveCount, "cả đàn phải bơi qua hết và trả về pool");
        }
        Assert.IsTrue(sawLeft && sawRight, "12 đàn phải có cả hai hướng");
    }

    [Test]
    public void School_CrossesVisibleArea()
    {
        BgFishSchool fish = Make(new Vector2(1080f, 2340f), new Vector2(1560f, 2340f));
        fish.TrySpawnSchool();
        var crossed = new Dictionary<Transform, bool>();
        foreach (RectTransform f in Active(fish)) crossed[f] = false;
        for (int i = 0; i < 600 && fish.ActiveCount > 0; i++)
        {
            fish.Simulate(0.1f);
            foreach (RectTransform f in Active(fish))
            {
                if (Mathf.Abs(f.anchoredPosition.x) < 100f) crossed[f] = true;
            }
        }
        foreach (KeyValuePair<Transform, bool> pair in crossed) Assert.IsTrue(pair.Value, $"{pair.Key.name} không đi qua giữa màn hình");
    }

    [Test]
    public void EdgeMarginTooSmall_FishStillStartHidden()
    {
        BgFishSchool fish = Make(new Vector2(1080f, 2340f), new Vector2(1560f, 2340f), edgeMargin: 0f);
        for (int round = 0; round < 6; round++)
        {
            fish.TrySpawnSchool();
            AssertFullyOutside(Active(fish), 540f);
            for (int i = 0; i < 600 && fish.ActiveCount > 0; i++) fish.Simulate(0.1f);
        }
    }

    [Test]
    public void Simulate_IsFrameRateIndependent()
    {
        var coarse = new List<Vector2>();
        var fine = new List<Vector2>();

        Random.InitState(7);
        BgFishSchool a = Make(new Vector2(1080f, 2340f), new Vector2(1560f, 2340f));
        a.TrySpawnSchool();
        a.Simulate(1f);
        foreach (RectTransform f in Active(a)) coarse.Add(f.anchoredPosition);
        rig.Dispose();

        Random.InitState(7);
        BgFishSchool b = Make(new Vector2(1080f, 2340f), new Vector2(1560f, 2340f));
        b.TrySpawnSchool();
        for (int i = 0; i < 10; i++) b.Simulate(0.1f);
        foreach (RectTransform f in Active(b)) fine.Add(f.anchoredPosition);

        Assert.AreEqual(coarse.Count, fine.Count);
        for (int i = 0; i < coarse.Count; i++)
        {
            Assert.AreEqual(coarse[i].x, fine[i].x, 1e-2f);
            Assert.AreEqual(coarse[i].y, fine[i].y, 1e-2f);
        }
    }

    [Test]
    public void PausedFrames_NothingMoves()
    {
        BgFishSchool fish = Make(new Vector2(1080f, 2340f), new Vector2(1560f, 2340f));
        fish.TrySpawnSchool();
        fish.Simulate(0.5f);
        var before = new List<Vector2>();
        foreach (RectTransform f in Active(fish)) before.Add(f.anchoredPosition);
        int count = fish.ActiveCount;

        for (int i = 0; i < 50; i++) fish.Simulate(0f);

        Assert.AreEqual(count, fish.ActiveCount);
        List<RectTransform> after = Active(fish);
        for (int i = 0; i < after.Count; i++)
        {
            Assert.IsFalse(float.IsNaN(after[i].anchoredPosition.x) || float.IsNaN(after[i].anchoredPosition.y));
            Assert.AreEqual(before[i], after[i].anchoredPosition);
        }
    }

    [Test]
    public void PoolExhausted_WaitsForFreeSlots()
    {
        BgFishSchool fish = Make(new Vector2(1080f, 2340f), new Vector2(1560f, 2340f), pool: 6);
        int spawned = 0;
        for (int i = 0; i < 5; i++)
        {
            if (fish.TrySpawnSchool()) spawned++;
            Assert.LessOrEqual(fish.ActiveCount, 6);
        }
        Assert.GreaterOrEqual(spawned, 1);
        Assert.IsFalse(fish.TrySpawnSchool(), "còn dưới 3 chỗ trống thì không ra đàn");
        for (int i = 0; i < 600 && fish.ActiveCount > 0; i++) fish.Simulate(0.1f);
        Assert.IsTrue(fish.TrySpawnSchool(), "pool trống lại thì ra đàn được");
    }

    [Test]
    public void ZeroSizeLayout_NoSchoolUntilLaidOut()
    {
        // Prefab trong scene lưu kích thước 0 cho tới lần layout đầu; ra đàn lúc đó thì cá xuất phát ngay giữa màn hình.
        BgFishSchool fish = Make(new Vector2(1080f, 2340f), Vector2.zero);
        Assert.IsFalse(fish.TrySpawnSchool());
        Assert.AreEqual(0, fish.ActiveCount);

        rig.Layer.sizeDelta = new Vector2(1560f, 2340f);
        Assert.IsTrue(fish.TrySpawnSchool());
        AssertFullyOutside(Active(fish), 540f);
    }

    [Test]
    public void MissingSprite_DisablesWithoutThrow()
    {
        BgFishSchool fish = Make(new Vector2(1080f, 2340f), new Vector2(1560f, 2340f), withSprite: false);
        Assert.IsFalse(fish.enabled);
        Assert.IsFalse(fish.TrySpawnSchool());
        fish.Simulate(0.1f);
        Assert.AreEqual(0, fish.transform.childCount, "không tạo Image nào (tránh ô trắng)");
    }

    [Test]
    public void OnlyNullSprites_DisablesWithoutThrow()
    {
        BgFishSchool fish = Make(new Vector2(1080f, 2340f), new Vector2(1560f, 2340f), sprites: new Sprite[] { null, null });
        Assert.IsFalse(fish.enabled);
        Assert.IsFalse(fish.TrySpawnSchool());
        Assert.AreEqual(0, fish.transform.childCount);
    }

    [Test]
    public void EachFish_UsesOneOfTheSprites_WithItsAspect()
    {
        Sprite wide = MakeSprite(4f, 2f);
        Sprite tall = MakeSprite(2f, 4f);
        BgFishSchool fish = Make(new Vector2(1080f, 2340f), new Vector2(1560f, 2340f), sprites: new[] { wide, null, tall });
        var seen = new HashSet<Sprite>();
        for (int round = 0; round < 8; round++)
        {
            fish.TrySpawnSchool();
            foreach (RectTransform f in Active(fish))
            {
                Sprite sprite = f.GetComponent<Image>().sprite;
                Assert.IsTrue(sprite == wide || sprite == tall, "chỉ dùng sprite hợp lệ, bỏ qua phần tử null");
                Assert.AreEqual(FishWidth, f.sizeDelta.x, 1e-3f);
                Assert.AreEqual(FishWidth * sprite.rect.height / sprite.rect.width, f.sizeDelta.y, 1e-3f, "chiều cao theo tỉ lệ sprite");
                seen.Add(sprite);
            }
            for (int i = 0; i < 600 && fish.ActiveCount > 0; i++) fish.Simulate(0.1f);
        }
        Assert.AreEqual(2, seen.Count, "các dáng cá đều được dùng");
    }

    [Test]
    public void FishMaterial_IsUsedByEveryPooledImage()
    {
        var material = new Material(Shader.Find("UI/Default"));
        extras.Add(material);
        BgFishSchool fish = Make(new Vector2(1080f, 2340f), new Vector2(1560f, 2340f), material: material);
        Assert.Greater(fish.transform.childCount, 0);
        foreach (Transform child in fish.transform) Assert.AreSame(material, child.GetComponent<Image>().material);
    }

    [Test]
    public void NearerFish_AreDrawnFirst()
    {
        // Shader cá chỉ vẽ mỗi điểm ảnh một lần (stencil): con vẽ trước nằm trên, nên con gần (scale lớn) phải đứng trước trong hierarchy.
        BgFishSchool fish = Make(new Vector2(1080f, 2340f), new Vector2(1560f, 2340f), pool: 30);
        for (int i = 0; i < 5; i++) fish.TrySpawnSchool();
        List<RectTransform> active = Active(fish);
        Assert.Greater(active.Count, 5);
        float previous = float.MaxValue;
        bool sawDifferentDepths = false;
        foreach (RectTransform f in active)
        {
            float scale = Mathf.Abs(f.localScale.x);
            Assert.LessOrEqual(scale, previous + 1e-4f, "cá gần phải đứng trước cá xa");
            if (previous != float.MaxValue && scale < previous - 1e-3f) sawDifferentDepths = true;
            previous = scale;
        }
        Assert.IsTrue(sawDifferentDepths, "5 đàn phải có độ xa khác nhau");
        foreach (Transform child in fish.transform)
        {
            if (!child.GetComponent<Image>().enabled) continue;
            Assert.Less(child.GetSiblingIndex(), active.Count, "cá đang bơi đứng trước các ô trống của pool");
        }
    }

    [Test]
    public void Simulate_DoesNotAllocate()
    {
        BgFishSchool fish = Make(new Vector2(1080f, 2340f), new Vector2(1560f, 2340f));
        fish.TrySpawnSchool();
        fish.Simulate(0.016f);

        long probe = System.GC.GetAllocatedBytesForCurrentThread();
        var junk = new int[256];
        if (System.GC.GetAllocatedBytesForCurrentThread() - probe < 1000 || junk.Length == 0) Assert.Ignore("runtime không đo được byte cấp phát");

        long start = System.GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 200; i++) fish.Simulate(0.016f);
        Assert.AreEqual(0L, System.GC.GetAllocatedBytesForCurrentThread() - start);
    }
}
