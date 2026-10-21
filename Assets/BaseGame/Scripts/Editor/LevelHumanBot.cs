using System;
using System.Collections.Generic;

/// <summary>
/// Bot "như người" - chỉ dùng trong tool để ĐO level (nút "Đo level đang mở" và ô "Bắt buộc có bẫy").
///
/// Vì sao không dùng LevelSolver.EstimateWinRate: con bot đó bấm ngẫu nhiên, nên hai chỗ nó chơi kém hẳn người thật:
/// - Băng: bóng băng chỉ vỡ khi các bóng sinh trước nó cùng cột đã nổ hết. Người chơi thấy băng che cá thì cố tình phá
///   bóng phía trên cho băng nổi lên; bot cũ không biết, bóng băng nằm chết chiếm chỗ trên bàn tới khi kẹt. Level 150
///   (8 băng, 13 cá ẩn) bot cũ thắng 0%; bản Python của bot này ra ~24% (lỡ tay 5%) / ~66% (không lỡ tay).
/// - Ô chờ: không có nước vào bể thì bot cũ bốc bừa một con lên ô chờ. Người chơi chọn con sắp được cần. Level cần 4/5
///   ô chờ thì bốc bừa là chết (level 144: 16% → 42% chỉ nhờ chọn có tính).
/// - Cá ẩn: bot không "nhìn trộm" - con ẩn không được tính là nước vào bể chắc chắn. Nhưng khi không có nước vào bể
///   chắc chắn thì bot dám bấm thử con "?" (có thể trúng loài bể đang cần), như người chơi. Né hẳn cá ẩn thì bóng có
///   cá ẩn không bao giờ nổ, bàn tắc: level 144 (6 cá ẩn) né hẳn 0%, dám bấm thử 62%.
/// - Cá ẩn tự lộ khi bóng chỉ còn toàn cá ẩn (BoardModel.TryAutoRevealAll). Trong game việc này do BoardManager gọi
///   sau mỗi lần cá rời bóng, RemoveFishAt của model không tự gọi - nên bot phải tự gọi, thiếu là cá ẩn không bao giờ
///   lộ và level nhiều cá ẩn đo ra 0%.
///
/// Luật bàn giữ y hệt LevelSolver (BoardModel, bể / ô chờ mặc định, cột băng AssignColumns, tự thu Drain) - chỉ khác
/// cách CHỌN nước. Không sửa LevelSolver: đó là code gameplay của dev. Vòng dò "% thắng" của tool mặc định đo bằng bot
/// này (thước "người chơi"), bỏ tick thì về bot cũ.
/// </summary>
public static class LevelHumanBot
{
    private const string CarefulPrefKey = "FishSort.LevelHumanBot.CarefulWhenFull";
    private static bool? careful;

    /// <summary>
    /// Bật (mặc định): người chơi cẩn thận dần khi ô chờ đầy - tỉ lệ lỡ tay nhân MistakeScale, trống ô chờ thì đủ
    /// mistakeRate, còn 1 ô trống thì không bấm bừa nữa. Lỡ tay đều mọi nước (tắt) làm bot tự sát đúng lúc người thật
    /// chậm lại nhìn kỹ nhất: level 254 ở lỡ tay 10% có 74% ván thua "ngay sau lỡ tay" - phần lớn là bấm bừa lúc 4/5 ô
    /// chờ. Lưu EditorPrefs để giữ qua domain reload; tắt để so với cách đo cũ trên cùng level.
    /// </summary>
    public static bool CarefulWhenFull
    {
        get => careful ??= UnityEditor.EditorPrefs.GetBool(CarefulPrefKey, true);
        set
        {
            careful = value;
            UnityEditor.EditorPrefs.SetBool(CarefulPrefKey, value);
        }
    }

    /// <summary>Hệ số nhân tỉ lệ lỡ tay theo ô chờ trống: 5 trống = 1, 4 = 0.75 ... 1 trống = 0.</summary>
    private static float MistakeScale(BoardModel model)
    {
        int slots = LevelBands.DefaultHoldingSlots;
        if (!CarefulWhenFull || slots <= 1) return 1f;
        return Math.Clamp((model.HoldingFreeCount - 1) / (float)(slots - 1), 0f, 1f);
    }

    /// <summary>Tỉ lệ lỡ tay mặc định khi đo: người chơi thật thỉnh thoảng bấm nhầm. Bot cũ bấm bừa 15%.</summary>
    public const float DefaultMistakeRate = 0.05f;

    /// <summary>
    /// Lỡ tay khớp người chơi thật: hiệu chỉnh 2026-09-25 bằng tay người thiết kế trên 3 level (dễ, 144 ~40%, 140 ~30%).
    /// Dùng cho dòng "≈ Người chơi thắng" của nút Đo và cột cùng tên trong báo cáo xlsx.
    /// </summary>
    public const float PlayerMistakeRate = 0.1f;

