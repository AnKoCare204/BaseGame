using System;
using System.Collections.Generic;
using System.IO;
using Core.GamePlay;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Bàn làm việc của tool vẽ level: giữ đúng một LevelData đang mở, còn lại chỉ là đường dẫn kho file.
/// "Tìm level" nạp LevelData_{n}.json vào levelData, "Save" ghi ngược ra chính file đó (DataSerializer.Serialize,
/// cùng format ConvertLevelData xuất ra), "Swap level" đổi chỗ hai file.
/// Biến thể của level nằm ở thư mục riêng (variantFolder), tên LevelData_{n}_{x}.json: n = level gốc, x = số lần chơi
/// level n để bật biến thể. "Load biến thể" / "Lưu biến thể" đọc ghi các file này, không đụng LevelGlobalConfig; game chơi biến thể
/// x từ lần chơi lại thứ x (LevelVariantRule), Setup đóng gói chúng.
/// Toàn bộ phần vẽ nằm ở LevelDesignDataEditor; asset này không dính gì tới runtime.
/// </summary>
[CreateAssetMenu(fileName = "LevelDesignData", menuName = "GlobalConfigs/LevelDesignData")]
public class LevelDesignData : ScriptableObject
{
    private const string FilePrefix = "LevelData_";

    [Tooltip("Data của level đang mở trong tool")]
    public LevelData levelData = new LevelData();

    [FolderPath, Tooltip("Thư mục chứa LevelData_{n}.json do ConvertLevelData xuất ra")]
    public string levelFolder = "Assets/BaseGame/TextAssets/LevelDataConverted";

    /// <summary>Thư mục biến thể mặc định; asset tạo trước khi có field variantFolder thì dùng giá trị này.</summary>
    public const string DefaultVariantFolder = CoreData.LevelAddress.VariantFolder;

    [FolderPath, Tooltip("Thư mục chứa biến thể LevelData_{n}_{x}.json: n = level gốc, x = số lần chơi level n để bật biến thể")]
    public string variantFolder = DefaultVariantFolder;

    [Min(0), Tooltip("Biến thể đang mở trong tool: 0 = level gốc, x = đang mở LevelData_{Level}_{x}.json. " +
                     "Giữ trên asset để qua domain reload nút Save vẫn biết đang mở biến thể")]
    public int openVariant;

    [Min(1), Tooltip("Level muốn đổi chỗ với level đang mở")]
    public int swapLevel = 1;

    /// <summary>Tổng cá của level: mọi lớp trong bóng + hàng chờ spawner - con số "Số lượng cá" hiện ở panel phải.</summary>
    public int TotalFishInBubbles
    {
        get
        {
            SoftBubbleData[] bubbles = levelData?.SoftBubbleDataArray;
            if (bubbles == null) return 0;
            int total = 0;
            foreach (SoftBubbleData bubble in bubbles)
            {
                if (bubble == null) continue;
                total += bubble.TotalFishCount;   // cả cá ở các lớp chưa lộ ra của bóng nhiều lớp

                // Cá trong hàng đợi spawner cũng phải gom vào bể, nên cũng tính vào phép cân với số đơn.
                if (bubble.isSpawner && !bubble.isRock) total += bubble.fishOnSpawner?.Length ?? 0;
            }

            return total;
        }
    }

    /// <summary>Số cá seed phải có để khớp OrderQueue (mỗi đơn ăn TankGoal con).</summary>
    public int RequiredFish => (levelData?.OrderQueue?.Length ?? 0) * GamePlayGlobalConfig.TankGoal;

    public string GetLevelPath(int level) => $"{levelFolder}/{FilePrefix}{level}.json";

    /// <summary>Thư mục biến thể đang dùng; ô để trống thì về mặc định.</summary>
    public string VariantFolder => string.IsNullOrWhiteSpace(variantFolder) ? DefaultVariantFolder : variantFolder.Trim();

    /// <summary>Tên file biến thể: LevelData_{level}_{variant}.json - level gốc, rồi số lần chơi để bật biến thể.</summary>
    public static string GetVariantFileName(int level, int variant) => $"{FilePrefix}{level}_{variant}.json";

    public string GetVariantPath(int level, int variant) => $"{VariantFolder}/{GetVariantFileName(level, variant)}";

