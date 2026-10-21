using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Nút Check Level, hai tầng:
/// - Tầng đếm: rà bóng trống, spawner chưa có cá, tổng cá = số đơn × TankGoal, và từng loài phải gom đủ TankGoal con
///   (cá đã gán cho một đơn thì không tính lại). Bóng / cá sai được tô đỏ đến lần check sau.
/// - Tầng solver: đếm đúng rồi mới chạy LevelSolver.Solve để biết level có đường thắng hay không. Đủ cá KHÔNG có
///   nghĩa là chơi được - xáo cá hay rắc Khoá / Đá xong hoàn toàn có thể ra bàn chết cứng.
///
/// Solver chỉ chạy khi bấm nút, không chạy trong RefreshCheckIfActive: mỗi lượt Solve duyệt tới CheckSolverMaxNodes
/// trạng thái, nhét vào đường sửa data thì mỗi lần kéo thả một con cá là treo tool.
/// Xem <see cref="LevelDesignDataEditor"/> cho danh sách các file partial khác.
/// </summary>
public partial class LevelDesignDataEditor
{
    private static readonly Color CheckError = new Color(0.95f, 0.22f, 0.16f, 1f);
    private static readonly Color CheckErrorFill = new Color(0.95f, 0.22f, 0.16f, 0.42f);

    private readonly HashSet<int> checkErrorBubbles = new HashSet<int>();
    private readonly HashSet<(int bubble, int fish, int list)> checkErrorFish = new HashSet<(int, int, int)>();

    /// <summary>
    /// Trần số trạng thái cho một lượt Solve. Vượt trần mà chưa ra đường thắng thì solver trả NodeCapHit, và kết luận
    /// chỉ là "không kết luận được" chứ không phải "không giải được" - level nhiều bóng dễ chạm trần này.
    /// </summary>
    private const int CheckSolverMaxNodes = 30000;

    /// <summary>Lệch quá bấy nhiêu so với mục tiêu theo tag thì "≈ Người chơi thắng" bị coi là lệch mục tiêu.</summary>
    private const float WinRateTolerance = 0.1f;

    /// <summary>Đã bấm Check Level trên level đang mở: sửa data thì chạy lại để viền đỏ khớp data mới.</summary>
    private bool checkActive;

    private bool IsCheckErrorBubble(int bubbleIndex) => checkErrorBubbles.Contains(bubbleIndex);

    /// <summary>Viền đỏ theo đúng danh sách cá đang vẽ: lớp 1, các lớp sau, hay hàng đợi spawner.</summary>
    private bool IsCheckErrorFish(int bubbleIndex, int fishIndex, int listId) =>
        checkErrorFish.Contains((bubbleIndex, fishIndex, listId));

    private void ClearCheckHighlights()
    {
        checkErrorBubbles.Clear();
        checkErrorFish.Clear();
        checkActive = false;
        ClearLossSpots();
    }

    /// <summary>Sửa data xong thì chỉ tô lại viền đỏ theo tầng đếm; solver để dành cho lần bấm nút sau.</summary>
    private void RefreshCheckIfActive(LevelDesignData data)
    {
        if (checkActive) CheckLevel(data, false);
    }

    private void CheckLevel(LevelDesignData data) => CheckLevel(data, true);

    /// <summary>
    /// Toàn bộ tầng đếm, kèm tô viền đỏ: bóng trống / spawner rỗng, khoá - chìa, tổng cá, đủ bộ 3, và các luật của
    /// LevelData.Validate (đá không nuôi cá, bóng nhiều lớp...). Danh sách rỗng = seed hợp lệ.
    /// Dùng chung cho nút Check Level và cho bước chặn trước khi Save, để hai chỗ không bao giờ phán khác nhau.
    /// </summary>
    private List<string> CollectDataErrors(LevelDesignData data)
    {
        checkErrorBubbles.Clear();
        checkErrorFish.Clear();
        checkActive = true;

        SoftBubbleData[] bubbles = data.levelData?.SoftBubbleDataArray ?? Array.Empty<SoftBubbleData>();
        int[] orders = data.levelData?.OrderQueue ?? Array.Empty<int>();
        int tankGoal = GamePlayGlobalConfig.TankGoal;
        var messages = new List<string>();

        CollectEmptyAndSpawnerErrors(bubbles, messages);
        CollectLockKeyErrors(bubbles, messages);
        CollectTotalFishError(data, orders.Length, tankGoal, messages);
        CollectFishGroupErrors(bubbles, orders, tankGoal, messages);

        // Luật của chính LevelData (runtime đọc theo đó), gộp vào đây để Save không lọt seed mà Validate sẽ chê.
        if (data.levelData != null)
        {
            var validateErrors = new List<string>();
            if (!data.levelData.Validate(validateErrors)) messages.AddRange(validateErrors);
        }

        return messages;
    }

