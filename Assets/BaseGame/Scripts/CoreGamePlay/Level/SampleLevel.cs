using Core.GamePlay;
using Core.GamePlay.GlobalEnum;
using Core.Manager;
using CoreData;
using CoreGame.GameGlobalConfig;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Gameplay mẫu của template: bấm vào vùng chơi đủ số lần trước khi hết giờ thì thắng, hết giờ thì kẹt (ModalLose → Play On
/// cộng thêm giờ). Đủ để chạy trọn vòng Home → Level → Win / Lose → Next và thử booster. Game thật thay bằng lớp kế thừa
/// Level riêng rồi gán prefab vào PrefabGlobalConfig.levelPrefab.
/// </summary>
public class SampleLevel : Level
{
    [field: SerializeField, Tooltip("Số lần bấm cần để thắng theo độ khó Normal / Hard / SuperHard")]
    private Vector3Int TapsByDifficulty { get; set; } = new(10, 20, 30);

    [field: SerializeField, Min(1f), Tooltip("Giây chơi mỗi lượt")] private float TimeLimit { get; set; } = 20f;
    [field: SerializeField, Min(0f), Tooltip("Giây cộng thêm khi Play On")] private float ReviveExtraTime { get; set; } = 10f;
    [field: SerializeField, Min(0), Tooltip("Booster Hint cộng bấy nhiêu lần bấm")] private int HintTaps { get; set; } = 3;
    [field: SerializeField, Min(0f), Tooltip("Booster Undo / Shuffle cộng bấy nhiêu giây")] private float BoosterExtraTime { get; set; } = 5f;

    private int taps;
    private int targetTaps;
    private float timeLeft;

    protected override UniTask OnStartGame(LevelConfig config)
    {
        targetTaps = config.LevelType switch
        {
            LevelDifficulty.Hard => TapsByDifficulty.y,
            LevelDifficulty.SuperHard => TapsByDifficulty.z,
            _ => TapsByDifficulty.x,
        };
        taps = 0;
        timeLeft = TimeLimit;
        SetProgress(taps, targetTaps);
        return UniTask.CompletedTask;
    }

    private void Update()
    {
        if (IsCompleted || CurLoseReason != LoseReason.None) return;
        if (LevelManager.Instance == null || LevelManager.Instance.IsPause) return;
        // Đang trong luồng nhận / ép dùng thử booster mới mở khoá (LevelManager bỏ pause để bấm được nút): chưa tính giờ.
        if (!GetIsBoosterClaimFlowDone()) return;
        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f) Lose(LoseReason.TimeUp);
    }

    public override void OnClick(Vector2 screenPosition)
    {
        AddTaps(1);
    }

    public override UniTask Revive()
    {
        timeLeft = Mathf.Max(0f, timeLeft) + ReviveExtraTime;
        return base.Revive();
    }

    public override UniTask<bool> UseBooster(ResourceType boosterType)
    {
        switch (boosterType)
        {
            case ResourceType.Hint:
                AddTaps(HintTaps);
                break;
            case ResourceType.Undo:
            case ResourceType.Shuffle:
                timeLeft += BoosterExtraTime;
                break;
            default:
                return UniTask.FromResult(false);
        }

        return UniTask.FromResult(true);
    }

    private void AddTaps(int value)
    {
        if (IsCompleted || CurLoseReason != LoseReason.None) return;
        taps = Mathf.Min(targetTaps, taps + value);
        SetProgress(taps, targetTaps);
        if (taps >= targetTaps) Win();
    }
}