    /// <summary>Đánh lại softBubbleId theo thứ tự 1..n để ID hiện trên khung bóng luôn duy nhất.</summary>
    public void NormalizeBubbleIds()
    {
        SoftBubbleData[] bubbles = levelData?.SoftBubbleDataArray;
        if (bubbles == null) return;
        for (int i = 0; i < bubbles.Length; i++)
        {
            if (bubbles[i] != null) bubbles[i].softBubbleId = i + 1;
        }
    }

#if UNITY_EDITOR
    /// <summary>Nạp LevelData_{level}.json vào levelData. Trả về false kèm lý do trong message.</summary>
    public bool LoadLevel(int level, out string message)
    {
        level = Mathf.Max(1, level);
        string path = GetLevelPath(level);
        if (!File.Exists(path))
        {
            UnityEditor.Undo.RecordObject(this, "Tìm level");
            openVariant = 0;
            levelData ??= new LevelData();
            levelData.EditorSetLevel(level);
            levelData.EditorSetOrderQueue(Array.Empty<int>());
            levelData.EditorSetBubbles(Array.Empty<SoftBubbleData>());
            UnityEditor.EditorUtility.SetDirty(this);
            message = $"Không thấy {path} — đã xoá OrderQueue và bóng";
            return false;
        }

        if (!ReadLevelFile(path, out LevelData loaded, out message)) return false;

        UnityEditor.Undo.RecordObject(this, "Tìm level");
        openVariant = 0;
        loaded.EditorSetLevel(level);
        // Gán từng phần vào instance Unity đang serialize, không thay cả object: inspector
        // SerializedObject còn giữ snapshot cũ sẽ Apply đè mất cờ (isIce) nếu đổi reference.
        ApplyLoadedLevel(loaded);
        NormalizeBubbleIds();
        UnityEditor.EditorUtility.SetDirty(this);
        message = $"Đã nạp level {level}: {levelData.BubbleCount} bóng, {TotalFishInBubbles} cá, {levelData.OrderQueue?.Length ?? 0} đơn";
        return true;
    }

    public bool HasVariant(int level, int variant) => File.Exists(GetVariantPath(level, variant));

    /// <summary>Các số x đang có file LevelData_{level}_{x}.json trong thư mục biến thể, tăng dần.</summary>
    public List<int> GetVariantNumbers(int level)
    {
        var numbers = new List<int>();
        string folder = VariantFolder;
        if (!Directory.Exists(folder)) return numbers;

        string prefix = $"{FilePrefix}{level}_";
        foreach (string file in Directory.GetFiles(folder, prefix + "*.json"))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            if (!name.StartsWith(prefix, StringComparison.Ordinal)) continue;
            string tail = name.Substring(prefix.Length);
            if (tail.Length == 0) continue;

            // Chỉ nhận toàn chữ số, như LevelAddress.TryParseLevelFileName: "LevelData_12_3 (1).json" do Windows đặt
            // khi copy đè không phải biến thể.
            bool digits = true;
            foreach (char c in tail)
            {
                if (c < '0' || c > '9') { digits = false; break; }
            }

            if (digits && int.TryParse(tail, out int variant) && variant >= 1) numbers.Add(variant);
        }

