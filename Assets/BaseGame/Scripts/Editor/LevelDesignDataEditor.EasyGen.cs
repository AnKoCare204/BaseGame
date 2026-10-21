using System.Collections.Generic;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Mức "Dễ (level đầu)" của cột Sinh theo độ khó - dành cho level 1..20, mục tiêu người chơi MATCH LIÊN TỤC
/// (~4 giây / match), thắng trên 90%.
///
/// Số liệu thật của 40 level đầu (bản test 1): level 1 chạy 3.3 s/match, level 2..14 tốn 5.5..12.4 s/match vì bóng
/// lẫn 2-3 loài, người chơi phải dò cả bàn. Mode này xếp cá thành CHUỖI LIỀN: 3 con một bộ đứng sát nhau, rồi cắt
/// chuỗi thành bóng 1..5 cá (đa số 1-3, ít bóng 4-5, không có bóng 6). Bấm theo thứ tự là vừa đủ bộ vừa nổ bóng liên
/// tục - không bắt bóng phải chứa đúng bội số 3.
///
/// Độ khó tăng dần theo level bằng "% bộ tách": con thứ 2 và 3 của bộ bị đẩy ra xa vài bóng, phải gom qua ô chờ hoặc
/// nhìn trước - người chơi bắt đầu tốn slot. Cộng thêm đẩy đơn nhẹ ở level cao. Không vật cản.
/// Bóng lấy cỡ NỚI TỐI ĐA (sàn của số cá kế tiếp + BubbleGenMaxBonus): bàn thưa, bóng to dễ nhìn dễ bấm.
///
/// Chọn "Level mẫu" 1..20 thì các núm tự điền theo đường cong mẫu (EasyTemplate); sửa tay được sau đó.
/// Nhận seed khi solver giải được và ô chờ tối đa không vượt trần của level đó.
/// </summary>
public partial class LevelDesignDataEditor
{
    private const int EasyMaxLevel = 20;
    private const int EasyAttempts = 40;

    private int easyLevel = 1;
    private int easyOrders = 8;
    private int easySpecies = 3;
    private int easySplitPercent;
    private int easySmallPercent = 35;
    private int easyLargePercent = 5;

    /// <summary>Khoảng cách (tính bằng số cá) đẩy con thứ 2 / thứ 3 của một bộ tách ra xa.</summary>
    private const int EasySplitGap = 6;
    private int easyHoldingCap;
    private int easyAdvance;

    /// <summary>Đường cong mẫu cho level 1..20 (t = 0 ở level 1, 1 ở level 20).</summary>
    private void ApplyEasyTemplate()
    {
        int level = Mathf.Clamp(easyLevel, 1, EasyMaxLevel);
        float t = (level - 1) / (float)(EasyMaxLevel - 1);
        easyOrders = Mathf.RoundToInt(Mathf.Lerp(8f, 20f, t));
        easySpecies = Mathf.RoundToInt(Mathf.Lerp(3f, 8f, t));
        easySplitPercent = level <= 3 ? 0 : Mathf.RoundToInt((level - 3) / 17f * 40f);
        easySmallPercent = Mathf.RoundToInt(Mathf.Lerp(35f, 20f, t));
        // Bóng 4-5 cá: không có ở level 1-5 (người mới chỉ thấy bóng 1-3 cá, nhìn là bấm), rồi tăng dần tới 20% ở level 20.
        easyLargePercent = level <= 5 ? 0 : Mathf.RoundToInt((level - 5) / 15f * 20f);
        easyHoldingCap = level <= 6 ? 0 : level <= 14 ? 1 : 2;
        easyAdvance = level <= 10 ? 0 : Mathf.RoundToInt((level - 10) * 0.3f);
    }

