using System.Collections.Generic;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// "Trick" cho cột Sinh theo độ khó: cài CÓ CHỦ ĐÍCH vài chỗ hiểm vào seed thay vì chỉ rải ngẫu nhiên rồi lọc theo %
/// thắng. Nhóm THÔNG TIN BỊ GIẤU (giấu con thứ 3, chặn chỗ gấp, spawner đánh lừa) người chơi không tính trước được nên
/// khó thật với cả người. Nhóm NHÌN THẤY ĐƯỢC (mồi nhử, đơn đôi, chìa rác, cá link sai lúc) người tính trước có thể
/// né, nhưng phạt người chơi bấm theo phản xạ - thêm sau khi người thiết kế chơi thử nhóm đầu thấy khó hơn thật.
///
/// - Mồi nhử: bóng có 2 con của đơn sắp tới, mấy con còn lại là rác. Dọn bóng là phải ôm rác.
/// - Đơn đôi: hai đơn cùng loài liền nhau trong hàng đơn - hai bể cùng đòi 6 con một loài.
/// - Chìa toàn cá rác: muốn mở ổ khoá phải ôm rác của bóng chìa lên ô chờ trước.
/// - Cá link sai lúc: đàn toàn cá chưa tới lượt - bấm là đổ cả đàn lên ô chờ.
/// - Giấu con thứ 3: đơn có 2 con lộ sớm, con thứ 3 bị dời ra sau vài bóng và giấu đi (cá ẩn / hàng chờ spawner /
///   lớp trong). Người chơi phải ôm 2 ô chờ mà không biết con thứ 3 ở đâu.
/// - Chặn chỗ gấp: dời khói (isIce) sang đúng bóng đang chứa cá của đơn sắp tới. Muốn lấy phải phá bóng "vô dụng"
///   phía trên trước, ô chờ đầy dần.
/// - Spawner đánh lừa: hàng chờ spawner đổi thành cá của đơn còn rất xa, cá cần sớm bị đẩy ra sau. Nổ cá hiện thì
///   bóng lại nhả ra cá rác.
/// - Dồn giữa màn (nút thắt): mọi trick chỉ cài trong khúc 35-70% level - đầu dễ, giữa nghẹt, cuối thả lỏng.
///
/// Trick chỉ ĐỔI CHỖ cá (đổi FishId giữa hai con) và dời cờ khói, nên số cá từng loài không đổi, phép cân với hàng đơn
/// giữ nguyên. Chạy sau AdvanceOrders, trước solver: solver vẫn loại seed không giải được, vòng dò % thắng vẫn chạy như
/// cũ - trick gánh bớt độ khó nên vòng dò cần đẩy đơn ít hơn.
/// Xem <see cref="LevelDesignDataEditor"/> cho danh sách các file partial khác.
/// </summary>
public partial class LevelDesignDataEditor
{
    private int diffTrickHideThird;
    private int diffTrickBlockUrgent;
    private bool diffTrickSpawnerJunk;
    private bool diffTrickMidChoke;
    private int diffTrickBait;
    private int diffTrickDoubleOrder;
    private bool diffTrickKeyJunk;
    private bool diffTrickBundleLate;
    private bool showTricks = true;

    /// <summary>Con thứ 3 bị dời ra sau con thứ 2 từng này bóng: đủ xa để nằm ngoài tầm nhìn lúc 2 con đầu lộ ra.</summary>
    private const int HideThirdMinAhead = 4;
    private const int HideThirdMaxAhead = 9;

    /// <summary>Cá được cần trong vòng từng này đơn kể từ lúc bóng của nó lên bàn thì coi là "gấp".</summary>
    private const int UrgentSlack = 3;

    /// <summary>Cá còn cách lúc được cần từng này đơn trở lên thì coi là "rác" khi nhả ra từ spawner.</summary>
    private const int JunkSlack = 8;

    private const int JunkSearchFrom = 3;
    private const int JunkSearchTo = 12;

    /// <summary>Hai bóng bị chặn phải cách nhau ít nhất từng này bóng, kẻo dồn cục thành bàn chết.</summary>
    private const int BlockSpacing = 3;

    private sealed class FishRef
    {
        public int Bubble;
        public FishData Fish;
        public bool Outer;
        public int Order;
    }

