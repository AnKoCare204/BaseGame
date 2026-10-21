using System.Collections.Generic;

/// <summary>
/// Gán cá ưu tiên (list chọn ở panel cheat, xem CheatFishPriority) vào bảng ID -> model đã xáo của level.
/// </summary>
public static class FishModelPriority
{
    /// <summary>
    /// Loài thứ k của level (theo thứ tự xuất hiện đầu tiên trong orderQueue) nhận preferred[k]; preferred bỏ null và con trùng.
    /// Gán bằng cách đổi chỗ: entry đang giữ con ưu tiên nhận lại model cũ của loài, nên bảng không có hai ID trùng model.
    /// Con ưu tiên không có trong bảng (Special không được bốc) thì model cũ của loài rơi ra khỏi bảng.
    /// Chỉ đụng entry ID ≥ 0: entry ID âm là model vật cản, cũng là object nằm trong asset FishModelGlobalConfig.
    /// Loài không có entry thì bỏ qua, con ưu tiên chuyển cho loài kế. Thừa cá ưu tiên thì các con cuối không dùng.
    /// </summary>
    public static void Apply(IList<FishModelConfig> table, IReadOnlyList<int> orderQueue, IReadOnlyList<FishModel> preferred)
    {
        if (table == null || orderQueue == null || preferred == null) return;

        var models = new List<FishModel>(preferred.Count);
        foreach (FishModel model in preferred)
        {
            if (model != null && !models.Contains(model)) models.Add(model);
        }

        var seen = new HashSet<int>();
        int next = 0;
        foreach (int species in orderQueue)
        {
            if (next >= models.Count) break;
            if (species < 0 || !seen.Add(species)) continue;

            FishModelConfig target = FindById(table, species);
            if (target == null) continue;

            FishModel wanted = models[next++];
            if (target.FishModel == wanted) continue;

            FishModelConfig holder = FindHolder(table, wanted);
            if (holder != null) holder.FishModel = target.FishModel;
            target.FishModel = wanted;
        }
    }

    /// <summary>
    /// Như Apply nhưng xáo một bản sao của preferred trước (Fisher-Yates): chọn nhiều cá hơn số loài của level thì mỗi level
    /// bốc ngẫu nhiên các con khác nhau, chơi vài level là thấy hết. List của người gọi giữ nguyên thứ tự.
    /// </summary>
    public static void ApplyShuffled(IList<FishModelConfig> table, IReadOnlyList<int> orderQueue, IReadOnlyList<FishModel> preferred,
        System.Random random)
    {
        if (preferred == null || random == null) return;

        var shuffled = new List<FishModel>(preferred);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        Apply(table, orderQueue, shuffled);
    }

    /// <summary>Entry đầu tiên có đúng ID, như FishModelGlobalConfig.GetFishModelConfigById tra.</summary>
    private static FishModelConfig FindById(IList<FishModelConfig> table, int id)
    {
        foreach (FishModelConfig config in table)
        {
            if (config != null && config.ID == id) return config;
        }

        return null;
    }

    private static FishModelConfig FindHolder(IList<FishModelConfig> table, FishModel model)
    {
        foreach (FishModelConfig config in table)
        {
            if (config != null && config.ID >= 0 && config.FishModel == model) return config;
        }

        return null;
    }
}