    private void CheckLevel(LevelDesignData data, bool runSolver)
    {
        List<string> messages = CollectDataErrors(data);

        if (messages.Count > 0)
        {
            SetStatus(false, string.Join("\n", messages));
            return;
        }

        // Thiếu / thừa cá thì kiểu gì solver cũng bảo không giải được, nói thêm câu đó chỉ che mất lỗi thật.
        if (runSolver) ReportSolver(data);
        else SetStatus(true, "Đủ cá theo đơn. Data vừa đổi nên solver chưa chạy lại - bấm Check Level để kiểm đường thắng");
    }

    /// <summary>
    /// Chạy solver trên seed đang mở và dịch kết quả ra một câu cho người dựng level. Bốn kết cục: giải được;
    /// giải được nhưng sát trần ô chờ; chỉ giải được khi bỏ luật băng; và không giải được / không kết luận được.
    /// </summary>
    private void ReportSolver(LevelDesignData data)
    {
        LevelData level = data.levelData;
        int holdingSlots = LevelBands.DefaultHoldingSlots;
        LevelSolver.Result result = LevelSolver.Solve(level, CheckSolverMaxNodes);

        if (result.Solved)
        {
            string line = $"Level hợp lệ - giải được trong {result.Moves} nước, dùng tối đa {result.MaxHoldingUsed}/{holdingSlots} ô chờ";
            string winRate = DescribeWinRate(level);
            if (winRate != null) line += "\n" + winRate;

            // Đường duy nhất tìm được đã ăn hết ô chờ: người chơi lệch một nước là kẹt, seed như vậy quá mong manh.
            bool tight = result.MaxHoldingUsed >= holdingSlots;
            if (tight) line += "\nĐường thắng dùng hết sạch ô chờ - người chơi lệch một nước là kẹt";
            SetStatus(true, line, tight ? MessageType.Warning : (MessageType?)null);
            return;
        }

        if (result.SolvedIgnoringIce)
        {
            SetStatus(true,
                $"Chỉ giải được khi coi khói tan tự do ({result.Moves} nước, {result.MaxHoldingUsed}/{holdingSlots} ô chờ).\n" +
                "Quy ước bóng khói (isIce) của solver là xấp xỉ (không có vị trí bóng) nên đây là cảnh báo chứ chưa phải kết luận hỏng - " +
                "xem lại các bóng khói.", MessageType.Warning);
            return;
        }

        if (result.NodeCapHit)
        {
            SetStatus(true,
                $"Không kết luận được: duyệt hết {result.NodesExplored} trạng thái mà chưa thấy đường thắng nào. " +
                "Level nhiều bóng thì chạm trần là thường, nhưng seed không có đường dễ cũng ra kết quả này.",
                MessageType.Warning);
            return;
        }

        SetStatus(false,
            $"Không giải được: đã duyệt hết {result.NodesExplored} trạng thái, không có đường nào thắng với " +
            $"{holdingSlots} ô chờ. Xáo lại cá hoặc bớt vật cản.");
    }