    private void DrawTrickKnobs()
    {
        GUILayout.Label("cài chỗ hiểm có chủ đích - chỉ trick người chơi không nhìn trước được",
            SirenixGUIStyles.CenteredGreyMiniLabel, GUILayout.ExpandWidth(true));

        TempContent.text = "Giấu con thứ 3";
        TempContent.tooltip = "Số đơn có 2 con lộ sớm nhưng con thứ 3 bị dời ra sau " +
                              $"{HideThirdMinAhead}-{HideThirdMaxAhead} bóng và giấu đi (cá ẩn, hàng chờ spawner hoặc lớp " +
                              "trong). Người chơi phải ôm 2 ô chờ mà không biết con cuối ở đâu.";
        diffTrickHideThird = Mathf.Clamp(EditorGUILayout.IntField(TempContent, diffTrickHideThird), 0, 12);

        TempContent.text = "Chặn chỗ gấp";
        TempContent.tooltip = "Số bóng chứa cá của đơn SẮP TỚI bị phủ khói. Lấy khói từ các bóng khói ít quan trọng " +
                              "(tổng số bóng khói giữ nguyên), thiếu thì thêm. Phải phá bóng phía trên trước, ô chờ đầy dần.";
        diffTrickBlockUrgent = Mathf.Clamp(EditorGUILayout.IntField(TempContent, diffTrickBlockUrgent), 0, 8);

        TempContent.text = "Spawner đánh lừa";
        TempContent.tooltip = "Hàng chờ spawner đổi thành cá của đơn còn rất xa, cá cần sớm bị đẩy ra sau. Bấm cá hiện " +
                              "xong bóng lại nhả cá rác. Cần Số bóng spawner > 0.";
        diffTrickSpawnerJunk = EditorGUILayout.Toggle(TempContent, diffTrickSpawnerJunk);
        if (diffTrickSpawnerJunk && diffSpawnerCount <= 0)
        {
            GUILayout.Label("↑ chưa có spawner - tăng Số bóng spawner", SirenixGUIStyles.CenteredGreyMiniLabel,
                GUILayout.ExpandWidth(true));
        }

        TempContent.text = "Mồi nhử";
        TempContent.tooltip = "Số bóng có 2 con của đơn sắp tới (bấm là vào bể ngay, rất hấp dẫn) nhưng mấy con còn " +
                              "lại là cá rác của đơn rất xa. Muốn dọn bóng cho bàn chạy thì phải ôm rác lên ô chờ.";
        diffTrickBait = Mathf.Clamp(EditorGUILayout.IntField(TempContent, diffTrickBait), 0, 8);

        TempContent.text = "Đơn đôi liền nhau";
        TempContent.tooltip = "Số lần kéo hai đơn cùng loài đứng sát nhau trong hàng đơn: hai bể cùng đòi một loài, " +
                              "cần 6 con một lúc. Gom đủ 3 con tưởng xong thì bể kia vẫn đói.";
        diffTrickDoubleOrder = Mathf.Clamp(EditorGUILayout.IntField(TempContent, diffTrickDoubleOrder), 0, 6);

        TempContent.text = "Chìa toàn cá rác";
        TempContent.tooltip = "Cá trong bóng chìa đổi thành cá của đơn còn rất xa: muốn mở ổ khoá phải ôm rác lên " +
                              "ô chờ trước. Cần Số cặp khoá - chìa > 0.";
        diffTrickKeyJunk = EditorGUILayout.Toggle(TempContent, diffTrickKeyJunk);
        if (diffTrickKeyJunk && diffLockPairs <= 0)
        {
            GUILayout.Label("↑ chưa có khoá - chìa - tăng Số cặp khoá - chìa", SirenixGUIStyles.CenteredGreyMiniLabel,
                GUILayout.ExpandWidth(true));
        }

        TempContent.text = "Cá link sai lúc";
        TempContent.tooltip = "Đàn cá link đổi thành cá của đơn còn rất xa (đàn cùng loài thì cả đàn thành một loài " +
                              "chưa tới lượt). Bấm là đổ cả đàn lên ô chờ, không bấm thì bóng nằm chắn bàn. " +
                              "Cần Số bóng cá link > 0.";
        diffTrickBundleLate = EditorGUILayout.Toggle(TempContent, diffTrickBundleLate);
        if (diffTrickBundleLate && diffBundleCount <= 0)
        {
            GUILayout.Label("↑ chưa có cá link - tăng Số bóng cá link", SirenixGUIStyles.CenteredGreyMiniLabel,
                GUILayout.ExpandWidth(true));
        }

        TempContent.text = "Dồn giữa màn";
        TempContent.tooltip = "Nút thắt: mọi trick chỉ cài trong khúc 35-70% level. Đầu dễ, giữa nghẹt, cuối thả lỏng - " +
                              "tạo cảm giác suýt thua. Tắt thì trick rải trong khúc 10-90%.";
        diffTrickMidChoke = EditorGUILayout.Toggle(TempContent, diffTrickMidChoke);
    }