    private void DrawEasyKnobs()
    {
        GUILayout.Label("bộ 3 con nguyên vẹn trong bóng · match liên tục · level 1-20", SirenixGUIStyles.CenteredGreyMiniLabel,
            GUILayout.ExpandWidth(true));

        EditorGUI.BeginChangeCheck();
        TempContent.text = "Level mẫu";
        TempContent.tooltip = "Chọn level 1..20 thì các núm bên dưới tự điền theo đường cong mẫu: level càng cao càng " +
                              "nhiều match, nhiều loài, nhiều bộ bị tách (tốn ô chờ). Sửa tay được sau đó.";
        easyLevel = EditorGUILayout.IntSlider(TempContent, easyLevel, 1, EasyMaxLevel);
        if (EditorGUI.EndChangeCheck()) ApplyEasyTemplate();

        EditorGUILayout.Space(2f);
        TempContent.text = "Số match";
        TempContent.tooltip = "Số đơn = tổng cá / 3. Mục tiêu ~4 giây / match: 12-18 match cho level 1-5, 18-22 cho level 6-20.";
        easyOrders = Mathf.Clamp(EditorGUILayout.IntField(TempContent, easyOrders), 2, 40);

        TempContent.text = "Số loài";
        TempContent.tooltip = "Ít loài thì nhìn là thấy cá cần bấm.";
        easySpecies = Mathf.Clamp(EditorGUILayout.IntField(TempContent, easySpecies), 2, Mathf.Max(2, fishIds.Length));

        TempContent.text = "% bộ tách";
        TempContent.tooltip = "Bấy nhiêu bộ bị tách 3 con ra 3 bóng pha trộn khác nhau: phải gom qua ô chờ. 0 = mọi bộ " +
                              "nằm gọn một bóng, bấm là ăn ngay.";
        easySplitPercent = Mathf.Clamp(EditorGUILayout.IntField(TempContent, easySplitPercent), 0, 80);

        TempContent.text = "% bóng 1-2 cá";
        TempContent.tooltip = "Bóng nhỏ nổ nhanh, cho cảm giác nổ bóng liên tục.";
        easySmallPercent = Mathf.Clamp(EditorGUILayout.IntField(TempContent, easySmallPercent), 0, 100);

        TempContent.text = "% bóng 4-5 cá";
        TempContent.tooltip = "Ít thôi. Không có bóng 6 cá ở mức Dễ. Phần còn lại là bóng 3 cá.";
        easyLargePercent = Mathf.Clamp(EditorGUILayout.IntField(TempContent, easyLargePercent), 0, 100 - easySmallPercent);

        TempContent.text = "Ô chờ tối đa";
        TempContent.tooltip = "Trần: đường thắng của solver không được dùng quá bấy nhiêu ô chờ. 0 = không bao giờ cần ô chờ.";
        easyHoldingCap = Mathf.Clamp(EditorGUILayout.IntField(TempContent, easyHoldingCap), 0, LevelBands.DefaultHoldingSlots);

        TempContent.text = "Đẩy đơn";
        TempContent.tooltip = "Số bậc đẩy vài đơn lên sớm hơn thứ tự tự nhiên. 0 = đơn đến đúng lúc cá lộ ra.";
        easyAdvance = Mathf.Clamp(EditorGUILayout.IntField(TempContent, easyAdvance), 0, 10);

        GUILayout.Label($"→ {easyOrders * GamePlayGlobalConfig.TankGoal} cá · ~{easyOrders * 4} giây nếu 4 s/match",
            SirenixGUIStyles.CenteredGreyMiniLabel, GUILayout.ExpandWidth(true));
    }

    /// <summary>Sinh level dễ: bóng từ bộ nguyên vẹn, vài bộ tách ra bóng pha trộn, solver kiểm trần ô chờ.</summary>
    private void GenerateEasy(LevelDesignData data)
    {
        if (fishIds.Length == 0)
        {
            SetStatus(false, "FishModelGlobalConfig trống - không có loài cá nào để sinh");
            return;
        }

        LevelData level = data.levelData;
        int tankGoal = GamePlayGlobalConfig.TankGoal;
        int species = Mathf.Clamp(easySpecies, 2, fishIds.Length);
        int orders = Mathf.Max(2, easyOrders);

        SoftBubbleData[] best = null;
        int[] bestQueue = null;
        int bestHolding = int.MaxValue;
        int used;
        for (used = 1; used <= EasyAttempts; used++)
        {
            SoftBubbleData[] bubbles = BuildEasyBubbles(species, orders, tankGoal);
            int[] queue = FixOpeningOrders(AdvanceOrders(BuildQueueFromBubbles(bubbles, tankGoal), easyAdvance));
            LevelData probe = LevelData.Create(level.Level, level.Difficulty, queue, bubbles);
            LevelSolver.Result result = LevelSolver.Solve(probe, CheckSolverMaxNodes);
            if (!result.Solved || result.MaxHoldingUsed >= bestHolding) continue;

            best = bubbles;
            bestQueue = queue;
            bestHolding = result.MaxHoldingUsed;
            if (bestHolding <= easyHoldingCap) break;
        }

        if (best == null)
        {
            SetStatus(false, $"Thử {EasyAttempts} lượt không ra seed giải được. Giảm % bộ tách hoặc đẩy đơn.");
            return;
        }

        SoftBubbleData[] chosen = best;
        int[] chosenQueue = bestQueue;
        Mutate(data, "Sinh level dễ", () =>
        {
            level.EditorSetOrderQueue(chosenQueue);
            level.EditorSetBubbles(chosen);
            data.NormalizeBubbleIds();
        });

        ResetSelection();
        difficultyReport = MeasureLevel(level);
        string line = $"Mức Dễ (mẫu level {easyLevel}): {chosen.Length} bóng, {orders} match, {species} loài\n" +
                      $"Sau {Mathf.Min(used, EasyAttempts)} lượt: ô chờ tối đa {bestHolding}/{LevelBands.DefaultHoldingSlots} " +
                      $"(trần {easyHoldingCap})";
        bool fits = bestHolding <= easyHoldingCap;
        SetStatus(true, fits ? line : $"{line}\nKhông seed nào lọt trần ô chờ, đành lấy seed ít ô chờ nhất - giảm % bộ tách.",
            fits ? (MessageType?)null : MessageType.Warning);
    }