    /// <summary>
    /// "≈ Người chơi thắng" (bot như người, cùng số của nút Đo) so với mục tiêu theo tag (LevelHumanBot.PlayerTargetWinRate);
    /// null khi seed chưa có số level để tra tag. Trước 2026-10-16 dòng này đo bằng bot cũ so với LevelBands.TargetWinRate.
    /// </summary>
    private static string DescribeWinRate(LevelData level)
    {
        if (level == null || level.Level < 1) return null;

        float estimated = LevelHumanBot.EstimateWinRate(level, LevelHumanBot.PlayerMistakeRate);
        int playouts = LevelHumanBot.PlayoutsFor(estimated);
        if (playouts > 200) estimated = LevelHumanBot.EstimateWinRate(level, LevelHumanBot.PlayerMistakeRate, playouts);
        float target = LevelHumanBot.PlayerTargetWinRate(level.Level, level.Difficulty);
        string line = $"≈ Người chơi thắng {estimated:P0}, mục tiêu {level.Difficulty} {target:P0}";
        if (estimated < target - WinRateTolerance) return $"{line} - khó hơn mục tiêu";
        if (estimated > target + WinRateTolerance) return $"{line} - dễ hơn mục tiêu";
        return line;
    }

    private void CollectEmptyAndSpawnerErrors(SoftBubbleData[] bubbles, List<string> messages)
    {
        var emptyBubbles = new List<int>();
        var emptySpawners = new List<int>();
        for (int i = 0; i < bubbles.Length; i++)
        {
            SoftBubbleData bubble = bubbles[i];
            if (bubble == null) continue;

            if (!bubble.isRock && bubble.FishCount == 0)
            {
                emptyBubbles.Add(bubble.softBubbleId);
                checkErrorBubbles.Add(i);
            }

            if (!bubble.isSpawner || bubble.isRock) continue;
            int spawnerCount = bubble.fishOnSpawner?.Length ?? 0;
            if (spawnerCount > 0) continue;

            emptySpawners.Add(bubble.softBubbleId);
            checkErrorBubbles.Add(i);
        }

        if (emptyBubbles.Count > 0)
            messages.Add($"Bóng trống (không phải Đá): ID {string.Join(", ID ", emptyBubbles)}");
        if (emptySpawners.Count > 0)
            messages.Add($"Spawner chưa có cá: ID {string.Join(", ID ", emptySpawners)}");
    }

    /// <summary>
    /// Luật khoá - chìa: chìa (isKey) mang keyId là softBubbleId của ổ khoá nó mở. Bốn kiểu sai:
    /// - Chìa không ghi keyId (bằng 0).
    /// - Chìa trỏ tới một ID không có trên bàn, hoặc trỏ tới bóng không phải ổ khoá.
    /// - Ổ khoá không có chìa nào mở: bóng đó khoá vĩnh viễn, level không thể xong.
    /// - Một bóng vừa là khoá vừa là chìa.
    /// Chìa trùng ổ khoá thì không tính là sai: nhiều chìa cùng mở một ổ vẫn chơi được.
    /// </summary>
    private void CollectLockKeyErrors(SoftBubbleData[] bubbles, List<string> messages)
    {
        var locks = new Dictionary<int, int>();   // softBubbleId của ổ khoá -> vị trí trong mảng
        for (int i = 0; i < bubbles.Length; i++)
        {
            if (bubbles[i] != null && bubbles[i].isLock) locks[bubbles[i].softBubbleId] = i;
        }

        var noId = new List<int>();
        var badTarget = new List<string>();
        var bothFlags = new List<int>();
        var openedLocks = new HashSet<int>();

        for (int i = 0; i < bubbles.Length; i++)
        {
            SoftBubbleData bubble = bubbles[i];
            if (bubble == null) continue;

            if (bubble.isLock && bubble.isKey)
            {
                bothFlags.Add(bubble.softBubbleId);
                checkErrorBubbles.Add(i);
            }

            if (!bubble.isKey) continue;

            if (bubble.keyId <= 0)
            {
                noId.Add(bubble.softBubbleId);
                checkErrorBubbles.Add(i);
                continue;
            }

            if (!locks.ContainsKey(bubble.keyId))
            {
                badTarget.Add($"chìa ID {bubble.softBubbleId} → ổ khoá ID {bubble.keyId}");
                checkErrorBubbles.Add(i);
                continue;
            }

            openedLocks.Add(bubble.keyId);
        }

        var orphanLocks = new List<int>();
        foreach (KeyValuePair<int, int> kv in locks)
        {
            if (openedLocks.Contains(kv.Key)) continue;

            orphanLocks.Add(kv.Key);
            checkErrorBubbles.Add(kv.Value);
        }

        if (noId.Count > 0)
            messages.Add($"Chìa chưa ghi ID ổ khoá: ID {string.Join(", ID ", noId)}");
        if (badTarget.Count > 0)
            messages.Add($"Chìa trỏ tới ổ khoá không có trên bàn: {string.Join("; ", badTarget)}");
        if (orphanLocks.Count > 0)
            messages.Add($"Ổ khoá không có chìa nào mở: ID {string.Join(", ID ", orphanLocks)}");
        if (bothFlags.Count > 0)
            messages.Add($"Bóng vừa là ổ khoá vừa là chìa: ID {string.Join(", ID ", bothFlags)}");
    }