    /// <summary>
    /// Mục tiêu "≈ Người chơi thắng" theo tag, trên thước bot như người chỉ bấm bóng thấy trên màn 9:16 (LevelSolver.VisibleArea).
    /// Chốt 2026-10-16 từ report winrate 12-14/10 (94 level): quy mục tiêu người thật của GDD BAL-02 (LevelBands.TargetWinRate:
    /// Thường 85 / Khó 60 / Siêu khó 40%) sang thước này - bot thắng ≥99% thì người thật ~90%, 50-90% thì ~80%, 10-50% thì
    /// ~40%, ≤2% thì ~30%. Thang cũ 35 / 12 / 5% là của bot thấy hết 15 bóng, khớp người thật kém (r 0.51, thước mới 0.73).
    /// </summary>
    public const float NormalTargetWinRate = 0.90f;
    public const float HardTargetWinRate = 0.50f;
    public const float SuperHardTargetWinRate = 0.20f;

    public static float PlayerTargetWinRate(int level, Core.GamePlay.LevelDifficulty difficulty)
    {
        // Level đầu vẫn theo band cũ (99% / 92%): bot như người thắng gần hết nên hai thang trùng nhau ở đó.
        if (level <= 9) return LevelBands.TargetWinRate(level, difficulty);
        return difficulty switch
        {
            Core.GamePlay.LevelDifficulty.Hard => HardTargetWinRate,
            Core.GamePlay.LevelDifficulty.SuperHard => SuperHardTargetWinRate,
            _ => NormalTargetWinRate
        };
    }

    /// <summary>Tỉ lệ dưới mức này thì đo thêm ván: 200 ván sai số ~±3 điểm, ở 5% là lệch gần gấp đôi.</summary>
    public const float LowWinRate = 0.2f;

    public const int LowWinPlayouts = 500;

    /// <summary>Số ván nên chạy khi tỉ lệ ước chừng là winRate: thấp thì chạy nhiều hơn cho số khỏi nhảy.</summary>
    public static int PlayoutsFor(float winRate, int normal = 200) =>
        winRate < LowWinRate ? Math.Max(normal, LowWinPlayouts) : normal;

    private struct Move
    {
        public BubbleEntry Bubble;
        public int FishId;
        public bool ToTank;
        public bool Known;
        public bool School;

        /// <summary>Cá link: số con của đàn phải lên ô chờ (không bể nào nhận) nếu bấm bây giờ.</summary>
        public int SchoolToHolding;
    }

    /// <summary>
    /// Cá link: đàn chỉ bấm được khi đủ chỗ cho CẢ đàn. Hai cách chơi sai đã thử:
    /// - Chấm đàn như cá thường theo con đầu đàn: hay đổ 2-3 con lẫn loài lên ô chờ.
    /// - Để dành đàn tới khi hết nước khác: suốt level bể lấy cá từ bóng lẻ, tới cuối bản sao cuối của loài cần chỉ
    ///   còn trong đàn, ô chờ đã chật, đàn không lọt - level 126 (6 đàn) bot thắng 0% dù solver giải chỉ với 2/5 ô chờ.
    /// Người chơi làm thế này: bể khớp đàn thì NUÔI BỂ TỪ ĐÀN trước bóng lẻ - đàn cần thời điểm, bóng lẻ lúc nào lấy
    /// cũng được. Bản Python: level 126 từ 0% lên 100 / 98 / 80 / 46% (lỡ tay 0 / 5 / 10 / 15%), level 101, 102 gần
    /// như không đổi.
    /// </summary>
    private const int SchoolHoldingAllowance = 1;

    /// <summary>Số ô chờ phải còn trống sau khi bấm đàn, để không tự khoá bàn.</summary>
    private const int SchoolHoldingReserve = 1;

    /// <summary>Nuôi bể từ đàn: có con vào bể, dư lên ô chờ không quá SchoolHoldingAllowance, sau đó vẫn còn chỗ trống.</summary>
    private static bool CanFeedFromSchool(Move m, BoardModel model) =>
        m.School && m.SchoolToHolding < m.Bubble.FishCount && m.SchoolToHolding <= SchoolHoldingAllowance &&
        model.HoldingFreeCount - m.SchoolToHolding >= SchoolHoldingReserve;

    /// <summary>Đàn bấm được mà không tự khoá bàn: sau khi đổ lên ô chờ vẫn còn chỗ trống.</summary>
    private static bool SchoolFits(Move m, BoardModel model) =>
        m.School && model.HoldingFreeCount - m.SchoolToHolding >= SchoolHoldingReserve;

    /// <summary>
    /// Lỡ tay: chọn bừa, nhưng không bấm nhầm vào đàn cá link - đàn nhìn khác hẳn bóng thường và ai cũng biết bấm
    /// là mất nhiều ô chờ, nên người chơi hiếm khi bấm nhầm nó. Chỉ còn toàn đàn thì mới bấm bừa vào đàn.
    /// </summary>
    private static Move PickMistake(List<Move> moves, List<Move> pool, Random rng)
    {
        pool.Clear();
        foreach (Move m in moves)
        {
            if (!m.School) pool.Add(m);
        }

        return pool.Count > 0 ? pool[rng.Next(pool.Count)] : moves[rng.Next(moves.Count)];
    }