        numbers.Sort();
        return numbers;
    }

    /// <summary>
    /// Nạp biến thể LevelData_{level}_{variant}.json vào levelData. Không có file thì dọn trống đơn và bóng để xếp biến
    /// thể mới từ đầu (như LoadLevel không thấy file), ghi nhớ openVariant = variant để SaveVariant ghi đúng số x, và
    /// trả về false kèm cảnh báo để tool báo vàng.
    /// </summary>
    public bool LoadVariant(int level, int variant, out string message)
    {
        level = Mathf.Max(1, level);
        variant = Mathf.Max(1, variant);
        string path = GetVariantPath(level, variant);
        if (!File.Exists(path))
        {
            List<int> existing = GetVariantNumbers(level);
            UnityEditor.Undo.RecordObject(this, "Load biến thể");
            levelData ??= new LevelData();
            levelData.EditorSetLevel(level);
            levelData.EditorSetOrderQueue(Array.Empty<int>());
            levelData.EditorSetBubbles(Array.Empty<SoftBubbleData>());
            openVariant = variant;
            UnityEditor.EditorUtility.SetDirty(this);
            message = $"Không có biến thể {variant} của level {level} ({path}) - đã xoá hết đơn và bóng để xếp mới, " +
                      "xong thì bấm \"Lưu biến thể\". " +
                      (existing.Count == 0
                          ? $"Level {level} chưa có biến thể nào."
                          : $"Level {level} đang có biến thể: {string.Join(", ", existing)}.");
            return false;
        }

        if (!ReadLevelFile(path, out LevelData loaded, out message)) return false;

        UnityEditor.Undo.RecordObject(this, "Load biến thể");
        openVariant = variant;
        loaded.EditorSetLevel(level);
        ApplyLoadedLevel(loaded);
        NormalizeBubbleIds();
        UnityEditor.EditorUtility.SetDirty(this);
        message = $"Đã nạp biến thể {variant} của level {level}: {levelData.BubbleCount} bóng, {TotalFishInBubbles} cá, " +
                  $"{levelData.OrderQueue?.Length ?? 0} đơn";
        return true;
    }

    /// <summary>
    /// Ghi levelData đang mở thành biến thể LevelData_{Level}_{variant}.json trong thư mục biến thể. Không đụng file
    /// level gốc và không đụng LevelGlobalConfig. Biến thể vào bản build / CDN qua Level Addressables Setup + Build Levels.
    /// </summary>
    public bool SaveVariant(int variant, out string message)
    {
        if (levelData == null)
        {
            message = "Chưa có levelData để ghi";
            return false;
        }

        variant = Mathf.Max(1, variant);
        int level = levelData.Level;
        string path = GetVariantPath(level, variant);
        bool overwrite = File.Exists(path);

        NormalizeBubbleIds();
        if (!WriteLevelFile(levelData, level, path, out message)) return false;
        ImportLevelFile(path);

        openVariant = variant;
        UnityEditor.EditorUtility.SetDirty(this);

        var errors = new List<string>();
        string head = $"Đã {(overwrite ? "ghi đè" : "ghi")} biến thể {variant} của level {level}: {path}";
        message = levelData.Validate(errors) ? head : $"{head} nhưng seed chưa hợp lệ: {string.Join("; ", errors)}";
        return true;
    }

    /// <summary>Ghi levelData ra LevelData_{levelData.Level}.json. Cảnh báo seed sai luật nhưng vẫn ghi.</summary>
    public bool SaveLevel(out string message)
    {
        if (levelData == null)
        {
            message = "Chưa có levelData để ghi";
            return false;
        }

        NormalizeBubbleIds();
        int level = levelData.Level;
        string path = GetLevelPath(level);
        if (!WriteLevelFile(levelData, level, path, out message)) return false;

        ImportLevelFile(path);
        if (!SyncRuntimeLevelSource(level, path, levelData.Difficulty, out message)) return false;
        if (openVariant != 0)
        {
            // File gốc giờ chính là data đang mở, không còn là biến thể nữa.
            openVariant = 0;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        var errors = new List<string>();
        message = levelData.Validate(errors)
            ? $"Đã ghi {path} và cập nhật LevelGlobalConfig"
            : $"Đã ghi {path} và cập nhật LevelGlobalConfig nhưng seed chưa hợp lệ: {string.Join("; ", errors)}";
        return true;
    }

    /// <summary>
    /// Đổi chỗ level đang mở với level other: ghi levelData vào file của other và data của other vào file hiện tại,
    /// trường Level trong mỗi file được sửa theo chỗ mới. Sau khi xong nạp lại level đang mở.
    /// </summary>
    public bool SwapLevel(int other, out string message)
    {
        if (levelData == null)
        {
            message = "Chưa có levelData để swap";
            return false;
        }

        int current = levelData.Level;
        other = Mathf.Max(1, other);
        if (other == current)
        {
            message = "Swap với chính nó thì khỏi swap";
            return false;
        }

        string otherPath = GetLevelPath(other);
        if (!File.Exists(otherPath))
        {
            message = $"Không thấy {otherPath}";
            return false;
        }

        if (!ReadLevelFile(otherPath, out LevelData otherData, out message)) return false;

        NormalizeBubbleIds();
        if (!WriteLevelFile(levelData, other, GetLevelPath(other), out message)) return false;
        if (!WriteLevelFile(otherData, current, GetLevelPath(current), out message)) return false;

        ImportLevelFile(GetLevelPath(other));
        ImportLevelFile(GetLevelPath(current));
        if (!SyncRuntimeLevelSource(other, GetLevelPath(other), levelData.Difficulty, out message)) return false;
        if (!SyncRuntimeLevelSource(current, GetLevelPath(current), otherData.Difficulty, out message)) return false;
        if (!LoadLevel(current, out string loadMessage))
        {
            message = loadMessage;
            return false;
        }

        message = $"Đã đổi chỗ level {current} ↔ {other}; đang mở level {current}";
        return true;
    }

    /// <summary>Ghi data ra path (file level gốc hay file biến thể), đặt lại trường Level cho khớp chỗ mới.</summary>
    private static bool WriteLevelFile(LevelData data, int level, string path, out string message)
    {
        data.EditorSetLevel(level);
        string payload = DataSerializer.Serialize(data);
        if (string.IsNullOrEmpty(payload))
        {
            message = $"Mã hoá level {level} thất bại, không ghi file";
            return false;
        }

        try
        {
            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            File.WriteAllText(path, payload);
        }
        catch (Exception e)
        {
            message = $"Ghi {path} lỗi - {e.Message}";
            return false;
        }

        message = "";
        return true;
    }

    /// <summary>Đọc và giải mã một file level. Trả về false kèm lý do; không đụng levelData.</summary>
    private static bool ReadLevelFile(string path, out LevelData loaded, out string message)
    {
        loaded = null;
        try
        {
            loaded = DataSerializer.Deserialize<LevelData>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            message = $"Đọc {path} lỗi - {e.Message}";
            return false;
        }

        if (loaded == null)
        {
            message = $"{path} giải mã ra null (file chưa mã hoá bằng DataSerializer.Serialize?)";
            return false;
        }

        message = "";
        return true;
    }

    /// <summary>
    /// Copy seed đã giải mã vào levelData đang gắn trên asset. Giữ nguyên instance để Unity serializer / inspector
    /// không Apply snapshot cũ đè mất field (đặc biệt isIce) sau Tìm level.
    /// </summary>
    private void ApplyLoadedLevel(LevelData loaded)
    {
        if (levelData == null || loaded == null)
        {
            levelData = loaded ?? new LevelData();
            return;
        }

        levelData.EditorSetLevel(loaded.Level);
        levelData.EditorSetDifficulty(loaded.Difficulty);
        levelData.EditorSetOrderQueue(loaded.OrderQueue);
        levelData.EditorSetBubbles(CloneBubbles(loaded.SoftBubbleDataArray));
    }

    /// <summary>
    /// Bản sao bóng để Unity serialize graph mới, không dùng object Newtonsoft tạo ra.
    ///
    /// Đi qua JsonUtility chứ KHÔNG liệt kê từng field: bản cũ gán tay từng field nên mỗi lần thêm cờ mới vào
    /// SoftBubbleData mà quên sửa đây là cờ đó bị rụng âm thầm lúc Load (isOrderBubble / fishOrderId đã bị như vậy).
    /// JsonUtility đọc đúng tập field Unity serialize, thêm field mới là tự có, và cũng chính là bộ serialize sẽ
    /// ghi asset nên vòng này không thể lệch với những gì lưu được.
    /// </summary>
    private static SoftBubbleData[] CloneBubbles(SoftBubbleData[] source)
    {
        if (source == null) return Array.Empty<SoftBubbleData>();
        var copy = new SoftBubbleData[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            SoftBubbleData b = source[i];
            if (b == null) continue;
            copy[i] = JsonUtility.FromJson<SoftBubbleData>(JsonUtility.ToJson(b));
        }

        return copy;
    }

    private static void ImportLevelFile(string path)
    {
        if (!string.IsNullOrEmpty(path)) UnityEditor.AssetDatabase.ImportAsset(path);
    }

    /// <summary>
    /// Cập nhật dòng cache độ khó trong LevelGlobalConfig cho level vừa ghi. Chỉ làm khi file nằm trong thư mục level
    /// production: tool đang trỏ vào thư mục một bộ test (TestLevel/{Version}) thì KHÔNG đụng cache của level production.
    /// </summary>
    private static bool SyncRuntimeLevelSource(int level, string path, LevelDifficulty difficulty, out string message)
    {
        CoreGame.GameGlobalConfig.LevelGlobalConfig config =
            CoreGame.GameGlobalConfig.LevelGlobalConfig.Instance;
        if (config == null)
        {
            message = "Đã ghi file nhưng không thấy LevelGlobalConfig để cập nhật cache độ khó";
            return false;
        }

        message = "";
        string folder = Path.GetDirectoryName(path);
        if (!CoreGame.GameGlobalConfig.LevelGlobalConfig.IsSameFolder(folder, config.LevelFolder)) return true;

        config.SyncLevelCache(level, difficulty);
        return true;
    }
#endif
}
