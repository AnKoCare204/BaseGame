using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Bộ giải seed (GEN-02): mô phỏng bàn chơi thuần C# trên BoardModel (cá tới bể tức thời, bể xong thì đơn kế FIFO + tự thu cá
/// từng con; ô chờ đầy thì không bấm được, theo luật WordSort 2026-09-13),
/// tìm đường đi bằng DFS có sắp thứ tự nước đi và bộ nhớ trạng thái. Trả về số ô chờ tối đa đã dùng trên đường tìm được
/// (MinHoldingUsed của seed) và số nước. Kèm ước lượng tỉ lệ thắng (BAL-02) bằng người chơi mô phỏng chọn nước ngẫu nhiên
/// có thiên hướng (ưu tiên cá vào bể). Dùng cho tool sinh level, DLB và Hint nâng cao.
/// </summary>
public static class LevelSolver
{
    public class Result
    {
        public bool Solved;

        /// <summary>
        /// Không giải được theo quy ước băng, nhưng giải được khi coi băng vỡ tự do: level chỉ mong manh ở phần băng. Quy ước băng của
        /// solver là xấp xỉ (không có vị trí) nên đây là cảnh báo, không phải kết luận hỏng. Moves / MaxHoldingUsed / Path khi đó là
        /// của lượt bỏ băng.
        /// </summary>
        public bool SolvedIgnoringIce;

        public int Moves;
        public int MaxHoldingUsed;
        public int NodesExplored;
        public bool NodeCapHit;
        public readonly List<(int bubbleIndex, int fishId)> Path = new();
    }

    private struct Move
    {
        public BubbleEntry Bubble;
        public int FishId;
        public bool ToTank;
        public int Priority;
        /// <summary>FishSchoolBubble: cả đàn bay (FishId = con đầu, ToTank = cả đàn vào bể).</summary>
        public bool School;
    }

    // ---------- Giải chính xác ----------

    /// <summary>
    /// Lượt một theo quy ước băng (IceReachesTop). Level có băng mà không giải được thì chạy lượt hai coi băng vỡ tự do: giải được ở lượt
    /// hai thì SolvedIgnoringIce = true (level mong manh ở phần băng), không thì kết luận không giải được.
    /// </summary>
    public static Result Solve(LevelData data, int maxNodes = 30000, int maxHoldingAllowed = -1)
    {
        var result = new Result();
        if (data == null) return result;
        RunSearch(data, maxNodes, maxHoldingAllowed, false, result);
        if (result.Solved || !HasIce(data)) return result;

        var relaxed = new Result();
        RunSearch(data, maxNodes, maxHoldingAllowed, true, relaxed);
        result.NodesExplored += relaxed.NodesExplored;
        if (!relaxed.Solved) return result;
        result.SolvedIgnoringIce = true;
        result.Moves = relaxed.Moves;
        result.MaxHoldingUsed = relaxed.MaxHoldingUsed;
        result.Path.AddRange(relaxed.Path);
        return result;
    }

    private static void RunSearch(LevelData data, int maxNodes, int maxHoldingAllowed, bool ignoreIce, Result result)
    {
        var model = NewModel(data);
        Dictionary<BubbleEntry, int> columns = AssignColumns(model);
        int holdingCap = maxHoldingAllowed > 0 ? maxHoldingAllowed : model.HoldingSlots;
        var visited = new HashSet<string>();
        var path = new List<(int, int)>();
        int nodes = 0;
        int best = 0;
        bool solved = Dfs(model, visited, path, ref nodes, maxNodes, holdingCap, 0, ref best, ignoreIce, columns);
        result.Solved = solved;
        result.NodesExplored = nodes;
        result.NodeCapHit = nodes >= maxNodes && !solved;
        if (solved)
        {
            result.Moves = path.Count;
            result.MaxHoldingUsed = best;
            result.Path.AddRange(path);
        }
    }

    private static bool HasIce(LevelData data)
    {
        if (data.SoftBubbleDataArray == null) return false;
        foreach (SoftBubbleData b in data.SoftBubbleDataArray)
        {
            if (b != null && b.isIce && !b.isRock) return true;
        }

        return false;
    }

