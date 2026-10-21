using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEngine;

/// <summary>
/// Một hàng của bảng sàn bán kính: bóng chứa đúng FishCount cá thì bán kính không được nhỏ hơn MinRadius.
/// Bảng để thưa cũng được - số cá không có hàng riêng thì lấy hàng gần nhất phía dưới (xem
/// GamePlayGlobalConfig.MinBubbleRadius).
/// </summary>
[System.Serializable]
public class BubbleRadiusFloor
{
    [Min(1), Tooltip("Số cá trong bóng")]
    public int FishCount = 3;

    [Min(0.1f), Tooltip("Bán kính nhỏ nhất bóng phải có khi chứa bấy nhiêu cá")]
    public float MinRadius = 1.8f;
}

/// <summary>Tham số gameplay lõi theo Docs/GDD/04_Config.tsv (GamePlayConfigModel) và vài tham số trình diễn.</summary>
[CreateAssetMenu(fileName = "GamePlayGlobalConfig", menuName = "GlobalConfigs/GamePlayGlobalConfig")]
[GlobalConfig("Assets/Resources/GlobalConfig/")]
public class GamePlayGlobalConfig : GlobalConfig<GamePlayGlobalConfig>
{
    public const int TankGoal = 3;

    [field: SerializeField, Range(1, 4), Tooltip("Số bể mở sẵn lúc bắt đầu level")]
    public int DefaultTankSlots { get; private set; } = 2;

    [field: SerializeField, Range(1, 4), Tooltip("Tổng số bể trên bàn (mở sẵn + khóa); booster UnlockTank mở dần tới mức này")]
    public int MaxTankSlots { get; private set; } = 4;

    [field: SerializeField, Range(1, 7), Tooltip("Số ô chờ hiện sẵn lúc bắt đầu level")]
    public int DefaultHoldingSlots { get; private set; } = 5;

    [field: SerializeField, Range(1, 7), Tooltip("Trần ô chờ khi dùng booster ExtraSlot (Play On dùng DefaultGlobalConfig.MaxHoldingSlots)")]
    public int BoosterHoldingSlotsMax { get; private set; } = 6;

    [field: SerializeField, Min(0.05f), Tooltip("Thời gian cá bay theo FishFlight.TestFly khi không có thời gian riêng (giây): hiện dùng cho cá từ ô chờ bay vào túi (PackedUp / Play On). Bay tới bể / ô chờ dùng nhóm Flight / Flight2")]
    public float FishMoveDuration { get; private set; } = 0.7f;

    [field: SerializeField, Min(0f), Tooltip("Trễ từ lúc thẻ đơn bay đi tới lúc thẻ kế rơi xuống (giây); 0 = ngay như WordSort")]
    public float TankRefillDelay { get; private set; } = 0f;

    [field: SerializeField, Min(0f), Tooltip("Bể rơi từ trên xuống ô cắm khi có đơn (giây)")]
    public float TankDropDuration { get; private set; } = 0.5f;

    [field: SerializeField, Min(0f), Tooltip("Bể đủ cá mang cá bay lên biến mất (giây)")]
    public float TankReleaseDuration { get; private set; } = 0.5f;

    [field: SerializeField, Min(0f), Tooltip("Độ cao bể xuất phát / bay tới so với ô cắm (m)")]
    public float TankDropHeight { get; private set; } = 10f;

    [field: SerializeField, Min(0f), Tooltip("Khoảng cách giữa hai lần tự thu cá từ ô chờ (giây); tự thu từng con như WordSort")]
    public float AutoCollectInterval { get; private set; } = 0.2f;

    [field: SerializeField, Min(0f), Tooltip("Ép dùng thử booster khi mở khóa (BoosterInfoData.ForceUsePrefillCount): khoảng cách giữa hai con cá tự bấm lên ô chờ (giây), như bấm tay")]
    public float HoldingPrefillInterval { get; private set; } = 0.15f;

    [field: SerializeField, Min(0f), Tooltip("Hàng ô chờ dồn lại khi thêm ô (giây)")]
    public float HoldingRelayoutDuration { get; private set; } = 0.5f;

    [field: SerializeField, Min(0f), Tooltip("Ô chờ mới sinh lệch sang phải bấy nhiêu rồi trượt vào hàng (m)")]
    public float HoldingSpawnOffset { get; private set; } = 5f;

    [field: SerializeField, Min(1), Tooltip("Số bóng hiện trên bàn lúc bắt đầu; mỗi bóng nổ thì sinh thêm một bóng từ hàng đợi")]
    public int VisibleBubbleCount { get; private set; } = 15;

    [field: SerializeField, Min(0f), Tooltip("Trễ từ lúc bóng hết cá tới lúc bắt đầu nổ (giây)")]
    public float BubblePopDelay { get; private set; } = 0.05f;

    [field: SerializeField, Min(1f), Tooltip("Bóng hết cá phình lên bao nhiêu lần bán kính rồi mới biến mất (1.1 = +10%)")]
    public float BubblePopScale { get; private set; } = 1.1f;

    [field: SerializeField, Min(0f), Tooltip("Thời gian phình lên trước khi biến mất (giây)")]
    public float BubblePopDuration { get; private set; } = 0.15f;

