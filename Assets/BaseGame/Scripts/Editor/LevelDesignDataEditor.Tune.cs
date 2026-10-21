using System.Collections.Generic;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// "Làm khó thêm" / "Làm dễ đi" cho level ĐANG MỞ (vẽ tay hay gen ra đều được), giữ bố cục người thiết kế dựng.
///
/// Leo đồi theo số đo: mỗi bước sinh TuneCandidates thay đổi nhỏ trên bản chép, đo từng cái bằng bot như người
/// (≈ người chơi thắng), giữ cái kéo về mục tiêu tốt nhất, lặp tới khi lọt mục tiêu hoặc hết số thay đổi cho phép.
/// Các phép đo cùng seed nên so được với nhau (common random numbers) - nhiễu giữa hai ứng viên nhỏ hơn nhiều so với
/// sai số tuyệt đối của 150 ván.
///
/// Thay đổi mặc định GIỮ NGUYÊN BỐ CỤC: chỉ đổi chỗ cá giữa các bóng, đổi thứ tự đơn, dời vật cản sẵn có (khói) -
/// không thêm / bớt bóng, cỡ bóng, số cá từng loài, số vật cản. Tick "Cho thêm / bớt vật cản" thì Làm khó được thêm
/// (khói, cá ẩn), Làm dễ được gỡ (khói, vỏ HP, cá ẩn, cá link, khoá - chìa).
///
/// "Ưu tiên bẫy": chấm điểm cộng thêm thay đổi làm bot KHÔNG lỡ tay cũng thua nhiều hơn (Làm khó) / ít hơn (Làm dễ) -
/// để level khó vì bẫy chứ không phải vì "bấm nhầm một lần là chết". Tốn thêm một phép đo mỗi ứng viên.
///
/// Làm khó: mỗi thay đổi được giữ phải qua solver (level vẫn có đường thắng). Cả lượt chỉnh là MỘT bước Undo.
/// Xem <see cref="LevelDesignDataEditor"/> cho danh sách các file partial khác.
/// </summary>
public partial class LevelDesignDataEditor
{
    /// <summary>Mục tiêu (%) ≈ người chơi thắng; âm = theo tag của level (LevelHumanBot.PlayerTargetWinRate).</summary>
    private int tuneTarget = -1;

    private int tuneMaxEdits = 8;
    private int tuneKeepOpening = OpeningSafeBubbles;
    private bool tuneAllowObstacles;
    private bool tunePreferTraps = true;
    private bool showTune = true;

    private const int TuneSearchPlayouts = 150;
    private const int TuneTrapPlayouts = 100;

    /// <summary>Thay đổi phải dịch % thắng ít nhất bấy nhiêu mới được giữ - nhỏ hơn là nhiễu đo.</summary>
    private const float TuneMinGain = 0.015f;

    /// <summary>Trọng số phần "bẫy" (đo ở lỡ tay 0%) khi chấm ứng viên, so với phần ≈ người chơi.</summary>
    private const float TuneTrapWeight = 0.5f;

    /// <summary>Một thay đổi thử trên bản chép: sửa thẳng bubbles / queue, trả mô tả, hoặc null khi không áp được.</summary>
    private delegate string TuneEdit(SoftBubbleData[] bubbles, int[] queue);

    private sealed class TuneCandidate
    {
        public LevelData Level;
        public string Description;
        public float Win;
        public float Trap;
        public float Score;
        public bool Overshoot;
    }

