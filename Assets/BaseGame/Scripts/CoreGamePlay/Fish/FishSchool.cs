using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Đàn cá đứng theo "Slots" trong một khối cầu, dùng chung cho cá trong bóng (BubbleFishContainer, bán kính = bóng) và cá trong bể
/// (TankView: tâm Center, bán kính SwimRadius, sức chứa Capacity = Goal). Lớp [Serializable] thuần gắn làm field của chủ sở hữu:
/// chủ gọi Initialize(root) rồi Tick(radius, dt) mỗi LateUpdate; cá là con của root nên đi theo chủ. Đàn tự đặt chỗ và di chuyển cá
/// (FishSwimmer của cá bị tắt).
///
/// Bố cục: theo số điểm N = max(Capacity, số cá + số chỗ đã giữ) dựng sẵn LayoutSets bộ điểm trong khối cầu 3D (mẫu đặt tay cho 1 đến
/// 4 điểm, vòng tròn cho nhiều hơn). Camera trực giao nên cá lùi sâu không nhỏ đi, chỉ bị con phía trước che: mỗi bộ được giãn trước
/// trên mặt phẳng màn hình (XY) bằng vài chục vòng đẩy nhau để thân cá chờm nhau ít nhất mà vẫn nằm trong hình chiếu quả cầu, rồi mới
/// xếp thứ tự trước sau theo phần thân còn lộ (VisibleFraction): điểm ít bị chờm nhất lùi ra sau, điểm bị chờm nhiều nhất ra trước, z
/// chỉ lùi vừa đủ để hai thân không cắt nhau trong 3D. Phía trước phải ở trong mặt cầu, phía sau được dùng tới BackDepthFactor lần bán
/// kính (bóng: 2, camera chỉ thấy mặt trước; bể: 1 = nằm gọn trong mặt cầu). Chật quá thì đứng xa nhau hết mức có thể. Bộ có điểm lộ
/// dưới MinVisibleFraction là "chật" (IsCrowded): đổi chỗ nhanh hơn theo CrowdedShuffleInterval để con bị che sớm được ra trước.
/// Cá bơi tại chỗ quanh điểm của mình, nhìn nghiêng (RestFacing: tự do theo phía vừa bơi tới, khóa luôn nhìn phải / trái như ô chờ,
/// hoặc Outward = đầu quay về phía mép gần hơn để phần ló ra khỏi đám là đầu); tư thế nghỉ chỉnh thêm được bằng RestYawToCamera (đầu
/// quay chéo về camera) và RestPitch (ngóc lên / chúi xuống), bộ điểm tính theo thân cá đã xoay. Khi cả đàn đã đứng yên đủ
/// ShuffleInterval (đếm từ lúc con cuối cùng về điểm, không tính lúc đang bơi) chọn một bộ khác bộ đang dùng, phát điểm còn trống theo
/// độ lộ (con đang bị che nhiều nhất nhận điểm lộ nhất của bộ mới, bằng nhau thì ngẫu nhiên), rồi mỗi con bơi theo cung cong 3D (quét
/// quanh tâm, phình ra mép, lặn ra sau) sang điểm mới, đầu quay theo hướng bơi. Đàn 1 con chỉ đổi chỗ khi ShuffleSingle (bể). Cá rời
/// đàn thì các con còn lại giữ nguyên điểm (bộ điểm chỉ dựng lại theo số cá mới ở lần đổi chỗ kế), nên không bị kéo giật.
/// Cá bay tới (bể): ReserveArrival giữ trước một điểm trống và trả về anchor (con của root, bám theo điểm kể cả khi đàn đổi bộ) để
/// cá bay thẳng tới điểm của mình; Arrive đưa cá vào đàn tại điểm đó, giữ vị trí và hướng hiện có rồi quay dần về tư thế nghỉ.
/// Shuffle: cá sắp đi được MarkLeaving trước khi giữ chỗ cho cá tới, nên không tính vào số điểm và nhường điểm của mình; bộ điểm vì
/// thế dựng đúng số cá cuối cùng ngay từ lúc giữ chỗ, cá tới hạ thẳng vào bố cục đúng, không phải chờ lần đổi chỗ kế.
/// </summary>
[Serializable]
public class FishSchool
{
    [SerializeField, Tooltip("Bật = scale cá theo bán kính bằng FishSizeRatio (bóng). Tắt = giữ nguyên scale hiện có của cá")]
    private bool ScaleToRadius = false;

    [SerializeField, Range(0.1f, 1f), Tooltip("Chỉ dùng khi ScaleToRadius bật: chiều dài cá so với bán kính")]
    private float FishSizeRatio = 0.45f;

    [SerializeField, Range(0f, 0.5f), Tooltip("Lề giữa cá và mặt cầu, tính theo tỉ lệ bán kính")]
    private float ZoneMargin = 0.05f;

    [Header("Bộ điểm 3D")]
    [SerializeField, Range(2, 6), Tooltip("Số bộ điểm dựng sẵn cho mỗi số điểm. Mỗi lần đổi chỗ chọn một bộ khác bộ đang dùng")]
    private int LayoutSets = 3;

    [SerializeField, Range(0f, 3f), Tooltip("Cá được đứng lùi ra sau tâm tới bao nhiêu lần bán kính. Bóng: 2 (phía sau quả cầu camera không thấy). <= 1 = nằm gọn trong mặt cầu (bể)")]
    private float BackDepthFactor = 2f;

    [SerializeField, Min(0f), Tooltip("Khoảng hở tối thiểu giữa hai thân cá trong 3D (m), đã gồm biên độ bơi tại chỗ")]
    private float SeparationGap = 0.12f;

    [SerializeField, Range(0f, 0.8f), Tooltip("Mức chờm trên màn hình (2D) còn chấp nhận để coi bố cục là 'vừa' (LayoutFits). Giãn điểm luôn cố gắng đưa về 0")]
    private float MaxOverlap = 0.35f;

    [SerializeField, Range(0f, 1f), Tooltip("Phần diện tích thân (chiếu lên màn hình) tối thiểu mỗi con phải lộ ra, không bị con phía trước che. Bộ điểm xếp thứ tự trước sau để mọi con đạt mức này; bộ nào không đạt nổi là 'chật' (IsCrowded), đổi chỗ theo CrowdedShuffleInterval")]
    private float MinVisibleFraction = 0.4f;

    [SerializeField, Range(4, 200), Tooltip("Số vòng lặp đẩy nhau khi giãn mỗi bộ điểm")]
    private int RelaxIterations = 64;

    [Header("Bơi tại chỗ và đổi chỗ")]
    [SerializeField, Tooltip("Cả đàn đứng yên bao lâu (giây, ngẫu nhiên trong khoảng) thì đổi bộ điểm. Đếm từ lúc con cuối cùng về điểm và dừng, thời gian đang bơi không tính")]
    private Vector2 ShuffleInterval = new(10f, 20f);

    [SerializeField, Tooltip("Như ShuffleInterval nhưng dùng khi bộ điểm đang chật (có con lộ dưới MinVisibleFraction), để con bị che sớm được ra trước")]
    private Vector2 CrowdedShuffleInterval = new(5f, 8f);

    [SerializeField, Tooltip("Đàn 1 con cũng đổi chỗ (bể). Tắt = 1 con đứng yên (bóng)")]
    private bool ShuffleSingle = false;

    [SerializeField, Tooltip("Hướng nhìn khi đứng yên: Free = nghiêng về phía vừa bơi tới (ngẫu nhiên nếu bơi thẳng); Right / Left = luôn nhìn về +X / -X (ô chờ); Outward = đầu quay về phía mép gần hơn theo x của điểm đang giữ, để trong bóng chật phần ló ra khỏi đám luôn là đầu")]
    private FacingMode RestFacing = FacingMode.Free;

    [SerializeField, Range(-90f, 90f), Tooltip("Khi đứng yên, đầu cá quay chéo về phía camera bấy nhiêu độ so với tư thế nhìn ngang (0 = ngang hẳn, 20 = hơi chéo về camera, âm = chéo ra phía sau). Đối xứng cho cả hai phía nhìn phải / trái")]
    private float RestYawToCamera = 0f;

    [SerializeField, Range(-90f, 90f), Tooltip("Khi đứng yên, đầu cá ngóc lên (dương) hoặc chúi xuống (âm) bấy nhiêu độ")]
    private float RestPitch = 0f;

    [SerializeField, Min(0.05f), Tooltip("Tốc độ bơi sang điểm mới theo cung cong (m/s)")]
    private float SwimSpeed = 1.2f;

    [SerializeField, Min(0f), Tooltip("Biên độ trôi quanh điểm khi đứng yên (m), theo cả 3 trục")]
    private float HoverAmplitude = 0.08f;

    [SerializeField, Min(0f), Tooltip("Tốc độ trôi quanh điểm (chu kỳ noise mỗi giây)")]
    private float HoverFrequency = 0.25f;

    [SerializeField, Min(0f), Tooltip("Nhấp nhô lên xuống (m)")]
    private float BobAmplitude = 0.03f;

    [SerializeField, Min(0f)] private float BobFrequency = 1.1f;

    [SerializeField, Min(1f), Tooltip("Tốc độ quay thân (độ/giây) khi đổi hướng bơi hoặc quay về tư thế nhìn nghiêng")]
    private float TurnSpeed = 200f;

    [SerializeField, Min(0.05f), Tooltip("Quãng đường tăng tốc lúc xuất phát và giảm tốc lúc về đích (m) để không giật ở hai đầu đường bơi")]
    private float EaseDistance = 0.5f;

    [SerializeField, Min(0f), Tooltip("Tốc độ (m/s) mà tại đó cá bắt đầu quay đầu theo hướng bơi")]
    private float FlipSpeedThreshold = 0.08f;

    [SerializeField, Range(0f, 3f), Tooltip("Speed animator khi cá đứng yên (vẫy đuôi nhẹ)")]
    private float AnimSpeedIdle = 0.6f;

    [SerializeField, Range(0f, 5f), Tooltip("Speed animator cộng thêm theo tốc độ bơi (mỗi m/s)")]
    private float AnimSpeedPerMeter = 1.5f;

    [Header("Đường bơi khi đổi chỗ")]
    [SerializeField, Range(0f, 1f), Tooltip("Mức phình cung ra sát mép theo mặt phẳng XY (0 = đi thẳng góc, 1 = ra tận mép)")]
    private float PathBulge = 0.75f;

    [SerializeField, Range(0f, 2f), Tooltip("Mức lặn ra phía sau ở giữa đường, tính theo bán kính. Cá tiến lùi theo trục z khi chuyển chỗ")]
    private float DiveDepth = 0.5f;

    [SerializeField, Range(0f, 360f), Tooltip("Góc quét tối thiểu quanh tâm (độ). Điểm mới quá gần theo chiều quay thì cá bơi thêm trọn một vòng")]
    private float MinSweepAngle = 60f;

    /// <summary>Bộ mặc định cho bể: nằm gọn trong mặt cầu, 1 con cũng đổi chỗ, lặn nông hơn.</summary>
    public static FishSchool ForTank() => new() { BackDepthFactor = 1f, ShuffleSingle = true, DiveDepth = 0.25f };

    /// <summary>Bộ mặc định cho ô chờ: nằm gọn trong mặt cầu, 1 con không đổi chỗ (vẫn trôi nhẹ tại chỗ), luôn nhìn phải.</summary>
    public static FishSchool ForHolding() => new() { BackDepthFactor = 1f, ShuffleSingle = false, RestFacing = FacingMode.Right };