    private bool AnyTrick => diffTrickHideThird > 0 || diffTrickBlockUrgent > 0 || diffTrickSpawnerJunk ||
                             diffTrickBait > 0 || diffTrickDoubleOrder > 0 || diffTrickKeyJunk || diffTrickBundleLate;

    /// <summary>Cài các trick đang bật lên seed; trả về dòng mô tả chỗ đã cài (trống khi không bật trick nào).</summary>
    private string ApplyTricks(SoftBubbleData[] bubbles, int[] queue, int tankGoal)
    {
        if (!AnyTrick || bubbles == null || bubbles.Length == 0 || queue == null || queue.Length == 0) return string.Empty;

        var log = new List<string>();
        int lo = diffTrickMidChoke ? Mathf.RoundToInt(bubbles.Length * 0.35f) : Mathf.RoundToInt(bubbles.Length * 0.1f);
        int hi = diffTrickMidChoke ? Mathf.RoundToInt(bubbles.Length * 0.7f) : Mathf.RoundToInt(bubbles.Length * 0.9f);
        lo = Mathf.Max(lo, OpeningSafeBubbles);
        hi = Mathf.Clamp(hi, lo + 1, bubbles.Length);

        // Thứ tự: spawner rồi giấu con thứ 3 (hai trick đổi chỗ cá), chặn chỗ gấp sau cùng để chấm "gấp" trên vị trí cá
        // cuối cùng.
        // Đơn đôi đổi HÀNG ĐƠN nên chạy đầu tiên: các trick sau chấm "gấp" / "rác" theo hàng đơn cuối cùng.
        if (diffTrickDoubleOrder > 0) DoubleOrders(queue, bubbles.Length, lo, hi, log, diffTrickDoubleOrder);
        if (diffTrickSpawnerJunk) SpawnerJunk(bubbles, queue, tankGoal, lo, hi, log);
        if (diffTrickKeyJunk) KeyJunk(bubbles, queue, tankGoal, log);
        if (diffTrickBundleLate) BundleLate(bubbles, queue, tankGoal, lo, hi, log);
        if (diffTrickBait > 0) BaitBubbles(bubbles, queue, tankGoal, lo, hi, log, diffTrickBait);
        if (diffTrickHideThird > 0) HideThirds(bubbles, queue, tankGoal, lo, hi, log, diffTrickHideThird);
        if (diffTrickBlockUrgent > 0) BlockUrgent(bubbles, queue, tankGoal, lo, hi, log, diffTrickBlockUrgent);

        return log.Count == 0
            ? "Trick: không cài được chỗ nào (level ngắn hoặc thiếu bóng phù hợp)"
            : "Trick: " + string.Join(" · ", log);
    }

    /// <summary>Bóng được phép đổi cá: không đá, không cá link (đàn), không khoá / chìa / bóng đơn.</summary>
    private static bool TrickSwappable(SoftBubbleData b) =>
        b != null && !b.isRock && !b.isBundleBubble && !b.isLock && !b.isKey && !b.isOrderBubble && b.FishCount > 0;

