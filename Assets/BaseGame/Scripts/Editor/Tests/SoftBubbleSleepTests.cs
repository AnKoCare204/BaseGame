using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// Test ngủ / thức của SoftBubbleWorld trên world và bóng thật (edit mode, mô phỏng đồng bộ bằng SoftBubbleWorld.Simulate):
/// bóng chỉ được ngủ khi có vật đỡ ngược lực nổi, và tự thức khi vật đỡ trôi đi (dù trôi chậm hơn ngưỡng đánh thức theo tốc độ).
/// Lỗi gốc (2026-10-07): ngọc trai / bóng thường ngủ dưới một khoảng trống, không bao giờ nổi lên cho tới khi có bóng vỡ.
/// Chạy qua menu Tools/FishSort/Run Soft Bubble Tests hoặc cửa sổ Test Runner (EditMode).
public class SoftBubbleSleepTests
{
    private const string ConfigFolder = "Assets/BaseGame/ScriptableObjects/SoftBody/";
    private const float StepDt = 1f / 60f;

    private readonly List<Object> created = new();
    private SoftBubbleWorld world;

    [SetUp]
    public void SetUp()
    {
        GameObject go = EditorUtility.CreateGameObjectWithHideFlags("SoftBubbleSleepTests World", HideFlags.HideAndDontSave, typeof(SoftBubbleWorld));
        world = go.GetComponent<SoftBubbleWorld>();
        created.Add(go);
    }

    [TearDown]
    public void TearDown()
    {
        // Edit mode không gọi OnDestroy: tự giải phóng NativeArray của world và mesh runtime của bóng.
        if (world != null) Call(world, "OnDestroy");
        foreach (Object obj in created)
        {
            if (obj is GameObject go && go.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null) Object.DestroyImmediate(filter.sharedMesh);
            if (obj != null) Object.DestroyImmediate(obj);
        }

        created.Clear();
        world = null;
    }

    [TestCase("SoftBubbleConfig_Default")]
    [TestCase("SoftBubbleConfig_Rock")]
    public void SleepingBubble_RisesAgain_WhenTheArchAboveOpensSlowly(string configName)
    {
        // Vòm hai bóng giữ bằng lò xo (pháp tuyến chạm lệch ~30 độ hai bên): bóng dưới tựa vào đó là cân bằng bền, ngủ được.
        // Một bóng đơn phía trên thì không đủ: bóng ma sát thấp trượt khỏi đỉnh rồi nổi lên, không bao giờ ngủ.
        SoftBubbleConfig config = LoadConfig(configName);
        SoftBubble below = AddBubble("Below", config, Vector3.zero, 1f);
        SoftBubble left = AddBubble("Left", config, new Vector3(-1.05f, 1.75f, 0f), 1f);
        SoftBubble right = AddBubble("Right", config, new Vector3(1.05f, 1.75f, 0f), 1f);
        Vector3 leftAnchor = left.transform.position;
        Vector3 rightAnchor = right.transform.position;
        left.SetTarget(leftAnchor);
        right.SetTarget(rightAnchor);

        world.Simulate(3f, StepDt);
        Assert.IsTrue(below.IsSleeping, "điều kiện đầu: bóng dưới phải đang ngủ dưới vòm");
        float restY = below.Center.y;

        // Hai bóng vòm tách ra hai bên, mỗi bóng 0.1 m/s (dưới ngưỡng đánh thức theo tốc độ của mọi config), tới khi hở hẳn.
        // Không dịch cả vòm cùng chiều: bóng dưới nằm trong vòm sẽ bị kéo theo, vẫn được đỡ đúng vật lý.
        float t = 0f;
        world.Simulate(30f, StepDt, dt =>
        {
            t += dt;
            var drift = new Vector3(0.1f * t, 0f, 0f);
            left.SetTarget(leftAnchor - drift);
            right.SetTarget(rightAnchor + drift);
        });
        world.Simulate(2f, StepDt);

        Assert.Greater(below.Center.y, restY + 1.5f, $"bóng dưới phải nổi lên khi hết vật đỡ (y lúc ngủ {restY:F2}, giờ {below.Center.y:F2} x {below.Center.x:F2}, ngủ {below.IsSleeping})");
    }