    /// <summary>
    /// Chuỗi cá: các bộ xếp liền (3 con một bộ đứng sát nhau); bộ bị tách thì con thứ 2 / 3 bị đẩy ra sau
    /// EasySplitGap / 2 x EasySplitGap vị trí. Rồi cắt chuỗi thành bóng 1..5 cá theo tỉ lệ bóng nhỏ / bóng lớn. Hai bộ
    /// đầu không bị tách để ván mở bằng vài cú ăn liền.
    /// </summary>
    private SoftBubbleData[] BuildEasyBubbles(int species, int orders, int tankGoal)
    {
        var pool = new List<int>(fishIds);
        Shuffle(pool);
        pool.RemoveRange(species, pool.Count - species);

        var sets = new List<int>(orders);
        for (int i = 0; i < orders; i++) sets.Add(pool[i % pool.Count]);
        Shuffle(sets);

        // Chuỗi liền: mỗi phần tử là (loài, bộ thứ mấy).
        var chain = new List<(int fish, int set)>(orders * tankGoal);
        for (int i = 0; i < sets.Count; i++)
        {
            for (int k = 0; k < tankGoal; k++) chain.Add((sets[i], i));
        }

        int split = Mathf.Min(Mathf.RoundToInt(orders * easySplitPercent / 100f), Mathf.Max(0, orders - 2));
        var candidates = new List<int>();
        for (int i = 2; i < sets.Count; i++) candidates.Add(i);
        Shuffle(candidates);
        for (int j = 0; j < split && j < candidates.Count; j++)
        {
            int set = candidates[j];

            // Đẩy con thứ 3 trước rồi con thứ 2, mỗi con ra sau chỗ cũ k x EasySplitGap vị trí.
            for (int k = tankGoal - 1; k >= 1; k--)
            {
                int index = -1;
                for (int i = 0, seen = 0; i < chain.Count; i++)
                {
                    if (chain[i].set != set) continue;
                    if (seen++ == k)
                    {
                        index = i;
                        break;
                    }
                }

                if (index < 0) continue;
                (int fish, int set) item = chain[index];
                chain.RemoveAt(index);
                chain.Insert(Mathf.Min(chain.Count, index + k * EasySplitGap), item);
            }
        }

        // Cắt chuỗi thành bóng 1..5 cá.
        var sizes = new List<int>();
        for (int left = chain.Count; left > 0;)
        {
            int roll = Random.Range(0, 100);
            int size = roll < easySmallPercent ? Random.Range(1, 3)
                : roll < easySmallPercent + easyLargePercent ? Random.Range(4, 6)
                : 3;
            size = Mathf.Min(size, left);
            sizes.Add(size);
            left -= size;
        }

        var bubbles = new SoftBubbleData[sizes.Count];
        int cursor = 0;
        for (int i = 0; i < sizes.Count; i++)
        {
            var ids = new int[sizes[i]];
            for (int k = 0; k < ids.Length; k++) ids[k] = chain[cursor + k].fish;
            cursor += ids.Length;
            bubbles[i] = SoftBubbleData.CreateWithRandomRadius(ids);
            bubbles[i].Radius = EasyRadius(ids.Length);
        }

        return bubbles;
    }

    /// <summary>
    /// Cỡ nới tối đa của bóng n cá: sàn của n + 1 cá + BubbleGenMaxBonus (trần trên của dải nới trong GenRadius), không
    /// vượt BubbleRadiusMax.
    /// </summary>
    private static float EasyRadius(int fishCount)
    {
        GamePlayGlobalConfig config = GamePlayGlobalConfig.Instance;
        if (config == null) return 2f;
        float max = config.MinBubbleRadius(fishCount + 1) + Mathf.Max(0f, config.BubbleGenMaxBonus);
        return Mathf.Round(Mathf.Min(max, LevelBands.BubbleRadiusMax) * 10f) / 10f;
    }

    /// <summary>Hai đơn đầu vào hai bể mở sẵn phải khác loài (LevelData.Validate). Trùng thì kéo đơn khác loài gần nhất lên.</summary>
    private static int[] FixOpeningOrders(int[] queue)
    {
        if (queue == null || queue.Length < 2 || queue[0] != queue[1]) return queue;
        for (int i = 2; i < queue.Length; i++)
        {
            if (queue[i] == queue[0]) continue;
            int pick = queue[i];
            for (int k = i; k > 1; k--) queue[k] = queue[k - 1];
            queue[1] = pick;
            break;
        }

        return queue;
    }
}