    [field: SerializeField, Min(0f), Tooltip("Bóng phình từ StartRadius lên Radius (giây)")]
    public float BubbleGrowDuration { get; private set; } = 0.5f;

    [field: SerializeField, Min(0.05f), Tooltip("Bán kính lúc vừa spawn")]
    public float BubbleStartRadius { get; private set; } = 0.5f;

    [field: SerializeField, Min(0.1f), Tooltip("Bán kính bóng ngẫu nhiên nhỏ nhất (không phụ thuộc số cá); seed có Radius > 0 thì dùng giá trị seed")]
    public float BubbleRadiusMin { get; private set; } = 0.9f;

    [field: SerializeField, Min(0.1f), Tooltip("Bán kính bóng ngẫu nhiên lớn nhất")]
    public float BubbleRadiusMax { get; private set; } = 1.5f;

    [field: SerializeField, Tooltip("Sàn bán kính theo số cá trong bóng. Dùng cho cả lúc random seed và tool vẽ level, " +
                                    "nên bóng dày cá không còn bốc được bán kính của bóng 1 cá. Thêm / bớt hàng thoải mái")]
    public List<BubbleRadiusFloor> BubbleRadiusFloors { get; private set; } = new List<BubbleRadiusFloor>
    {
        new BubbleRadiusFloor { FishCount = 1, MinRadius = 1.15f },
        new BubbleRadiusFloor { FishCount = 2, MinRadius = 1.7f },
        new BubbleRadiusFloor { FishCount = 3, MinRadius = 1.8f },
        new BubbleRadiusFloor { FishCount = 4, MinRadius = 2f },
        new BubbleRadiusFloor { FishCount = 5, MinRadius = 2.3f }
    };

    [field: SerializeField, Min(0f), Tooltip("CHỈ cột Sinh theo độ khó: bóng sinh ra phải to hơn sàn của số cá nó chứa " +
                                            "ít nhất bấy nhiêu. Bóng to che bớt tầm nhìn nên bàn cờ khó hơn")]
    public float BubbleGenMinPadding { get; private set; } = 0.2f;

    [field: SerializeField, Min(0f), Tooltip("CHỈ cột Sinh theo độ khó: trần bán kính nới thêm bấy nhiêu so với sàn của " +
                                            "bóng nhiều hơn một con, nên bóng ít cá thỉnh thoảng to bằng bóng nhiều cá")]
    public float BubbleGenMaxBonus { get; private set; } = 0.3f;

    [field: SerializeField] public bool AutoCollectEnabled { get; private set; } = true;
    [field: SerializeField] public bool StuckCheckAfterResolve { get; private set; } = true;
    [field: SerializeField, Min(0)] public int HoldingWarnRemaining { get; private set; } = 1;
    [field: SerializeField, Min(0f)] public float ComboWindow { get; private set; } = 2f;
    [field: SerializeField] public bool InputLockDuringFlight { get; private set; } = false;
    [field: SerializeField, Min(0f)] public float HintHighlightDuration { get; private set; } = 3f;
    [field: SerializeField, Min(0.1f), Tooltip("Shuffle: thời gian bay chuẩn từ bóng cũ sang bóng mới (giây); từng con nhân thêm 0.75..1.25 theo quãng đường xa gần rồi lệch ngẫu nhiên ±ShuffleFlyDurationVariance. Bàn bận trong lúc bay")]
    public float ShuffleFlyDuration { get; private set; } = 0.6f;

    [field: SerializeField, Range(0f, 0.9f), Tooltip("Shuffle: thời gian bay mỗi con lệch ngẫu nhiên ±bấy nhiêu phần (0.25 = ±25%) để cả đàn không về đích đều tăm tắp")]
    public float ShuffleFlyDurationVariance { get; private set; } = 0.25f;

    [field: SerializeField, Min(0), Tooltip("Shuffle: gom cá cho tối đa bấy nhiêu bể đang mở (thiếu ít ưu tiên). Bể có đủ số con còn thiếu trong các bóng bấm được thì gom đúng số đó vào một bóng: bóng trên cùng còn chứa được cả cụm; bể không đủ cá thì bỏ qua. Cá còn lại trộn ngẫu nhiên. 0 = trộn thuần ngẫu nhiên")]
    public int ShuffleGroupCount { get; private set; } = 1;

    [field: SerializeField, Min(0f), Tooltip("Shuffle: cá xuất phát lệch dần từng con theo thứ tự ngẫu nhiên, con sau cách con trước bấy nhiêu giây (con trước vẫn đang bay). 0 = bay cùng lúc")]
    public float ShuffleStaggerStep { get; private set; } = 0.05f;

    [field: SerializeField, Min(0f), Tooltip("Shuffle: tổng thời gian lệch xuất phát tối đa (giây); nhiều cá thì bước tự co lại để cả đợt không kéo dài")]
    public float ShuffleStaggerMax { get; private set; } = 0.8f;

    [field: SerializeField, Range(0.05f, 1f), Tooltip("AutoFill: scale ban đầu của cá sinh ra để bay vào bể (lấy từ hàng đợi spawner hoặc ứng từ đáy màn); lớn dần tới TankFishScale trong lúc bay")]
    public float AutoFillSpawnScale { get; private set; } = 0.35f;