    [Test]
    public void UnsupportedBubble_KeepsRising_WhenSlowerThanSleepThreshold()
    {
        // Lực nổi ròng 0.1 m/s2, cản 1.5/s: vận tốc cuối ~0.067 m/s, thấp hơn SleepThreshold 0.1 của config thường.
        SoftBubbleConfig config = Object.Instantiate(LoadConfig("SoftBubbleConfig_Default"));
        created.Add(config);
        config.GravityScale = 1f;
        config.Buoyancy = 9.81f + 0.1f;
        config.SleepThreshold = 0.1f;
        SoftBubble bubble = AddBubble("Slow", config, Vector3.zero, 1f);
        float startY = bubble.Center.y;

        world.Simulate(3f, StepDt);

        Assert.IsFalse(bubble.IsSleeping, "không có gì đỡ phía trên thì không được ngủ");
        Assert.Greater(bubble.Center.y, startY + 0.1f, $"bóng phải tiếp tục nổi (đi được {bubble.Center.y - startY:F3})");
    }

    [Test]
    public void BubbleResting_UnderRigidTop_FallsAsleep()
    {
        world.SetRigidBounds(new Rect(-10f, -10f, 20f, 12.2f), false, false, false, true); // trần cứng y = 2.2
        SoftBubble bubble = AddBubble("Top", LoadConfig("SoftBubbleConfig_Default"), Vector3.zero, 1f);

        world.Simulate(4f, StepDt);

        Assert.IsTrue(bubble.IsSleeping, $"bóng tựa trần cứng phải ngủ được (y {bubble.Center.y:F2}, v {bubble.Velocity.magnitude:F3})");
    }

    [Test]
    public void BubbleResting_UnderShapeCeiling_FallsAsleep()
    {
        GameObject go = EditorUtility.CreateGameObjectWithHideFlags("Ceiling", HideFlags.HideAndDontSave, typeof(SoftBodyShape));
        created.Add(go);
        go.transform.position = new Vector3(0f, 2.7f, 0f); // đáy hộp ở y = 2.2
        var shape = go.GetComponent<SoftBodyShape>();
        shape.Configure(SoftShapeType.Box, new Vector3(20f, 1f, 20f), false, 0.3f);
        world.Register(shape);
        SoftBubble bubble = AddBubble("Top", LoadConfig("SoftBubbleConfig_Default"), Vector3.zero, 1f);

        world.Simulate(4f, StepDt);

        Assert.IsTrue(bubble.IsSleeping, $"bóng tựa trần shape phải ngủ được (y {bubble.Center.y:F2}, v {bubble.Velocity.magnitude:F3})");
    }

    private static SoftBubbleConfig LoadConfig(string name)
    {
        var config = AssetDatabase.LoadAssetAtPath<SoftBubbleConfig>(ConfigFolder + name + ".asset");
        Assert.IsNotNull(config, "thiếu config " + name);
        return config;
    }

    private SoftBubble AddBubble(string name, SoftBubbleConfig config, Vector3 position, float radius)
    {
        GameObject go = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave,
            typeof(MeshFilter), typeof(MeshRenderer), typeof(SoftBubble));
        created.Add(go);
        go.transform.position = position;
        var bubble = go.GetComponent<SoftBubble>();
        var so = new SerializedObject(bubble);
        so.FindProperty("<Config>k__BackingField").objectReferenceValue = config;
        so.FindProperty("<MeshFilter>k__BackingField").objectReferenceValue = go.GetComponent<MeshFilter>();
        so.FindProperty("<Radius>k__BackingField").floatValue = radius;
        so.ApplyModifiedPropertiesWithoutUndo();
        Call(bubble, "Awake"); // edit mode không gọi Awake: dựng lưới như lúc chạy
        world.Register(bubble);
        return bubble;
    }

    private static void Call(object target, string method)
    {
        target.GetType().GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(target, null);
    }
}