    /// <summary>
    /// Chơi thử nhiều ván, trả về tỉ lệ thắng (0..1). mistakeRate = xác suất mỗi nước bấm bừa trong mọi nước hợp lệ;
    /// còn lại bot chơi như người: vào bể trước (ưu tiên bóng đang đè băng), không có thì phá bóng đè băng, không có
    /// băng thì bỏ lên ô chờ con sắp được cần nhất.
    /// </summary>
    /// <param name="knowsBoard">
    /// Lượt chơi lại: người chơi đã thấy bàn một lần nên biết loài của cá ẩn - bot coi cá ẩn như cá lộ khi chọn nước.
    /// </param>
    /// <param name="tankSlots">Số order mở; 0 = LevelBands.DefaultTankSlots (FTUE L1-L3 mở 3).</param>
    public static float EstimateWinRate(LevelData data, float mistakeRate = DefaultMistakeRate, int playouts = 200,
        int seed = 1, bool knowsBoard = false, int tankSlots = 0)
    {
        if (data == null || playouts <= 0) return 0f;

        var session = new Session(data, seed, knowsBoard, tankSlots);
        int wins = 0;
        for (int p = 0; p < playouts; p++)
        {
            if (session.Play(mistakeRate, out _, out _)) wins++;
        }

        return wins / (float)playouts;
    }

    /// <summary>Bộ đệm dùng lại giữa các ván, và vòng chơi một ván - chung cho EstimateWinRate và AnalyzeLosses.</summary>
    private sealed class Session
    {
        private readonly LevelData data;
        private readonly Random rng;
        private readonly List<Move> moves = new();
        private readonly List<Move> pool = new();
        private readonly HashSet<int> seen = new();
        private readonly HashSet<BubbleEntry> blockers = new();
        private readonly List<SchoolMove> plan = new();
        private readonly bool knowsBoard;
        private readonly int tankSlots;

        // Bộ đệm riêng cho nhìn trước: ván thử chạy Generate / Choose lồng bên trong lúc danh sách nước thật còn dùng.
        private readonly List<Move> candidates = new();
        private readonly List<Move> lookMoves = new();
        private readonly List<Move> lookPool = new();
        private readonly HashSet<int> lookSeen = new();
        private readonly HashSet<BubbleEntry> lookBlockers = new();
        private readonly List<SchoolMove> lookPlan = new();

        /// <param name="tankSlots">Số order mở; 0 = LevelBands.DefaultTankSlots (FTUE L1-L3 mở 3).</param>
        public Session(LevelData data, int seed, bool knowsBoard = false, int tankSlots = 0)
        {
            this.data = data;
            this.knowsBoard = knowsBoard;
            this.tankSlots = tankSlots > 0 ? tankSlots : LevelBands.DefaultTankSlots;
            rng = new Random(seed);
        }

        /// <summary>Số nước của ván vừa chơi.</summary>
        public int MoveCount { get; private set; }

        /// <summary>Số nước lỡ tay của ván vừa chơi.</summary>
        public int MistakeCount { get; private set; }

        /// <summary>Thứ tự (0-based) của nước lỡ tay cuối cùng trong ván vừa chơi; -1 khi không lỡ tay lần nào.</summary>
        public int LastMistakeAt { get; private set; }

        /// <summary>Chơi một ván; trả model lúc dừng (thắng hoặc kẹt) để AnalyzeLosses soi.</summary>
        public bool Play(float mistakeRate, out BoardModel model, out Dictionary<BubbleEntry, int> columns)
        {
            model = new BoardModel(data, tankSlots, Math.Max(tankSlots, LevelBands.DefaultTankSlots),
                LevelBands.DefaultHoldingSlots, LevelBands.DefaultHoldingSlots, LevelBands.VisibleBubbles);
            columns = AssignColumns(model);
            MoveCount = 0;
            MistakeCount = 0;
            LastMistakeAt = -1;
            int guard = 0;
            while (!model.IsWin && guard++ < 1000)
            {
                Generate(model, columns, moves, seen, plan, knowsBoard);
                if (moves.Count == 0) break;

                bool mistake = rng.NextDouble() < mistakeRate * MistakeScale(model);
                Move pick = mistake
                    ? PickMistake(moves, pool, rng)
                    : ChooseLooking(model, columns);
                if (mistake)
                {
                    MistakeCount++;
                    LastMistakeAt = MoveCount;
                }

                MoveCount++;
                if (!Apply(model, pick, plan)) break;
            }

            return model.IsWin;
        }

