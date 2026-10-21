using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// "Vật cản gánh độ khó": nút Đo chép level ra một bản tạm, bỏ đi TỪNG LOẠI vật cản một (băng, vỏ HP, cá ẩn, spawner,
/// nhiều lớp, cá link, khoá - chìa, khoá đơn, đá), rồi đo lại "≈ Người chơi thắng". Thắng tăng bao nhiêu khi bỏ loại
/// nào thì loại đó gánh bấy nhiêu độ khó; tăng ~0 là vật cản chỉ để trang trí.
///
/// Bỏ vật cản trên bản chép, level đang mở không đổi. Cách "bỏ" giữ nguyên số cá:
/// - Spawner / nhiều lớp: cá hàng chờ / lớp trong dồn hết ra lớp ngoài (lộ sẵn) thay vì xoá - xoá thì lệch hàng đơn.
/// - Đá: xoá bóng đá, đánh lại softBubbleId và keyId của chìa theo vị trí mới (chìa trỏ ổ theo index + 1).
/// Các phép đo dùng cùng số ván và seed với dòng "≈ Người chơi thắng" nên số gốc khớp với dòng đó.
/// Xem <see cref="LevelDesignDataEditor"/> cho danh sách các file partial khác.
/// </summary>
public partial class LevelDesignDataEditor
{
    private sealed class ObstacleImpact
    {
        public string Name;
        public int Count;
        public float Without;
    }

    /// <summary>Kết quả lần Đo gần nhất; null khi chưa đo, level không có vật cản, hoặc đã xoá tô.</summary>
    private static List<ObstacleImpact> obstacleImpacts;

    private static float obstacleBase;
    private static int obstaclePlayouts;

    private static readonly Color ImpactBar = new Color(0.25f, 0.6f, 1f, 0.9f);

    private static void ClearObstacleImpact() => obstacleImpacts = null;