    private void DrawTunePanel(LevelDesignData data)
    {
        EditorGUILayout.Space(6f);
        if (GUILayout.Button(showTune ? "▼ Chỉnh độ khó level đang mở" : "▶ Chỉnh độ khó level đang mở",
                SirenixGUIStyles.BoldLabelCentered, GUILayout.ExpandWidth(true)))
        {
            showTune = !showTune;
        }

        if (!showTune) return;

        LevelData level = data.levelData;
        int auto = TagTargetPercent(level);
        int shown = tuneTarget < 0 ? auto : tuneTarget;

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent("Chỉnh theo lỡ tay:", "Tỉ lệ thắng ở mức lỡ tay này được kéo về mục tiêu. " +
                                                             "Đồng bộ với tab \"Xem theo lỡ tay\" của báo cáo Đo."),
            EditorStyles.miniLabel, GUILayout.Width(110f));
        for (int i = 0; i < MistakeRange.Length; i++)
        {
            bool on = TuneRateIndex == i;
            string label = $"{Mathf.RoundToInt(MistakeRange[i] * 100f)}%";
            if (GUILayout.Toggle(on, label, EditorStyles.miniButton) && !on) tuneRateIndex = i;
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        TempContent.text = "Mục tiêu thắng %";
        TempContent.tooltip = "Tỉ lệ thắng muốn đạt ở mức lỡ tay đang chọn. \"Theo tag\" (Thường 35 / Khó 12 / Siêu khó 5) " +
                              "là thang của mức lỡ tay 10% (≈ người chơi) - chỉnh theo mức khác thì nên nhập tay.";
        int typed = Mathf.Clamp(EditorGUILayout.IntField(TempContent, shown), 1, 99);
        if (typed != shown) tuneTarget = typed;
        if (GUILayout.Button(new GUIContent("Theo tag", $"Mục tiêu của tag {level?.Difficulty}: {auto}%"),
                EditorStyles.miniButton, GUILayout.Width(64f)))
        {
            tuneTarget = -1;
        }

        EditorGUILayout.EndHorizontal();

        TempContent.text = "Số thay đổi tối đa";
        TempContent.tooltip = "Tool dừng sau bấy nhiêu thay đổi dù chưa tới mục tiêu - để level vẫn là level của bạn.";
        tuneMaxEdits = Mathf.Clamp(EditorGUILayout.IntField(TempContent, tuneMaxEdits), 1, 20);

        TempContent.text = "Giữ nguyên bóng đầu";
        TempContent.tooltip = "Không đụng tới bấy nhiêu bóng đầu level, để đoạn mở màn giữ đúng như bạn dựng.";
        tuneKeepOpening = Mathf.Clamp(EditorGUILayout.IntField(TempContent, tuneKeepOpening), 0, 20);

        TempContent.text = "Cho thêm / bớt vật cản";
        TempContent.tooltip = "Tắt (mặc định): chỉ đổi chỗ cá, thứ tự đơn, dời khói sẵn có - số vật cản giữ nguyên. " +
                              "Bật: Làm khó được thêm khói / cá ẩn, Làm dễ được gỡ khói, vỏ HP, cá ẩn, cá link, khoá - chìa.";
        tuneAllowObstacles = EditorGUILayout.Toggle(TempContent, tuneAllowObstacles);

        TempContent.text = "Ưu tiên bẫy";
        TempContent.tooltip = "Chấm điểm cộng cho thay đổi làm bot KHÔNG lỡ tay cũng thua nhiều hơn (khó vì bẫy, không " +
                              "phải vì bấm nhầm là chết). Chậm hơn khoảng 2/3.";
        tunePreferTraps = EditorGUILayout.Toggle(TempContent, tunePreferTraps);

        bool usable = level?.SoftBubbleDataArray != null && level.SoftBubbleDataArray.Length > 0 &&
                      level.OrderQueue is { Length: > 0 };
        GUI.enabled = usable;
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Làm khó thêm", GUILayout.Height(26f))) pending = () => TuneLevel(data, true);
        if (GUILayout.Button("Làm dễ đi", GUILayout.Height(26f))) pending = () => TuneLevel(data, false);
        EditorGUILayout.EndHorizontal();
        GUI.enabled = true;
    }

    private static int TagTargetPercent(LevelData level) => level == null
        ? Mathf.RoundToInt(LevelHumanBot.NormalTargetWinRate * 100f)
        : Mathf.RoundToInt(LevelHumanBot.PlayerTargetWinRate(level.Level, level.Difficulty) * 100f);

    /// <summary>
    /// Mức lỡ tay (index trong MistakeRange) hai nút đo theo. Đồng bộ với tab "Xem theo lỡ tay" của báo cáo Đo: bấm tab
    /// nào thì chỉnh theo mức đó (ShowLossLevel gán lại). Chưa chọn thì là mức ≈ người chơi.
    /// </summary>
    private static int tuneRateIndex = -1;

    private static int TuneRateIndex
    {
        get
        {
            if (tuneRateIndex >= 0 && tuneRateIndex < MistakeRange.Length) return tuneRateIndex;
            for (int i = 0; i < MistakeRange.Length; i++)
            {
                if (Mathf.Approximately(MistakeRange[i], PlayerMistakeRate)) return i;
            }

            return 0;
        }
    }

    private static float TuneRate => MistakeRange[TuneRateIndex];

    private static string TuneRateLabel => $"thắng (lỡ tay {Mathf.RoundToInt(TuneRate * 100f)}%)";

    private static float TuneEval(LevelData level) =>
        LevelHumanBot.EstimateWinRate(level, TuneRate, TuneSearchPlayouts);

    private static float TuneEvalTrap(LevelData level) =>
        LevelHumanBot.EstimateWinRate(level, 0f, TuneTrapPlayouts);

    private static LevelData CloneLevel(LevelData level) => CopyWithout(level, _ => { });