        /// <summary>
        /// Nước "như người": luật cứng của Choose, cộng nhìn trước ở nước khó. Nước khó = không có cá vào bể chắc chắn,
        /// không nuôi bể được từ đàn, và ô chờ đã căng (còn ≤ LookAheadFreeHolding chỗ) - lúc người chơi dừng lại tính.
        /// Thử LookAheadCandidates nước tốt nhất theo luật cứng: mỗi nước đi thử rồi chơi tiếp LookAheadDepth nước
        /// bằng luật cứng (không lỡ tay), chấm trạng thái cuối (Evaluate), chọn nước điểm cao nhất; hoà thì giữ nước
        /// của luật cứng. Bàn được Snapshot / Restore tại chỗ nên các BubbleEntry (và bảng cột băng) giữ nguyên.
        /// </summary>
        private Move ChooseLooking(BoardModel model, Dictionary<BubbleEntry, int> columns)
        {
            Move greedy = Choose(model, columns, moves, pool, blockers, rng);
            if (!LookAhead || model.HoldingFreeCount > LookAheadFreeHolding || IsEasyDecision(model, moves)) return greedy;

            candidates.Clear();
            candidates.Add(greedy);
            foreach (Move m in moves)
            {
                if (candidates.Count >= LookAheadCandidates) break;
                if (!m.Known || (m.School && !SchoolFits(m, model))) continue;
                if (m.Bubble == greedy.Bubble && m.FishId == greedy.FishId) continue;
                candidates.Add(m);
            }

            if (candidates.Count < 2) return greedy;

            // Nước theo thứ tự của luật cứng (HoldingScore thấp trước) để khi phải cắt bớt thì giữ nước đáng thử nhất.
            candidates.Sort(1, candidates.Count - 1,
                Comparer<Move>.Create((a, b) => HoldingScore(model, a).CompareTo(HoldingScore(model, b))));

            BoardSnapshot snapshot = model.Snapshot();
            Move best = greedy;
            int bestScore = int.MinValue;
            foreach (Move candidate in candidates)
            {
                int score = Apply(model, candidate, lookPlan) ? Rollout(model, columns) : int.MinValue;
                model.Restore(snapshot);
                if (score <= bestScore) continue;

                bestScore = score;
                best = candidate;
            }

            return best;
        }

        private int Rollout(BoardModel model, Dictionary<BubbleEntry, int> columns)
        {
            for (int depth = 0; depth < LookAheadDepth; depth++)
            {
                if (model.IsWin) break;
                Generate(model, columns, lookMoves, lookSeen, lookPlan, knowsBoard);
                if (lookMoves.Count == 0) return Evaluate(model, true);

                Move next = Choose(model, columns, lookMoves, lookPool, lookBlockers, rng);
                if (!Apply(model, next, lookPlan)) return Evaluate(model, true);
            }

            return Evaluate(model, false);
        }
    }

    // ---------- Nhìn trước ----------

    private const string LookAheadPrefKey = "FishSort.LevelHumanBot.LookAhead";
    private static bool? lookAhead;

    /// <summary>
    /// Bật (mặc định): bot nhìn trước ở nước khó (ChooseLooking). Người chơi kỹ không bấm theo luật cứng lúc ô chờ căng
    /// mà tính "bấm con này thì vài nước sau ra sao" - thiếu bước này bot thua cả level người chơi kỹ qua ngay lượt đầu
    /// (level 254: lỡ tay 0% mà bot vẫn thua ~46%). Chậm hơn vài lần ở level khó. Lưu EditorPrefs; tắt để so cách cũ.
    /// </summary>
    public static bool LookAhead
    {
        get => lookAhead ??= UnityEditor.EditorPrefs.GetBool(LookAheadPrefKey, true);
        set
        {
            lookAhead = value;
            UnityEditor.EditorPrefs.SetBool(LookAheadPrefKey, value);
        }
    }

    /// <summary>Chỉ nhìn trước khi ô chờ còn từng này chỗ trống trở xuống - bàn thoáng thì luật cứng là đủ.</summary>
    private const int LookAheadFreeHolding = 3;

    private const int LookAheadCandidates = 4;
    private const int LookAheadDepth = 6;

    /// <summary>Nước dễ: có cá vào bể chắc chắn hoặc nuôi được bể từ đàn - luật cứng đi luôn, không cần tính.</summary>
    private static bool IsEasyDecision(BoardModel model, List<Move> moves)
    {
        foreach (Move m in moves)
        {
            if ((m.ToTank && m.Known) || CanFeedFromSchool(m, model)) return true;
        }

        return false;
    }

    /// <summary>
    /// Điểm trạng thái sau ván thử: thắng là cao nhất, kẹt là thấp nhất; còn lại tính cá đã vào bể (Collected + cá
    /// đang nằm trong bể) và chỗ trống ô chờ. Ô chờ trống nặng ký ngang gần một con cá vào bể: còn chỗ thì còn đường.
    /// </summary>
    private static int Evaluate(BoardModel model, bool stuck)
    {
        if (model.IsWin) return 1000000;

        int inTanks = 0;
        foreach (TankEntry tank in model.Tanks) inTanks += tank.Count;
        int score = (model.Collected + inTanks) * 10 + model.HoldingFreeCount * 6;
        return stuck ? score - 1000000 : score;
    }

