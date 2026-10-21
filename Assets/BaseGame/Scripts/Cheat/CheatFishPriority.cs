using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// List cá ưu tiên của bản cheat: tên prefab FishModel (Fish_24, Prop_4...) theo thứ tự chọn, lưu trong PlayerPrefs.
/// Level.StartGame đọc list này để FishModelGlobalConfig.ApplyPriorityModels gán các con này vào các loài của level.
/// Chỉ đọc / ghi ở bản cheat và trong Editor; bản Release luôn thấy list rỗng (giống LevelTestSelector.CheatOverride).
/// Lưu theo tên chứ không theo index: thứ tự Regular / Special trong config đổi thì list vẫn đúng con, tên không còn thì bỏ qua.
/// </summary>
public static class CheatFishPriority
{
    public const string PrefsKey = "Cheat_FishPriority";
    private const string Separator = "|";

    public static List<string> Names
    {
        get
        {
#if CHEAT_ONLY || UNITY_EDITOR
            return Parse(PlayerPrefs.GetString(PrefsKey, ""));
#else
            return new List<string>();
#endif
        }
    }

    public static bool Contains(string name) => !string.IsNullOrEmpty(name) && Names.Contains(name.Trim());

    /// <summary>Chưa có thì thêm vào cuối, có rồi thì bỏ ra. Lưu ngay.</summary>
    public static void Toggle(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        name = name.Trim();
        List<string> names = Names;
        if (!names.Remove(name)) names.Add(name);
        Save(names);
    }

    public static void Clear() => Save(new List<string>());

    /// <summary>Model của các tên đang lưu, tra trong pickable (Regular + Special của FishModelGlobalConfig).</summary>
    public static List<FishModel> ResolveModels(IEnumerable<FishModel> pickable) => Resolve(Names, pickable);

    private static void Save(List<string> names)
    {
#if CHEAT_ONLY || UNITY_EDITOR
        PlayerPrefs.SetString(PrefsKey, Format(names));
        PlayerPrefs.Save();
#endif
    }

    /// <summary>Tách chuỗi đã lưu thành list tên: bỏ khoảng trắng hai đầu, tên rỗng và tên trùng, giữ thứ tự.</summary>
    public static List<string> Parse(string raw)
    {
        var names = new List<string>();
        if (string.IsNullOrEmpty(raw)) return names;

        foreach (string part in raw.Split(Separator[0]))
        {
            string name = part.Trim();
            if (name.Length > 0 && !names.Contains(name)) names.Add(name);
        }

        return names;
    }

    public static string Format(IEnumerable<string> names) => names == null ? "" : string.Join(Separator, names);

    /// <summary>FishModel có tên trong names, theo thứ tự của names; tên không có trong pickable thì bỏ qua.</summary>
    public static List<FishModel> Resolve(IReadOnlyList<string> names, IEnumerable<FishModel> pickable)
    {
        var models = new List<FishModel>();
        if (names == null || pickable == null) return models;

        var byName = new Dictionary<string, FishModel>();
        foreach (FishModel model in pickable)
        {
            if (model != null && !byName.ContainsKey(model.name)) byName.Add(model.name, model);
        }

        foreach (string name in names)
        {
            if (name != null && byName.TryGetValue(name, out FishModel model) && !models.Contains(model)) models.Add(model);
        }

        return models;
    }
}