    [field: SerializeField, Min(0f), Tooltip("AutoFill: overlay cần câu (ActivityBoosterFishingRod, animation 2 s) chạy trước bấy nhiêu giây rồi cá mới bắt đầu bay vào bể; trong lúc chờ khoá bấm cá và booster khác")]
    public float AutoFillOverlayLeadTime { get; private set; } = 1f;
    [field: SerializeField, Min(0f)] public float TapColliderPadding { get; private set; } = 0.05f;

    [field: SerializeField, Min(0f), Tooltip("Đè / rê tay chọn cá: không trúng thẳng collider cá thì tìm thêm trong bán kính này (m) quanh tia chạm, để ngón tay che cá nhỏ vẫn bắt được")]
    public float FishPickPadding { get; private set; } = 0.15f;

    [field: SerializeField, Min(0f), Tooltip("IceBubble: mép trên bóng cách đỉnh vùng chơi không quá bấy nhiêu (m) thì coi là chạm đỉnh (bóng mềm ép vào trần không bao giờ chạm tuyệt đối)")]
    public float IceBreakTopMargin { get; private set; } = 0.15f;

    [field: SerializeField, Min(0f), Tooltip("Bóng khói (isIce): chạm đỉnh liên tục bấy nhiêu giây thì lớp khói tan (tránh tan khi bóng nảy chạm trần một nhịp rồi bị đẩy xuống). Trong thời gian này múi khói co nhỏ dần (_SmokeThin) nên để đủ dài để thấy, khoảng 0.75 giây")]
    public float IceBreakHoldTime { get; private set; } = 0.1f;

    [field: SerializeField, Min(0.05f), Tooltip("Spawner: cá mới bay từ con spawner tới ô vừa trống, scale từ 0 lên 1, trong bấy nhiêu giây")]
    public float SpawnerSpawnDuration { get; private set; } = 0.35f;

    [field: SerializeField, Min(0.05f), Tooltip("Spawner: hết cá thì con spawner co về 0 trong bấy nhiêu giây rồi ẩn, đàn còn lại xếp lại chỗ")]
    public float SpawnerHideDuration { get; private set; } = 0.25f;

    [field: SerializeField, Min(0.05f), Tooltip("MultilayerBubble: lớp cá mới bay từ tâm bóng ra điểm của mình, scale từ 0 lên 1, trong bấy nhiêu giây")]
    public float LayerRevealDuration { get; private set; } = 0.45f;

    [field: SerializeField, Min(0f), Tooltip("MultilayerBubble: mỗi con cá của lớp mới bay chậm hơn con trước bấy nhiêu giây để cả lớp không ra đều một nhịp")]
    public float LayerRevealStagger { get; private set; } = 0.06f;

    [field: SerializeField, Min(0f), Tooltip("FishSchoolBubble: bấm một con là cả đàn bay, con sau cất cánh chậm hơn con trước bấy nhiêu giây (0 = cùng lúc)")]
    public float FishSchoolFlyStagger { get; private set; } = 0.06f;

    [field: SerializeField, Min(0.05f), Tooltip("Lock & Key: bóng chìa nổ thì token chìa bay sang ổ khoá trong bấy nhiêu giây, tới nơi ổ mới mở và cá trong ổ mới bấm được")]
    public float KeyFlyDuration { get; private set; } = 0.5f;

    [field: SerializeField, Tooltip("Lock & Key: material (màu) cho từng cặp ổ khoá / chìa theo thứ tự ổ khoá xuất hiện trong level data: cặp 0 lấy phần tử 0 (vàng), cặp 1 phần tử 1 (xanh lá), cặp 2 phần tử 2 (xanh dương); nhiều cặp hơn thì quay vòng. Trống = giữ material trên prefab Lock_FishSort / Key_FishSort")]
    public Material[] KeyLockMaterials { get; private set; } = System.Array.Empty<Material>();

    /// <summary>Material của cặp ổ khoá / chìa thứ pairIndex (BubbleEntry.KeyPairIndex), quay vòng theo KeyLockMaterials; null nếu chưa cấu hình hoặc pairIndex âm.</summary>
    public Material GetKeyLockMaterial(int pairIndex)
    {
        if (KeyLockMaterials == null || KeyLockMaterials.Length == 0 || pairIndex < 0) return null;
        return KeyLockMaterials[pairIndex % KeyLockMaterials.Length];
    }

    [field: SerializeField, Tooltip("FishSchoolBubble: màu nhuộm ô chờ khi đè vào đàn không đủ chỗ (cá viền đỏ theo denySettings của FishOutlineFeature trên Renderer2D)")]
    public Color SchoolDenyColor { get; private set; } = new(1f, 0.35f, 0.3f, 1f);

    [field: SerializeField, Min(0f), Tooltip("FishSchoolBubble: nhấc tay khỏi đàn không đủ chỗ thì viền đỏ và ô chờ đỏ còn giữ thêm bấy nhiêu giây rồi tắt")]
    public float SchoolDenyLinger { get; private set; } = 0.5f;

    [field: SerializeField, Min(0f), Tooltip("FishSchoolBubble: ô chờ chuyển sang / trả về màu trong bấy nhiêu giây")]
    public float SchoolDenyFade { get; private set; } = 0.15f;