    // ---------- Hay thua ở đâu ----------

    /// <summary>Chỗ cá mà bể đang cần nằm lúc bot kẹt.</summary>
    public enum NeedSpot
    {
        /// <summary>Đang hiện và bấm được - kẹt vì ô chờ đầy (ô chờ đầy là khoá bấm cả nước đúng).</summary>
        Visible,
        Ice,
        HpShell,
        Hidden,
        SpawnerQueue,
        InnerLayer,

        /// <summary>Nằm trong đàn cá link mà đàn không lọt (thiếu ô chờ cho cả đàn).</summary>
        School,

        /// <summary>Trong bóng đang khoá: ổ chờ bóng chìa nổ, hoặc khoá đơn chờ xong bể đúng loài.</summary>
        Locked,

        /// <summary>Đã lên bàn nhưng nằm dưới thanh booster (LevelSolver.VisibleArea): người chơi không thấy để bấm.</summary>
        BelowScreen,

        /// <summary>Chưa lên bàn: nằm ở bóng ngoài VisibleBubbles bóng đầu.</summary>
        NotOnBoard
    }

    /// <summary>
    /// Lỡ tay trong bấy nhiêu nước cuối trước lúc kẹt thì tính ván đó là "thua do lỡ tay". Một nước bấm nhầm chiếm 1 ô
    /// chờ; 5 ô chờ nên ảnh hưởng của nó thường lộ ra trong vài nước sau.
    /// </summary>
    public const int RecentMistakeMoves = 5;

    /// <summary>Tổng hợp các ván thua: thua ở đơn nào, chặng nào, cá cần nằm đâu, bóng nào giữ cá cần mà không lấy được.</summary>
    public sealed class LossReport
    {
        public int Playouts;
        public int Losses;
        public int OrderCount;
        public float MistakeRate;

        /// <summary>
        /// Số ván thua mà nước lỡ tay cuối cùng nằm trong RecentMistakeMoves nước trước lúc kẹt: thua vì vừa bấm nhầm.
        /// Phần còn lại (không lỡ tay, hoặc lỡ tay từ lâu) là thua vì bàn - bẫy của level.
        /// </summary>
        public int RecentMistakeLosses;

        /// <summary>Số ván thua mà đơn (index trong OrderQueue) này đang mở ở bể lúc kẹt.</summary>
        public readonly Dictionary<int, int> OrderHits = new();

        /// <summary>Số ván thua mà bóng (index trong SoftBubbleDataArray) này đang giữ cá cần nhưng không lấy được.</summary>
        public readonly Dictionary<int, int> BubbleHits = new();

        public readonly Dictionary<NeedSpot, int> Spots = new();

        /// <summary>Số ván thua theo 5 chặng tiến độ (0-20%, 20-40%...).</summary>
        public readonly int[] Phases = new int[5];
    }

    /// <summary>
    /// Chơi nhiều ván và soi từng ván thua ngay lúc kẹt: 2 bể đang mở đơn nào (đơn nào hay "giết" người chơi), và cá
    /// bể cần đang nằm đâu - trong băng, vỏ HP, cá ẩn, hàng chờ spawner, lớp trong, chưa lên bàn, hay đang hiện mà ô
    /// chờ đã đầy. Bóng nào giữ cá cần mà không lấy được thì cộng điểm cho bóng đó.
    /// Chỗ thua là chỗ BOT thua: loại "bức tường" do cá không lấy được (spawner, cá ẩn, băng) thì đúng cả với người;
    /// loại "ô chờ đầy" thì phụ thuộc cách chọn cá lên ô chờ, người có thể chơi khác.
    /// </summary>
    public static LossReport AnalyzeLosses(LevelData data, float mistakeRate, int playouts = 300, int seed = 7)
    {
        var report = new LossReport
        {
            Playouts = playouts, OrderCount = data?.OrderQueue?.Length ?? 0, MistakeRate = mistakeRate
        };
        if (data == null || playouts <= 0 || report.OrderCount == 0) return report;

        int[] queue = data.OrderQueue;
        var session = new Session(data, seed);
        var used = new HashSet<int>();
        var blockedIn = new HashSet<int>();
        for (int p = 0; p < playouts; p++)
        {
            if (session.Play(mistakeRate, out BoardModel model, out Dictionary<BubbleEntry, int> columns)) continue;

            report.Losses++;
            if (session.LastMistakeAt >= 0 && session.MoveCount - session.LastMistakeAt <= RecentMistakeMoves)
            {
                report.RecentMistakeLosses++;
            }

            int taken = queue.Length - model.OrderQueue.Count;
            int open = 0;
            used.Clear();
            blockedIn.Clear();
            foreach (TankEntry tank in model.Tanks)
            {
                if (!tank.HasOrder || tank.Count >= tank.Goal) continue;
                open++;

                // Đơn đang mở của bể = lần gần nhất loài này được rút khỏi hàng đơn mà chưa gán cho bể khác.
                for (int i = taken - 1; i >= 0; i--)
                {
                    if (queue[i] != tank.FishId || !used.Add(i)) continue;
                    report.OrderHits[i] = report.OrderHits.TryGetValue(i, out int hit) ? hit + 1 : 1;
                    break;
                }

                LocateNeeded(model, columns, tank.FishId, tank.Goal - tank.Count, report, blockedIn);
            }

            foreach (int bubble in blockedIn)
            {
                report.BubbleHits[bubble] = report.BubbleHits.TryGetValue(bubble, out int hit) ? hit + 1 : 1;
            }

            int done = Math.Max(0, taken - open);
            report.Phases[Math.Min(4, done * 5 / queue.Length)]++;
        }

        return report;
    }

