using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// Test EditMode cho máy trạng thái FishHomeSwimmer: GameObject trần (không FishModel, không animator), gọi Restart() rồi Simulate(dt) tay
/// vì edit mode không chạy OnEnable / Update. Random được seed cố định để kết quả lặp lại.
public class FishHomeSwimmerTests
{
    private const float Dt = 1f / 60f;
    private const float HalfWidth = 3.9f;
    private const float HalfLength = 1f;          // fallbackBodySize.x / 2
    private const float LimitX = HalfWidth - HalfLength;

    private GameObject go;
    private FishHomeSwimmer fish;

    private class FakeArbiter : IFishHomeArbiter
    {
        public bool Leave;
        public bool Curious;
        public float Lane = 2f;
        public bool CanLeave() => Leave;
        public bool CanBeCurious() => Curious;
        public float PickLane(FishHomeSwimmer who) => Lane;
    }

    [SetUp]
    public void SetUp()
    {
        Random.InitState(1234);
        go = new GameObject("TestFish");
        go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); // đầu hướng +X: bơi sang phải
        fish = go.AddComponent<FishHomeSwimmer>();
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(go);

    /// Đặt vùng bơi chuẩn của Home (nửa khung 3.9 x 7, làn 0) rồi Restart. Gọi Set(...) TRƯỚC hàm này vì Restart đọc các khoảng thời gian.
    private void Start(IFishHomeArbiter arbiter = null, Vector3? position = null)
    {
        if (position.HasValue) go.transform.localPosition = position.Value;
        fish.SetArea(Vector2.zero, new Vector2(HalfWidth, 7f), 0f, arbiter);
        fish.Restart();
    }