    [field: SerializeField, Range(0.1f, 1f), Tooltip("Cá thu nhỏ còn bao nhiêu khi đậu trong bể")]
    public float TankFishScale { get; private set; } = 0.55f;

    [field: SerializeField, Range(0.1f, 1f), Tooltip("Cá thu nhỏ còn bao nhiêu khi đậu trong ô chờ")]
    public float HoldingFishScale { get; private set; } = 0.55f;

    [field: SerializeField] public string FishLayerName { get; private set; } = "Fish";

    // ---------- Bay đơn giản qua các điểm cho trước (FishFlight.TestFly): Shuffle đổi bóng, cá từ ô chờ vào túi. Trục curve 0..1 ----------
    // Tách khỏi nhóm Flight* bên dưới (trục 0..2, nghĩa khác). Giá trị mặc định = bộ curve cũ của TestFly trước khi tách.

    [field: Title("Simple Flight Config")]
    [field: SerializeField, Tooltip("Nhịp bay: trục 0..1 = thời gian, giá trị 0..1 = phần quãng đường (1 = đích). Mặc định OutQuad")]
    public AnimationCurve SimpleFlightMoveCurve { get; private set; } = new(
        new Keyframe(0f, 0f, 2f, 2f),
        new Keyframe(1f, 1f, 0f, 0f));

    [field: SerializeField, Tooltip("Trọng số lerp scale lúc xuất phát (0) → scale đích (1) theo thời gian 0..1")]
    public AnimationCurve SimpleFlightScaleCurve { get; private set; } = new(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(1f, 1f, 0f, 0f));

    [field: SerializeField, Tooltip("Kéo về phía camera (đơn vị world) theo thời gian 0..1; hai đầu = 0 để không lệch điểm hạ")]
    public AnimationCurve SimpleFlightDepthCurve { get; private set; } = new(
        new Keyframe(0f, 0f, 6.667f, 6.667f),
        new Keyframe(0.3f, 2f, 3.333f, 3.333f),
        new Keyframe(0.7f, 2f, -3.333f, -3.333f),
        new Keyframe(1f, 0f, -6.667f, -6.667f));

    // ---------- Cá bay tới bể / ô chờ (FishFlight.Fly): progress 0..2, 0..1 = pha nhấc, 1..2 = pha bay ----------

    [field: Title("Flight Config")]
    [field: SerializeField, Min(0.01f), Tooltip("Pha nhấc (chạm → hết quãng nhấc), giây; cố định, không theo quãng đường (gif ≈ 0.1)")]
    public float FlightLiftDuration { get; private set; } = 0.1f;

    [field: SerializeField, Range(0f, 0.3f), Tooltip("Quãng đường của pha nhấc trên đường bay, phần chiều cao màn hình (gif ≈ 0.05); điểm điều khiển đầu của đường = 2× giá trị này theo hướng nhấc nên cũng quyết định độ nhô")]
    public float FlightLiftHeight { get; private set; } = 0.05f;

    [field: SerializeField, Range(0f, 2f), Tooltip("Hướng nhấc: thẳng lên + chừng này phần ngang về phía ô (0 = bốc thẳng đứng, 1 = chéo 45°)")]
    public float FlightLiftTilt { get; private set; } = 0.4f;

    [field: SerializeField, Min(0.1f), Tooltip("Tốc độ pha bay, chiều cao màn hình / giây (gif ≈ 3.3). Thời gian bay = Base + quãng bay / Speed, kẹp [Min, Max]")]
    public float FlightSpeed { get; private set; } = 3.3f;

    [field: SerializeField, Min(0f), Tooltip("Phần thời gian bay cố định cộng thêm, giây (gif ≈ 0.2)")]
    public float FlightTravelBaseDuration { get; private set; } = 0.2f;

    [field: SerializeField, Min(0.01f), Tooltip("Thời gian bay ngắn nhất, giây")]
    public float FlightTravelMinDuration { get; private set; } = 0.22f;

    [field: SerializeField, Min(0.01f), Tooltip("Thời gian bay dài nhất, giây")]
    public float FlightTravelMaxDuration { get; private set; } = 0.5f;

    [field: SerializeField, Range(0f, 1f), Tooltip("Độ chúi theo tiếp tuyến khi đang bay: 0 = luôn giữ tư thế cầm, 1 = quay hẳn theo đường bay")]
    public float FlightLean { get; private set; } = 0.2f;

    [field: SerializeField, Tooltip("Nhịp bay. Trục 0..2 = progress; giá trị 0..1 = phần quãng nhấc (1 = hết nhấc), 1..2 = phần quãng bay (2 = ô)")]
    public AnimationCurve FlightMoveCurve { get; private set; }

    [field: SerializeField, Tooltip("Trọng số lerp scale bóng (0) → scale ô (1) theo progress 0..2")]
    public AnimationCurve FlightScaleCurve { get; private set; }

    [field: SerializeField, Tooltip("Hệ số nhân scale thêm (pop 1 → 1.5 → 1) theo progress 0..2")]
    public AnimationCurve FlightScaleOffsetCurve { get; private set; }