    /// <summary>Hướng nhìn khi đứng yên.</summary>
    public enum FacingMode
    {
        /// <summary>Nghiêng về phía vừa bơi tới (bóng, bể).</summary>
        Free,
        /// <summary>Luôn nhìn về +X.</summary>
        Right,
        /// <summary>Luôn nhìn về -X.</summary>
        Left,
        /// <summary>Đầu quay về phía mép gần hơn theo x của điểm đang giữ (bóng chật: phần ló ra là đầu). Điểm ngay giữa thì theo phía vừa bơi tới.</summary>
        Outward
    }

    /// <summary>Cá vừa vào đàn giữ vị trí (Arrive / Attach / AddFlyIn) không đổi hướng nhìn theo vận tốc trong bấy nhiêu giây.</summary>
    private const float ArrivalFacingLock = 0.6f;

    /// <summary>Cá bay vào (FlyIn): góc ngóc / chúi tối đa theo hướng bay lúc xuất phát (độ), duỗi thẳng dần tới 0 khi tới nơi.</summary>
    private const float FlyInPitchMax = 35f;

    /// <summary>Số điểm tối thiểu của bộ điểm (bể = Goal, để cá tới sau có sẵn chỗ mà không phải dựng lại bộ điểm). 0 = theo số cá.</summary>
    public int Capacity { get; set; }

    /// <summary>Transform tâm đàn, cha của cá.</summary>
    public Transform Root => root;

    public int Count => states.Count;

    /// <summary>Bộ điểm đang dùng (0..LayoutSets-1), -1 khi chưa có.</summary>
    public int CurrentSet { get; private set; } = -1;

    /// <summary>Bộ đang dùng có đủ chỗ (không cắt nhau trong 3D, chờm 2D không quá MaxOverlap, mọi điểm lộ đủ MinVisibleFraction) hay quá chật.</summary>
    public bool LayoutFits { get; private set; } = true;

    /// <summary>Phần thân còn lộ (0..1) của điểm bị che nhiều nhất trong bộ đang dùng; 1 khi chưa có bộ.</summary>
    public float MinVisible { get; private set; } = 1f;

    /// <summary>Bộ đang dùng có điểm lộ dưới MinVisibleFraction: đổi chỗ theo CrowdedShuffleInterval thay vì ShuffleInterval.</summary>
    public bool IsCrowded => MinVisible < MinVisibleFraction;

    /// <summary>Phần thân còn lộ (0..1) của điểm bị che nhiều nhất trong bộ set (0..LayoutSets-1); 1 khi bộ chưa dựng. Dùng cho tool / kiểm tra.</summary>
    public float MinVisibleOf(int set)
    {
        if (set < 0 || set >= layoutVisible.Count) return 1f;
        float min = 1f;
        foreach (float v in layoutVisible[set]) min = Mathf.Min(min, v);
        return min;
    }

    /// <summary>Có con nào đang bơi sang điểm mới không.</summary>
    public bool IsShuffling
    {
        get
        {
            foreach (FishState s in states)
            {
                if (s.Fish == null || s.Leaving) continue;   // con sắp rời đàn đứng tại chỗ, không tính
                if (s.OnPath || s.FlyIn || Vector3.Distance(s.Pos, PointOf(s)) > HoverAmplitude + 0.15f) return true;
            }

            return false;
        }
    }

    private class FishState
    {
        public Fish Fish;
        public float BaseSize;      // cạnh lớn nhất của model (unscaled)
        public float HalfLength;    // nửa chiều dài (trục Z model), unscaled
        public float HalfHeight;    // nửa chiều cao (trục Y model)
        public float HalfWidth;     // nửa bề dày (trục X model)
        public float Seed;
        public float Facing = 1f;   // +1 nhìn về +X, -1 nhìn về -X khi đứng yên
        public Quaternion Rotation = Quaternion.identity;
        public int PointIndex;
        public Vector3 Pos;         // vị trí hiện tại trong không gian root (chưa cộng nhấp nhô)
        public Vector3 HoverVel;    // vận tốc cho SmoothDamp khi bơi tại chỗ
        public bool HasPos;

        // Đường bơi cong khi đổi chỗ.
        public bool OnPath;
        public Vector3 PathStart;
        public float PathDir;       // +1 ngược chiều kim đồng hồ quanh tâm (nhìn từ camera), -1 thuận chiều
        public int PathExtraLoops;
        public float PathProgress;  // quãng đường đã bơi (m)

        // Bay vào (cá spawner sinh, lớp mới của MultilayerBubble): đi thẳng từ FlyInStart tới điểm của mình trong FlyInDuration giây,
        // scale từ 0 lên BaseScale. Suốt lúc bay giữ tư thế nghỉ nhìn về phía mình bay tới (FlyInRestRotation), không quay đầu theo tiếp
        // tuyến 3D: điểm trong bóng lùi sâu theo Z (BackDepthFactor) nên tiếp tuyến 3D hay gần dọc trục Z, cá chĩa đuôi về camera suốt
        // lúc bay rồi tới nơi mới xoay lại.
        public bool FlyIn;
        public Vector3 FlyInStart;
        public float FlyInDuration;
        public float FlyInElapsed;
        public Vector3 BaseScale = Vector3.one;

        // Còn bấy nhiêu giây không đổi hướng nhìn theo vận tốc (vừa vào đàn giữ vị trí: cú bám điểm vài cm đầu có thể vượt
        // FlipSpeedThreshold làm cá quay đầu 180° ngay khi hạ cánh).
        public float FacingLock;

        // Sắp rời đàn (Shuffle: chờ tới lượt cất cánh): không tính vào số điểm, không bị kéo sang điểm mới, chỉ trôi nhẹ quanh LeavePos
        // tới khi Remove; điểm đang giữ được nhường cho cá sắp tới.
        public bool Leaving;
        public Vector3 LeavePos;
    }

    /// <summary>Chỗ giữ trước cho cá đang bay tới: điểm + anchor bám theo điểm + kích thước cá (đã scale) để dựng bộ điểm.</summary>
    private class Reservation
    {
        public Transform Anchor;
        public int PointIndex;
        public float HalfLength, HalfHeight, HalfWidth;
    }

    /// <summary>Tham số đường bơi cong, dựng lại mỗi frame từ điểm xuất phát và điểm đích hiện tại.</summary>
    private struct PathParams
    {
        public float R0, R1, A0, Sweep, Dir, Z0, Z1, Bulge, Dive;
        public Vector3 End;
    }

    private const int PathSamples = 64;
    private readonly float[] pathLengths = new float[PathSamples + 1];

    private readonly List<FishState> states = new();
    private readonly List<Vector3[]> layoutSets = new();
    private readonly List<float[]> layoutVisible = new();   // song song layoutSets: phần thân còn lộ của từng điểm (0..1) theo thứ tự trước sau của bộ
    private readonly List<Reservation> reservations = new();
    private readonly List<Transform> anchorPool = new();
    private Transform root;
    private float layoutRadius = -1f;
    private int layoutCount = -1;       // số điểm của bộ đang dùng; có thể lớn hơn số cá (Capacity, hoặc sau khi cá rời đàn tới lần đổi chỗ kế)
    private float layoutRestYaw, layoutRestPitch;   // tư thế nghỉ lúc dựng bộ điểm: chỉnh trong Inspector lúc đang chơi thì dựng lại
    private float layoutHx, layoutHy, layoutHz;     // bán trục hộp bao (tư thế nghỉ, cỡ lớn nhất trong đàn) đã dùng để dựng bộ điểm
    private float layoutRawHx, layoutRawHy, layoutRawHz;   // cỡ lớn nhất trước khi xoay theo tư thế nghỉ, để nhận ra cá đổi scale (ExtentsChanged)
    private float shuffleTimer;

    private bool RestPoseChanged => !Mathf.Approximately(layoutRestYaw, RestYawToCamera) || !Mathf.Approximately(layoutRestPitch, RestPitch);

    // ---------- Vòng đời ----------

    /// <summary>Gắn tâm đàn (cha của cá). Gọi một lần ở Awake của chủ sở hữu.</summary>
    public void Initialize(Transform root)
    {
        this.root = root;
        shuffleTimer = UnityEngine.Random.Range(ShuffleInterval.x, ShuffleInterval.y);
    }

    /// <summary>Mỗi LateUpdate của chủ: bán kính hiện tại (bóng đổi cỡ thì bộ điểm co giãn theo, giữ PointIndex nên không nhảy).</summary>
    public void Tick(float radius, float dt)
    {
        if (root == null) return;
        int wanted = WantedPoints();
        if (wanted > 0 && (layoutCount < 0 || layoutCount < wanted)) ComputeLayouts(radius, Mathf.Max(wanted, layoutCount));
        else if (layoutCount > 0 && (!Mathf.Approximately(layoutRadius, radius) || RestPoseChanged || ExtentsChanged())) ComputeLayouts(radius, layoutCount);
        UpdateAnchors();
        if (states.Count == 0) return;

        for (int i = 0; i < states.Count; i++) ApplyScale(states[i], radius);

        bool needRelayout = layoutCount != wanted;
        if (states.Count >= 2 || needRelayout || (ShuffleSingle && states.Count == 1))
        {
            // ShuffleInterval là khoảng đứng yên giữa hai lần đổi chỗ: timer chỉ đếm khi cả đàn đã về điểm và dừng (không đếm lúc
            // đang bơi sang điểm mới, bay vào hay trôi về điểm), nên lúc nào cũng có trọn khoảng "đứng lại bấm được". Reset ngay khi
            // bắt đầu đổi chỗ, đứng yên lại thì bắt đầu đếm; RequestRelayout đặt timer = 0 nên đổi ở tick đầu tiên cả đàn đứng yên.
            bool shuffling = IsShuffling;
            if (!shuffling) shuffleTimer -= dt;
            if (shuffleTimer <= 0f && !shuffling)
            {
                Shuffle(radius);
                Vector2 interval = IsCrowded ? CrowdedShuffleInterval : ShuffleInterval;   // bộ vừa chọn chật thì đứng ngắn hơn
                shuffleTimer = UnityEngine.Random.Range(interval.x, interval.y);
            }
        }

        for (int i = 0; i < states.Count; i++) TickSlot(states[i], dt);
    }

    /// <summary>Bỏ hết trạng thái đàn (không hủy GameObject cá; chủ sở hữu tự hủy).</summary>
    public void Clear()
    {
        states.Clear();
        layoutSets.Clear();
        layoutVisible.Clear();
        foreach (Reservation r in reservations) ReleaseAnchor(r.Anchor);
        reservations.Clear();
        CurrentSet = -1;
        layoutRadius = -1f;
        layoutCount = -1;
        MinVisible = 1f;
        LayoutFits = true;
    }

    // ---------- Vào / ra đàn ----------

    /// <summary>
    /// Thêm cá vào đàn ở vị trí index (bóng): điểm = chỉ số, bộ điểm dựng lại theo số cá. keepWorldPosition = giữ chỗ đang đứng rồi
    /// trôi về điểm (cá quay lại); ngược lại đặt thẳng vào điểm.
    /// </summary>
    public void Add(Fish fish, int index, bool keepWorldPosition, float radius)
    {
        if (fish == null || root == null || IndexOf(fish) >= 0) return;
        Register(fish, Mathf.Clamp(index, 0, states.Count), -1, keepWorldPosition, radius);
    }

