using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// Test EditMode cho FishHomeRenderManager: dựng camera ortho + RendererGroup + hai cá bằng tay, gán field qua SerializedObject
/// (Awake của Singleton không chạy trong edit mode), gọi StartRender() rồi kiểm trọng tài, dải làn và điểm chạm theo viewport.
public class FishHomeRenderManagerTests
{
    private GameObject root;
    private FishHomeRenderManager manager;
    private FishHomeSwimmer[] fishes;
    private Camera camera;

    [SetUp]
    public void SetUp()
    {
        Random.InitState(42);
        root = new GameObject("TestHome");
        camera = new GameObject("Camera").AddComponent<Camera>();
        camera.transform.SetParent(root.transform, false);
        camera.transform.localPosition = new Vector3(0f, 0f, -10f);
        camera.orthographic = true;
        camera.orthographicSize = 7f;

        GameObject group = new GameObject("RendererGroup");
        group.transform.SetParent(root.transform, false);
        fishes = new FishHomeSwimmer[2];
        for (int i = 0; i < fishes.Length; i++)
        {
            GameObject go = new GameObject("Fish" + i);
            go.transform.SetParent(group.transform, false);
            go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); // bơi sang phải
            fishes[i] = go.AddComponent<FishHomeSwimmer>();
        }

        manager = root.AddComponent<FishHomeRenderManager>();
        SerializedObject so = new SerializedObject(manager);
        so.FindProperty("<RendererGroup>k__BackingField").objectReferenceValue = group;
        so.FindProperty("<RenderCamera>k__BackingField").objectReferenceValue = camera;
        SerializedProperty swimmers = so.FindProperty("<Swimmers>k__BackingField");
        swimmers.arraySize = fishes.Length;
        for (int i = 0; i < fishes.Length; i++) swimmers.GetArrayElementAtIndex(i).objectReferenceValue = fishes[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        manager.StartRender();                                  // ApplySwimArea: vùng bơi, làn, trọng tài
        foreach (FishHomeSwimmer fish in fishes) fish.Restart(); // edit mode không chạy OnEnable
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(root);

    private static void SetCuriousChance(FishHomeSwimmer fish, float value)
    {
        SerializedObject so = new SerializedObject(fish);
        so.FindProperty("curiousChance").floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    [Test]
    public void StartRender_AssignsDistinctCenteredLanes()
    {
        // 2 cá + 2 làn dư = 4 slot cách nhau 2: -3, -1, 1, 3; cá nhận hai slot giữa
        Assert.AreEqual(-1f, fishes[0].LaneZ, 1e-4f);
        Assert.AreEqual(1f, fishes[1].LaneZ, 1e-4f);
    }

    [Test]
    public void PickLane_ReturnsFreeSlotOtherThanCurrent()
    {
        float lane = manager.PickLane(fishes[0]);
        Assert.IsTrue(Mathf.Approximately(lane, -3f) || Mathf.Approximately(lane, 3f), $"làn mới phải là slot trống ở hai đầu, nhận {lane}");
    }

    [Test]
    public void PickLane_Repeatedly_NeverCollidesWithOtherFish()
    {
        for (int i = 0; i < 20; i++)
        {
            float a = manager.PickLane(fishes[0]);
            float b = manager.PickLane(fishes[1]);
            Assert.AreNotEqual(a, b, "hai cá không bao giờ cùng làn");
        }
    }

    [Test]
    public void CanLeave_FalseWhileAnotherFishIsOutside()
    {
        Assert.IsTrue(manager.CanLeave());
        fishes[1].transform.localPosition = new Vector3(100f, 0f, 0f);
        fishes[1].Restart(); // ngoài khung -> Outside
        Assert.AreEqual(FishHomeSwimmer.State.Outside, fishes[1].CurrentState);
        Assert.IsFalse(manager.CanLeave(), "MaxOutside mặc định 1");
    }

    [Test]
    public void CanLeave_FalseWhileAnotherFishIsStillSwimmingOut()
    {
        // Lỗi thấy trong Play mode: hai cá cùng "rời khung" vì cá đang bơi ra (chưa Outside) không được đếm
        SerializedObject so = new SerializedObject(fishes[1]);
        so.FindProperty("leaveChance").floatValue = 1f;
        so.FindProperty("leaveInterval").vector2Value = Vector2.zero;
        so.ApplyModifiedPropertiesWithoutUndo();
        fishes[1].Restart();
        bool swimmingOut = false;
        for (float t = 0f; t < 60f && !swimmingOut; t += 1f / 60f)
        {
            fishes[1].Simulate(1f / 60f);
            swimmingOut = fishes[1].IsOutsideOrLeaving && fishes[1].CurrentState == FishHomeSwimmer.State.Swim;
        }
        Assert.IsTrue(swimmingOut, "cá 1 phải bắt đầu bơi ra khung (chưa Outside)");
        Assert.IsFalse(manager.CanLeave(), "đang có cá bơi ra thì không cho con khác rời khung");
    }

    [Test]
    public void CanBeCurious_FalseWhileAnotherFishIsPending()
    {
        Assert.IsTrue(manager.CanBeCurious());
        SetCuriousChance(fishes[0], 1f);
        fishes[0].NoteTapPoint(Vector2.zero);
        Assert.IsTrue(fishes[0].IsCuriousOrPending);
        Assert.IsFalse(manager.CanBeCurious(), "MaxCurious mặc định 1");
    }

    [Test]
    public void Startle_AtFishViewport_MakesOnlyNearbyFishFlee()
    {
        fishes[1].transform.localPosition = new Vector3(0f, 6f, 0f); // xa hơn bán kính nhấn 3.5
        Vector3 vp = camera.WorldToViewportPoint(fishes[0].transform.position);
        manager.Startle(new Vector2(vp.x, vp.y), false);
        Assert.AreEqual(FishHomeSwimmer.State.Flee, fishes[0].CurrentState);
        Assert.AreEqual(FishHomeSwimmer.State.Swim, fishes[1].CurrentState);
    }

    [Test]
    public void Startle_Trail_DoesNotNoteTapPoint()
    {
        SetCuriousChance(fishes[0], 0f);
        SetCuriousChance(fishes[1], 1f);
        fishes[1].transform.localPosition = new Vector3(0f, 6f, 0f);
        Vector3 vp = camera.WorldToViewportPoint(fishes[0].transform.position);
        manager.Startle(new Vector2(vp.x, vp.y), true);
        Assert.IsFalse(fishes[1].IsCuriousOrPending, "vệt rê không ghi điểm tò mò");
        manager.Startle(new Vector2(vp.x, vp.y), false);
        Assert.IsTrue(fishes[1].IsCuriousOrPending, "cú nhấn thì có");
    }
}