    [field: SerializeField, Tooltip("Trọng số xoay từ tư thế trong bóng (0) sang tư thế cầm (1) theo progress 0..2")]
    public AnimationCurve FlightRotateCurve { get; private set; }

    [field: SerializeField, Tooltip("Kéo về phía camera (đơn vị world) theo progress 0..2; hai đầu = 0 để không lệch điểm hạ")]
    public AnimationCurve FlightDepthCurve { get; private set; }

    // ---------- Cá bay vào bể (FishFlight.FlyIntoTank): progress 0..3, 0..1 = pha nhấc, 1..2 = pha lên tới JumpPoint, 2..3 = pha rơi ----------
    // Pha nhấc và thời gian pha lên dùng chung FlightLift* / FlightSpeed / FlightTravel*Duration ở trên.

    [field: Title("Tank Flight Config")]
    [field: SerializeField, Min(0.01f), Tooltip("Pha rơi (JumpPoint → DropPoint → điểm đậu, gồm cả nhún nước), giây; cố định vì quãng rơi do prefab bể quyết định")]
    public float TankFlightFallDuration { get; private set; } = 0.55f;

    [field: SerializeField, Range(0.05f, 0.6f), Tooltip("Độ tròn của đỉnh cung tại JumpPoint: cá lướt ngang qua đỉnh chừng này × quãng JumpPoint→DropPoint rồi mới vòng xuống (nhỏ = gập gắt, lớn = vòng rộng)")]
    public float TankFlightApexRoundness { get; private set; } = 0.35f;

    [field: SerializeField, Tooltip("Nhịp bay. Trục 0..3 = progress; giá trị 0..1 = phần quãng nhấc, 1..2 = phần đường lên (2 = JumpPoint), 2..3 = phần đường rơi (3 = điểm đậu). Vượt 3 = lún quá điểm đậu (phần dư × chiều dài đường rơi) để nhún nước; phải kết thúc ở 3")]
    public AnimationCurve TankFlightMoveCurve { get; private set; } = new(
        new Keyframe(0f, 0f, 0f, 1.75f),
        new Keyframe(1f, 1f, 0.78f, 0.45f),
        new Keyframe(1.45f, 1.55f, 1.6f, 1.6f),
        new Keyframe(2f, 2f, 0.25f, 0.25f),
        new Keyframe(2.6f, 3f, 2.8f, 1.8f),
        new Keyframe(2.75f, 3.1f, 0f, 0f),
        new Keyframe(3f, 3f, 0f, 0f));

    [field: SerializeField, Tooltip("Trọng số lerp scale lúc xuất phát (0) → scale trong bể (1) theo progress 0..3")]
    public AnimationCurve TankFlightScaleCurve { get; private set; } = new(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(1f, 0f, 0f, 0f),
        new Keyframe(3f, 1f, 0f, 0f));

    [field: SerializeField, Tooltip("Hệ số nhân scale thêm (pop lúc nhấc, còn to ở đỉnh vì gần camera, về 1 khi vào bể) theo progress 0..3")]
    public AnimationCurve TankFlightScaleOffsetCurve { get; private set; } = new(
        new Keyframe(0f, 1f, 0f, 1f),
        new Keyframe(1f, 1.5f, 0f, -0.1f),
        new Keyframe(2f, 1.35f, -0.25f, -0.25f),
        new Keyframe(2.7f, 1f, 0f, 0f),
        new Keyframe(3f, 1f, 0f, 0f));

    [field: SerializeField, Tooltip("Trọng số xoay từ tư thế lúc xuất phát (0) sang tư thế cầm (1) theo progress 0..3")]
    public AnimationCurve TankFlightRotateCurve { get; private set; } = new(
        new Keyframe(0f, 0f, 0f, 0.91f),
        new Keyframe(1.8f, 1f, 0f, 0f),
        new Keyframe(3f, 1f, 0f, 0f));

    [field: SerializeField, Tooltip("Độ chúi theo tiếp tuyến theo progress 0..3: 0 = giữ tư thế cầm (nằm ngang), 1 = quay hẳn theo đường bay. Mặc định: ngóc nhẹ lúc bay lên, về 0 tại đỉnh và giữ nằm ngang suốt pha rơi")]
    public AnimationCurve TankFlightLeanCurve { get; private set; } = new(
        new Keyframe(0f, 0f, 0f, 0.2f),
        new Keyframe(1f, 0.2f, 0.2f, 0f),
        new Keyframe(1.6f, 0.2f, 0f, 0f),
        new Keyframe(2f, 0f, 0f, 0f),
        new Keyframe(3f, 0f, 0f, 0f));

    [field: SerializeField, Tooltip("Kéo thêm về phía camera (đơn vị world) theo progress 0..3, cộng vào Z nội suy theo các mốc (chỗ cá → JumpPoint → DropPoint → điểm đậu); từ 2 trở đi nên = 0 để Z của DropPoint / điểm đậu đưa cá ra sau kính bể")]
    public AnimationCurve TankFlightDepthCurve { get; private set; } = new(
        new Keyframe(0f, 0f, 0f, 8f),
        new Keyframe(1f, 4f, 0f, 0f),
        new Keyframe(2f, 0f, 0f, 0f),
        new Keyframe(3f, 0f, 0f, 0f));