    /// <summary>
    /// Cá bay vào đàn (spawner sinh, hoặc lớp mới của MultilayerBubble bung từ tâm bóng): vào đàn ở vị trí index, lấy đúng điểm
    /// pointIndex vừa trống (con bị bấm để lại; &lt; 0 = đánh lại điểm theo chỉ số), xuất phát từ worldStart với scale 0, bay thẳng tới
    /// điểm trong tư thế nghỉ và lớn dần trong duration giây rồi bơi tại chỗ như cả đàn. Giữ pointIndex thì số điểm không đổi nên bộ
    /// điểm giữ nguyên, các con khác không nhảy.
    /// </summary>
    public void AddFlyIn(Fish fish, int index, int pointIndex, Vector3 worldStart, float duration, float radius)
    {
        if (fish == null || root == null || IndexOf(fish) >= 0) return;
        Register(fish, Mathf.Clamp(index, 0, states.Count), pointIndex, true, radius);
        FishState s = states[IndexOf(fish)];
        s.Pos = root.InverseTransformPoint(worldStart);
        s.HoverVel = Vector3.zero;
        s.FlyIn = true;
        s.FlyInStart = s.Pos;
        s.FlyInDuration = Mathf.Max(0.01f, duration);
        s.FlyInElapsed = 0f;
        s.BaseScale = fish.transform.localScale;
        s.Rotation = FlyInRestRotation(s, PointOf(s) - s.Pos, 0f);   // cả lớp vào cùng frame thì bộ điểm còn đổi; tick đầu tính lại theo điểm đã chốt
        fish.transform.localPosition = s.Pos;
        fish.transform.localRotation = s.Rotation;
        fish.transform.localScale = Vector3.zero;
    }

    /// <summary>Cá còn đang bay vào từ spawner (chưa tới điểm; chủ sở hữu chưa bật collider bấm).</summary>
    public bool IsFlyingIn(Fish fish)
    {
        int i = IndexOf(fish);
        return i >= 0 && states[i].FlyIn;
    }

    /// <summary>Ép đàn đổi chỗ ở tick kế (khi cả đàn đứng yên): dùng khi số thành viên đổi mà muốn dựng lại bộ điểm ngay (spawner ẩn).</summary>
    public void RequestRelayout() => shuffleTimer = 0f;

    /// <summary>
    /// Cá sắp rời đàn nhưng chưa cất cánh (Shuffle chờ tới lượt): từ giờ không tính vào số điểm, không bị kéo sang điểm mới, đứng trôi
    /// nhẹ tại chỗ đang đứng; điểm của nó được nhường cho cá sắp tới (ReserveArrival), nên bộ điểm dựng đúng số cá cuối cùng ngay từ lúc
    /// giữ chỗ. Cất cánh thì Remove như thường.
    /// </summary>
    public void MarkLeaving(Fish fish)
    {
        int i = IndexOf(fish);
        if (i < 0 || states[i].Leaving) return;
        FishState s = states[i];
        s.Leaving = true;
        s.LeavePos = s.Pos;
        s.OnPath = false;   // đang bơi dở sang điểm mới thì dừng tại chỗ
        s.HasPos = true;
    }

    /// <summary>
    /// Giữ trước một điểm trống cho cá sắp bay tới (bể) và trả về anchor bám theo điểm đó để cá bay thẳng tới. arrivalScale = scale
    /// world của cá khi hạ (kích thước dùng để dựng bộ điểm). Null nếu chưa Initialize.
    /// </summary>
    public Transform ReserveArrival(Fish fish, float arrivalScale, float radius)
    {
        if (root == null) return null;
        // PointIndex = -1 (chưa có điểm) để chỗ giữ này không tự "chiếm" điểm 0 khi tìm điểm trống; trước đây mặc định 0 làm
        // FreePointIndex bỏ qua điểm 0 và đàn 1 chỗ (ô chờ) bị phình lên 2 điểm, cá đứng lệch tâm.
        var res = new Reservation { PointIndex = -1 };
        if (fish != null)
        {
            Vector3 size = fish.BodySize;
            float s = arrivalScale / Mathf.Max(1e-4f, root.lossyScale.x);
            res.HalfLength = size.z * 0.5f * s;
            res.HalfHeight = size.y * 0.5f * s;
            res.HalfWidth = size.x * 0.5f * s;
        }

        reservations.Add(res);
        // Có cá sắp rời đàn (Shuffle): dựng đúng số điểm cuối cùng (cá ở lại + chỗ giữ) thay vì giữ bộ to hơn, để cá tới hạ vào bố cục đúng.
        ComputeLayouts(radius, HasLeaving ? WantedPoints() : Mathf.Max(layoutCount, WantedPoints()));
        res.PointIndex = FreePointIndex();
        if (res.PointIndex < 0)
        {
            ComputeLayouts(radius, layoutCount + 1);
            res.PointIndex = Mathf.Max(0, FreePointIndex());
        }

        res.Anchor = GetAnchor();
        res.Anchor.localPosition = PointAt(res.PointIndex);
        return res.Anchor;
    }

    /// <summary>
    /// Cá đã bay tới anchor (bể): vào đàn tại điểm đã giữ, giữ vị trí và hướng hiện có rồi trôi/quay dần về tư thế nghỉ. anchor không
    /// phải chỗ đã giữ (hoặc null) thì lấy điểm trống bất kỳ.
    /// </summary>
    public void Arrive(Fish fish, Transform anchor, float radius)
    {
        if (fish == null || root == null || IndexOf(fish) >= 0) return;
        int pointIndex = -1;
        Transform usedAnchor = null;
        for (int i = 0; i < reservations.Count; i++)
        {
            if (reservations[i].Anchor != anchor) continue;
            pointIndex = reservations[i].PointIndex;
            usedAnchor = reservations[i].Anchor;
            reservations.RemoveAt(i);
            break;
        }

        if (pointIndex < 0)
        {
            ComputeLayouts(radius, Mathf.Max(layoutCount, WantedPoints() + 1));
            pointIndex = FreePointIndex();
            if (pointIndex < 0)
            {
                ComputeLayouts(radius, layoutCount + 1);
                pointIndex = Mathf.Max(0, FreePointIndex());
            }
        }

        // Cá có thể đang là con của anchor (FishFlight gắn vào đích): đổi cha về root trước rồi mới tắt anchor.
        Register(fish, states.Count, pointIndex, true, radius);
        ReleaseAnchor(usedAnchor);
    }

    /// <summary>
    /// Cá rời đàn (bị bấm / Swap): bỏ khỏi đàn, giữ nguyên GameObject cho bên gọi. Trả về index cũ, -1 nếu không thuộc đàn.
    /// Không dựng lại bộ điểm ở đây: các con còn lại giữ PointIndex (điểm vừa trống bỏ đó), cá đang bơi dở đường vẫn đi tiếp đúng
    /// đường; bộ điểm cho số cá mới chỉ dựng ở lần đổi chỗ kế.
    /// </summary>
    public int Remove(Fish fish) => Remove(fish, out _);

    /// <summary>Như trên, kèm điểm con cá vừa để trống (−1 nếu không thuộc đàn) để cá spawner sinh ra vào đúng chỗ đó (AddFlyIn).</summary>
    public int Remove(Fish fish, out int pointIndex)
    {
        int index = IndexOf(fish);
        pointIndex = index >= 0 ? states[index].PointIndex : -1;
        if (index < 0) return -1;
        states.RemoveAt(index);
        return index;
    }

    public int IndexOf(Fish fish)
    {
        for (int i = 0; i < states.Count; i++) if (states[i].Fish == fish) return i;
        return -1;
    }

    /// <summary>Đưa cá vào đàn. pointIndex &lt; 0 = điểm theo chỉ số (đánh lại cho cả đàn, bóng); ngược lại giữ điểm đã giữ (bể).</summary>
    private void Register(Fish fish, int index, int pointIndex, bool keepWorldPosition, float radius)
    {
        var state = new FishState
        {
            Fish = fish,
            Seed = UnityEngine.Random.Range(0f, 1000f),
            Facing = UnityEngine.Random.value < 0.5f ? -1f : 1f,   // chốt lại theo RestFacing khi đã có điểm (ComputeLayouts bên dưới)
            PointIndex = pointIndex
        };

        // Bộ điểm theo hộp bao thật của model (bounds mesh collider); scale theo bán kính vẫn chia cho Fish.BaseSize (Size nhập tay).
        Vector3 size = fish.BodySize;
        state.BaseSize = fish.BaseSize;
        state.HalfLength = size.z * 0.5f;
        state.HalfHeight = size.y * 0.5f;
        state.HalfWidth = size.x * 0.5f;

        if (fish.transform.parent != root) fish.transform.SetParent(root, true);
        states.Insert(index, state);
        if (pointIndex < 0)
        {
            for (int i = 0; i < states.Count; i++) states[i].PointIndex = i;
        }

        ApplyScale(state, radius);

        // Đàn tự di chuyển cá; swimmer của cá tắt hẳn.
        if (fish.Swimmer != null)
        {
            fish.Swimmer.StopSwimming();
            fish.Swimmer.enabled = false;
        }

        ComputeLayouts(radius, pointIndex < 0 ? WantedPoints() : Mathf.Max(layoutCount, WantedPoints()));
        state.Facing = ResolveFacing(state, state.Facing);
        if (keepWorldPosition)
        {
            state.Pos = fish.transform.localPosition;
            state.Rotation = fish.transform.localRotation;
            float fx = (state.Rotation * Vector3.forward).x;
            if (Mathf.Abs(fx) > 0.2f) state.Facing = ResolveFacing(state, fx >= 0f ? 1f : -1f);   // quay về phía gần hơn
            state.HoverVel = Vector3.zero;
            state.FacingLock = ArrivalFacingLock;   // bám điểm vài cm đầu không làm cá quay đầu
        }
        else
        {
            state.Pos = PointOf(state);
            fish.transform.localPosition = state.Pos;
            fish.transform.localRotation = state.Rotation;
        }

        state.HasPos = true;
    }

    /// <summary>Số cá đang thực sự ở đàn (không tính con sắp rời đàn, MarkLeaving).</summary>
    private int ActiveCount
    {
        get
        {
            int n = 0;
            foreach (FishState s in states) if (!s.Leaving) n++;
            return n;
        }
    }

    private bool HasLeaving
    {
        get
        {
            foreach (FishState s in states) if (s.Leaving) return true;
            return false;
        }
    }

    /// <summary>Số điểm cần có: Capacity, hoặc số cá (không tính con sắp rời đàn) + số chỗ đã giữ nếu nhiều hơn.</summary>
    private int WantedPoints() => Mathf.Max(Capacity, ActiveCount + reservations.Count);

    /// <summary>
    /// Điểm nhỏ nhất chưa có cá và chưa bị giữ, -1 nếu hết. Điểm của con sắp rời đàn (Leaving) coi như trống nhưng chỉ dùng khi không
    /// còn điểm nào khác, để cá tới ít khi hạ đúng lúc con cũ chưa kịp cất cánh.
    /// </summary>
    private int FreePointIndex()
    {
        int fallback = -1;
        for (int p = 0; p < layoutCount; p++)
        {
            bool used = false, leaving = false;
            foreach (FishState s in states)
            {
                if (s.PointIndex != p) continue;
                if (s.Leaving) leaving = true;
                else { used = true; break; }
            }

            if (!used) foreach (Reservation r in reservations) if (r.PointIndex == p) { used = true; break; }
            if (used) continue;
            if (!leaving) return p;
            if (fallback < 0) fallback = p;
        }

        return fallback;
    }