    private void Set(string field, float value)
    {
        SerializedObject so = new SerializedObject(fish);
        Assert.IsNotNull(so.FindProperty(field), $"không có field {field}");
        so.FindProperty(field).floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private void Set(string field, Vector2 value)
    {
        SerializedObject so = new SerializedObject(fish);
        Assert.IsNotNull(so.FindProperty(field), $"không có field {field}");
        so.FindProperty(field).vector2Value = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// Mô phỏng tối đa seconds giây, dừng sớm khi done() đúng. Trả về done() có từng đúng không.
    private bool SimulateUntil(float seconds, System.Func<bool> done)
    {
        for (float t = 0f; t < seconds; t += Dt)
        {
            fish.Simulate(Dt);
            if (done()) return true;
        }
        return false;
    }

    private Vector2 FishXY => new Vector2(go.transform.localPosition.x, go.transform.localPosition.y);

    [Test]
    public void Restart_InsideFrame_StartsSwimmingRight()
    {
        Start();
        Assert.AreEqual(FishHomeSwimmer.State.Swim, fish.CurrentState);
        Assert.AreEqual(1f, fish.DirX);
    }

    [Test]
    public void Rest_WithChanceOne_EntersRestThenResumesSwim()
    {
        Set("restChance", 1f);
        Set("restInterval", Vector2.zero);
        Start();
        Assert.IsTrue(SimulateUntil(30f, () => fish.CurrentState == FishHomeSwimmer.State.Rest), "phải nghỉ trong vòng 30 s");
        Vector2 restPos = FishXY;
        Assert.IsTrue(SimulateUntil(9f, () => fish.CurrentState == FishHomeSwimmer.State.Swim), "nghỉ tối đa 8 s rồi phải bơi lại");
        Assert.Less(Vector2.Distance(restPos, FishXY), 1.5f, "khi nghỉ cá chỉ trôi nhẹ quanh chỗ cũ");
    }

    [Test]
    public void Rest_WithChanceZero_NeverRests()
    {
        Set("restChance", 0f);
        Start();
        Assert.IsFalse(SimulateUntil(60f, () => fish.CurrentState == FishHomeSwimmer.State.Rest));
    }

    [Test]
    public void Simulate_KeepsWholeBodyInsideFrame()
    {
        Start();
        Assert.IsFalse(SimulateUntil(60f, () => Mathf.Abs(go.transform.localPosition.x) > LimitX + 0.01f || Mathf.Abs(go.transform.localPosition.y) > 7f - 0.75f + 0.01f));
    }

    [Test]
    public void NoArbiter_NeverLeavesFrame()
    {
        Set("leaveChance", 1f);
        Set("leaveInterval", Vector2.zero);
        Start();
        bool left = SimulateUntil(60f, () => Mathf.Abs(go.transform.localPosition.x) > LimitX + 0.01f || fish.CurrentState == FishHomeSwimmer.State.Outside);
        Assert.IsFalse(left, "không có trọng tài thì không được rời khung");
    }

    [Test]
    public void ArbiterRefuses_NeverLeavesFrame()
    {
        Set("leaveChance", 1f);
        Set("leaveInterval", Vector2.zero);
        Start(new FakeArbiter { Leave = false });
        Assert.IsFalse(SimulateUntil(60f, () => fish.CurrentState == FishHomeSwimmer.State.Outside));
    }

    [Test]
    public void ArbiterAllows_GoesOutsideThenReentersOnNewLane()
    {
        Set("leaveChance", 1f);
        Set("leaveInterval", Vector2.zero);
        Set("outsideDuration", new Vector2(0.5f, 0.5f));
        Start(new FakeArbiter { Leave = true, Lane = 2f });

        Assert.IsTrue(SimulateUntil(40f, () => fish.CurrentState == FishHomeSwimmer.State.Outside), "phải ra ngoài khung");
        Assert.GreaterOrEqual(Mathf.Abs(go.transform.localPosition.x), HalfWidth + HalfLength - 0.01f, "cả thân phải ở ngoài khung");

        Assert.IsTrue(SimulateUntil(1f, () => fish.CurrentState == FishHomeSwimmer.State.Swim), "sau 0.5 s ở ngoài phải vào lại");
        Assert.AreEqual(2f, fish.LaneZ, "làn mới lấy từ trọng tài");
        Assert.AreEqual(2f, go.transform.localPosition.z, 1e-4f, "vào lại đúng làn mới");
        Assert.Greater(Mathf.Abs(go.transform.localPosition.x), HalfWidth, "vào lại từ ngoài mép");
        Assert.AreEqual(-Mathf.Sign(go.transform.localPosition.x), fish.DirX, "hướng bơi vào trong");

        Assert.IsTrue(SimulateUntil(10f, () => Mathf.Abs(go.transform.localPosition.x) < LimitX), "phải bơi vào trong khung, không bị kẹp ở mép");
    }

    [Test]
    public void Restart_OutsideFrame_WaitsThenReenters()
    {
        Start(new FakeArbiter(), new Vector3(6f, 0f, 0f));
        Assert.AreEqual(FishHomeSwimmer.State.Outside, fish.CurrentState, "bật lại khi đang ngoài khung thì chờ vào");
        Assert.IsTrue(SimulateUntil(2.1f, () => fish.CurrentState == FishHomeSwimmer.State.Swim), "vào lại trong tối đa 2 s");
    }

    [Test]
    public void SetArea_WhileOutside_KeepsOutsideState()
    {
        Start(new FakeArbiter(), new Vector3(6f, 0f, 0f));
        Assert.AreEqual(FishHomeSwimmer.State.Outside, fish.CurrentState);
        fish.SetArea(Vector2.zero, new Vector2(HalfWidth, 7f), 1f, new FakeArbiter());
        Assert.AreEqual(FishHomeSwimmer.State.Outside, fish.CurrentState, "SetArea khi đang ở ngoài chỉ đổi vùng, giữ trạng thái");
        Assert.AreEqual(1f, fish.LaneZ);
    }

    [Test]
    public void Restart_PartiallyOutsideHeadingOut_SwimsOnOut()
    {
        // thân ló nửa ngoài mép phải, đầu hướng ra (+X): không đóng băng nửa thân trong khung, bơi nốt ra rồi mới thành Outside
        Start(new FakeArbiter(), new Vector3(3.5f, 0f, 0f));
        Assert.AreEqual(FishHomeSwimmer.State.Swim, fish.CurrentState, "còn ló trong khung thì chưa phải Outside");
        Assert.IsTrue(SimulateUntil(4f, () => fish.CurrentState == FishHomeSwimmer.State.Outside), "phải bơi nốt ra ngoài");
    }

    [Test]
    public void Restart_PartiallyOutsideHeadingIn_SwimsBackIn()
    {
        go.transform.localRotation = Quaternion.Euler(0f, 270f, 0f); // đầu hướng -X: bơi vào trong
        Start(new FakeArbiter(), new Vector3(3.5f, 0f, 0f));
        Assert.AreEqual(FishHomeSwimmer.State.Swim, fish.CurrentState);
        Assert.IsTrue(SimulateUntil(5f, () => Mathf.Abs(go.transform.localPosition.x) < LimitX), "phải bơi vào trong khung");
    }

    [Test]
    public void Startle_WhileEnteringFrame_DoesNotSnapInside()
    {
        Start(new FakeArbiter(), new Vector3(6f, 0f, 0f));
        Assert.IsTrue(SimulateUntil(2.5f, () => fish.CurrentState == FishHomeSwimmer.State.Swim), "vào lại");
        SimulateUntil(0.3f, () => false); // bơi vào một chút, tâm vẫn còn ngoài giới hạn
        float x0 = go.transform.localPosition.x;
        float dir = fish.DirX;
        Assert.Greater(Mathf.Abs(x0), LimitX, "đang vào, thân còn ló nửa ngoài");
        fish.Startle(new Vector2(x0 + dir * 2f, go.transform.localPosition.y), 3.5f, 1f); // chạm phía trước, trong khung
        fish.Simulate(Dt);
        Assert.AreEqual(dir, fish.DirX, "đang vào thì không quay ra");
        Assert.Less(Mathf.Abs(go.transform.localPosition.x - x0), 0.2f, "không được kẹp giật vào trong một frame");
    }

    [Test]
    public void Curious_TargetNearEdge_DoesNotOscillate()
    {
        Set("curiousChance", 1f);
        Set("curiousDelay", Vector2.zero);
        Set("curiousTimeout", 20f);
        Start(new FakeArbiter { Curious = true });
        fish.NoteTapPoint(new Vector2(LimitX, 3f)); // sát mép phải, cao hơn cá 3 đơn vị: tới mép mà chưa tới độ cao
        int flips = 0;
        float dir = fish.DirX;
        SimulateUntil(30f, () =>
        {
            if (fish.DirX != dir) { flips++; dir = fish.DirX; }
            return fish.CurrentState == FishHomeSwimmer.State.Swim;
        });
        Assert.LessOrEqual(flips, 1, "tới mép thì dừng lại ngắm, không quay qua quay lại");
    }

    [Test]
    public void Startle_OutsideRadius_Ignored()
    {
        Start();
        fish.Startle(new Vector2(10f, 10f), 3.5f, 1f);
        Assert.AreEqual(FishHomeSwimmer.State.Swim, fish.CurrentState);
    }

    [Test]
    public void Startle_OnFish_FleesThenCalms()
    {
        Start();
        fish.Startle(FishXY, 3.5f, 1f);
        Assert.AreEqual(FishHomeSwimmer.State.Flee, fish.CurrentState);
        Assert.IsTrue(SimulateUntil(1.5f, () => fish.CurrentState == FishHomeSwimmer.State.Swim), "hoảng tối đa 0.9 s rồi bình tĩnh");
    }

    [Test]
    public void Startle_PointAhead_TurnsAround()
    {
        Start();
        Assert.AreEqual(1f, fish.DirX);
        fish.Startle(new Vector2(go.transform.localPosition.x + 1f, go.transform.localPosition.y), 3.5f, 1f);
        Assert.AreEqual(-1f, fish.DirX, "điểm chạm phía trước thì quay đầu chạy ngược lại");
    }

    [Test]
    public void Startle_PointBehind_KeepsDirection()
    {
        Start();
        fish.Startle(new Vector2(go.transform.localPosition.x - 1f, go.transform.localPosition.y), 3.5f, 1f);
        Assert.AreEqual(1f, fish.DirX);
        Assert.AreEqual(FishHomeSwimmer.State.Flee, fish.CurrentState);
    }

    [Test]
    public void Startle_WeakerWhileFleeing_KeepsDirection()
    {
        Start();
        fish.Startle(new Vector2(go.transform.localPosition.x - 1f, go.transform.localPosition.y), 3.5f, 1f); // mạnh, từ sau lưng
        Assert.AreEqual(1f, fish.DirX);
        // gợn rê yếu phía trước: cường độ sqrt(1 - 1/2) * 0.5 ~ 0.35 < 1 -> không đổi hướng, chỉ làm mới timer
        fish.Startle(new Vector2(go.transform.localPosition.x + 1f, go.transform.localPosition.y), 2f, 0.5f);
        Assert.AreEqual(1f, fish.DirX, "cú yếu hơn không được đổi hướng khi đang hoảng");
        Assert.AreEqual(FishHomeSwimmer.State.Flee, fish.CurrentState);
    }

    [Test]
    public void Startle_WhileResting_WakesUp()
    {
        Set("restChance", 1f);
        Set("restInterval", Vector2.zero);
        Start();
        Assert.IsTrue(SimulateUntil(30f, () => fish.CurrentState == FishHomeSwimmer.State.Rest));
        fish.Startle(FishXY, 3.5f, 1f);
        Assert.AreEqual(FishHomeSwimmer.State.Flee, fish.CurrentState);
    }

    [Test]
    public void Startle_WhileOutside_Ignored()
    {
        Start(new FakeArbiter(), new Vector3(6f, 0f, 0f));
        Assert.AreEqual(FishHomeSwimmer.State.Outside, fish.CurrentState);
        fish.Startle(new Vector2(6f, 0f), 3.5f, 1f);
        Assert.AreEqual(FishHomeSwimmer.State.Outside, fish.CurrentState);
        fish.NoteTapPoint(new Vector2(6f, 0f));
        Assert.IsFalse(fish.IsCuriousOrPending);
    }

    [Test]
    public void NoteTapPoint_NoArbiter_Ignored()
    {
        Set("curiousChance", 1f);
        Start();
        fish.NoteTapPoint(Vector2.zero);
        Assert.IsFalse(fish.IsCuriousOrPending);
    }

    [Test]
    public void NoteTapPoint_ArbiterRefuses_Ignored()
    {
        Set("curiousChance", 1f);
        Start(new FakeArbiter { Curious = false });
        fish.NoteTapPoint(Vector2.zero);
        Assert.IsFalse(fish.IsCuriousOrPending);
    }

    [Test]
    public void NoteTapPoint_WithArbiter_BecomesCuriousLingersThenSwims()
    {
        Set("curiousChance", 1f);
        Set("curiousDelay", Vector2.zero);
        Start(new FakeArbiter { Curious = true });
        Vector2 p = FishXY;
        fish.NoteTapPoint(new Vector2(p.x + 0.5f, p.y));
        Assert.IsTrue(fish.IsCuriousOrPending);
        fish.Simulate(Dt);
        Assert.AreEqual(FishHomeSwimmer.State.Curious, fish.CurrentState, "hết chờ thì bơi lại");
        Assert.IsTrue(SimulateUntil(5f, () => fish.CurrentState == FishHomeSwimmer.State.Swim), "điểm ở ngay cạnh: ngắm tối đa 4 s rồi bơi tiếp");
        Assert.IsFalse(fish.IsCuriousOrPending);
    }

    [Test]
    public void NoteTapPoint_FarPoint_SwimsTowardIt()
    {
        Set("curiousChance", 1f);
        Set("curiousDelay", Vector2.zero);
        Set("curiousTimeout", 10f);
        Start(new FakeArbiter { Curious = true }, new Vector3(-2f, 0f, 0f));
        Vector2 target = new Vector2(2f, 0f);
        fish.NoteTapPoint(target);
        float before = Vector2.Distance(FishXY, target);
        Assert.IsTrue(SimulateUntil(9f, () => Vector2.Distance(FishXY, target) <= 1f + 0.05f), "phải bơi tới cách điểm chạm ~1 đơn vị");
        Assert.Less(Vector2.Distance(FishXY, target), before);
    }

    [Test]
    public void Startle_WhileCurious_FleeWins()
    {
        Set("curiousChance", 1f);
        Set("curiousDelay", Vector2.zero);
        Start(new FakeArbiter { Curious = true }, new Vector3(-2f, 0f, 0f));
        fish.NoteTapPoint(new Vector2(2f, 0f));
        fish.Simulate(Dt);
        Assert.AreEqual(FishHomeSwimmer.State.Curious, fish.CurrentState);
        fish.Startle(FishXY, 3.5f, 1f);
        Assert.AreEqual(FishHomeSwimmer.State.Flee, fish.CurrentState);
        Assert.IsTrue(SimulateUntil(1.5f, () => fish.CurrentState == FishHomeSwimmer.State.Swim));
        Assert.IsFalse(fish.IsCuriousOrPending, "hoảng xong không quay lại tò mò");
    }
}