    // ---------- Bản bay thứ hai (FishFlight2.Fly): progress 0..3, 0..1 = lấy đà tại chỗ, 1..2 = bay, 2..3 = hạ (thu nhỏ tại ô) ----------
    // Số trong ngoặc là số đo trên gif tham chiếu Fish Sort Puzzle - Bubble Jam (30 fps).

    [field: Title("Flight2 Config")]
    [field: SerializeField, Tooltip("Bật: BoardManager dùng FishFlight2 (cảm giác theo Fish Sort Puzzle - Bubble Jam); tắt: FishFlight cũ")]
    public bool UseFishFlight12 { get; private set; } = true;

    [field: SerializeField, Min(0.01f), Tooltip("Pha lấy đà: cá phóng to và quay sang tư thế bay tại chỗ, giây (gif ≈ 0.07..0.1). Dùng chung cho cả bay vào bể")]
    public float Flight2AnticipationDuration { get; private set; } = 0.08f;

    [field: SerializeField, Min(0.1f), Tooltip("Tốc độ pha bay, chiều cao màn hình / giây. Thời gian bay = Base + quãng bay / Speed, kẹp [Min, Max] (gif: 0.23 màn hình trong 0.33 s)")]
    public float Flight2Speed { get; private set; } = 3f;

    [field: SerializeField, Min(0f), Tooltip("Phần thời gian bay cố định cộng thêm, giây")]
    public float Flight2TravelBaseDuration { get; private set; } = 0.25f;

    [field: SerializeField, Min(0.01f), Tooltip("Thời gian bay ngắn nhất, giây")]
    public float Flight2TravelMinDuration { get; private set; } = 0.25f;

    [field: SerializeField, Min(0.01f), Tooltip("Thời gian bay dài nhất, giây")]
    public float Flight2TravelMaxDuration { get; private set; } = 0.45f;

    [field: SerializeField, Min(0.01f), Tooltip("Pha hạ: cá đã tới ô, thu nhỏ tại chỗ về scale ô, giây (gif ≈ 0.13)")]
    public float Flight2LandDuration { get; private set; } = 0.13f;

    [field: SerializeField, Range(0f, 1f), Tooltip("Hướng vọt đi: 0 = bay thẳng theo đường nối cá → ô (gif gần như thẳng), 1 = bốc thẳng đứng rồi mới ngoặt về ô")]
    public float Flight2LaunchUp { get; private set; } = 0.15f;

    [field: SerializeField, Range(0f, 1f), Tooltip("Độ chúi theo đường bay: 0 = luôn nằm ngang (gif), 1 = mũi quay hẳn theo đường bay")]
    public float Flight2Lean { get; private set; }

    [field: SerializeField, Range(0f, 60f), Tooltip("Lúc bay cá ngoảnh mặt về camera chừng này độ (nhìn 3/4 thay vì nhìn ngang hẳn); tự về 0 trong pha cuối. Dùng chung cho cả bay vào bể")]
    public float Flight2CarryYaw { get; private set; } = 20f;

    [field: SerializeField, Tooltip("Nhịp bay. Trục 0..3 = progress; giá trị 0..1 = phần quãng đường (1 = ô). Mặc định: đứng yên lúc lấy đà, OutQuad lúc bay (vọt nhanh, tới ô thì chậm)")]
    public AnimationCurve Flight2MoveCurve { get; private set; } = new(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(1f, 0f, 0f, 2f),
        new Keyframe(2f, 1f, 0f, 0f),
        new Keyframe(3f, 1f, 0f, 0f));

    [field: SerializeField, Tooltip("Trọng số lerp scale lúc xuất phát (0) → scale ô (1) theo progress 0..3. Mặc định: chỉ thu nhỏ trong pha hạ, quá 1 một chút (nhỏ hơn đích) rồi bật về")]
    public AnimationCurve Flight2ScaleCurve { get; private set; } = new(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(2f, 0f, 0f, 0f),
        new Keyframe(2.65f, 1.1f, 0f, 0f),
        new Keyframe(3f, 1f, 0f, 0f));

    [field: SerializeField, Tooltip("Hệ số nhân scale thêm theo progress 0..3 (gif: phóng to ≈ 1.2 lúc lấy đà, giữ ≈ 1.3 suốt pha bay, về 1 trong pha hạ)")]
    public AnimationCurve Flight2ScaleOffsetCurve { get; private set; } = new(
        new Keyframe(0f, 1f, 0f, 0f),
        new Keyframe(1f, 1.2f, 0.25f, 0.25f),
        new Keyframe(1.4f, 1.3f, 0f, 0f),
        new Keyframe(2f, 1.3f, 0f, 0f),
        new Keyframe(2.65f, 1f, 0f, 0f),
        new Keyframe(3f, 1f, 0f, 0f));

    [field: SerializeField, Tooltip("Trọng số xoay từ tư thế trong bóng (0) sang tư thế bay (1) theo progress 0..3; mặc định quay xong trong pha lấy đà")]
    public AnimationCurve Flight2RotateCurve { get; private set; } = new(
        new Keyframe(0f, 0f, 0f, 1.5f),
        new Keyframe(1f, 1f, 0f, 0f),
        new Keyframe(3f, 1f, 0f, 0f));