    private void UpdateAnchors()
    {
        foreach (Reservation r in reservations)
        {
            if (r.Anchor != null) r.Anchor.localPosition = PointAt(r.PointIndex);
        }
    }

    private Transform GetAnchor()
    {
        while (anchorPool.Count > 0)
        {
            Transform t = anchorPool[^1];
            anchorPool.RemoveAt(anchorPool.Count - 1);
            if (t == null) continue;
            t.gameObject.SetActive(true);
            return t;
        }

        GameObject go = new GameObject("Arrival");
        Transform anchor = go.transform;
        anchor.SetParent(root, false);
        anchor.localRotation = Quaternion.identity;
        anchor.localScale = Vector3.one;
        return anchor;
    }

    private void ReleaseAnchor(Transform t)
    {
        if (t == null) return;
        t.gameObject.SetActive(false);
        anchorPool.Add(t);
    }

    // ---------- Đổi chỗ ----------

    /// <summary>
    /// Chọn một bộ điểm khác bộ đang dùng, phát các điểm còn trống (không tính chỗ đã giữ) cho cá theo độ lộ rồi cả đàn bơi sang:
    /// con đang bị che nhiều nhất (theo vị trí thật lúc này) nhận điểm lộ nhất của bộ mới, con lộ nhất nhận điểm bị che nhất, nên
    /// không con nào đứng sau mãi; độ lộ bằng nhau thì ngẫu nhiên (bộ rộng, mọi điểm lộ hết, thành xáo ngẫu nhiên như trước).
    /// Nếu số điểm đã lệch với số cần (cá rời đàn) thì dựng bộ mới cho số hiện tại trước; khi đó 1 con cũng bơi về điểm mới.
    /// </summary>
    private void Shuffle(float radius)
    {
        int n = ActiveCount;   // con sắp rời đàn (Leaving) đứng tại chỗ, không đổi điểm
        if (n == 0) return;
        int wanted = WantedPoints();
        bool relayout = layoutCount != wanted;
        if (relayout) ComputeLayouts(radius, wanted);
        if (layoutSets.Count == 0) return;
        if (!relayout && (layoutSets.Count < 2 || (n < 2 && !ShuffleSingle))) return;

        // Độ lộ hiện tại của từng con, tính trước khi đổi bộ (điểm cũ không còn sau CurrentSet = next, nên dùng vị trí thật).
        float[] fishVisible = CurrentVisibility();

        int next;
        if (relayout || layoutSets.Count < 2)
        {
            next = UnityEngine.Random.Range(0, layoutSets.Count);   // bộ nào cũng mới
        }
        else
        {
            next = UnityEngine.Random.Range(0, layoutSets.Count - 1);
            if (next >= CurrentSet) next++;   // bỏ qua bộ đang dùng
        }

        CurrentSet = next;
        RefreshFit();

        // Điểm cho cá = mọi điểm trừ chỗ đã giữ cho cá đang bay tới.
        var avail = new List<int>(layoutCount);
        for (int p = 0; p < layoutCount; p++)
        {
            bool reserved = false;
            foreach (Reservation r in reservations) if (r.PointIndex == p) { reserved = true; break; }
            if (!reserved) avail.Add(p);
        }

        // Cá xếp từ bị che nhiều tới lộ nhiều, điểm xếp từ lộ nhiều tới bị che nhiều, rồi ghép cặp theo thứ tự. Khóa = độ lộ làm tròn
        // 5% cộng nhiễu nhỏ hơn bước làm tròn: bằng nhau thì thứ tự ngẫu nhiên.
        float[] pointVisible = layoutVisible[CurrentSet];
        var fishOrder = new List<int>(n);
        var fishKey = new float[states.Count];
        for (int i = 0; i < states.Count; i++)
        {
            if (states[i].Leaving) continue;
            fishOrder.Add(i);
            fishKey[i] = VisibilityKey(fishVisible[i]);
        }

        var pointKey = new float[layoutCount];
        foreach (int p in avail) pointKey[p] = -VisibilityKey(pointVisible[p]);
        fishOrder.Sort((a, b) => fishKey[a].CompareTo(fishKey[b]));
        avail.Sort((a, b) => pointKey[a].CompareTo(pointKey[b]));

        float dir = UnityEngine.Random.value < 0.5f ? -1f : 1f;   // cả đàn quét cùng chiều quanh tâm
        for (int k = 0; k < fishOrder.Count; k++)
        {
            FishState s = states[fishOrder[k]];
            if (k < avail.Count) s.PointIndex = avail[k];
            s.OnPath = true;
            s.PathStart = s.Pos;
            s.PathDir = dir;
            s.PathProgress = 0f;
            s.PathExtraLoops = SweepAngle(s, 0) * Mathf.Rad2Deg < MinSweepAngle ? 1 : 0;
        }
    }

    /// <summary>Khóa sắp xếp theo độ lộ: làm tròn 5% rồi cộng nhiễu ngẫu nhiên nhỏ hơn một bước, để bằng nhau thì ngẫu nhiên.</summary>
    private static float VisibilityKey(float visible) => Mathf.Round(visible * 20f) * 0.05f + UnityEngine.Random.value * 0.04f;

    /// <summary>
    /// Phần thân còn lộ của từng con theo vị trí thật lúc này (ellipse hộp bao tư thế nghỉ của chính nó, bị che bởi các con có z nhỏ
    /// hơn = gần camera hơn). Song song với states.
    /// </summary>
    private float[] CurrentVisibility()
    {
        int n = states.Count;
        var result = new float[n];
        var shapes = new Ellipse[n];
        for (int i = 0; i < n; i++) shapes[i] = FishEllipse(states[i]);

        var front = new List<Ellipse>(n);
        for (int i = 0; i < n; i++)
        {
            front.Clear();
            for (int j = 0; j < n; j++)
            {
                if (j != i && states[j].Pos.z < states[i].Pos.z) front.Add(shapes[j]);
            }

            result[i] = VisibleFraction(shapes[i], front);
        }

        return result;
    }

    /// <summary>Ellipse hình chiếu hộp bao thân cá ở tư thế nghỉ, tâm tại vị trí thật của nó (kích thước đã scale, đã xoay theo RestExtents).</summary>
    private Ellipse FishEllipse(FishState s)
    {
        float hx = ScaledHalfLength(s);
        float hy = ScaledHalfHeight(s);
        float hz = ScaledHalfWidth(s);
        RestExtents(ref hx, ref hy, ref hz);
        return new Ellipse(s.Pos.x, s.Pos.y, hx, hy);
    }

    /// <summary>Con cá nằm dưới điểm world (chiếu theo XY), ưu tiên con gần camera hơn; hoặc null. Dùng khi không muốn raycast collider.</summary>
    public Fish FindNear(Vector3 worldPoint, float extraRadius = 0f)
    {
        Fish best = null;
        float bestZ = float.MaxValue;
        foreach (FishState s in states)
        {
            if (s.Fish == null) continue;
            Vector3 local = worldPoint - s.Fish.transform.position;
            float hx = ScaledHalfLength(s) + extraRadius;
            float hy = ScaledHalfHeight(s) + extraRadius;
            if (Mathf.Abs(local.x) > hx || Mathf.Abs(local.y) > hy) continue;
            float z = s.Fish.transform.position.z;
            if (z >= bestZ) continue;
            bestZ = z;
            best = s.Fish;
        }

        return best;
    }

    // ---------- Bộ điểm ----------

    private Vector3 PointOf(FishState s) => PointAt(s.PointIndex);

    private Vector3 PointAt(int pointIndex)
    {
        if (CurrentSet < 0 || CurrentSet >= layoutSets.Count) return Vector3.zero;
        Vector3[] set = layoutSets[CurrentSet];
        if (set.Length == 0) return Vector3.zero;
        return set[Mathf.Clamp(pointIndex, 0, set.Length - 1)];
    }

    private float InnerRadius(float radius) => Mathf.Max(0.05f, radius * (1f - ZoneMargin));

    private bool SphereBack => BackDepthFactor <= 1f;

    /// <summary>
    /// Dựng lại toàn bộ bộ điểm với n điểm cho bán kính hiện tại (n không nhỏ hơn số cá + chỗ đã giữ). Giữ bộ đang dùng và PointIndex
    /// đã gán nên chỉ đổi cỡ thì điểm của từng con chỉ co giãn theo, không nhảy. Kích thước cá dùng cỡ lớn nhất trong đàn và trong các
    /// chỗ đã giữ nên gán cá nào vào điểm nào cũng được; cùng đầu vào cho cùng kết quả (không ngẫu nhiên).
    /// </summary>
    private void ComputeLayouts(float radius, int n)
    {
        n = Mathf.Max(n, ActiveCount + reservations.Count);   // con sắp rời đàn không tính
        layoutRadius = radius;
        layoutCount = n;
        layoutRestYaw = RestYawToCamera;
        layoutRestPitch = RestPitch;
        layoutSets.Clear();
        layoutVisible.Clear();
        if (n == 0)
        {
            MinVisible = 1f;
            LayoutFits = true;
            return;
        }

        float hx = MaxHalfLength();
        float hy = MaxHalfHeight();
        float hz = MaxHalfWidth();
        layoutRawHx = hx;
        layoutRawHy = hy;
        layoutRawHz = hz;
        Vector3[] bodyRight = RestSamples(hx, hy, hz, 1f);
        Vector3[] bodyLeft = RestSamples(hx, hy, hz, -1f);
        RestExtents(ref hx, ref hy, ref hz);
        layoutHx = hx;
        layoutHy = hy;
        layoutHz = hz;
        for (int k = 0; k < LayoutSets; k++)
        {
            Vector3[] pts = BuildSet(n, k, radius, hx, hy, hz, bodyRight, bodyLeft, out float[] visible);
            layoutSets.Add(pts);
            layoutVisible.Add(visible);
        }

        if (CurrentSet < 0 || CurrentSet >= layoutSets.Count) CurrentSet = 0;
        for (int i = 0; i < states.Count; i++)
        {
            FishState s = states[i];
            if (s.Leaving || (s.PointIndex >= 0 && s.PointIndex < n)) continue;   // con sắp rời đàn đứng tại chỗ, không cần điểm
            s.PointIndex = -1;
            s.PointIndex = Mathf.Max(0, FreePointIndex());   // bộ co lại (Shuffle): lấy điểm trống thay vì chỉ số để không trùng điểm
        }

        // Chỗ giữ chưa có điểm (-1, đang trong ReserveArrival) giữ nguyên để ReserveArrival tự chọn; chỉ sửa chỗ vượt quá số điểm.
        foreach (Reservation r in reservations)
        {
            if (r.PointIndex >= n) r.PointIndex = Mathf.Max(0, FreePointIndex());
        }

        RefreshFit();
    }

    /// <summary>Cập nhật MinVisible và LayoutFits theo bộ đang dùng (sau ComputeLayouts hoặc khi Shuffle đổi bộ).</summary>
    private void RefreshFit()
    {
        if (CurrentSet < 0 || CurrentSet >= layoutSets.Count)
        {
            MinVisible = 1f;
            LayoutFits = true;
            return;
        }

        float[] visible = layoutVisible[CurrentSet];
        float min = 1f;
        foreach (float v in visible) min = Mathf.Min(min, v);
        MinVisible = min;
        LayoutFits = SetFits(layoutSets[CurrentSet], visible, layoutHx, layoutHy, layoutHz);
    }