    /// <summary>Tìm từng con cá loài fishId mà bể còn thiếu: nằm ở đâu, bóng nào đang giữ nó mà không lấy được.</summary>
    private static void LocateNeeded(BoardModel model, Dictionary<BubbleEntry, int> columns, int fishId, int missing,
        LossReport report, HashSet<int> blockedIn)
    {
        float covered = 0f;
        foreach (BubbleEntry b in model.Bubbles)
        {
            if (missing <= 0) return;
            if (b.Popped) continue;

            bool onBoard = model.IsFront(b);
            bool below = onBoard && LevelSolver.BelowScreen(b, ref covered);   // Bubbles theo thứ tự sinh, như FrontBubbles
            if (b.IsRock) continue;
            foreach (FishEntry fish in b.Fishes)
            {
                if (missing <= 0) return;
                if (fish.FishId != fishId) continue;
                NeedSpot spot = !onBoard ? NeedSpot.NotOnBoard
                    : below ? NeedSpot.BelowScreen
                    : b.Hp > 0 ? NeedSpot.HpShell
                    : b.IsGated ? NeedSpot.Locked
                    : b.IsFrozen && !IceUncovered(model, b, columns) ? NeedSpot.Ice
                    : fish.IsHidden ? NeedSpot.Hidden
                    : b.IsFishSchool ? NeedSpot.School
                    : NeedSpot.Visible;
                CountNeeded(spot, b, report, blockedIn, ref missing);
            }

            foreach (FishEntry fish in b.SpawnerFishes)
            {
                if (missing <= 0) return;
                if (fish.FishId != fishId) continue;
                CountNeeded(!onBoard ? NeedSpot.NotOnBoard : below ? NeedSpot.BelowScreen : NeedSpot.SpawnerQueue, b, report, blockedIn, ref missing);
            }

            foreach (List<FishEntry> layer in b.Layers)
            {
                foreach (FishEntry fish in layer)
                {
                    if (missing <= 0) return;
                    if (fish.FishId != fishId) continue;
                    CountNeeded(!onBoard ? NeedSpot.NotOnBoard : below ? NeedSpot.BelowScreen : NeedSpot.InnerLayer, b, report, blockedIn, ref missing);
                }
            }
        }
    }

    private static void CountNeeded(NeedSpot spot, BubbleEntry bubble, LossReport report, HashSet<int> blockedIn,
        ref int missing)
    {
        missing--;
        report.Spots[spot] = report.Spots.TryGetValue(spot, out int n) ? n + 1 : 1;

        // Cá đang hiện thì bóng không có lỗi gì - kẹt vì ô chờ đầy - nên không tô bóng đó.
        if (spot != NeedSpot.Visible) blockedIn.Add(bubble.Index);
    }

    // ---------- Chọn nước như người ----------