    private static void MeasureObstacleImpact(LevelData level)
    {
        obstacleImpacts = null;
        SoftBubbleData[] bubbles = level?.SoftBubbleDataArray;
        if (bubbles == null || bubbles.Length == 0) return;

        int Count(Func<SoftBubbleData, bool> has)
        {
            int n = 0;
            foreach (SoftBubbleData b in bubbles)
            {
                if (b != null && has(b)) n++;
            }

            return n;
        }

        int hidden = 0;
        foreach (SoftBubbleData b in bubbles)
        {
            if (b == null) continue;
            hidden += CountHidden(b.FishDataArray) + CountHidden(b.fishOnSpawner);
            if (b.layers == null) continue;
            foreach (BubbleLayerData layer in b.layers) hidden += CountHidden(layer?.FishDataArray);
        }

        var kinds = new List<(string name, int count, Action<List<SoftBubbleData>> strip)>
        {
            ("Băng / khói", Count(b => b.isIce && !b.isRock), StripIce),
            ("Vỏ HP", Count(b => b.isBubbleHp && !b.isRock), StripHp),
            ("Cá ẩn", hidden, StripHidden),
            ("Spawner", Count(b => b.isSpawner && !b.isRock), StripSpawners),
            ("Nhiều lớp", Count(b => b.isMultiLayer && !b.isRock), StripLayers),
            ("Cá link", Count(b => b.isBundleBubble && !b.isRock), StripBundles),
            ("Khoá - chìa", Count(b => b.isLock && !b.isRock), StripLocks),
            ("Khoá đơn", Count(b => b.isOrderBubble && !b.isRock), StripOrderLocks),
            ("Đá", Count(b => b.isRock), StripRocks)
        };

        kinds.RemoveAll(k => k.count == 0);
        if (kinds.Count == 0) return;

        float quick = LevelHumanBot.EstimateWinRate(level, PlayerMistakeRate);
        obstaclePlayouts = LevelHumanBot.PlayoutsFor(quick);
        obstacleBase = LevelHumanBot.EstimateWinRate(level, PlayerMistakeRate, obstaclePlayouts);

        var result = new List<ObstacleImpact>();
        try
        {
            for (int i = 0; i < kinds.Count; i++)
            {
                (string name, int count, Action<List<SoftBubbleData>> strip) = kinds[i];
                EditorUtility.DisplayProgressBar("Đo vật cản", $"Bỏ {name}", (float)i / kinds.Count);

                LevelData copy = CopyWithout(level, strip);
                if (copy == null) continue;

                result.Add(new ObstacleImpact
                {
                    Name = name,
                    Count = count,
                    Without = LevelHumanBot.EstimateWinRate(copy, PlayerMistakeRate, obstaclePlayouts)
                });
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        result.Sort((a, b) => (b.Without - obstacleBase).CompareTo(a.Without - obstacleBase));
        obstacleImpacts = result;
    }

    private static int CountHidden(FishData[] fish)
    {
        int n = 0;
        if (fish == null) return n;
        foreach (FishData one in fish)
        {
            if (one != null && one.isHidden) n++;
        }

        return n;
    }

    /// <summary>Chép sâu level qua JSON rồi bỏ một loại vật cản trên bản chép. Null khi chép lỗi.</summary>
    private static LevelData CopyWithout(LevelData level, Action<List<SoftBubbleData>> strip)
    {
        LevelData clone;
        try
        {
            clone = DataSerializer.FromJson<LevelData>(DataSerializer.ToJson(level));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Đo vật cản: không chép được level - {e.Message}");
            return null;
        }

        if (clone?.SoftBubbleDataArray == null) return null;

        var list = new List<SoftBubbleData>(clone.SoftBubbleDataArray);
        strip(list);
        return LevelData.Create(clone.Level, clone.Difficulty, clone.OrderQueue, list.ToArray());
    }

    private static void StripIce(List<SoftBubbleData> list)
    {
        foreach (SoftBubbleData b in list)
        {
            if (b != null) b.isIce = false;
        }
    }

    private static void StripHp(List<SoftBubbleData> list)
    {
        foreach (SoftBubbleData b in list)
        {
            if (b == null) continue;
            b.isBubbleHp = false;
            b.bubbleHp = 0;
        }
    }

    private static void StripHidden(List<SoftBubbleData> list)
    {
        static void Reveal(FishData[] fish)
        {
            if (fish == null) return;
            foreach (FishData one in fish)
            {
                if (one != null) one.isHidden = false;
            }
        }

        foreach (SoftBubbleData b in list)
        {
            if (b == null) continue;
            Reveal(b.FishDataArray);
            Reveal(b.fishOnSpawner);
            if (b.layers == null) continue;
            foreach (BubbleLayerData layer in b.layers) Reveal(layer?.FishDataArray);
        }
    }

    private static void StripSpawners(List<SoftBubbleData> list)
    {
        foreach (SoftBubbleData b in list)
        {
            if (b == null || !b.isSpawner) continue;
            var fish = new List<FishData>(b.FishDataArray ?? Array.Empty<FishData>());
            if (b.fishOnSpawner != null) fish.AddRange(b.fishOnSpawner);
            b.FishDataArray = fish.ToArray();
            b.fishOnSpawner = Array.Empty<FishData>();
            b.isSpawner = false;
        }
    }

    private static void StripLayers(List<SoftBubbleData> list)
    {
        foreach (SoftBubbleData b in list)
        {
            if (b == null || !b.isMultiLayer) continue;
            var fish = new List<FishData>(b.FishDataArray ?? Array.Empty<FishData>());
            if (b.layers != null)
            {
                foreach (BubbleLayerData layer in b.layers)
                {
                    if (layer?.FishDataArray != null) fish.AddRange(layer.FishDataArray);
                }
            }

            b.FishDataArray = fish.ToArray();
            b.layers = Array.Empty<BubbleLayerData>();
            b.isMultiLayer = false;
        }
    }

    private static void StripBundles(List<SoftBubbleData> list)
    {
        foreach (SoftBubbleData b in list)
        {
            if (b != null) b.isBundleBubble = false;
        }
    }

    private static void StripLocks(List<SoftBubbleData> list)
    {
        foreach (SoftBubbleData b in list)
        {
            if (b == null) continue;
            b.isLock = false;
            b.isKey = false;
            b.keyId = 0;
        }
    }

    private static void StripOrderLocks(List<SoftBubbleData> list)
    {
        foreach (SoftBubbleData b in list)
        {
            if (b != null) b.isOrderBubble = false;
        }
    }

    /// <summary>Xoá bóng đá, đánh lại softBubbleId và keyId (chìa trỏ ổ theo index + 1 cũ → mới).</summary>
    private static void StripRocks(List<SoftBubbleData> list)
    {
        var newIndex = new int[list.Count];
        var kept = new List<SoftBubbleData>(list.Count);
        for (int i = 0; i < list.Count; i++)
        {
            SoftBubbleData b = list[i];
            if (b == null || b.isRock)
            {
                newIndex[i] = -1;
                continue;
            }

            newIndex[i] = kept.Count;
            kept.Add(b);
        }

        for (int i = 0; i < kept.Count; i++)
        {
            SoftBubbleData b = kept[i];
            b.softBubbleId = i + 1;
            if (!b.isKey || b.keyId <= 0 || b.keyId > newIndex.Length) continue;
            int target = newIndex[b.keyId - 1];
            b.keyId = target >= 0 ? target + 1 : 0;
        }

        list.Clear();
        list.AddRange(kept);
    }

    /// <summary>Bảng "bỏ loại vật cản nào thì thắng thêm bao nhiêu", vẽ dưới chi tiết Hay thua.</summary>
    private static void DrawObstacleImpact()
    {
        if (obstacleImpacts == null || obstacleImpacts.Count == 0) return;

        EditorGUILayout.Space(6f);
        GUILayout.Label("Vật cản gánh độ khó", EditorStyles.boldLabel);
        GUILayout.Label($"≈ Người chơi thắng {obstacleBase:P0} - bỏ từng loại vật cản rồi đo lại ({obstaclePlayouts} ván)",
            EditorStyles.wordWrappedMiniLabel);

        float maxGain = 0.05f;
        foreach (ObstacleImpact impact in obstacleImpacts) maxGain = Mathf.Max(maxGain, impact.Without - obstacleBase);

        foreach (ObstacleImpact impact in obstacleImpacts)
        {
            float gain = impact.Without - obstacleBase;
            string label = $"Bỏ {impact.Name} ({impact.Count})";
            string value = $"{obstacleBase:P0} → {impact.Without:P0}  {(gain >= 0f ? "+" : "")}{Mathf.RoundToInt(gain * 100f)}";
            DrawBarRow(label, Mathf.Clamp01(gain / maxGain), value, ImpactBar);
        }

        GUILayout.Label("Thanh dài = vật cản gánh nhiều độ khó. +0 = vật cản gần như không tạo khó.",
            EditorStyles.centeredGreyMiniLabel);
    }
}
