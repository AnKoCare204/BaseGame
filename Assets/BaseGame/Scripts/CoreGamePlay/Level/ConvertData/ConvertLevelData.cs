using System;
using System.Collections.Generic;
using System.IO;
using Core.GamePlay;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Tool Editor: ghép dữ liệu level của game cũ thành LevelData của FishSort rồi xuất ra file JSON riêng từng level.
/// - oldLevelDataAsset: các file LevelConfig*.json (mảng {LevelID, BubbleDataStr, LevelDifficulty, ...}).
/// - bubbleDataAsset: neo thư mục chứa các file BubbleConfig_*.json; file cho từng level lấy theo BubbleDataStr đọc được
///   từ oldLevelDataAsset, nên chỉ cần kéo vào một file bất kỳ trong thư mục đó.
/// Việc dịch BubbleConfig sang LevelData (ánh xạ loài cá, bỏ cá lẻ, sinh OrderQueue, kiểm tra bằng LevelSolver) do
/// BubbleConfigImporter lo; ở đây chỉ nối hai nguồn, đặt Difficulty và ghi file.
/// </summary>
public class ConvertLevelData : MonoBehaviour
{
    public TextAsset[] oldLevelDataAsset;

    [Tooltip("Neo thư mục: kéo vào một file BubbleConfig_*.json bất kỳ, file thật cho từng level lấy theo BubbleDataStr")]
    public TextAsset bubbleDataAsset;

    [FolderPath]
    public string outputFolder = "Assets/BaseGame/TextAssets/LevelDataConverted";

    [Tooltip("Bật: lấy LevelDifficulty của game cũ (1/2/3). Tắt: tính theo LevelDifficultRuleGlobalConfig.GetDifficulty(level)")]
    public bool useOldDifficulty = true;

    [Tooltip("Tắt khi convert cả nghìn level cho nhanh; bật để biết level có giải được không")]
    public bool validateWithSolver = true;

    [ShowIf(nameof(validateWithSolver)), Min(1)]
    public int solverAttempts = 30;

    public int seed = 12345;

    /// <summary>Một entry trong LevelConfig*.json của game cũ. Các field còn lại (LevelTime, PointRangeStr, *AdWeight,
    /// CountdownTargetProgress) không có chỗ trong LevelData nên bỏ qua.</summary>
    [Serializable]
    private class OldLevelConfigEntry
    {
        public int LevelID;
        public string BubbleDataStr;

        // Đặt tên khác field JSON để không đụng tên enum LevelDifficulty.
        [JsonProperty("LevelDifficulty")] public int Difficulty;
    }

    /// <summary>Đọc lại các file đã xuất và chạy LevelData.Validate để chắc vòng serialize/deserialize không mất dữ liệu.</summary>
    [Button]
    private void VerifyOutput()
    {
        if (!Directory.Exists(outputFolder))
        {
            Debug.LogError($"Không thấy thư mục {outputFolder}");
            return;
        }

        string[] files = Directory.GetFiles(outputFolder, "LevelData_*.json");
        int ok = 0;
        foreach (string file in files)
        {
            LevelData data = DataSerializer.Deserialize<LevelData>(File.ReadAllText(file));
            if (data == null)
            {
                Debug.LogWarning($"{Path.GetFileName(file)}: giải mã / đọc ra null (file cũ chưa mã hoá?)");
                continue;
            }

            var errors = new List<string>();
            if (data.Validate(errors)) ok++;
            else Debug.LogWarning($"{Path.GetFileName(file)}: {string.Join("; ", errors)}");
        }

        Debug.Log($"VerifyOutput: {ok}/{files.Length} file hợp lệ");
    }
}