    private void TuneLevel(LevelDesignData data, bool harder)
    {
        LevelData level = data.levelData;
        string action = harder ? "Làm khó thêm" : "Làm dễ đi";
        if (level?.SoftBubbleDataArray == null || level.SoftBubbleDataArray.Length == 0 || level.OrderQueue.Length == 0)
        {
            SetStatus(false, $"{action}: level đang mở chưa có bóng / đơn");
            return;
        }

        int targetPercent = tuneTarget < 0 ? TagTargetPercent(level) : tuneTarget;
        float target = targetPercent / 100f;
        float tolerance = AcceptPoints(targetPercent) / 100f;
        int tankGoal = GamePlayGlobalConfig.TankGoal;

        float rate = TuneRate;
        int beforePlayouts = LevelHumanBot.PlayoutsFor(LevelHumanBot.EstimateWinRate(level, rate));
        float before = LevelHumanBot.EstimateWinRate(level, rate, beforePlayouts);

        // Đang chỉnh theo lỡ tay 0% thì phần "ưu tiên bẫy" (cũng đo ở 0%) trùng với chính số đang chỉnh - bỏ cho nhanh.
        bool preferTraps = tunePreferTraps && rate > 0f;
        bool Reached(float win) => harder ? win <= target + tolerance : win >= target - tolerance;

        if (Reached(before))
        {
            SetStatus(true, $"{action}: {TuneRateLabel} {before:P0}, đã lọt mục tiêu {target:P0} " +
                            $"(±{AcceptPoints(targetPercent)}%) - không cần chỉnh.");
            return;
        }

        LevelData work = CloneLevel(level);
        if (work == null)
        {
            SetStatus(false, $"{action}: không chép được level để thử");
            return;
        }

        // Level gốc solver chạm trần nút (level dài) thì không đòi "Giải được" - chỉ loại thay đổi làm solver kết luận vô nghiệm.
        bool requireSolved = !harder || LevelSolver.Solve(level, CheckSolverMaxNodes).Solved;

        float current = TuneEval(work);
        float currentTrap = preferTraps ? TuneEvalTrap(work) : 0f;
        var log = new List<string>();
        bool cancelled = false;

        try
        {
            for (int step = 0; step < tuneMaxEdits && !Reached(current); step++)
            {
                if (EditorUtility.DisplayCancelableProgressBar(action,
                        $"Thay đổi {step + 1}/{tuneMaxEdits} · đang ≈ {current:P0} (mục tiêu {target:P0})",
                        (float)step / tuneMaxEdits))
                {
                    cancelled = true;
                    break;
                }

                int[] counts = CountObstacles(work.SoftBubbleDataArray);
                var candidates = new List<TuneCandidate>();
                foreach (TuneEdit edit in harder ? HardenEdits(tankGoal) : EaseEdits(tankGoal))
                {
                    LevelData copy = CloneLevel(work);
                    if (copy == null) continue;

                    SoftBubbleData[] bubbles = copy.SoftBubbleDataArray;
                    var queue = (int[])copy.OrderQueue.Clone();
                    string description = edit(bubbles, queue);
                    if (string.IsNullOrEmpty(description)) continue;
                    if (!ObstacleChangeAllowed(counts, CountObstacles(bubbles), harder)) continue;

                    LevelData probe = LevelData.Create(copy.Level, copy.Difficulty, queue, bubbles);
                    float win = TuneEval(probe);
                    float gain = harder ? current - win : win - current;
                    if (gain < TuneMinGain) continue;

                    float trap = 0f;
                    float trapGain = 0f;
                    if (preferTraps)
                    {
                        trap = TuneEvalTrap(probe);
                        trapGain = harder ? currentTrap - trap : trap - currentTrap;
                    }

                    candidates.Add(new TuneCandidate
                    {
                        Level = probe,
                        Description = description,
                        Win = win,
                        Trap = trap,
                        Score = gain + TuneTrapWeight * trapGain,
                        Overshoot = harder ? win < target - tolerance : win > target + tolerance
                    });
                }

                // Không vượt mục tiêu: điểm cao trước. Vượt quá: chỉ nhận khi gần mục tiêu hơn hiện tại, gần trước.
                candidates.RemoveAll(c => c.Overshoot && Mathf.Abs(c.Win - target) >= Mathf.Abs(current - target));
                candidates.Sort((a, b) =>
                {
                    if (a.Overshoot != b.Overshoot) return a.Overshoot ? 1 : -1;
                    return a.Overshoot
                        ? Mathf.Abs(a.Win - target).CompareTo(Mathf.Abs(b.Win - target))
                        : b.Score.CompareTo(a.Score);
                });

                TuneCandidate chosen = null;
                foreach (TuneCandidate candidate in candidates)
                {
                    if (harder)
                    {
                        LevelSolver.Result result = LevelSolver.Solve(candidate.Level, CheckSolverMaxNodes);
                        if (!(result.Solved || (!requireSolved && result.NodeCapHit))) continue;
                    }

                    chosen = candidate;
                    break;
                }

                if (chosen == null) break;

                log.Add($"{chosen.Description} ({current:P0} → {chosen.Win:P0})");
                work = chosen.Level;
                current = chosen.Win;
                currentTrap = chosen.Trap;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (log.Count == 0)
        {
            SetStatus(true, $"{action}: {(cancelled ? "đã huỷ, " : "")}không tìm được thay đổi nào kéo được về mục tiêu " +
                            $"{target:P0} (đang {before:P0}). " +
                            (tuneAllowObstacles ? "Thử giảm \"Giữ nguyên bóng đầu\"." : "Thử tick \"Cho thêm / bớt vật cản\"."),
                MessageType.Warning);
            return;
        }

        int afterPlayouts = LevelHumanBot.PlayoutsFor(current);
        float after = LevelHumanBot.EstimateWinRate(work, rate, afterPlayouts);
        LevelData tuned = work;
        Mutate(data, action, () =>
        {
            level.EditorSetOrderQueue(tuned.OrderQueue);
            level.EditorSetBubbles(tuned.SoftBubbleDataArray);
            data.NormalizeBubbleIds();
        });
        ResetSelection();

        bool reached = Reached(after);
        string head = $"{action}: {TuneRateLabel} {before:P0} → {after:P0} (mục tiêu {target:P0}), " +
                      $"{log.Count} thay đổi{(cancelled ? " - đã huỷ giữa chừng" : "")}";
        var lines = new List<string> { head };
        for (int i = 0; i < log.Count; i++) lines.Add($"{i + 1}. {log[i]}");
        lines.Add("Ctrl+Z để hoàn tác cả lượt. Bấm \"Đo level đang mở\" để xem lại chỗ thua.");
        difficultyReport = string.Join("\n", lines);

        SetStatus(true, reached
            ? head
            : $"{head}\nChưa tới mục tiêu - bấm thêm lần nữa, tăng \"Số thay đổi tối đa\"" +
              (tuneAllowObstacles ? "." : " hoặc tick \"Cho thêm / bớt vật cản\"."),
            reached ? (MessageType?)null : MessageType.Warning);
    }

    // ---------------------------------------------------------------- vật cản: đếm và luật cho phép

    /// <summary>Số vật cản từng loại: băng, HP, cá ẩn, spawner, nhiều lớp, cá link, khoá, chìa, đá, khoá đơn.</summary>
    private static int[] CountObstacles(SoftBubbleData[] bubbles)
    {
        var counts = new int[10];
        foreach (SoftBubbleData b in bubbles)
        {
            if (b == null) continue;
            if (b.isIce) counts[0]++;
            if (b.isBubbleHp) counts[1]++;
            counts[2] += CountHidden(b.FishDataArray) + CountHidden(b.fishOnSpawner);
            if (b.layers != null)
            {
                foreach (BubbleLayerData layer in b.layers) counts[2] += CountHidden(layer?.FishDataArray);
            }

            if (b.isSpawner) counts[3]++;
            if (b.isMultiLayer) counts[4]++;
            if (b.isBundleBubble) counts[5]++;
            if (b.isLock) counts[6]++;
            if (b.isKey) counts[7]++;
            if (b.isRock) counts[8]++;
            if (b.isOrderBubble) counts[9]++;
        }

        return counts;
    }

    /// <summary>Không tick "Cho thêm / bớt vật cản": số vật cản phải y nguyên. Có tick: Làm khó chỉ thêm, Làm dễ chỉ bớt.</summary>
    private bool ObstacleChangeAllowed(int[] before, int[] after, bool harder)
    {
        for (int i = 0; i < before.Length; i++)
        {
            if (after[i] == before[i]) continue;
            if (!tuneAllowObstacles) return false;
            if (harder && after[i] < before[i]) return false;
            if (!harder && after[i] > before[i]) return false;
        }

        return true;
    }

    // ---------------------------------------------------------------- ứng viên

    /// <summary>Khúc bóng ngẫu nhiên dài ~1/4 level (ít nhất 4 bóng), không đụng tuneKeepOpening bóng đầu.</summary>
    private (int lo, int hi) TuneWindow(int count)
    {
        int keep = Mathf.Clamp(tuneKeepOpening, 0, Mathf.Max(0, count - 1));
        int width = Mathf.Max(4, count / 4);
        int lo = Random.Range(keep, Mathf.Max(keep + 1, count - width + 1));
        return (lo, Mathf.Min(count, lo + width));
    }

    /// <summary>Vị trí đầu hàng đơn tương ứng với các bóng giữ nguyên - không dời đơn vào đó.</summary>
    private int KeepOrders(int bubbleCount, int orderCount) =>
        Mathf.Clamp(Mathf.RoundToInt((float)tuneKeepOpening / Mathf.Max(1, bubbleCount) * orderCount), 0, orderCount);

    private static string First(List<string> log) => log.Count > 0 ? log[0] : null;

    private List<TuneEdit> HardenEdits(int tankGoal)
    {
        var edits = new List<TuneEdit>
        {
            (b, q) => WithWindow(b, (lo, hi, log) => HideThirds(b, q, tankGoal, lo, hi, log, 1)),
            (b, q) => WithWindow(b, (lo, hi, log) => BaitBubbles(b, q, tankGoal, lo, hi, log, 1)),
            (b, q) => WithWindow(b, (lo, hi, log) => DoubleOrders(q, b.Length, lo, hi, log, 1)),
            (b, q) => WithWindow(b, (lo, hi, log) => BlockUrgent(b, q, tankGoal, lo, hi, log, 1)),
            (b, q) => WithWindow(b, (lo, hi, log) => SpawnerJunk(b, q, tankGoal, lo, hi, log)),
            (b, q) => WithWindow(b, (lo, hi, log) => BundleLate(b, q, tankGoal, lo, hi, log)),
            (b, q) => MoveOrder(b.Length, q, true),
            (b, q) => MoveOrder(b.Length, q, true),
            (b, q) => SpreadSwap(b),
            (b, q) => SpreadSwap(b)
        };

        edits.Add((b, q) =>
        {
            var log = new List<string>();
            KeyJunk(b, q, tankGoal, log);
            return log.Count > 0 ? string.Join(", ", log) : null;
        });

        if (tuneAllowObstacles) edits.Add((b, q) => HideUrgentFish(b, q, tankGoal));
        return edits;
    }

    private List<TuneEdit> EaseEdits(int tankGoal)
    {
        var edits = new List<TuneEdit>
        {
            (b, q) => MoveOrder(b.Length, q, false),
            (b, q) => MoveOrder(b.Length, q, false),
            (b, q) => SplitDoubleOrder(b.Length, q),
            (b, q) => PullThird(b, q, tankGoal),
            (b, q) => PullThird(b, q, tankGoal),
            (b, q) => MoveIceAway(b, q, tankGoal),
            (b, q) => MoveHiddenToJunk(b, q, tankGoal),
            (b, q) => SpawnerFeed(b, q, tankGoal),
            (b, q) => SpreadSwap(b)
        };

        if (tuneAllowObstacles)
        {
            edits.Add((b, q) => RemoveObstacle(b, q, tankGoal, "khói", x => x.isIce, x => x.isIce = false));
            edits.Add((b, q) => RemoveObstacle(b, q, tankGoal, "vỏ HP", x => x.isBubbleHp, x =>
            {
                x.isBubbleHp = false;
                x.bubbleHp = 0;
            }));
            edits.Add((b, q) => RemoveObstacle(b, q, tankGoal, "cá link", x => x.isBundleBubble, x => x.isBundleBubble = false));
            edits.Add((b, q) => RevealUrgentFish(b, q, tankGoal));
            edits.Add((b, q) => RemoveLockPair(b));
        }

        return edits;
    }

    private string WithWindow(SoftBubbleData[] bubbles, System.Action<int, int, List<string>> apply)
    {
        (int lo, int hi) = TuneWindow(bubbles.Length);
        var log = new List<string>();
        apply(lo, hi, log);
        return First(log);
    }

    /// <summary>Dời một đơn trong khúc ngẫu nhiên lên sớm (Làm khó) hoặc xuống muộn (Làm dễ) 3-8 bậc.</summary>
    private string MoveOrder(int bubbleCount, int[] queue, bool earlier)
    {
        if (queue.Length < 3) return null;

        (int lo, int hi) = TuneWindow(bubbleCount);
        int keep = KeepOrders(bubbleCount, queue.Length);
        int qlo = Mathf.Max(keep, Mathf.RoundToInt((float)lo / bubbleCount * queue.Length));
        int qhi = Mathf.Clamp(Mathf.RoundToInt((float)hi / bubbleCount * queue.Length), qlo + 1, queue.Length);
        if (qlo >= queue.Length) return null;

        int from = Random.Range(qlo, qhi);
        int distance = Random.Range(3, 9);
        int to = earlier ? Mathf.Max(keep, from - distance) : Mathf.Min(queue.Length - 1, from + distance);
        if (to == from || queue[to] == queue[from]) return null;

        int order = queue[from];
        if (to < from)
        {
            for (int k = from; k > to; k--) queue[k] = queue[k - 1];
        }
        else
        {
            for (int k = from; k < to; k++) queue[k] = queue[k + 1];
        }

        queue[to] = order;
        return earlier
            ? $"đẩy đơn {from + 1} (loài {order}) lên {from - to} bậc"
            : $"lùi đơn {from + 1} (loài {order}) xuống {to - from} bậc";
    }

    /// <summary>Gỡ đơn đôi: hai đơn cùng loài liền nhau thì lùi đơn sau xuống 4-8 bậc.</summary>
    private string SplitDoubleOrder(int bubbleCount, int[] queue)
    {
        int keep = KeepOrders(bubbleCount, queue.Length);
        var doubles = new List<int>();
        for (int q = Mathf.Max(0, keep - 1); q < queue.Length - 2; q++)
        {
            if (queue[q] == queue[q + 1]) doubles.Add(q + 1);
        }

        if (doubles.Count == 0) return null;

        int from = doubles[Random.Range(0, doubles.Count)];
        int to = Mathf.Min(queue.Length - 1, from + Random.Range(4, 9));
        int order = queue[from];
        for (int k = from; k < to; k++) queue[k] = queue[k + 1];
        queue[to] = order;
        return $"gỡ đơn đôi #{from}-#{from + 1} (lùi đơn sau {to - from} bậc)";
    }

    /// <summary>Đổi một cặp cá lộ giữa một bóng trong khúc và một bóng cách 4-12 bóng phía sau.</summary>
    private string SpreadSwap(SoftBubbleData[] bubbles)
    {
        (int lo, int hi) = TuneWindow(bubbles.Length);
        int i = Random.Range(lo, hi);
        int j = i + Random.Range(4, 13);
        if (j >= bubbles.Length || !TrickSwappable(bubbles[i]) || !TrickSwappable(bubbles[j])) return null;

        FishData a = RandomVisibleFish(bubbles[i]);
        FishData b = RandomVisibleFish(bubbles[j]);
        if (a == null || b == null || a.FishId == b.FishId) return null;

        (a.FishId, b.FishId) = (b.FishId, a.FishId);
        return $"đổi cá bóng {i + 1} ↔ bóng {j + 1}";
    }

    private static FishData RandomVisibleFish(SoftBubbleData bubble)
    {
        var pool = new List<FishData>();
        foreach (FishData one in bubble.FishDataArray)
        {
            if (one != null && !one.isHidden) pool.Add(one);
        }

        return pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : null;
    }

    /// <summary>Số đơn còn tới lúc được cần (slack) của từng bóng - nhỏ là gấp - và đơn gấp nhất của nó.</summary>
    private static float[] BubbleSlack(SoftBubbleData[] bubbles, int[] queue, int tankGoal, out int[] urgentOrder)
    {
        float[] progress = BubbleProgress(bubbles, tankGoal);
        var slack = new float[bubbles.Length];
        urgentOrder = new int[bubbles.Length];
        for (int b = 0; b < bubbles.Length; b++)
        {
            slack[b] = float.MaxValue;
            urgentOrder[b] = -1;
        }

        foreach (FishRef r in CollectFish(bubbles, queue, tankGoal))
        {
            if (r.Order >= queue.Length) continue;
            float s = r.Order - progress[r.Bubble];
            if (s >= slack[r.Bubble]) continue;
            slack[r.Bubble] = s;
            urgentOrder[r.Bubble] = r.Order;
        }

        return slack;
    }

    /// <summary>Làm dễ: con cuối của một đơn ở xa thì đổi về sát con thứ 2 (bóng trước / sau nó một chút).</summary>
    private string PullThird(SoftBubbleData[] bubbles, int[] queue, int tankGoal)
    {
        (int lo, int hi) = TuneWindow(bubbles.Length);
        var byOrder = new Dictionary<int, List<FishRef>>();
        foreach (FishRef r in CollectFish(bubbles, queue, tankGoal))
        {
            if (r.Order >= queue.Length) continue;
            if (!byOrder.TryGetValue(r.Order, out List<FishRef> list)) byOrder[r.Order] = list = new List<FishRef>();
            list.Add(r);
        }

        var orders = new List<int>();
        foreach (KeyValuePair<int, List<FishRef>> pair in byOrder)
        {
            List<FishRef> set = pair.Value;
            if (set.Count < 2) continue;
            if (set[1].Bubble >= lo && set[1].Bubble < hi && set[set.Count - 1].Bubble - set[1].Bubble >= 3) orders.Add(pair.Key);
        }

        Shuffle(orders);
        foreach (int order in orders)
        {
            List<FishRef> set = byOrder[order];
            FishRef last = set[set.Count - 1];
            int near = set[1].Bubble;
            for (int t = Mathf.Max(tuneKeepOpening, near - 1); t <= Mathf.Min(bubbles.Length - 1, near + 1); t++)
            {
                if (t == last.Bubble || !TrickSwappable(bubbles[t]) || bubbles[t].isSpawner) continue;

                foreach (FishData one in bubbles[t].FishDataArray)
                {
                    if (one == null || one.isHidden || one.FishId == last.Fish.FishId) continue;

                    (one.FishId, last.Fish.FishId) = (last.Fish.FishId, one.FishId);
                    return $"kéo con cuối đơn #{order + 1} về bóng {t + 1} (từ bóng {last.Bubble + 1})";
                }
            }
        }

        return null;
    }

    /// <summary>Làm dễ: dời khói từ bóng gấp nhất trong khúc sang bóng ít gấp nhất (số bóng khói giữ nguyên).</summary>
    private string MoveIceAway(SoftBubbleData[] bubbles, int[] queue, int tankGoal)
    {
        float[] slack = BubbleSlack(bubbles, queue, tankGoal, out _);
        (int lo, int hi) = TuneWindow(bubbles.Length);

        int from = -1;
        for (int b = lo; b < hi; b++)
        {
            if (bubbles[b] != null && bubbles[b].isIce && (from < 0 || slack[b] < slack[from])) from = b;
        }

        if (from < 0 || slack[from] > UrgentSlack) return null;

        int to = -1;
        for (int b = tuneKeepOpening; b < bubbles.Length; b++)
        {
            SoftBubbleData x = bubbles[b];
            if (!TrickSwappable(x) || x.isIce || x.isBubbleHp || x.isSpawner || x.isMultiLayer) continue;
            if (to < 0 || slack[b] > slack[to]) to = b;
        }

        if (to < 0 || slack[to] <= slack[from]) return null;

        bubbles[from].isIce = false;
        bubbles[to].isIce = true;
        return $"dời khói bóng {from + 1} → bóng {to + 1}";
    }

    /// <summary>Làm dễ: trong bóng có cá ẩn đang gấp, chuyển "ẩn" sang con ít gấp nhất cùng bóng (số cá ẩn giữ nguyên).</summary>
    private string MoveHiddenToJunk(SoftBubbleData[] bubbles, int[] queue, int tankGoal)
    {
        float[] progress = BubbleProgress(bubbles, tankGoal);
        Dictionary<FishData, FishRef> byFish = IndexFish(CollectFish(bubbles, queue, tankGoal));
        (int lo, int hi) = TuneWindow(bubbles.Length);

        for (int b = lo; b < hi; b++)
        {
            SoftBubbleData bubble = bubbles[b];
            if (bubble?.FishDataArray == null || bubble.isRock) continue;

            FishData hidden = null;
            FishData junk = null;
            foreach (FishData one in bubble.FishDataArray)
            {
                if (one == null || !byFish.TryGetValue(one, out FishRef r)) continue;
                if (one.isHidden)
                {
                    if (r.Order - progress[b] <= UrgentSlack) hidden = one;
                }
                else if (junk == null || r.Order > byFish[junk].Order)
                {
                    junk = one;
                }
            }

            if (hidden == null || junk == null || byFish[junk].Order <= byFish[hidden].Order) continue;

            hidden.isHidden = false;
            junk.isHidden = true;
            return $"chuyển cá ẩn bóng {b + 1} sang con chưa cần";
        }

        return null;
    }

    /// <summary>Làm dễ: hàng chờ spawner nhả cá rác thì đổi một con rác lấy cá gấp ở vài bóng phía sau.</summary>
    private string SpawnerFeed(SoftBubbleData[] bubbles, int[] queue, int tankGoal)
    {
        float[] progress = BubbleProgress(bubbles, tankGoal);
        Dictionary<FishData, FishRef> byFish = IndexFish(CollectFish(bubbles, queue, tankGoal));
        (int lo, int hi) = TuneWindow(bubbles.Length);

        for (int b = lo; b < hi; b++)
        {
            SoftBubbleData bubble = bubbles[b];
            if (bubble == null || bubble.isRock || !bubble.isSpawner || bubble.fishOnSpawner == null) continue;

            foreach (FishData queued in bubble.fishOnSpawner)
            {
                if (queued == null || !byFish.TryGetValue(queued, out FishRef mine)) continue;
                if (mine.Order - progress[b] < JunkSlack) continue;

                for (int t = b + JunkSearchFrom; t <= Mathf.Min(bubbles.Length - 1, b + JunkSearchTo); t++)
                {
                    if (!TrickSwappable(bubbles[t]) || bubbles[t].isSpawner) continue;
                    foreach (FishData fish in bubbles[t].FishDataArray)
                    {
                        if (fish == null || fish.isHidden || fish.FishId == queued.FishId) continue;
                        if (!byFish.TryGetValue(fish, out FishRef other) || other.Order - progress[b] > UrgentSlack) continue;

                        (fish.FishId, queued.FishId) = (queued.FishId, fish.FishId);
                        return $"spawner bóng {b + 1} nhả cá cần (đổi với bóng {t + 1})";
                    }
                }
            }
        }

        return null;
    }

    /// <summary>Làm khó (có tick vật cản): giấu một con cá đang gấp ngay tại chỗ.</summary>
    private string HideUrgentFish(SoftBubbleData[] bubbles, int[] queue, int tankGoal)
    {
        float[] progress = BubbleProgress(bubbles, tankGoal);
        (int lo, int hi) = TuneWindow(bubbles.Length);
        List<FishRef> refs = CollectFish(bubbles, queue, tankGoal);
        var order = new List<int>(refs.Count);
        for (int i = 0; i < refs.Count; i++) order.Add(i);
        Shuffle(order);
        foreach (int index in order)
        {
            FishRef r = refs[index];
            if (!r.Outer || r.Bubble < lo || r.Bubble >= hi || r.Order >= queue.Length) continue;
            if (r.Order - progress[r.Bubble] > UrgentSlack) continue;
            if (TryHideInPlace(bubbles[r.Bubble], r.Fish)) return $"giấu cá gấp ở bóng {r.Bubble + 1} (đơn #{r.Order + 1})";
        }

        return null;
    }

    /// <summary>Làm dễ (có tick vật cản): lộ một con cá ẩn đang gấp.</summary>
    private string RevealUrgentFish(SoftBubbleData[] bubbles, int[] queue, int tankGoal)
    {
        float[] progress = BubbleProgress(bubbles, tankGoal);
        (int lo, int hi) = TuneWindow(bubbles.Length);
        foreach (FishRef r in CollectFish(bubbles, queue, tankGoal))
        {
            if (!r.Fish.isHidden || r.Bubble < lo || r.Bubble >= hi) continue;
            if (r.Order - progress[r.Bubble] > UrgentSlack) continue;

            r.Fish.isHidden = false;
            return $"lộ cá ẩn bóng {r.Bubble + 1} (đơn #{r.Order + 1})";
        }

        return null;
    }

    /// <summary>Làm dễ (có tick vật cản): gỡ một vật cản loại "name" ở bóng gấp nhất trong khúc.</summary>
    private string RemoveObstacle(SoftBubbleData[] bubbles, int[] queue, int tankGoal, string name,
        System.Func<SoftBubbleData, bool> has, System.Action<SoftBubbleData> remove)
    {
        float[] slack = BubbleSlack(bubbles, queue, tankGoal, out _);
        (int lo, int hi) = TuneWindow(bubbles.Length);
        int pick = -1;
        for (int b = lo; b < hi; b++)
        {
            if (bubbles[b] == null || bubbles[b].isRock || !has(bubbles[b])) continue;
            if (pick < 0 || slack[b] < slack[pick]) pick = b;
        }

        if (pick < 0) return null;
        remove(bubbles[pick]);
        return $"gỡ {name} bóng {pick + 1}";
    }

    /// <summary>Làm dễ (có tick vật cản): gỡ một cặp khoá - chìa (ổ và chìa trỏ tới nó).</summary>
    private string RemoveLockPair(SoftBubbleData[] bubbles)
    {
        var locks = new List<int>();
        for (int b = tuneKeepOpening; b < bubbles.Length; b++)
        {
            if (bubbles[b] != null && bubbles[b].isLock && !bubbles[b].isRock) locks.Add(b);
        }

        if (locks.Count == 0) return null;

        int lockIndex = locks[Random.Range(0, locks.Count)];
        bubbles[lockIndex].isLock = false;
        foreach (SoftBubbleData b in bubbles)
        {
            if (b == null || !b.isKey || b.keyId != lockIndex + 1) continue;
            b.isKey = false;
            b.keyId = 0;
        }

        return $"gỡ khoá bóng {lockIndex + 1} và chìa của nó";
    }
}