    private static bool Dfs(BoardModel model, HashSet<string> visited, List<(int, int)> path, ref int nodes, int maxNodes,
        int holdingCap, int holdingUsed, ref int bestHolding, bool ignoreIce, Dictionary<BubbleEntry, int> columns)
    {
        if (model.IsWin)
        {
            bestHolding = holdingUsed;
            return true;
        }

        if (nodes >= maxNodes) return false;
        nodes++;
        if (!visited.Add(StateKey(model))) return false;

        List<Move> moves = GenerateMoves(model, ignoreIce, columns);
        if (moves.Count == 0) return false;
        moves.Sort((a, b) => a.Priority.CompareTo(b.Priority));

        foreach (Move move in moves)
        {
            BoardSnapshot snapshot = model.Snapshot();
            int usedAfter = holdingUsed;
            if (!Apply(model, move, ref usedAfter) || usedAfter > holdingCap)
            {
                model.Restore(snapshot);
                continue;
            }

            path.Add((move.Bubble.Index, move.FishId));
            if (Dfs(model, visited, path, ref nodes, maxNodes, holdingCap, usedAfter, ref bestHolding, ignoreIce, columns)) return true;
            path.RemoveAt(path.Count - 1);
            model.Restore(snapshot);
            if (nodes >= maxNodes) return false;
        }

        return false;
    }

    // ---------- Ước lượng tỉ lệ thắng ----------

    /// <summary>
    /// Chơi thử nhiều ván bằng "người chơi mô phỏng": có nước vào bể thì chọn ngẫu nhiên một nước vào bể (xác suất
    /// tankBias), còn lại chọn ngẫu nhiên trong mọi nước hợp lệ; kẹt = thua. Trả về tỉ lệ ván thắng (0..1).
    /// </summary>
    public static float EstimateWinRate(LevelData data, int playouts = 200, int seed = 1, float tankBias = 0.85f)
    {
        if (data == null || playouts <= 0) return 0f;
        var rng = new Random(seed);
        int wins = 0;
        var holding = new List<Move>();
        var tank = new List<Move>();
        for (int p = 0; p < playouts; p++)
        {
            var model = NewModel(data);
            Dictionary<BubbleEntry, int> columns = AssignColumns(model);
            int guard = 0;
            while (!model.IsWin && guard++ < 1000)
            {
                List<Move> moves = GenerateMoves(model, false, columns);
                if (moves.Count == 0) break;
                tank.Clear();
                holding.Clear();
                foreach (Move m in moves) (m.ToTank ? tank : holding).Add(m);
                Move pick;
                if (tank.Count > 0 && (holding.Count == 0 || rng.NextDouble() < tankBias)) pick = tank[rng.Next(tank.Count)];
                else pick = moves[rng.Next(moves.Count)];
                int unused = 0;
                if (!Apply(model, pick, ref unused)) break;
            }

            if (model.IsWin) wins++;
        }

        return wins / (float)playouts;
    }

    // ---------- Vùng nhìn thấy ----------

    /// <summary>
    /// Diện tích bóng (đơn vị thế giới²) nhìn thấy trên màn 9:16 (chuẩn 1080x1920): từ đỉnh vùng chơi tới mép trên nền thanh
    /// booster. Bóng sinh trước nổi cao hơn, nên bóng thấy được là các bóng trước cùng theo thứ tự sinh, cộng dồn πr² (bóng cuối
    /// tính nửa, tức tâm còn trên mép) tới mức này; bóng sau đó nằm dưới thanh booster nên không bấm được. Đo trong Play
    /// 2026-10-16: 10-13 trên 15 bóng, khớp nhất ở ~165. Mặc định bật (DefaultVisibleArea) cho mọi số đo của tool level;
    /// 0 = mọi bóng trước cùng đều bấm được (cách cũ, khớp winrate thật kém hơn: r 0.51 so với 0.73).
    /// </summary>
    public static float VisibleArea { get; set; } = DefaultVisibleArea;

    public const float DefaultVisibleArea = 165f;

    /// <summary>Gọi lần lượt theo FrontBubbles với cùng biến covered: true = bóng này (và mọi bóng sau) nằm dưới màn.</summary>
    public static bool BelowScreen(BubbleEntry front, ref float covered)
    {
        if (VisibleArea <= 0f) return false;
        float r = front.Radius > 0f ? front.Radius : 0.5f * (LevelBands.BubbleRadiusMin + LevelBands.BubbleRadiusMax);
        float area = (float)Math.PI * r * r;
        bool below = covered + 0.5f * area > VisibleArea;
        covered += area;
        return below;
    }

    // ---------- Chung ----------

    /// <summary>Bàn theo luật cố định: bể mở sẵn, ô chờ mặc định, không booster.</summary>
    private static BoardModel NewModel(LevelData data)
    {
        int tanks = LevelBands.DefaultTankSlots;
        int holding = LevelBands.DefaultHoldingSlots;
        return new BoardModel(data, tanks, tanks, holding, holding, LevelBands.VisibleBubbles);
    }