    /// <summary>
    /// Mọi con cá theo đúng thứ tự người chơi gặp (lớp ngoài, hàng chờ spawner, lớp trong - như BuildQueueFromBubbles),
    /// kèm vị trí trong hàng đơn của đơn sẽ ăn nó: con thứ r của một loài thuộc đơn thứ r / TankGoal của loài đó.
    /// </summary>
    private static List<FishRef> CollectFish(SoftBubbleData[] bubbles, int[] queue, int tankGoal)
    {
        var refs = new List<FishRef>();

        void Add(int bubble, FishData[] fish, bool outer)
        {
            if (fish == null) return;
            foreach (FishData one in fish)
            {
                if (one != null) refs.Add(new FishRef { Bubble = bubble, Fish = one, Outer = outer });
            }
        }

        for (int b = 0; b < bubbles.Length; b++)
        {
            SoftBubbleData bubble = bubbles[b];
            if (bubble == null || bubble.isRock) continue;

            Add(b, bubble.FishDataArray, true);
            if (bubble.isSpawner) Add(b, bubble.fishOnSpawner, false);
            if (bubble.isMultiLayer && bubble.layers != null)
            {
                foreach (BubbleLayerData layer in bubble.layers) Add(b, layer?.FishDataArray, false);
            }
        }

        var positions = new Dictionary<int, List<int>>();
        for (int q = 0; q < queue.Length; q++)
        {
            if (!positions.TryGetValue(queue[q], out List<int> list)) positions[queue[q]] = list = new List<int>();
            list.Add(q);
        }

        var seen = new Dictionary<int, int>();
        foreach (FishRef r in refs)
        {
            int id = r.Fish.FishId;
            int n = seen.TryGetValue(id, out int c) ? c : 0;
            seen[id] = n + 1;
            int set = n / Mathf.Max(1, tankGoal);
            r.Order = positions.TryGetValue(id, out List<int> list) && set < list.Count ? list[set] : queue.Length;
        }

        return refs;
    }

    /// <summary>
    /// Ước lượng số đơn đã xong lúc bóng b vừa lên bàn: bàn giữ VisibleBubbles bóng, nên bóng b lên khi các bóng đứng
    /// trước b - VisibleBubbles + 1 đã nổ hết. Xấp xỉ (bỏ qua đá chiếm chỗ) nhưng đủ để chấm cá nào gấp, cá nào rác.
    /// </summary>
    private static float[] BubbleProgress(SoftBubbleData[] bubbles, int tankGoal)
    {
        var fishBefore = new int[bubbles.Length + 1];
        for (int b = 0; b < bubbles.Length; b++)
        {
            SoftBubbleData bubble = bubbles[b];
            int fish = 0;
            if (bubble != null && !bubble.isRock)
            {
                fish = bubble.TotalFishCount + (bubble.isSpawner ? bubble.fishOnSpawner?.Length ?? 0 : 0);
            }

            fishBefore[b + 1] = fishBefore[b] + fish;
        }

        var progress = new float[bubbles.Length];
        for (int b = 0; b < bubbles.Length; b++)
        {
            int cleared = Mathf.Max(0, b - LevelBands.VisibleBubbles + 1);
            progress[b] = (float)fishBefore[cleared] / Mathf.Max(1, tankGoal);
        }

        return progress;
    }

    private static Dictionary<FishData, FishRef> IndexFish(List<FishRef> refs)
    {
        var map = new Dictionary<FishData, FishRef>(refs.Count);
        foreach (FishRef r in refs) map[r.Fish] = r;
        return map;
    }

    /// <summary>Spawner đánh lừa: cá gấp trong hàng chờ đổi với cá "rác" (đơn còn xa) ở vài bóng phía sau.</summary>
    private static void SpawnerJunk(SoftBubbleData[] bubbles, int[] queue, int tankGoal, int lo, int hi, List<string> log)
    {
        float[] progress = BubbleProgress(bubbles, tankGoal);
        Dictionary<FishData, FishRef> byFish = IndexFish(CollectFish(bubbles, queue, tankGoal));

        for (int b = lo; b < hi; b++)
        {
            SoftBubbleData bubble = bubbles[b];
            if (bubble == null || bubble.isRock || !bubble.isSpawner || bubble.fishOnSpawner == null) continue;

            // Chỉ đổi nửa hàng chờ: đổi hết thì bóng thành kho rác thuần, nhìn vài lần là người chơi đoán ra.
            int limit = Mathf.CeilToInt(bubble.fishOnSpawner.Length / 2f);
            int swapped = 0;
            foreach (FishData queued in bubble.fishOnSpawner)
            {
                if (swapped >= limit) break;
                if (queued == null || !byFish.TryGetValue(queued, out FishRef mine)) continue;
                if (mine.Order - progress[b] > UrgentSlack + 1) continue;

                if (SwapWithJunk(bubbles, byFish, progress, b, queued)) swapped++;
            }

            if (swapped > 0) log.Add($"spawner bóng {b + 1} ({swapped} rác)");
        }
    }