    private static Move Choose(BoardModel model, Dictionary<BubbleEntry, int> columns, List<Move> moves,
        List<Move> pool, HashSet<BubbleEntry> blockers, Random rng)
    {
        CollectIceBlockers(model, columns, blockers);

        // 0. Nuôi bể từ đàn cá link trước bóng lẻ, đàn dư ít cá lên ô chờ nhất trước (xem CanFeedFromSchool).
        pool.Clear();
        int leastSpill = int.MaxValue;
        foreach (Move m in moves)
        {
            if (CanFeedFromSchool(m, model)) leastSpill = Math.Min(leastSpill, m.SchoolToHolding);
        }

        foreach (Move m in moves)
        {
            if (CanFeedFromSchool(m, model) && m.SchoolToHolding == leastSpill) pool.Add(m);
        }

        if (pool.Count > 0) return pool[rng.Next(pool.Count)];

        // 1. Có nước vào bể (loài biết rõ) thì đi - bóng đang đè băng trước.
        foreach (Move m in moves)
        {
            if (m.ToTank && m.Known && blockers.Contains(m.Bubble)) pool.Add(m);
        }

        if (pool.Count == 0)
        {
            foreach (Move m in moves)
            {
                if (m.ToTank && m.Known) pool.Add(m);
            }
        }

        if (pool.Count > 0) return pool[rng.Next(pool.Count)];

        // 2. Không có nước vào bể: còn dư ô chờ thì phá bóng đang đè băng, bóng ít cá trước (sắp nổ). Không phá băng
        //    bằng một đàn cá link lẫn loài: đổi 2-3 ô chờ lấy một bóng băng là lỗ, còn bóng khác thì phá bóng khác.
        if (blockers.Count > 0 && model.HoldingFreeCount >= 2)
        {
            int least = int.MaxValue;
            foreach (Move m in moves)
            {
                if (blockers.Contains(m.Bubble) && !m.School) least = Math.Min(least, m.Bubble.TotalFishCount);
            }

            pool.Clear();
            foreach (Move m in moves)
            {
                if (blockers.Contains(m.Bubble) && !m.School && m.Bubble.TotalFishCount == least) pool.Add(m);
            }

            if (pool.Count > 0) return pool[rng.Next(pool.Count)];
        }

        // 3. Bỏ lên ô chờ con sắp được cần nhất, bóng ít cá trước - như thứ tự ưu tiên của LevelSolver. Đàn cá link
        //    được xét khi bấm xong vẫn còn ô trống (SchoolFits) - dọn đàn lúc ô chờ còn rộng, xếp ngang loài bể đang
        //    cần; đàn không lọt thì chỉ bấm khi hết nước khác, và chọn đàn đổ ít cá lên ô chờ nhất.
        int best = int.MaxValue;
        foreach (Move m in moves)
        {
            if (!m.School || SchoolFits(m, model)) best = Math.Min(best, HoldingScore(model, m));
        }

        pool.Clear();
        foreach (Move m in moves)
        {
            if ((!m.School || SchoolFits(m, model)) && HoldingScore(model, m) == best) pool.Add(m);
        }

        if (pool.Count > 0) return pool[rng.Next(pool.Count)];

        int fewest = int.MaxValue;
        foreach (Move m in moves) fewest = Math.Min(fewest, m.SchoolToHolding);
        foreach (Move m in moves)
        {
            if (m.SchoolToHolding == fewest) pool.Add(m);
        }

        return pool[rng.Next(pool.Count)];
    }

    /// <summary>
    /// Hạng của cá ẩn khi chọn cá lên ô chờ: 0 = ngang loài bể đang cần, tức dám bấm thử. Đo trên level 144/150/140:
    /// hạng 0 cho số gần người chơi nhất; hạng càng cao bot càng né cá ẩn và số tụt về 0%.
    /// </summary>
    private const int HiddenRank = 0;

    /// <summary>Loài sắp cần trước (cá ẩn: bấm thử, xem HiddenRank), rồi bóng ít cá trước.</summary>
    private static int HoldingScore(BoardModel model, Move m)
    {
        // Đàn cá link tới được đây là đàn "đáng" (có con vào bể), nên xếp ngang loài bể đang cần.
        int rank = m.School ? 0 : m.Known ? UpcomingRank(model, m.FishId) : HiddenRank;
        return rank * 10 + m.Bubble.TotalFishCount;
    }

    /// <summary>Bóng cá đang đè lên một bóng băng còn đóng: cùng cột, sinh trước, còn cá.</summary>
    private static void CollectIceBlockers(BoardModel model, Dictionary<BubbleEntry, int> columns,
        HashSet<BubbleEntry> blockers)
    {
        blockers.Clear();
        foreach (BubbleEntry ice in model.FrontBubbles)
        {
            if (!ice.IsFrozen || ice.FishCount == 0) continue;
            foreach (BubbleEntry front in model.FrontBubbles)
            {
                if (front == ice || front.IsRock || front.FishCount == 0) continue;
                if (front.SpawnOrder < ice.SpawnOrder && columns[front] == columns[ice]) blockers.Add(front);
            }
        }
    }

    /// <summary>0 = có bể đang cần, k = vị trí trong hàng đơn, 50 = không cần.</summary>
    private static int UpcomingRank(BoardModel model, int fishId)
    {
        foreach (TankEntry t in model.Tanks)
        {
            if (t.HasOrder && t.FishId == fishId) return 0;
        }

        int index = model.OrderQueue.IndexOf(fishId);
        return index >= 0 ? index + 1 : 50;
    }

    // ---------- Luật bàn: chép từ LevelSolver để hai bot chơi cùng một bàn ----------