    /// <summary>Số góc xoay của mẫu được thử cho mỗi bộ điểm (rải đều trong 180 độ), giữ góc cho bố cục lộ tốt nhất.</summary>
    private const int LayoutCandidates = 6;

    /// <summary>
    /// Một bộ điểm: mẫu chuẩn hóa được scale theo bán kính và kích thước cá, giãn trên màn hình (XY) cho chờm nhau ít nhất, xếp thứ
    /// tự trước sau theo độ lộ rồi gán z vừa đủ để không cắt nhau. visible[i] = phần thân còn lộ của điểm i (0..1) trong thứ tự đó.
    /// Giãn XY chỉ đẩy dọc hướng lệch sẵn có nên mẫu nằm ngang trong bóng hẹp ngang không tự tìm ra chỗ trống theo chiều dọc: thử
    /// LayoutCandidates góc xoay của mẫu (mỗi bộ lệch pha một chút để ba bộ không hội tụ về cùng một hình) và giữ góc cho mức lộ thấp
    /// nhất cao nhất, bằng nhau thì mức lộ trung bình cao nhất. Một điểm thì đứng ngay theo mẫu (kể cả z), không có gì để che.
    /// </summary>
    private Vector3[] BuildSet(int n, int setIndex, float radius, float hx, float hy, float hz, Vector3[] bodyRight, Vector3[] bodyLeft, out float[] visible)
    {
        float inner = InnerRadius(radius);
        float backDepth = radius * BackDepthFactor;
        float ax = Mathf.Max(0f, inner - hx);
        float ay = Mathf.Max(0f, inner - hy);
        float azFront = Mathf.Max(0f, inner - hz);
        float azBack = Mathf.Max(0f, backDepth - hz);

        Vector3[] template = Template(n, setIndex);
        if (n == 1)
        {
            Vector3 t = template[0];
            float z = t.z < 0f ? t.z * azFront : t.z * azBack;
            visible = new[] { 1f };
            return new[] { Contain(new Vector3(t.x * ax, t.y * ay, z), inner, backDepth, hx, hy, bodyRight, bodyLeft) };
        }

        Vector3[] best = null;
        int[] bestRank = null;
        float[] bestVisible = null;
        float bestMin = -1f, bestMean = -1f;
        float step = 180f / LayoutCandidates;
        for (int c = 0; c < LayoutCandidates; c++)
        {
            float angle = c * step + setIndex * (step / Mathf.Max(1, LayoutSets));
            Quaternion q = Quaternion.Euler(0f, 0f, angle);
            var pts = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 t = template[i];
                Vector3 xy = q * new Vector3(t.x, t.y, 0f);
                float z = t.z < 0f ? t.z * azFront : t.z * azBack;
                pts[i] = new Vector3(xy.x * ax, xy.y * ay, z);
            }

            RelaxXY(pts, inner, hx, hy);
            var rank = new int[n];
            var vis = new float[n];
            RankByVisibility(pts, hx, hy, rank, vis);
            float min = 1f, sum = 0f;
            foreach (float v in vis)
            {
                min = Mathf.Min(min, v);
                sum += v;
            }

            float mean = sum / n;
            bool better = best == null || min > bestMin + 0.02f || (min > bestMin - 0.02f && mean > bestMean + 1e-3f);
            if (!better) continue;
            best = pts;
            bestRank = rank;
            bestVisible = vis;
            bestMin = min;
            bestMean = mean;
        }