    /// <summary>
    /// Nước đi hợp lệ: mỗi bóng đang hiện, mỗi loài một lần. Ưu tiên: hoàn thành bể → vào bể → ô chờ (loài sắp cần trước, bóng ít cá trước).
    /// ignoreIce = coi băng vỡ tự do (lượt hai của Solve).
    /// </summary>
    private static List<Move> GenerateMoves(BoardModel model, bool ignoreIce, Dictionary<BubbleEntry, int> columns)
    {
        var moves = new List<Move>();
        int freeHolding = model.HoldingFreeCount;
        if (freeHolding == 0) return moves;   // ô chờ đầy → khóa bấm (luật WordSort); chỉ tự thu (Drain) mới gỡ
        List<SchoolMove> schoolPlan = null;
        float covered = 0f;
        foreach (BubbleEntry front in model.FrontBubbles)
        {
            if (BelowScreen(front, ref covered)) break;   // từ đây trở đi nằm dưới thanh booster, người chơi không thấy
            if (front.IsRock || front.Hp > 0) continue;   // đá không có cá; vỏ HP còn thì chưa bấm được (model trừ HP chính xác khi bể xong)
            if (front.IsGated) continue;   // ổ khoá (chờ bóng chìa nổ) / khoá đơn (chờ xong bể đúng loài): runtime chặn bấm (IsTappable), model tự mở khi đủ điều kiện
            if (front.IsFrozen && !ignoreIce && !IceUncovered(model, front, columns)) continue;
            if (front.IsFishSchool)
            {
                // FishSchoolBubble: một nước = cả đàn bay (bấm con nào cũng vậy), chỉ khi đủ chỗ cho cả đàn. Cả đàn vào bể thì ưu tiên
                // như hoàn thành bể; còn con phải lên ô chờ thì xếp sau các nước vào bể, càng nhiều con lên ô chờ càng sau.
                schoolPlan ??= new List<SchoolMove>();
                if (!model.TryPlanSchool(front, 0, schoolPlan)) continue;
                int toHolding = 0;
                foreach (SchoolMove m in schoolPlan)
                {
                    if (m.Tank == null) toHolding++;
                }

                moves.Add(new Move
                {
                    Bubble = front, FishId = front.Fishes[0].FishId, ToTank = toHolding == 0, School = true,
                    Priority = toHolding == 0 ? 5 : 100 + toHolding * 10,
                });
                continue;
            }

            var seen = new HashSet<int>();
            for (int i = 0; i < front.Fishes.Count; i++)
            {
                int id = front.Fishes[i].FishId;
                if (!seen.Add(id)) continue;
                TankEntry tank = model.FindTankFor(id);
                if (tank != null)
                {
                    int priority = tank.Missing == 1 ? 0 : 10 + tank.Missing;
                    priority += front.TotalFishCount == 1 ? 0 : 1; // bóng sắp hết cá (kể cả hàng đợi spawner) thì ưu tiên (nổ để lộ bóng sau)
                    moves.Add(new Move { Bubble = front, FishId = id, ToTank = true, Priority = priority });
                }
                else if (freeHolding > 0)
                {
                    int rank = UpcomingRank(model, id);
                    moves.Add(new Move { Bubble = front, FishId = id, ToTank = false, Priority = 100 + rank * 10 + front.TotalFishCount });
                }
            }
        }

        return moves;
    }