    private static void Generate(BoardModel model, Dictionary<BubbleEntry, int> columns, List<Move> moves,
        HashSet<int> seen, List<SchoolMove> plan, bool knowsBoard = false)
    {
        moves.Clear();
        if (model.HoldingFreeCount == 0) return;   // ô chờ đầy → khoá bấm, như LevelSolver.GenerateMoves

        float covered = 0f;
        foreach (BubbleEntry front in model.FrontBubbles)
        {
            if (LevelSolver.BelowScreen(front, ref covered)) break;   // dưới thanh booster: không thấy để bấm
            if (front.IsRock || front.Hp > 0) continue;
            if (front.IsGated) continue;   // ổ khoá / khoá đơn: game chặn bấm cho tới khi mở (BubbleEntry.IsTappable)
            if (front.IsFrozen && !IceUncovered(model, front, columns)) continue;

            if (front.IsFishSchool)
            {
                plan.Clear();
                if (!model.TryPlanSchool(front, 0, plan)) continue;
                int toHolding = 0;
                foreach (SchoolMove m in plan)
                {
                    if (m.Tank == null) toHolding++;
                }

                moves.Add(new Move
                {
                    Bubble = front, FishId = front.Fishes[0].FishId, ToTank = toHolding == 0, Known = true,
                    School = true, SchoolToHolding = toHolding
                });
                continue;
            }

            seen.Clear();
            foreach (FishEntry fish in front.Fishes)
            {
                if (!seen.Add(fish.FishId)) continue;
                moves.Add(new Move
                {
                    Bubble = front, FishId = fish.FishId, ToTank = model.FindTankFor(fish.FishId) != null,
                    Known = knowsBoard || !fish.IsHidden
                });
            }
        }
    }

    private static bool Apply(BoardModel model, Move move, List<SchoolMove> plan)
    {
        if (move.School) return ApplySchool(model, move.Bubble, plan);

        int index = move.Bubble.IndexOfFish(move.FishId);
        if (index < 0) return false;
        if (move.Bubble.IsFrozen) model.TryBreakIce(move.Bubble);
        int id = model.RemoveFishAt(move.Bubble, index).FishId;
        if (move.Bubble.Popped) model.TakeNextFromQueue();
        else model.TryAutoRevealAll(move.Bubble);   // như BoardManager: bóng chỉ còn cá ẩn thì lộ hết

        TankEntry tank = model.FindTankFor(id);
        if (tank != null)
        {
            model.ReserveTank(tank);
            model.ArriveAtTank(tank);
            Drain(model);
            return true;
        }

        int holding = model.FindFreeHoldingIndex();
        if (holding < 0) return false;
        model.ReserveHolding(holding);
        model.ArriveAtHolding(holding, id);
        Drain(model);
        return true;
    }

    private static bool ApplySchool(BoardModel model, BubbleEntry bubble, List<SchoolMove> plan)
    {
        if (bubble.IsFrozen) model.TryBreakIce(bubble);
        plan.Clear();
        if (!model.TryPlanSchool(bubble, 0, plan)) return false;
        foreach (SchoolMove m in plan)
        {
            if (m.Tank != null) model.ReserveTank(m.Tank);
            else model.ReserveHolding(m.HoldingIndex);
        }

        plan.Sort((a, b) => b.FishIndex.CompareTo(a.FishIndex));
        foreach (SchoolMove m in plan)
        {
            int id = model.RemoveFishAt(bubble, m.FishIndex).FishId;
            if (m.Tank != null) model.ArriveAtTank(m.Tank);
            else model.ArriveAtHolding(m.HoldingIndex, id);
        }

        if (bubble.Popped) model.TakeNextFromQueue();
        else model.TryAutoRevealAll(bubble);
        Drain(model);
        return true;
    }

    private static void Drain(BoardModel model)
    {
        int guard = 0;
        while (guard++ < 256)
        {
            bool progressed = false;
            foreach (TankEntry tank in model.Tanks)
            {
                if (!tank.IsComplete) continue;
                model.CompleteTank(tank);
                progressed = true;
            }

            if (model.TryFindAutoCollect(out int holdingIndex, out TankEntry target))
            {
                model.TakeFromHolding(holdingIndex);
                model.ReserveTank(target);
                model.ArriveAtTank(target);
                progressed = true;
            }

            if (!progressed) return;
        }
    }

    private static Dictionary<BubbleEntry, int> AssignColumns(BoardModel model)
    {
        float sum = 0f;
        foreach (BubbleEntry b in model.Bubbles) sum += Diameter(b);
        float avg = model.Bubbles.Count > 0 ? sum / model.Bubbles.Count : 2f * LevelBands.BubbleRadiusMax;
        int columnCount = Math.Max(1,
            (int)Math.Round(LevelBands.TopRowWidth * LevelBands.TopRowFill / Math.Max(0.1f, avg)));
        var height = new float[columnCount];
        var map = new Dictionary<BubbleEntry, int>(model.Bubbles.Count);
        foreach (BubbleEntry b in model.Bubbles)
        {
            int best = 0;
            for (int c = 1; c < columnCount; c++)
            {
                if (height[c] < height[best]) best = c;
            }

            map[b] = best;
            height[best] += Diameter(b);
        }

        return map;
    }

    private static bool IceUncovered(BoardModel model, BubbleEntry ice, Dictionary<BubbleEntry, int> columns)
    {
        int column = columns[ice];
        foreach (BubbleEntry front in model.FrontBubbles)
        {
            if (front == ice || front.IsRock || front.FishCount == 0) continue;
            if (front.SpawnOrder < ice.SpawnOrder && columns[front] == column) return false;
        }

        return true;
    }

    private static float Diameter(BubbleEntry entry) =>
        2f * (entry.Radius > 0f ? entry.Radius : LevelBands.BubbleRadiusMax);
}