    private static void CollectTotalFishError(LevelDesignData data, int orderCount, int tankGoal, List<string> messages)
    {
        int total = data.TotalFishInBubbles;
        int required = orderCount * tankGoal;
        if (total != required)
            messages.Add($"Số cá trong bóng (mọi lớp) + spawner = {total}, cần {required} ({orderCount} đơn × {tankGoal})");
    }

    /// <summary>
    /// Mỗi đơn lấy TankGoal con cùng FishId chưa gán. Cá đã lấy không tính cho đơn sau (cùng loài cũng vậy).
    /// Đơn nào thiếu thì loài đó vào log đỏ; phần cá thừa / không đủ nhóm sau cùng cũng tô đỏ.
    /// Cá mọi lớp và cá trong hàng đợi spawner tính chung với cá lớp 1: chúng cũng lên bàn và phải vào bể như nhau.
    /// </summary>
    private void CollectFishGroupErrors(SoftBubbleData[] bubbles, int[] orders, int tankGoal, List<string> messages)
    {
        var unused = new Dictionary<int, List<(int bubble, int fish, int list)>>();
        for (int b = 0; b < bubbles.Length; b++)
        {
            SoftBubbleData bubble = bubbles[b];
            if (bubble == null) continue;

            // Mọi lớp cá (lớp 1 đang hiện + các lớp lộ dần) và hàng đợi spawner đều phải vào bể như nhau.
            for (int layer = 0; layer < bubble.LayerCount; layer++)
            {
                AddUnused(unused, b, bubble.GetLayerFish(layer), FishListMain + layer);
            }

            if (bubble.isSpawner && !bubble.isRock) AddUnused(unused, b, bubble.fishOnSpawner, FishListSpawner);
        }

        var shortIds = new List<int>();
        var shortSet = new HashSet<int>();

        foreach (int orderId in orders)
        {
            if (!unused.TryGetValue(orderId, out List<(int bubble, int fish, int list)> slots) || slots.Count < tankGoal)
            {
                if (shortSet.Add(orderId)) shortIds.Add(orderId);
                if (slots == null) continue;
                MarkErrorFish(slots);
                slots.Clear();
                continue;
            }

            slots.RemoveRange(0, tankGoal);
        }

        foreach (KeyValuePair<int, List<(int bubble, int fish, int list)>> kv in unused)
        {
            List<(int bubble, int fish, int list)> leftover = kv.Value;
            if (leftover.Count == 0) continue;

            MarkErrorFish(leftover);
            int remaining = leftover.Count;
            leftover.Clear();
            while (remaining >= tankGoal) remaining -= tankGoal;
            if (remaining > 0 && shortSet.Add(kv.Key)) shortIds.Add(kv.Key);
        }

        if (shortIds.Count > 0)
            messages.Add($"Cá không đủ {tankGoal} con trong bóng + spawner: Id {string.Join(", Id ", shortIds)}");
    }

    private static void AddUnused(Dictionary<int, List<(int bubble, int fish, int list)>> unused, int bubbleIndex,
        FishData[] fish, int listId)
    {
        if (fish == null) return;
        for (int f = 0; f < fish.Length; f++)
        {
            if (fish[f] == null) continue;
            int id = fish[f].FishId;
            if (!unused.TryGetValue(id, out List<(int bubble, int fish, int list)> slots))
            {
                slots = new List<(int, int, int)>();
                unused[id] = slots;
            }

            slots.Add((bubbleIndex, f, listId));
        }
    }

    private void MarkErrorFish(List<(int bubble, int fish, int list)> slots)
    {
        for (int i = 0; i < slots.Count; i++) checkErrorFish.Add(slots[i]);
    }
}