    /// <summary>
    /// Luật runtime: băng vỡ khi bóng chạm đỉnh vùng chơi, tức bóng đã nổi lên hàng trên cùng. Solver không có vị trí nên mô phỏng xếp cột: số cột = bề ngang hàng
    /// trên cùng (LevelBands.TopRowWidth × TopRowFill) chia đường kính trung bình của bóng trong level; bóng theo thứ tự sinh rơi vào
    /// cột đang thấp nhất (tổng đường kính nhỏ nhất), bóng sinh trước nằm cao hơn trong cột. Tính một lần cho cả level (bóng trong hàng
    /// đợi cũng có cột sẵn). Xấp xỉ, nên Solve chạy thêm lượt bỏ băng để phân biệt "không giải được" với "chỉ mong manh ở phần băng".
    /// </summary>
    private static Dictionary<BubbleEntry, int> AssignColumns(BoardModel model)
    {
        float sum = 0f;
        foreach (BubbleEntry b in model.Bubbles) sum += Diameter(b);
        float avg = model.Bubbles.Count > 0 ? sum / model.Bubbles.Count : 2f * LevelBands.BubbleRadiusMax;
        int columnCount = Math.Max(1, (int)Math.Round(LevelBands.TopRowWidth * LevelBands.TopRowFill / Math.Max(0.1f, avg)));   // làm tròn: hàng chứa 2.6 bóng thì xếp hex được ~3 cột
        var height = new float[columnCount];
        var map = new Dictionary<BubbleEntry, int>(model.Bubbles.Count);
        foreach (BubbleEntry b in model.Bubbles)   // Bubbles theo thứ tự sinh (SpawnOrder = index)
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

    /// <summary>
    /// Bóng băng không còn bóng cá sinh trước nào trong cùng cột thì coi như đã lên đỉnh và vỡ được. Đá không tính: runtime đá ở đỉnh
    /// vẫn chắn, nhưng bóng mềm trôi sang cạnh đá khi hàng xóm nổ nên coi đá là chắn vĩnh viễn sẽ quá bi quan; lượt bỏ băng vẫn phân
    /// biệt được level "mong manh".
    /// </summary>
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

    private static float Diameter(BubbleEntry entry) => 2f * (entry.Radius > 0f ? entry.Radius : LevelBands.BubbleRadiusMax);

    /// <summary>Loài này sẽ được cần sớm tới đâu: 0 = có bể đang hiện, k = vị trí trong hàng đợi, lớn = không cần.</summary>
    private static int UpcomingRank(BoardModel model, int fishId)
    {
        foreach (TankEntry t in model.Tanks)
        {
            if (t.HasOrder && t.FishId == fishId) return 0;
        }

        int index = model.OrderQueue.IndexOf(fishId);
        return index >= 0 ? index + 1 : 50;
    }

    private static bool Apply(BoardModel model, Move move, ref int holdingUsed)
    {
        if (move.School) return ApplySchool(model, move.Bubble, ref holdingUsed);
        int index = move.Bubble.IndexOfFish(move.FishId);
        if (index < 0) return false;
        if (move.Bubble.IsFrozen) model.TryBreakIce(move.Bubble);   // quy ước IceUncovered (hoặc lượt bỏ băng): băng đã vỡ
        int id = model.RemoveFishAt(move.Bubble, index).FishId;
        if (move.Bubble.Popped) model.TakeNextFromQueue();   // bóng nổ → bóng kế trong hàng đợi hiện ra
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
        holdingUsed = Math.Max(holdingUsed, model.HoldingFishCount);
        Drain(model);
        return true;
    }

    /// <summary>
    /// FishSchoolBubble: cả đàn bay theo TryPlanSchool (bể nhận thì vào bể, không thì ô chờ), đặt chỗ cho cả đàn trước rồi gỡ cá theo
    /// index giảm dần để index trong plan không lệch. Không đủ chỗ thì false (không đổi model).
    /// </summary>
    private static bool ApplySchool(BoardModel model, BubbleEntry bubble, ref int holdingUsed)
    {
        if (bubble.IsFrozen) model.TryBreakIce(bubble);
        var plan = new List<SchoolMove>();
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
        holdingUsed = Math.Max(holdingUsed, model.HoldingFishCount);
        Drain(model);
        return true;
    }

    /// <summary>
    /// TANK-03 + HOLD-02 tức thời kiểu WordSort: bể đủ cá thì chốt và lấy đơn kế (FIFO); cá trong ô chờ có bể cần thì tự thu;
    /// lặp tới khi bàn đứng yên (có thể hoàn thành nhiều bể liên tiếp).
    /// </summary>
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

    private static string StateKey(BoardModel model)
    {
        var sb = new StringBuilder(256);
        sb.Append(model.SpawnedCount).Append('|');
        foreach (TankEntry t in model.Tanks) sb.Append(t.FishId).Append(':').Append(t.Count).Append(',');
        sb.Append('|');
        var holding = new List<int>();
        foreach (HoldingEntry h in model.Holding) if (h.HasFish) holding.Add(h.FishId);
        holding.Sort();
        foreach (int id in holding) sb.Append(id).Append(',');
        sb.Append('|');
        foreach (BubbleEntry b in model.Bubbles)
        {
            if (b.Popped) { sb.Append('x'); continue; }
            if (b.IsFrozen) sb.Append('f');
            if (b.Hp > 0) sb.Append('h').Append(b.Hp);
            foreach (FishEntry f in b.Fishes) sb.Append(f.FishId).Append('.');
            if (b.SpawnerFishes.Count > 0)
            {
                sb.Append('s');
                foreach (FishEntry f in b.SpawnerFishes) sb.Append(f.FishId).Append('.');
            }

            foreach (List<FishEntry> layer in b.Layers)   // MultilayerBubble: các lớp còn ẩn cũng là một phần trạng thái
            {
                sb.Append('l');
                foreach (FishEntry f in layer) sb.Append(f.FishId).Append('.');
            }

            sb.Append(';');
        }

        sb.Append('|');
        foreach (int id in model.OrderQueue) sb.Append(id).Append(',');
        return sb.ToString();
    }
}