    [field: SerializeField, Tooltip("Kéo về phía camera (đơn vị world) theo progress 0..3; hai đầu = 0 để không lệch điểm hạ")]
    public AnimationCurve Flight2DepthCurve { get; private set; } = new(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(0.8f, 4f, 0f, 0f),
        new Keyframe(2.2f, 4f, 0f, 0f),
        new Keyframe(3f, 0f, 0f, 0f));

    // ---------- Bản bay thứ hai vào bể (FishFlight2.FlyIntoTank): progress 0..3, 0..1 = lấy đà, 1..2 = lên tới JumpPoint, 2..3 = rơi + nhún nước ----------
    // Pha lấy đà và Flight2CarryYaw dùng chung với nhóm Flight2 ở trên.

    [field: Title("Flight2 Tank Config")]
    [field: SerializeField, Min(0.1f), Tooltip("Tốc độ pha lên, chiều cao màn hình / giây. Thời gian lên = Base + quãng lên / Speed, kẹp [Min, Max] (gif: 0.5 màn hình trong 0.33 s)")]
    public float Flight2TankRiseSpeed { get; private set; } = 3f;

    [field: SerializeField, Min(0f), Tooltip("Phần thời gian pha lên cố định cộng thêm, giây")]
    public float Flight2TankRiseBaseDuration { get; private set; } = 0.16f;

    [field: SerializeField, Min(0.01f), Tooltip("Thời gian pha lên ngắn nhất, giây")]
    public float Flight2TankRiseMinDuration { get; private set; } = 0.22f;

    [field: SerializeField, Min(0.01f), Tooltip("Thời gian pha lên dài nhất, giây")]
    public float Flight2TankRiseMaxDuration { get; private set; } = 0.45f;

    [field: SerializeField, Min(0.01f), Tooltip("Pha rơi (JumpPoint → DropPoint → điểm đậu, gồm cả nhún nước), giây (gif: rơi ≈ 0.1 + nhún ≈ 0.1)")]
    public float Flight2TankFallDuration { get; private set; } = 0.22f;

    [field: SerializeField, Range(0f, 1f), Tooltip("Hướng vọt đi: 0 = thẳng theo đường nối cá → JumpPoint, 1 = bốc thẳng đứng rồi mới ngoặt về bể (gif ≈ 0.5: lệch 17° so với phương đứng khi đường nối lệch 34°)")]
    public float Flight2TankLaunchUp { get; private set; } = 0.5f;

    [field: SerializeField, Range(0.05f, 0.6f), Tooltip("Độ căng đường lên: nhỏ = gần thẳng, tới sát đỉnh mới bẻ ngang (gif); lớn = lượn rộng, đi ngang sớm")]
    public float Flight2TankRiseTension { get; private set; } = 0.2f;

    [field: SerializeField, Range(0.05f, 0.6f), Tooltip("Độ tròn của đỉnh cung tại JumpPoint: cá lướt ngang qua đỉnh chừng này × quãng JumpPoint→DropPoint rồi mới vòng xuống (nhỏ = gập gắt, lớn = vòng rộng)")]
    public float Flight2TankApexRoundness { get; private set; } = 0.35f;

    [field: SerializeField, Tooltip("Nhịp bay. Trục 0..3 = progress; giá trị 0..1 = phần đường lên (1 = JumpPoint), 1..2 = phần đường rơi (2 = điểm đậu). Vượt 2 = lún quá điểm đậu (phần dư × chiều dài đường rơi) để nhún nước; phải kết thúc ở 2. Mặc định: lên gần đều (chậm nhẹ ở đỉnh), rơi nhanh dần")]
    public AnimationCurve Flight2TankMoveCurve { get; private set; } = new(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(1f, 0f, 0f, 1.25f),
        new Keyframe(2f, 1f, 0.7f, 1.4f),
        new Keyframe(2.5f, 2f, 2.4f, 0.8f),
        new Keyframe(2.7f, 2.07f, 0f, 0f),
        new Keyframe(3f, 2f, 0f, 0f));

    [field: SerializeField, Tooltip("Trọng số lerp scale lúc xuất phát (0) → scale trong bể (1) theo progress 0..3; mặc định chỉ thu nhỏ khi rơi vào bể")]
    public AnimationCurve Flight2TankScaleCurve { get; private set; } = new(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(1.9f, 0f, 0f, 0f),
        new Keyframe(2.5f, 1f, 0f, 0f),
        new Keyframe(3f, 1f, 0f, 0f));

    [field: SerializeField, Tooltip("Hệ số nhân scale thêm theo progress 0..3 (gif: phóng to rất nhanh tới ≈ 1.6, giữ tới đỉnh, về 1 khi vào bể)")]
    public AnimationCurve Flight2TankScaleOffsetCurve { get; private set; } = new(
        new Keyframe(0f, 1f, 0f, 0f),
        new Keyframe(1f, 1.3f, 0.6f, 0.6f),
        new Keyframe(1.25f, 1.6f, 0f, 0f),
        new Keyframe(2f, 1.6f, 0f, 0f),
        new Keyframe(2.5f, 1f, 0f, 0f),
        new Keyframe(3f, 1f, 0f, 0f));