    private static bool SwapWithJunk(SoftBubbleData[] bubbles, Dictionary<FishData, FishRef> byFish, float[] progress,
        int from, FishData queued)
    {
        int last = Mathf.Min(bubbles.Length - 1, from + JunkSearchTo);
        for (int t = from + JunkSearchFrom; t <= last; t++)
        {
            SoftBubbleData target = bubbles[t];
            if (!TrickSwappable(target) || target.isSpawner) continue;

            foreach (FishData fish in target.FishDataArray)
            {
                if (fish == null || fish.isHidden || fish.FishId == queued.FishId) continue;
                if (!byFish.TryGetValue(fish, out FishRef other) || other.Order - progress[from] < JunkSlack) continue;

                (fish.FishId, queued.FishId) = (queued.FishId, fish.FishId);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Giấu con thứ 3: chọn đơn có con thứ 2 lộ trong khúc trick, dời con cuối ra sau HideThirdMinAhead..MaxAhead bóng
    /// vào chỗ vốn đã khuất (hàng chờ spawner / lớp trong), không có thì vào lớp ngoài rồi bật cá ẩn.
    /// </summary>
    private static void HideThirds(SoftBubbleData[] bubbles, int[] queue, int tankGoal, int lo, int hi, List<string> log,
        int count)
    {
        List<FishRef> refs = CollectFish(bubbles, queue, tankGoal);
        var byOrder = new Dictionary<int, List<FishRef>>();
        foreach (FishRef r in refs)
        {
            if (r.Order >= queue.Length) continue;
            if (!byOrder.TryGetValue(r.Order, out List<FishRef> list)) byOrder[r.Order] = list = new List<FishRef>();
            list.Add(r);
        }

        var candidates = new List<int>();
        foreach (KeyValuePair<int, List<FishRef>> pair in byOrder)
        {
            List<FishRef> set = pair.Value;
            if (set.Count != tankGoal || tankGoal < 2) continue;
            if (set[1].Bubble >= lo && set[1].Bubble < hi && !set[set.Count - 1].Fish.isHidden) candidates.Add(pair.Key);
        }

        Shuffle(candidates);
        var usedBubbles = new HashSet<int>();
        int done = 0;
        foreach (int order in candidates)
        {
            if (done >= count) break;

            List<FishRef> set = byOrder[order];
            FishRef third = set[set.Count - 1];
            int start = set[1].Bubble + Random.Range(HideThirdMinAhead, HideThirdMaxAhead + 1);

            // Con cuối vốn đã ở xa: chỉ cần giấu tại chỗ.
            if (third.Bubble >= start)
            {
                if (usedBubbles.Contains(third.Bubble) || !third.Outer) continue;
                if (!TryHideInPlace(bubbles[third.Bubble], third.Fish)) continue;

                usedBubbles.Add(third.Bubble);
                log.Add($"giấu con 3 đơn #{order + 1} (bóng {third.Bubble + 1})");
                done++;
                continue;
            }

            int end = Mathf.Min(bubbles.Length - 1, start + 4);
            for (int t = start; t <= end; t++)
            {
                if (usedBubbles.Contains(t) || !TrickSwappable(bubbles[t])) continue;
                if (!TryHideSwap(bubbles[t], third.Fish)) continue;

                usedBubbles.Add(t);
                log.Add($"giấu con 3 đơn #{order + 1} → bóng {t + 1}");
                done++;
                break;
            }
        }
    }

    /// <summary>Bật cá ẩn cho con này nếu bóng chưa có con ẩn nào và vẫn còn ít nhất một con lộ khác.</summary>
    private static bool TryHideInPlace(SoftBubbleData bubble, FishData fish)
    {
        if (bubble == null || !TrickSwappable(bubble) || fish.isHidden) return false;

        int visible = 0;
        foreach (FishData one in bubble.FishDataArray)
        {
            if (one == null) continue;
            if (one.isHidden) return false;
            visible++;
        }

        if (visible < 2) return false;
        fish.isHidden = true;
        return true;
    }

    /// <summary>
    /// Đổi con thứ 3 (moving) với một con trong bóng target, ưu tiên chỗ vốn đã khuất: hàng chờ spawner, rồi lớp trong,
    /// cuối cùng là lớp ngoài kèm bật cá ẩn.
    /// </summary>
    private static bool TryHideSwap(SoftBubbleData target, FishData moving)
    {
        int species = moving.FishId;

        if (target.isSpawner && target.fishOnSpawner != null)
        {
            foreach (FishData one in target.fishOnSpawner)
            {
                if (one == null || one.FishId == species) continue;
                (one.FishId, moving.FishId) = (moving.FishId, one.FishId);
                return true;
            }
        }

        if (target.isMultiLayer && target.layers != null)
        {
            foreach (BubbleLayerData layer in target.layers)
            {
                if (layer?.FishDataArray == null) continue;
                foreach (FishData one in layer.FishDataArray)
                {
                    if (one == null || one.FishId == species) continue;
                    (one.FishId, moving.FishId) = (moving.FishId, one.FishId);
                    return true;
                }
            }
        }

        FishData[] outer = target.FishDataArray;
        if (outer == null || outer.Length < 2) return false;
        foreach (FishData one in outer)
        {
            if (one != null && one.isHidden) return false;
        }

        foreach (FishData one in outer)
        {
            if (one == null || one.FishId == species) continue;

            (one.FishId, moving.FishId) = (moving.FishId, one.FishId);
            one.isHidden = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Chặn chỗ gấp: phủ khói lên bóng chứa cá của đơn sắp tới (gấp nhất trước, các bóng cách nhau BlockSpacing), rồi gỡ
    /// khói ở bấy nhiêu bóng khói ít gấp nhất để tổng số bóng khói không đổi so với núm "Số bóng khói".
    /// </summary>
    private static void BlockUrgent(SoftBubbleData[] bubbles, int[] queue, int tankGoal, int lo, int hi, List<string> log,
        int count)
    {
        float[] slack = BubbleSlack(bubbles, queue, tankGoal, out int[] urgentOrder);

        var candidates = new List<int>();
        for (int b = lo; b < hi; b++)
        {
            SoftBubbleData bubble = bubbles[b];
            if (!TrickSwappable(bubble) || bubble.isIce || bubble.isBubbleHp || bubble.isSpawner || bubble.isMultiLayer) continue;
            if (slack[b] <= UrgentSlack) candidates.Add(b);
        }

        // Gấp nhất trước; hoà thì bốc ngẫu nhiên để hai seed cùng thông số không chặn y hệt nhau.
        Shuffle(candidates);
        candidates.Sort((a, b) => slack[a].CompareTo(slack[b]));

        var chosen = new List<int>();
        foreach (int b in candidates)
        {
            if (chosen.Count >= count) break;

            bool near = false;
            foreach (int c in chosen)
            {
                if (Mathf.Abs(c - b) < BlockSpacing) near = true;
            }

            if (!near) chosen.Add(b);
        }

        if (chosen.Count == 0) return;

        // Khói cũ ít gấp nhất nhường chỗ, để núm "Số bóng khói" vẫn là tổng số bóng khói.
        var oldIce = new List<int>();
        for (int b = 0; b < bubbles.Length; b++)
        {
            if (bubbles[b] != null && bubbles[b].isIce) oldIce.Add(b);
        }

        oldIce.Sort((a, b) => slack[b].CompareTo(slack[a]));

        int released = 0;
        foreach (int b in chosen)
        {
            bubbles[b].isIce = true;
            if (released < oldIce.Count && slack[oldIce[released]] > UrgentSlack)
            {
                bubbles[oldIce[released]].isIce = false;
                released++;
            }

            log.Add($"chặn bóng {b + 1} (đơn #{urgentOrder[b] + 1})");
        }
    }

    /// <summary>
    /// Đơn đôi: kéo đơn cùng loài gần nhất phía sau lên đứng ngay sau một đơn trong khúc trick. Hai bể FIFO nhận hai
    /// đơn kế nhau nên cả hai cùng đòi một loài. Chỉ dời một đơn trong hàng, số đơn từng loài không đổi.
    /// </summary>
    private static void DoubleOrders(int[] queue, int bubbleCount, int lo, int hi, List<string> log, int count)
    {
        // Khúc trick tính theo bóng; quy ra vị trí trong hàng đơn theo cùng tỉ lệ.
        int qlo = Mathf.Clamp(Mathf.RoundToInt((float)lo / bubbleCount * queue.Length), 0, queue.Length);
        int qhi = Mathf.Clamp(Mathf.RoundToInt((float)hi / bubbleCount * queue.Length), qlo, queue.Length);

        var candidates = new List<int>();
        for (int q = qlo; q < qhi - 1; q++) candidates.Add(q);
        Shuffle(candidates);

        var used = new HashSet<int>();
        int done = 0;
        foreach (int q in candidates)
        {
            if (done >= count) break;
            if (used.Contains(q) || used.Contains(q + 1) || queue[q + 1] == queue[q]) continue;
            if (q > 0 && queue[q - 1] == queue[q]) continue;

            int from = -1;
            for (int k = q + 2; k < queue.Length; k++)
            {
                if (queue[k] != queue[q]) continue;
                from = k;
                break;
            }

            if (from < 0) continue;

            int species = queue[from];
            for (int k = from; k > q + 1; k--) queue[k] = queue[k - 1];
            queue[q + 1] = species;

            used.Add(q);
            used.Add(q + 1);
            log.Add($"đơn đôi #{q + 1}-#{q + 2}");
            done++;
        }
    }

    /// <summary>Chìa toàn cá rác: cá chưa phải rác trong bóng chìa đổi với cá rác ở vài bóng phía sau.</summary>
    private static void KeyJunk(SoftBubbleData[] bubbles, int[] queue, int tankGoal, List<string> log)
    {
        float[] progress = BubbleProgress(bubbles, tankGoal);
        Dictionary<FishData, FishRef> byFish = IndexFish(CollectFish(bubbles, queue, tankGoal));

        for (int b = 0; b < bubbles.Length; b++)
        {
            SoftBubbleData bubble = bubbles[b];
            if (bubble == null || bubble.isRock || !bubble.isKey || bubble.FishDataArray == null) continue;

            int swapped = JunkifyOuter(bubbles, byFish, progress, b);
            if (swapped > 0) log.Add($"chìa bóng {b + 1} ({swapped} rác)");
        }
    }

    /// <summary>
    /// Cá link sai lúc: đàn lẫn loài thì từng con đổi thành rác; đàn thuần một loài thì đổi cả đàn sang một đơn còn xa
    /// (các con của đơn đó phải nằm lộ ở các bóng phía sau) để đàn vẫn thuần.
    /// </summary>
    private static void BundleLate(SoftBubbleData[] bubbles, int[] queue, int tankGoal, int lo, int hi, List<string> log)
    {
        for (int b = lo; b < hi; b++)
        {
            SoftBubbleData bubble = bubbles[b];
            if (bubble == null || bubble.isRock || !bubble.isBundleBubble || bubble.FishDataArray == null) continue;

            float[] progress = BubbleProgress(bubbles, tankGoal);
            List<FishRef> refs = CollectFish(bubbles, queue, tankGoal);
            Dictionary<FishData, FishRef> byFish = IndexFish(refs);

            FishData[] school = bubble.FishDataArray;
            bool pure = school.Length > 1;
            foreach (FishData one in school)
            {
                if (one == null || one.FishId != school[0].FishId) pure = false;
            }

            if (pure)
            {
                if (MakeSchoolLate(bubbles, refs, progress, b, school)) log.Add($"cá link bóng {b + 1} (cả đàn chưa tới lượt)");
                continue;
            }

            int swapped = JunkifyOuter(bubbles, byFish, progress, b);
            if (swapped > 0) log.Add($"cá link bóng {b + 1} ({swapped} rác)");
        }
    }

    private static bool MakeSchoolLate(SoftBubbleData[] bubbles, List<FishRef> refs, float[] progress, int b,
        FishData[] school)
    {
        FishRef mine = refs.Find(r => r.Fish == school[0]);
        if (mine == null || mine.Order - progress[b] >= JunkSlack) return false;

        var byOrder = new Dictionary<int, List<FishRef>>();
        foreach (FishRef r in refs)
        {
            if (!byOrder.TryGetValue(r.Order, out List<FishRef> list)) byOrder[r.Order] = list = new List<FishRef>();
            list.Add(r);
        }

        foreach (KeyValuePair<int, List<FishRef>> pair in byOrder)
        {
            List<FishRef> set = pair.Value;
            if (set.Count < school.Length || pair.Key - progress[b] < JunkSlack) continue;
            if (set[0].Fish.FishId == school[0].FishId) continue;

            bool ok = true;
            for (int k = 0; k < school.Length && ok; k++)
            {
                FishRef r = set[k];
                SoftBubbleData host = bubbles[r.Bubble];
                ok = r.Bubble > b && r.Outer && !r.Fish.isHidden && TrickSwappable(host) && !host.isSpawner;
            }

            if (!ok) continue;

            for (int k = 0; k < school.Length; k++)
            {
                FishData other = set[k].Fish;
                (other.FishId, school[k].FishId) = (school[k].FishId, other.FishId);
            }

            return true;
        }

        return false;
    }

    /// <summary>Đổi mọi cá lộ chưa phải rác ở lớp ngoài bóng b thành rác (SwapWithJunk). Trả về số con đã đổi.</summary>
    private static int JunkifyOuter(SoftBubbleData[] bubbles, Dictionary<FishData, FishRef> byFish, float[] progress, int b)
    {
        int swapped = 0;
        foreach (FishData fish in bubbles[b].FishDataArray)
        {
            if (fish == null || fish.isHidden || !byFish.TryGetValue(fish, out FishRef mine)) continue;
            if (mine.Order - progress[b] >= JunkSlack) continue;

            if (SwapWithJunk(bubbles, byFish, progress, b, fish)) swapped++;
        }

        return swapped;
    }

    /// <summary>
    /// Mồi nhử: bóng có 2 con của một đơn gấp (thiếu thì kéo con thứ 2 của đơn đó từ bóng phía sau về), mấy con còn lại
    /// đổi thành rác. Bấm 2 con là vào bể ngay, nhưng muốn bóng nổ thì phải ôm rác.
    /// </summary>
    private static void BaitBubbles(SoftBubbleData[] bubbles, int[] queue, int tankGoal, int lo, int hi, List<string> log,
        int count)
    {
        var candidates = new List<int>();
        for (int b = lo; b < hi; b++)
        {
            SoftBubbleData bubble = bubbles[b];
            if (!TrickSwappable(bubble) || bubble.isSpawner || bubble.isMultiLayer || bubble.FishCount < 3) continue;

            bool anyHidden = false;
            foreach (FishData one in bubble.FishDataArray)
            {
                if (one != null && one.isHidden) anyHidden = true;
            }

            if (!anyHidden) candidates.Add(b);
        }

        Shuffle(candidates);
        int done = 0;
        foreach (int b in candidates)
        {
            if (done >= count) break;

            float[] progress = BubbleProgress(bubbles, tankGoal);
            List<FishRef> refs = CollectFish(bubbles, queue, tankGoal);
            Dictionary<FishData, FishRef> byFish = IndexFish(refs);

            FishRef anchor = refs.Find(r => r.Bubble == b && r.Outer && r.Order < queue.Length &&
                                            r.Order - progress[b] >= 0f && r.Order - progress[b] <= UrgentSlack);
            if (anchor == null) continue;

            int species = anchor.Fish.FishId;
            int pair = 0;
            foreach (FishData one in bubbles[b].FishDataArray)
            {
                if (one != null && one.FishId == species) pair++;
            }

            if (pair < 2 && !PullPartner(bubbles, refs, b, anchor)) continue;

            // Mấy con còn lại thành rác; không đổi được con nào mà cũng chẳng có rác sẵn thì bóng không thành mồi.
            int junk = 0;
            foreach (FishData one in bubbles[b].FishDataArray)
            {
                if (one == null || one.FishId == species) continue;
                if (byFish.TryGetValue(one, out FishRef r) && r.Order - progress[b] >= JunkSlack) junk++;
                else if (SwapWithJunk(bubbles, byFish, progress, b, one)) junk++;
            }

            if (junk == 0) continue;

            log.Add($"mồi bóng {b + 1} (đơn #{anchor.Order + 1})");
            done++;
        }
    }

    /// <summary>Kéo một con cùng đơn với anchor từ bóng phía sau về bóng b, đổi chỗ với một con khác loài trong b.</summary>
    private static bool PullPartner(SoftBubbleData[] bubbles, List<FishRef> refs, int b, FishRef anchor)
    {
        int species = anchor.Fish.FishId;
        FishRef partner = refs.Find(r => r.Order == anchor.Order && r.Bubble > b && r.Outer && !r.Fish.isHidden &&
                                         TrickSwappable(bubbles[r.Bubble]) && !bubbles[r.Bubble].isSpawner);
        if (partner == null) return false;

        foreach (FishData one in bubbles[b].FishDataArray)
        {
            if (one == null || one == anchor.Fish || one.FishId == species) continue;

            (one.FishId, partner.Fish.FishId) = (partner.Fish.FishId, one.FishId);
            return true;
        }

        return false;
    }
}