        AssignDepth(best, bestRank, inner, backDepth, hx, hy, hz, bodyRight, bodyLeft);
        visible = bestVisible;
        return best;
    }

    /// <summary>
    /// Mẫu chuẩn hóa: x, y trong [-1, 1] theo bán kính XY dùng được; z âm = về phía camera (trong mặt cầu), z dương = ra sau
    /// (tới BackDepthFactor). Ba bộ đặt tay cho 1 đến 4 điểm, bộ thứ tư trở đi xoay bộ đầu; nhiều hơn 4 dùng vòng tròn.
    /// </summary>
    private static Vector3[] Template(int n, int setIndex)
    {
        Vector3[][] sets;
        switch (n)
        {
            case 1:
                sets = new[]
                {
                    new[] { new Vector3(0f, 0f, 0f) },
                    new[] { new Vector3(0f, 0f, -0.3f) },
                    new[] { new Vector3(0f, 0f, 0.4f) }
                };
                break;
            case 2:
                sets = new[]
                {
                    new[] { new Vector3(-0.6f, 0.1f, -0.4f), new Vector3(0.6f, -0.1f, 0.5f) },
                    new[] { new Vector3(0.55f, 0.4f, 0.4f), new Vector3(-0.55f, -0.4f, -0.3f) },
                    new[] { new Vector3(0f, 0.6f, -0.2f), new Vector3(0f, -0.6f, 0.6f) }
                };
                break;
            case 3:
                sets = new[]
                {
                    new[] { new Vector3(0f, 0.6f, -0.3f), new Vector3(-0.6f, -0.35f, 0.3f), new Vector3(0.6f, -0.35f, 0.7f) },
                    new[] { new Vector3(0f, -0.6f, -0.2f), new Vector3(-0.6f, 0.35f, 0.6f), new Vector3(0.6f, 0.35f, 0.2f) },
                    new[] { new Vector3(-0.65f, 0.05f, -0.1f), new Vector3(0.35f, 0.55f, 0.6f), new Vector3(0.4f, -0.5f, 0.3f) }
                };
                break;
            case 4:
                sets = new[]
                {
                    new[] { new Vector3(-0.55f, 0.5f, -0.2f), new Vector3(0.55f, 0.5f, 0.5f), new Vector3(-0.55f, -0.5f, 0.6f), new Vector3(0.55f, -0.5f, -0.1f) },
                    new[] { new Vector3(0f, 0.65f, 0.3f), new Vector3(0.65f, 0f, -0.2f), new Vector3(0f, -0.65f, 0.6f), new Vector3(-0.65f, 0f, 0f) },
                    new[] { new Vector3(-0.5f, 0.3f, 0.6f), new Vector3(0.6f, 0.25f, -0.2f), new Vector3(-0.3f, -0.6f, -0.1f), new Vector3(0.35f, -0.55f, 0.7f) }
                };
                break;
            default:
            {
                // Vòng tròn (n - 1) điểm quanh một điểm ở giữa lùi ra sau, mỗi bộ xoay một góc và đảo chiều sâu xen kẽ.
                var pts = new Vector3[n];
                float baseDeg = 90f + setIndex * (360f / Mathf.Max(1, n - 1)) / 3f;
                for (int i = 0; i < n - 1; i++)
                {
                    float a = (baseDeg + 360f * i / (n - 1)) * Mathf.Deg2Rad;
                    float z = (i + setIndex) % 2 == 0 ? -0.2f : 0.6f;
                    pts[i] = new Vector3(Mathf.Cos(a) * 0.7f, Mathf.Sin(a) * 0.7f, z);
                }

                pts[n - 1] = new Vector3(0f, 0f, 0.9f);
                return pts;
            }
        }

        if (setIndex < sets.Length) return (Vector3[])sets[setIndex].Clone();

        // Bộ thêm: xoay bộ đầu quanh trục z một góc theo chỉ số.
        Vector3[] extra = (Vector3[])sets[setIndex % sets.Length].Clone();
        float rot = 40f * (setIndex / sets.Length);
        Quaternion q = Quaternion.Euler(0f, 0f, rot);
        for (int i = 0; i < extra.Length; i++)
        {
            Vector3 p = extra[i];
            Vector3 xy = q * new Vector3(p.x, p.y, 0f);
            extra[i] = new Vector3(xy.x, xy.y, p.z);
        }

        return extra;
    }

    /// <summary>
    /// Giãn bộ điểm trên mặt phẳng màn hình. Thân cá chiếu lên màn hình được coi là ellipse bán trục (hx, hy); hai ellipse không
    /// chờm nhau khi khoảng cách chuẩn hóa n2 = |(dx/(2hx+gap), dy/(2hy+gap))| >= 1. Cặp có n2 &lt; 1 bị đẩy tách theo hướng chuẩn hóa
    /// (nửa phần chờm, nửa sau giảm còn một nửa để lắng), sau mỗi vòng ép điểm vào hình chiếu quả cầu. Gom lực của mọi cặp rồi áp
    /// dụng trung bình (kiểu Jacobi) để con ở giữa hai con khác dừng đúng điểm cân bằng. Không đụng z: camera trực giao nên lùi
    /// sâu không giúp gì cho mắt nhìn, chiều sâu chỉ dùng ở AssignDepth để hai thân không cắt nhau.
    /// </summary>
    private void RelaxXY(Vector3[] pts, float inner, float hx, float hy)
    {
        int n = pts.Length;
        float sx = 2f * hx + SeparationGap;
        float sy = 2f * hy + SeparationGap;
        var acc = new Vector2[n];
        var cnt = new int[n];

        for (int it = 0; it < RelaxIterations; it++)
        {
            float damp = it < RelaxIterations / 2 ? 1f : 0.5f;
            for (int i = 0; i < n; i++)
            {
                acc[i] = Vector2.zero;
                cnt[i] = 0;
            }

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    Vector2 u = new((pts[j].x - pts[i].x) / sx, (pts[j].y - pts[i].y) / sy);
                    float n2 = u.magnitude;
                    if (n2 >= 1f) continue;
                    Vector2 dir = n2 > 1e-4f ? u / n2 : ((i + j) % 2 == 0 ? Vector2.right : Vector2.up);   // trùng điểm: tách theo trục thay phiên
                    float push = (1f - n2) * 0.5f * damp;
                    Vector2 w = new(dir.x * sx * push, dir.y * sy * push);
                    acc[i] -= w;
                    acc[j] += w;
                    cnt[i]++;
                    cnt[j]++;
                }
            }

            for (int i = 0; i < n; i++)
            {
                if (cnt[i] > 0)
                {
                    pts[i].x += acc[i].x / cnt[i];
                    pts[i].y += acc[i].y / cnt[i];
                }

                ContainXY(ref pts[i], inner, hx, hy);
            }
        }
    }

    /// <summary>Điểm trước nhất cách mặt trước của quả cầu bấy nhiêu lần bán kính trong, để bóng mềm lõm vào không chạm cá.</summary>
    private const float FrontCushion = 0.1f;

    /// <summary>
    /// Thứ tự trước sau cho bộ điểm đã giãn XY, tham lam từ sau ra trước: mỗi bước chọn trong các điểm còn lại điểm mà nếu đứng sau
    /// tất cả các điểm còn lại thì vẫn lộ nhiều nhất (điểm ít bị chờm nhất lùi ra sau, điểm bị chờm nhiều nhất ra trước, nên mức lộ
    /// thấp nhất của cả bộ cao nhất có thể). rank[i] = 0 là trước nhất; visible[i] = phần thân còn lộ theo thứ tự đó. Cùng đầu vào cho
    /// cùng kết quả.
    /// </summary>
    private static void RankByVisibility(Vector3[] pts, float hx, float hy, int[] rank, float[] visible)
    {
        int n = pts.Length;

        // Không cặp nào chờm nhau trên màn hình: thứ tự nào cũng lộ hết, khỏi tính.
        bool anyOverlap = false;
        for (int i = 0; i < n && !anyOverlap; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                if (Normalized2D(pts[j] - pts[i], hx, hy) < 1f) { anyOverlap = true; break; }
            }
        }

        if (!anyOverlap)
        {
            for (int i = 0; i < n; i++)
            {
                rank[i] = i;
                visible[i] = 1f;
            }

            return;
        }

        var remaining = new List<int>(n);
        for (int i = 0; i < n; i++) remaining.Add(i);
        var front = new List<Ellipse>(n);

        for (int r = n - 1; r >= 0; r--)
        {
            int best = -1;
            float bestVisible = -1f;
            foreach (int i in remaining)
            {
                front.Clear();
                foreach (int j in remaining)
                {
                    if (j != i) front.Add(new Ellipse(pts[j].x, pts[j].y, hx, hy));
                }

                float v = VisibleFraction(new Ellipse(pts[i].x, pts[i].y, hx, hy), front);
                if (v > bestVisible + 1e-4f)
                {
                    bestVisible = v;
                    best = i;
                }
            }

            rank[best] = r;
            visible[best] = bestVisible;
            remaining.Remove(best);
        }
    }

    /// <summary>
    /// Gán z theo thứ tự trước sau: điểm trước nhất sát mặt trước quả cầu (cộng đệm FrontCushion), mỗi điểm sau lùi vừa đủ để không cắt
    /// (ellipsoid có khoảng hở) các điểm đứng trước nó mà chờm nó trên màn hình; cặp không chờm nhau thì z tự do. Kẹp trong
    /// [zFront, zBack] của điểm đó (ZRange, theo thân cá đã xoay): chật quá thì chịu cắt.
    /// </summary>
    private void AssignDepth(Vector3[] pts, int[] rank, float inner, float backDepth, float hx, float hy, float hz, Vector3[] bodyRight, Vector3[] bodyLeft)
    {
        int n = pts.Length;
        var order = new int[n];
        for (int i = 0; i < n; i++) order[rank[i]] = i;

        float sx = 2f * hx + SeparationGap;
        float sy = 2f * hy + SeparationGap;
        float sz = 2f * hz + SeparationGap;
        float cushion = FrontCushion * inner;
        for (int r = 0; r < n; r++)
        {
            int i = order[r];
            ZRange(pts[i], inner, backDepth, bodyRight, bodyLeft, out float zFront, out float zBack);
            float z = zFront + cushion;
            for (int q = 0; q < r; q++)
            {
                int j = order[q];
                float ux = (pts[i].x - pts[j].x) / sx;
                float uy = (pts[i].y - pts[j].y) / sy;
                float rest = 1f - ux * ux - uy * uy;
                if (rest <= 0f) continue;
                z = Mathf.Max(z, pts[j].z + Mathf.Sqrt(rest) * sz);
            }

            pts[i].z = Mathf.Clamp(z, zFront, zBack);
        }
    }

    /// <summary>Ellipse trên mặt phẳng màn hình: tâm (X, Y), bán trục (Hx, Hy).</summary>
    private readonly struct Ellipse
    {
        public readonly float X, Y, Hx, Hy;

        public Ellipse(float x, float y, float hx, float hy)
        {
            X = x;
            Y = y;
            Hx = Mathf.Max(hx, 1e-4f);
            Hy = Mathf.Max(hy, 1e-4f);
        }

        public bool Contains(float px, float py)
        {
            float u = (px - X) / Hx;
            float v = (py - Y) / Hy;
            return u * u + v * v <= 1f;
        }
    }

    /// <summary>Điểm mẫu rải đều trong ellipse đơn vị (lưới 11 x 9 trên hình vuông bao, giữ điểm nằm trong) để ước lượng diện tích.</summary>
    private static readonly Vector2[] EllipseSamples = BuildEllipseSamples();

    private static Vector2[] BuildEllipseSamples()
    {
        const int nx = 11, ny = 9;
        var list = new List<Vector2>(nx * ny);
        for (int i = 0; i < nx; i++)
        {
            float u = (i + 0.5f) / nx * 2f - 1f;
            for (int j = 0; j < ny; j++)
            {
                float v = (j + 0.5f) / ny * 2f - 1f;
                if (u * u + v * v <= 1f) list.Add(new Vector2(u, v));
            }
        }

        return list.ToArray();
    }

    /// <summary>Phần diện tích của ellipse không bị các ellipse đứng trước (front) che, 0..1, ước lượng bằng điểm mẫu.</summary>
    private static float VisibleFraction(in Ellipse e, List<Ellipse> front)
    {
        if (front.Count == 0) return 1f;
        int visible = 0;
        foreach (Vector2 s in EllipseSamples)
        {
            float px = e.X + s.x * e.Hx;
            float py = e.Y + s.y * e.Hy;
            bool covered = false;
            for (int k = 0; k < front.Count && !covered; k++) covered = front[k].Contains(px, py);
            if (!covered) visible++;
        }

        return (float)visible / EllipseSamples.Length;
    }

    /// <summary>Ép một điểm vào vùng cho phép cả XY lẫn z (xem ContainXY, ZRange). Dùng cho bộ 1 điểm.</summary>
    private Vector3 Contain(Vector3 p, float inner, float backDepth, float hx, float hy, Vector3[] bodyRight, Vector3[] bodyLeft)
    {
        ContainXY(ref p, inner, hx, hy);
        ZRange(p, inner, backDepth, bodyRight, bodyLeft, out float zFront, out float zBack);
        p.z = Mathf.Clamp(p.z, zFront, zBack);
        return p;
    }

    /// <summary>Kéo điểm về tâm cho tới khi ellipse thân cá (hx, hy) nằm trong hình chiếu tròn (kiểm tra 12 điểm trên biên ellipse).</summary>
    private static void ContainXY(ref Vector3 p, float inner, float hx, float hy)
    {
        float inner2 = inner * inner;
        for (int k = 0; k < 16; k++)
        {
            if (EllipseInsideCircle(p.x, p.y, hx, hy, inner2)) break;
            if (Mathf.Abs(p.x) < 1e-4f && Mathf.Abs(p.y) < 1e-4f) break;   // cá to hơn vùng, chịu
            p.x *= 0.9f;
            p.y *= 0.9f;
        }
    }

    /// <summary>
    /// Khoảng z cho phép của tâm cá tại điểm (x, y): mọi điểm mẫu trên thân cá đã xoay theo tư thế nghỉ (RestSamples, phía nhìn theo
    /// LayoutFacing) phải nằm sau mặt trước quả cầu tại đúng cột của điểm mẫu đó (cá chéo về camera thì mũi ở cột khác tâm, nơi mặt
    /// cầu nông hơn nhiều), trừ thêm biên độ bơi tại chỗ để lúc trôi vẫn không ló ra trước lớp vỏ; phía sau không quá backDepth
    /// (sphereBack: cũng không ra khỏi mặt cầu). Không đủ chỗ thì zBack = zFront, chịu cắt.
    /// </summary>
    private void ZRange(Vector3 p, float inner, float backDepth, Vector3[] bodyRight, Vector3[] bodyLeft, out float zFront, out float zBack)
    {
        float facing = LayoutFacing(p.x);
        float frontInner = Mathf.Max(0.05f, inner - (HoverAmplitude + BobAmplitude));
        zFront = float.NegativeInfinity;
        zBack = float.PositiveInfinity;
        if (facing >= 0f) ZRangeFor(p, frontInner, inner, backDepth, bodyRight, ref zFront, ref zBack);
        if (facing <= 0f) ZRangeFor(p, frontInner, inner, backDepth, bodyLeft, ref zFront, ref zBack);
        if (zBack < zFront) zBack = zFront;
    }

    private void ZRangeFor(Vector3 p, float frontInner, float inner, float backDepth, Vector3[] body, ref float zFront, ref float zBack)
    {
        bool sphereBack = SphereBack;
        float front2 = frontInner * frontInner;
        float inner2 = inner * inner;
        foreach (Vector3 o in body)
        {
            float sx = p.x + o.x;
            float sy = p.y + o.y;
            float column = sx * sx + sy * sy;
            float frontHalf = Mathf.Sqrt(Mathf.Max(0f, front2 - column));
            zFront = Mathf.Max(zFront, -frontHalf - o.z);
            float back = sphereBack ? Mathf.Sqrt(Mathf.Max(0f, inner2 - column)) : backDepth;
            zBack = Mathf.Min(zBack, back - o.z);
        }
    }

    /// <summary>Phía cá sẽ nhìn khi đứng ở điểm có hoành độ x: +1 / -1 theo RestFacing, Outward theo dấu x, Free = 0 (chưa biết, xét cả hai phía).</summary>
    private float LayoutFacing(float x)
    {
        switch (RestFacing)
        {
            case FacingMode.Right: return 1f;
            case FacingMode.Left: return -1f;
            case FacingMode.Outward: return x < 0f ? -1f : 1f;
            default: return 0f;
        }
    }

    /// <summary>12 điểm đơn vị trên đường tròn (cos, sin) để kiểm tra biên ellipse mà không gọi lượng giác trong vòng giãn.</summary>
    private static readonly Vector2[] EllipseRing = BuildEllipseRing();

    private static Vector2[] BuildEllipseRing()
    {
        var ring = new Vector2[12];
        for (int k = 0; k < ring.Length; k++)
        {
            float a = k * (Mathf.PI * 2f / ring.Length);
            ring[k] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        return ring;
    }

    private static bool EllipseInsideCircle(float cx, float cy, float hx, float hy, float radius2)
    {
        foreach (Vector2 r in EllipseRing)
        {
            float px = cx + r.x * hx;
            float py = cy + r.y * hy;
            if (px * px + py * py > radius2) return false;
        }

        return true;
    }

    /// <summary>Khoảng cách chuẩn hóa 3D giữa hai thân cá (>= 1 là không cắt nhau).</summary>
    private static float Normalized3D(Vector3 d, float hx, float hy, float hz, float gap)
    {
        return new Vector3(d.x / (2f * hx + gap), d.y / (2f * hy + gap), d.z / (2f * hz + gap)).magnitude;
    }

    /// <summary>Khoảng cách chuẩn hóa 2D trên màn hình (>= 1 là không chờm). Mức chờm = 1 - giá trị này.</summary>
    private static float Normalized2D(Vector3 d, float hx, float hy)
    {
        return new Vector2(d.x / (2f * hx), d.y / (2f * hy)).magnitude;
    }

    /// <summary>Bộ điểm có đạt: không cắt nhau trong 3D (với nửa khoảng hở), chờm 2D không quá MaxOverlap và mọi điểm lộ đủ MinVisibleFraction.</summary>
    private bool SetFits(Vector3[] pts, float[] visible, float hx, float hy, float hz)
    {
        foreach (float v in visible) if (v < MinVisibleFraction) return false;
        for (int i = 0; i < pts.Length; i++)
        {
            for (int j = i + 1; j < pts.Length; j++)
            {
                Vector3 d = pts[j] - pts[i];
                if (Normalized3D(d, hx, hy, hz, SeparationGap * 0.5f) < 1f) return false;
                if (1f - Normalized2D(d, hx, hy) > MaxOverlap) return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Bán kính nhỏ nhất để mọi bộ điểm của đàn hiện tại đạt (không cắt 3D, chờm 2D không quá MaxOverlap, lộ đủ MinVisibleFraction).
    /// Khi ScaleToRadius bật thì cá luôn vừa nên trả về bán kính hiện tại.
    /// </summary>
    public float RequiredRadius(float currentRadius)
    {
        int n = states.Count;
        if (n == 0 || ScaleToRadius) return currentRadius;

        float hx = MaxHalfLength();
        float hy = MaxHalfHeight();
        float hz = MaxHalfWidth();
        Vector3[] bodyRight = RestSamples(hx, hy, hz, 1f);
        Vector3[] bodyLeft = RestSamples(hx, hy, hz, -1f);
        RestExtents(ref hx, ref hy, ref hz);
        float lo = 0.05f, hi = 50f;
        for (int it = 0; it < 24; it++)
        {
            float mid = (lo + hi) * 0.5f;
            bool ok = true;
            for (int k = 0; k < LayoutSets && ok; k++)
            {
                Vector3[] pts = BuildSet(n, k, mid, hx, hy, hz, bodyRight, bodyLeft, out float[] visible);
                ok = SetFits(pts, visible, hx, hy, hz);
            }

            if (ok) hi = mid;
            else lo = mid;
        }

        return hi * 1.02f;
    }

    private float MaxHalfLength()
    {
        float m = 0f;
        foreach (FishState s in states) if (!s.Leaving) m = Mathf.Max(m, ScaledHalfLength(s));
        foreach (Reservation r in reservations) m = Mathf.Max(m, r.HalfLength);
        return m;
    }

    private float MaxHalfHeight()
    {
        float m = 0f;
        foreach (FishState s in states) if (!s.Leaving) m = Mathf.Max(m, ScaledHalfHeight(s));
        foreach (Reservation r in reservations) m = Mathf.Max(m, r.HalfHeight);
        return m;
    }

    private float MaxHalfWidth()
    {
        float m = 0f;
        foreach (FishState s in states) if (!s.Leaving) m = Mathf.Max(m, ScaledHalfWidth(s));
        foreach (Reservation r in reservations) m = Mathf.Max(m, r.HalfWidth);
        return m;
    }

    // ---------- Bơi ----------

    /// <summary>Góc quét quanh tâm (rad, dương) từ vị trí xuất phát tới điểm đích theo chiều PathDir, cộng số vòng phụ.</summary>
    private float SweepAngle(FishState s, int extraLoops)
    {
        Vector3 end = PointOf(s);
        float a0 = Mathf.Atan2(s.PathStart.y, s.PathStart.x);
        float a1 = Mathf.Atan2(end.y, end.x);
        float delta = Mathf.Repeat(s.PathDir * (a1 - a0), Mathf.PI * 2f);
        return delta + extraLoops * Mathf.PI * 2f;
    }

    /// <summary>
    /// Đường bơi 3D dạng công thức liên tục: quét góc đều quanh tâm theo mặt phẳng XY, bán kính XY nội suy và phình ra mép
    /// (PathBulge), z nội suy và lặn ra phía sau ở giữa đường (DiveDepth). Vị trí và tiếp tuyến lấy trực tiếp từ công thức
    /// nên không có gấp khúc; bảng chiều dài chỉ dùng để đổi quãng đường đã bơi sang tham số t.
    /// </summary>
    private PathParams BuildPath(FishState s)
    {
        Vector3 end = PointOf(s);
        Vector2 xy0 = new(s.PathStart.x, s.PathStart.y);
        Vector2 xy1 = new(end.x, end.y);
        float r0 = xy0.magnitude;
        float r1 = xy1.magnitude;
        float swimRadius = InnerRadius(layoutRadius) - ScaledHalfHeight(s) - 0.05f;
        return new PathParams
        {
            R0 = r0,
            R1 = r1,
            A0 = Mathf.Atan2(xy0.y, xy0.x),
            Sweep = SweepAngle(s, s.PathExtraLoops),
            Dir = s.PathDir,
            Z0 = s.PathStart.z,
            Z1 = end.z,
            Bulge = Mathf.Max(0f, swimRadius - Mathf.Max(r0, r1)) * PathBulge,
            Dive = DiveDepth * layoutRadius,
            End = end
        };
    }

    private static Vector3 EvalPath(in PathParams p, float t)
    {
        if (t >= 1f) return p.End;
        float r = Mathf.Lerp(p.R0, p.R1, t) + p.Bulge * Mathf.Sin(t * Mathf.PI);
        float a = p.A0 + p.Dir * p.Sweep * t;
        float z = Mathf.Lerp(p.Z0, p.Z1, t) + p.Dive * Mathf.Sin(t * Mathf.PI);
        return new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, z);
    }

    /// <summary>Tiếp tuyến (đã chuẩn hóa) của đường tại t, lấy bằng sai phân nhỏ trên công thức.</summary>
    private static Vector3 PathTangent(in PathParams p, float t)
    {
        const float h = 0.004f;
        Vector3 d = EvalPath(p, Mathf.Min(1f, t + h)) - EvalPath(p, Mathf.Max(0f, t - h));
        return d.sqrMagnitude > 1e-10f ? d.normalized : Vector3.zero;
    }

    /// <summary>Dựng bảng chiều dài tích lũy theo t đều, trả về tổng chiều dài.</summary>
    private float BuildLengthTable(in PathParams p)
    {
        float total = 0f;
        Vector3 prev = EvalPath(p, 0f);
        pathLengths[0] = 0f;
        for (int i = 1; i <= PathSamples; i++)
        {
            Vector3 cur = EvalPath(p, (float)i / PathSamples);
            total += Vector3.Distance(prev, cur);
            pathLengths[i] = total;
            prev = cur;
        }

        return total;
    }

    /// <summary>Đổi quãng đường đã bơi sang tham số t (nội suy trong bảng chiều dài).</summary>
    private float TFromLength(float length)
    {
        for (int i = 1; i <= PathSamples; i++)
        {
            if (length > pathLengths[i]) continue;
            float seg = pathLengths[i] - pathLengths[i - 1];
            float f = seg > 1e-6f ? (length - pathLengths[i - 1]) / seg : 1f;
            return (i - 1 + f) / PathSamples;
        }

        return 1f;
    }

    /// <summary>
    /// Hướng mục tiêu khi bơi: nhìn theo dir, lưng hướng lên trời khi bơi ngang; khi hướng bơi gần thẳng đứng thì
    /// giữ lưng theo tư thế hiện tại (up của thân cá) thay vì up thế giới đã suy biến, pha trộn dần trong khoảng
    /// |dir.y| 0.7 đến 0.95 nên không lật đột ngột khi bơi lên xuống. Đã thử cách xoay góc nhỏ nhất kèm tự lật lưng chậm:
    /// mượt hơn chút nhưng cá bơi nghiêng hoặc ngửa tới 24% thời gian và về đích lệch lưng tới 155 độ, nên giữ cách này.
    /// </summary>
    private static Quaternion HeadingTowards(Quaternion current, Vector3 dir)
    {
        Vector3 worldUp = Vector3.ProjectOnPlane(Vector3.up, dir);
        Vector3 ownUp = Vector3.ProjectOnPlane(current * Vector3.up, dir);
        if (ownUp.sqrMagnitude < 1e-6f) ownUp = Vector3.ProjectOnPlane(current * Vector3.right, dir);
        if (worldUp.sqrMagnitude < 1e-6f) worldUp = ownUp;
        if (ownUp.sqrMagnitude < 1e-6f) return Quaternion.LookRotation(dir, Vector3.up);

        float vertical = Mathf.InverseLerp(0.7f, 0.95f, Mathf.Abs(dir.y));
        Vector3 up = Vector3.Slerp(worldUp.normalized, ownUp.normalized, vertical);
        if (up.sqrMagnitude < 1e-6f) up = ownUp.normalized;
        return Quaternion.LookRotation(dir, up);
    }

    private void TickSlot(FishState s, float dt)
    {
        Fish fish = s.Fish;
        if (fish == null) return;

        Vector3 newPos;
        Vector3 pathTangent = Vector3.zero;
        bool flyingIn = s.FlyIn;
        Quaternion flyInRot = Quaternion.identity;
        // Loài di chuyển theo nhịp (loài phụt nước; hiện chưa loài nào bật — bạch tuộc đã thử rồi quay về phụt đều theo Speed): tốc độ bơi = SwimSpeed × nhịp animation (mở tay chậm, chụm xong vọt lên rồi trượt chậm dần).
        float moveMul = fish.Animator != null && fish.Animator.IsPulseDriven ? fish.Animator.MoveSpeedMultiplier : 1f;
        if (s.FlyIn)
        {
            // Bay vào: thẳng tới điểm, chậm dần về cuối, scale từ 0 lên BaseScale, giữ tư thế nghỉ; tới nơi thì bàn giao cho bơi tại chỗ.
            s.FlyInElapsed += dt;
            float t = Mathf.Clamp01(s.FlyInElapsed / s.FlyInDuration);
            float e = 1f - (1f - t) * (1f - t);
            Vector3 end = s.Leaving ? s.LeavePos : PointOf(s);
            newPos = Vector3.Lerp(s.FlyInStart, end, e);
            Vector3 d = end - s.FlyInStart;
            pathTangent = d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.zero;
            fish.transform.localScale = s.BaseScale * t;
            flyInRot = FlyInRestRotation(s, d, e);
            if (t >= 1f)
            {
                s.FlyIn = false;
                s.FacingLock = ArrivalFacingLock;   // đã nhìn đúng phía, cú bám điểm lúc tới nơi không làm quay đầu
                fish.transform.localScale = s.BaseScale;
                s.HoverVel = pathTangent * (SwimSpeed * 0.25f);
            }
        }
        else if (s.OnPath)
        {
            PathParams path = BuildPath(s);
            float total = BuildLengthTable(path);

            // Tăng tốc ở đầu, giảm tốc về đích, không thấp hơn 25% để không lết.
            float ease = Mathf.Max(0.05f, EaseDistance);
            float ramp = Mathf.Min((s.PathProgress + 0.05f) / ease, (total - s.PathProgress + 0.05f) / ease);
            ramp = Mathf.Clamp(ramp, 0.25f, 1f);
            s.PathProgress += SwimSpeed * moveMul * ramp * dt;

            if (s.PathProgress >= total)
            {
                newPos = path.End;
                pathTangent = PathTangent(path, 1f);
                s.OnPath = false;
                // Về đích thì đứng nghiêng theo phía vừa bơi tới (Outward: về phía mép), và bàn giao vận tốc cho bước bơi tại chỗ để không khựng.
                if (Mathf.Abs(pathTangent.x) > 0.2f) s.Facing = ResolveFacing(s, pathTangent.x >= 0f ? 1f : -1f);
                s.HoverVel = pathTangent * (SwimSpeed * 0.25f);
            }
            else
            {
                float t = TFromLength(s.PathProgress);
                newPos = EvalPath(path, t);
                pathTangent = PathTangent(path, t);
            }
        }
        else
        {
            float t = Time.time * HoverFrequency + s.Seed;
            Vector3 drift = new(
                (Mathf.PerlinNoise(t, s.Seed) - 0.5f) * 2f * HoverAmplitude,
                (Mathf.PerlinNoise(s.Seed, t * 0.8f) - 0.5f) * 2f * HoverAmplitude,
                (Mathf.PerlinNoise(t * 0.6f, s.Seed + 37f) - 0.5f) * 2f * HoverAmplitude * 0.5f);
            Vector3 target = (s.Leaving ? s.LeavePos : PointOf(s)) + drift;   // sắp rời đàn: trôi quanh chỗ đang đứng

            if (!s.HasPos)
            {
                s.Pos = target;
                s.HoverVel = Vector3.zero;
                s.HasPos = true;
            }

            // Bám điểm bằng SmoothDamp: không giật khi điểm đổi (bóng phình) hay khi vừa bơi tới nơi.
            newPos = Vector3.SmoothDamp(s.Pos, target, ref s.HoverVel, 0.35f, SwimSpeed * moveMul, dt);
        }

        Vector3 velocity = dt > 0f ? (newPos - s.Pos) / dt : Vector3.zero;
        s.Pos = newPos;

        // Đang bơi: quay đầu theo tiếp tuyến đường bơi trong 3D (kể cả về phía camera), xoay góc nhỏ nhất nên không lật.
        // Đứng yên: về tư thế nghỉ (nhìn nghiêng trái/phải, chéo về camera và ngóc/chúi theo RestYawToCamera / RestPitch).
        float speed = velocity.magnitude;
        if (flyingIn)
        {
            s.Rotation = flyInRot;   // góc ngóc đổi liên tục theo tiến độ nên đặt thẳng, không cần RotateTowards
        }
        else
        {
            Quaternion targetRot;
            if (s.OnPath && pathTangent != Vector3.zero)
            {
                targetRot = HeadingTowards(s.Rotation, pathTangent);
            }
            else
            {
                if (RestFacing == FacingMode.Outward) s.Facing = ResolveFacing(s, s.Facing);   // luôn theo mép của điểm đang giữ, không theo vận tốc trôi
                else if (s.FacingLock > 0f) s.FacingLock -= dt;
                else if (Mathf.Abs(velocity.x) > FlipSpeedThreshold) s.Facing = ResolveFacing(s, velocity.x >= 0f ? 1f : -1f);
                targetRot = RestRotation(s.Facing);
            }

            s.Rotation = Quaternion.RotateTowards(s.Rotation, targetRot, TurnSpeed * dt);
        }

        float bob = Mathf.Sin((Time.time * BobFrequency + s.Seed) * Mathf.PI * 2f) * BobAmplitude;
        fish.transform.localPosition = s.Pos + new Vector3(0f, bob, 0f);
        fish.transform.localRotation = s.Rotation;

        if (fish.Animator != null) fish.Animator.Speed = AnimSpeedIdle + speed / Mathf.Max(moveMul, 0.05f) * AnimSpeedPerMeter;   // loài theo nhịp: báo tốc độ chưa nhân nhịp
    }

    // ---------- chung ----------

    /// <summary>
    /// Hướng nghiêng khi đứng yên: Right / Left khóa cứng; Outward = về phía mép gần hơn theo x của điểm cá đang giữ (điểm ngay giữa
    /// thì theo hướng đề nghị); Free = hướng đề nghị (+1 nhìn +X, -1 nhìn -X).
    /// </summary>
    private float ResolveFacing(FishState s, float preferred)
    {
        switch (RestFacing)
        {
            case FacingMode.Right: return 1f;
            case FacingMode.Left: return -1f;
            case FacingMode.Outward:
            {
                float x = PointOf(s).x;
                return Mathf.Abs(x) > 1e-3f ? Mathf.Sign(x) : preferred;
            }
            default: return preferred;
        }
    }

    /// <summary>
    /// Tư thế của cá đang bay vào (FlyIn): tư thế nghỉ nhìn về phía mình bay tới (theo X của travel = đích − xuất phát),
    /// ngóc / chúi theo độ dốc của đường bay trên mặt phẳng màn hình (tối đa FlyInPitchMax độ) rồi duỗi thẳng dần theo eased (0..1)
    /// để tới nơi là đúng tư thế nghỉ, không phải xoay thêm. Đường bay gần như thẳng đứng thì giữ phía đang nhìn. Outward vẫn nhìn theo
    /// hướng bay (không bay lùi), tới nơi mới quay ra mép.
    /// </summary>
    private Quaternion FlyInRestRotation(FishState s, Vector3 travel, float eased)
    {
        if (Mathf.Abs(travel.x) > 0.05f)
        {
            float toward = travel.x >= 0f ? 1f : -1f;
            s.Facing = RestFacing == FacingMode.Outward ? toward : ResolveFacing(s, toward);
        }

        float climb = Mathf.Atan2(travel.y, Mathf.Max(Mathf.Abs(travel.x), 0.01f)) * Mathf.Rad2Deg;
        climb = Mathf.Clamp(climb, -FlyInPitchMax, FlyInPitchMax) * (1f - Mathf.Clamp01(eased));
        return Quaternion.Euler(-RestPitch - climb, s.Facing * (90f + RestYawToCamera), 0f);
    }

    /// <summary>
    /// Tư thế nghỉ trong không gian root (camera ở phía -Z): nhìn nghiêng theo facing (+1 = +X, -1 = -X), đầu quay chéo về camera
    /// RestYawToCamera độ (đối xứng hai phía) và ngóc lên RestPitch độ. Cả hai = 0 thì nằm ngang hẳn như trước.
    /// </summary>
    private Quaternion RestRotation(float facing)
    {
        return Quaternion.Euler(-RestPitch, facing * (90f + RestYawToCamera), 0f);
    }

    /// <summary>
    /// Đổi bán trục thân cá (dài, cao, dày khi nhìn ngang hẳn) sang bán trục hộp bao theo X, Y, Z của ellipsoid đã xoay về tư thế
    /// nghỉ, để bộ điểm chừa đủ chỗ: cá quay chéo về camera thì ngắn lại theo X nhưng dày thêm theo Z (mũi không chọc ra khỏi mặt
    /// cầu), ngóc / chúi thì cao thêm theo Y. Hai phía nhìn phải / trái đối xứng nên chỉ tính một phía.
    /// </summary>
    private void RestExtents(ref float hx, ref float hy, ref float hz)
    {
        if (Mathf.Approximately(RestYawToCamera, 0f) && Mathf.Approximately(RestPitch, 0f)) return;
        Quaternion rest = RestRotation(1f);
        Vector3 length = rest * Vector3.forward * hx;
        Vector3 height = rest * Vector3.up * hy;
        Vector3 width = rest * Vector3.right * hz;
        hx = Mathf.Sqrt(length.x * length.x + height.x * height.x + width.x * width.x);
        hy = Mathf.Sqrt(length.y * length.y + height.y * height.y + width.y * width.y);
        hz = Mathf.Sqrt(length.z * length.z + height.z * height.z + width.z * width.z);
    }

    /// <summary>26 hướng đơn vị (lưới 3x3x3 bỏ tâm) để lấy điểm mẫu trên mặt ellipsoid thân cá.</summary>
    private static readonly Vector3[] BodyDirections = BuildBodyDirections();

    private static Vector3[] BuildBodyDirections()
    {
        var list = new List<Vector3>(26);
        for (int x = -1; x <= 1; x++)
        for (int y = -1; y <= 1; y++)
        for (int z = -1; z <= 1; z++)
        {
            if (x == 0 && y == 0 && z == 0) continue;
            list.Add(new Vector3(x, y, z).normalized);
        }

        return list.ToArray();
    }

    /// <summary>
    /// Điểm mẫu trên mặt ellipsoid thân cá (bán trục: dày hz theo X, cao hy theo Y, dài hx theo Z của model; khớp mesh collider thật
    /// trong khoảng vài cm, hộp bao thì quá dè dặt) đã xoay theo tư thế nghỉ nhìn về facing, trong không gian root. ZRange dùng để ép
    /// từng phần thân, nhất là mũi cá chéo về camera, vào sau mặt trước quả cầu.
    /// </summary>
    private Vector3[] RestSamples(float hx, float hy, float hz, float facing)
    {
        Quaternion rest = RestRotation(facing);
        var semi = new Vector3(hz, hy, hx);
        var pts = new Vector3[BodyDirections.Length];
        for (int i = 0; i < pts.Length; i++) pts[i] = rest * Vector3.Scale(BodyDirections[i], semi);
        return pts;
    }

    private void ApplyScale(FishState s, float radius)
    {
        if (!ScaleToRadius || s.Fish == null || s.FlyIn) return;   // đang bay vào từ spawner: scale do TickSlot điều khiển
        float scale = Mathf.Max(0.01f, radius * FishSizeRatio) / s.BaseSize;
        s.Fish.transform.localScale = Vector3.one * scale;
    }

    // Cá đang bay vào (FlyIn) lớn dần từ 0: tính theo scale đích (BaseScale) để bộ điểm không dựng theo cỡ dở dang.
    private float ScaledHalfLength(FishState s) => s.HalfLength * (s.Fish == null ? 1f : s.FlyIn ? s.BaseScale.x : s.Fish.transform.localScale.x);
    private float ScaledHalfHeight(FishState s) => s.HalfHeight * (s.Fish == null ? 1f : s.FlyIn ? s.BaseScale.y : s.Fish.transform.localScale.y);
    private float ScaledHalfWidth(FishState s) => s.HalfWidth * (s.Fish == null ? 1f : s.FlyIn ? s.BaseScale.z : s.Fish.transform.localScale.z);

    /// <summary>Cỡ cá lớn nhất trong đàn (trước khi xoay theo tư thế nghỉ) đã lệch quá 1% so với lúc dựng bộ điểm (cá đổi scale, cá vào / ra).</summary>
    private bool ExtentsChanged()
    {
        return Differs(MaxHalfLength(), layoutRawHx) || Differs(MaxHalfHeight(), layoutRawHy) || Differs(MaxHalfWidth(), layoutRawHz);
        static bool Differs(float a, float b) => Mathf.Abs(a - b) > 0.01f * Mathf.Max(a, b, 1e-3f);
    }

    /// <summary>
    /// Gizmo: các bộ điểm (bộ đang dùng màu vàng, kèm phần trăm thân còn lộ của từng điểm), hộp thân cá tại điểm của nó, chỗ đã giữ màu
    /// lục lam. Chủ gọi từ OnDrawGizmosSelected.
    /// </summary>
    public void DrawGizmos()
    {
        if (!Application.isPlaying || root == null || CurrentSet < 0) return;
        for (int k = 0; k < layoutSets.Count; k++)
        {
            Gizmos.color = k == CurrentSet ? new Color(1f, 0.9f, 0.3f, 0.9f) : new Color(0.5f, 0.5f, 0.5f, 0.4f);
            foreach (Vector3 p in layoutSets[k]) Gizmos.DrawWireSphere(root.TransformPoint(p), 0.08f);
        }

#if UNITY_EDITOR
        if (CurrentSet < layoutVisible.Count)
        {
            Vector3[] set = layoutSets[CurrentSet];
            float[] visible = layoutVisible[CurrentSet];
            for (int p = 0; p < set.Length && p < visible.Length; p++)
            {
                UnityEditor.Handles.Label(root.TransformPoint(set[p]), $"{p}: {Mathf.RoundToInt(visible[p] * 100f)}%");
            }
        }
#endif

        Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.9f);
        foreach (FishState s in states)
        {
            Vector3 c = root.TransformPoint(PointOf(s));
            Gizmos.DrawWireCube(c, new Vector3(ScaledHalfLength(s) * 2f, ScaledHalfHeight(s) * 2f, ScaledHalfWidth(s) * 2f));
        }

        Gizmos.color = new Color(0.3f, 0.9f, 0.9f, 0.9f);
        foreach (Reservation r in reservations) Gizmos.DrawWireSphere(root.TransformPoint(PointAt(r.PointIndex)), 0.1f);
    }
}