    [field: SerializeField, Tooltip("Trọng số xoay từ tư thế lúc xuất phát (0) sang tư thế bay (1) theo progress 0..3; mặc định quay xong trong pha lấy đà")]
    public AnimationCurve Flight2TankRotateCurve { get; private set; } = new(
        new Keyframe(0f, 0f, 0f, 1.5f),
        new Keyframe(1f, 1f, 0f, 0f),
        new Keyframe(3f, 1f, 0f, 0f));

    [field: SerializeField, Tooltip("Độ chúi theo đường bay theo progress 0..3: 0 = nằm ngang, 1 = mũi quay hẳn theo đường bay. Mặc định theo gif: ngóc theo đường lên, cắm mũi xuống khi rơi rồi nằm ngang lại lúc nhún nước. Muốn cá nằm ngang khi rơi thì kéo đoạn từ 2 trở đi về 0")]
    public AnimationCurve Flight2TankLeanCurve { get; private set; } = new(
        new Keyframe(0f, 0.85f, 0f, 0f),
        new Keyframe(2.35f, 0.85f, 0f, 0f),
        new Keyframe(2.75f, 0f, 0f, 0f),
        new Keyframe(3f, 0f, 0f, 0f));

    [field: SerializeField, Tooltip("Kéo thêm về phía camera (đơn vị world) theo progress 0..3, cộng vào Z nội suy theo các mốc (chỗ cá → JumpPoint → DropPoint → điểm đậu); từ 2 trở đi nên = 0 để Z của DropPoint / điểm đậu đưa cá ra sau kính bể")]
    public AnimationCurve Flight2TankDepthCurve { get; private set; } = new(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(0.8f, 3f, 0f, 0f),
        new Keyframe(1.5f, 3f, 0f, 0f),
        new Keyframe(2f, 0f, 0f, 0f),
        new Keyframe(3f, 0f, 0f, 0f));

    public float RandomBubbleRadius()
    {
        float min = Mathf.Min(BubbleRadiusMin, BubbleRadiusMax);
        float max = Mathf.Max(BubbleRadiusMin, BubbleRadiusMax);
        return Random.Range(min, max);
    }
    /// <summary>
    /// Bán kính nhỏ nhất cho bóng chứa fishCount cá, tra trong BubbleRadiusFloors. Số cá không có hàng riêng thì lấy
    /// hàng gần nhất phía dưới; vượt hàng cuối bảng thì nới tiếp theo đúng bước nhảy của hai hàng cuối, để bóng 6-7 cá
    /// không phải dùng lại sàn của bóng 5 cá. Dưới hàng đầu tiên (hoặc bảng trống) thì về BubbleRadiusMin.
    /// </summary>
    public float MinBubbleRadius(int fishCount)
    {
        float floor = Mathf.Min(BubbleRadiusMin, BubbleRadiusMax);
        if (BubbleRadiusFloors == null || fishCount <= 0) return floor;

        // Tìm hàng cao nhất còn <= fishCount, giữ luôn hàng liền trước để suy ra bước nhảy.
        int topCount = 0, prevCount = 0;
        float topRadius = floor, prevRadius = floor;
        foreach (BubbleRadiusFloor row in BubbleRadiusFloors)
        {
            if (row == null || row.FishCount <= 0 || row.FishCount > fishCount) continue;

            if (row.FishCount > topCount)
            {
                prevCount = topCount;
                prevRadius = topRadius;
                topCount = row.FishCount;
                topRadius = row.MinRadius;
            }
            else if (row.FishCount > prevCount && row.FishCount != topCount)
            {
                prevCount = row.FishCount;
                prevRadius = row.MinRadius;
            }
        }

        if (topCount == 0) return floor;
        if (fishCount == topCount || prevCount == 0) return Mathf.Max(floor, topRadius);

        float step = Mathf.Max(0f, (topRadius - prevRadius) / (topCount - prevCount));
        return Mathf.Max(floor, topRadius + step * (fishCount - topCount));
    }

    /// <summary>
    /// Bán kính lúc sinh seed cho bóng chứa fishContain cá: bốc trong [sàn của fishContain cá, sàn của fishContain + 1 cá],
    /// làm tròn 0.1. Trần lấy theo sàn của bóng nhiều hơn một con chứ không lấy BubbleRadiusMax, để bóng ít cá không
    /// sinh ra to bằng bóng nhiều cá. Chỉ áp cho lúc sinh - bóng cố ý chỉnh to trong tool vẽ level để tạo độ khó thì
    /// không bị kẹp lại theo trần này.
    ///
    /// Cột "Sinh theo độ khó" KHÔNG dùng dải này: nó nới rộng thêm bằng BubbleGenMinPadding / BubbleGenMaxBonus,
    /// xem LevelDesignDataEditor.DifficultyGen.
    /// </summary>
    public float RandomBubbleRadius(int fishContain)
    {
        float floor = MinBubbleRadius(fishContain);
        float max = Mathf.Max(floor, MinBubbleRadius(fishContain + 1));
        float radius = Mathf.Round(Random.Range(floor, max) * 10f) / 10f;

        // Làm tròn xuống có thể tụt dưới sàn khi sàn không nằm trên lưới 0.1.
        return Mathf.Max(floor, radius);
    }
}
