using System.Collections.Generic;
using UnityEditor;

/// <summary>
/// Bảng loài cá cho tool vẽ level, gán ID ĐÚNG như game gán lúc vào màn (FishModelGlobalConfig.ShuffleFishModelConfigs):
/// - ID 0..n-1 = RegularFishModelConfigs[0..n-1] theo thứ tự trong asset (ID 0 = Fish_1, ID 19 = Fish_20...).
/// - ID n.. = vài con "đặc biệt" bốc NGẪU NHIÊN từ SpecialFishModelConfigs mỗi ván, nên tool không biết trước model -
///   không đưa vào bảng màu / tool gen.
/// - ID âm = model vật cản (cá ẩn, spawner, chìa) trong DefaultFishModelConfigs.
/// Level trên 10 thì game còn xáo model giữa các ID, nên hình trong tool chỉ đúng với 10 level đầu; từ level 11 chỉ
/// còn ý nghĩa "loài nào khác loài nào".
///
/// Vì sao không dùng FishModelGlobalConfig.GetAllIds / FishModelConfigs: đó là bảng cũ (ID 0 = Fish_20, đuôi toàn
/// Prop), game hết dùng từ commit "Update New Model" (27/09) nhưng tool vẫn đọc nên cá nào cũng hiện lệch một bậc.
/// Đọc qua SerializedObject vì các danh sách mới là private trong code gameplay của dev - không phải sửa file đó.
/// </summary>
public static class ToolFishCatalog
{
    private const string RegularProperty = "<RegularFishModelConfigs>k__BackingField";
    private const string SpecialProperty = "<SpecialFishModelConfigs>k__BackingField";
    private const string DefaultProperty = "<DefaultFishModelConfigs>k__BackingField";

    private static readonly List<FishModel> regular = new();
    private static readonly Dictionary<int, FishModel> obstacles = new();
    private static int specialCount;
    private static bool loaded;

    /// <summary>Số loài thường (ID 0..RegularCount-1) - đây là số loài tối đa tool gen dùng được.</summary>
    public static int RegularCount
    {
        get
        {
            Load();
            return regular.Count;
        }
    }

    /// <summary>Số ID "đặc biệt" game bốc ngẫu nhiên mỗi ván, nằm ngay sau các ID thường.</summary>
    public static int SpecialCount
    {
        get
        {
            Load();
            return specialCount;
        }
    }

    /// <summary>ID các loài thường, dùng cho bảng màu và tool gen.</summary>
    public static int[] GetIds()
    {
        Load();
        var ids = new int[regular.Count];
        for (int i = 0; i < ids.Length; i++) ids[i] = i;
        return ids;
    }

    public static bool IsSpecial(int id)
    {
        Load();
        return id >= regular.Count && id < regular.Count + specialCount;
    }

    /// <summary>Model game hiện cho ID này ở level không xáo; null với ID đặc biệt (ngẫu nhiên) hoặc ID lạ.</summary>
    public static FishModel GetModel(int id)
    {
        Load();
        if (id >= 0) return id < regular.Count ? regular[id] : null;
        return obstacles.TryGetValue(id, out FishModel model) ? model : null;
    }

    /// <summary>Đọc lại asset: gọi khi dev đổi danh sách cá (bảng màu sẽ nhận ngay).</summary>
    public static void Reload()
    {
        loaded = false;
        Load();
    }

    private static void Load()
    {
        if (loaded) return;
        loaded = true;
        regular.Clear();
        obstacles.Clear();
        specialCount = 0;

        FishModelGlobalConfig config = FishModelGlobalConfig.Instance;
        if (config == null) return;

        var so = new SerializedObject(config);
        SerializedProperty list = so.FindProperty(RegularProperty);
        if (list != null && list.isArray)
        {
            for (int i = 0; i < list.arraySize; i++)
            {
                regular.Add(list.GetArrayElementAtIndex(i).objectReferenceValue as FishModel);
            }
        }

        // Game bốc Special.Length / 3 con đặc biệt mỗi ván (ShuffleFishModelConfigs).
        SerializedProperty special = so.FindProperty(SpecialProperty);
        if (special != null && special.isArray) specialCount = special.arraySize / 3;

        SerializedProperty defaults = so.FindProperty(DefaultProperty);
        if (defaults == null || !defaults.isArray) return;
        for (int i = 0; i < defaults.arraySize; i++)
        {
            SerializedProperty entry = defaults.GetArrayElementAtIndex(i);
            SerializedProperty id = entry.FindPropertyRelative("ID");
            SerializedProperty model = entry.FindPropertyRelative("FishModel");
            if (id == null || model == null) continue;
            obstacles[id.intValue] = model.objectReferenceValue as FishModel;
        }
    }
}
