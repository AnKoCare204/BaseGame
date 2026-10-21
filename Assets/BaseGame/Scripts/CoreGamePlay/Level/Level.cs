using System.Collections.Generic;
using Core;
using Core.GamePlay.GlobalEnum;
using CoreData;
using CoreGame.GameGlobalConfig;
using Cysharp.Threading.Tasks;
using TW.Utility.CustomComponent;
using UnityEngine;

/// <summary>
/// Gốc của một màn chơi trong template. LevelManager sinh prefab PrefabGlobalConfig.levelPrefab rồi gọi StartGame.
/// Game mới kế thừa lớp này (xem SampleLevel): dựng bàn trong OnStartGame, dọn trong OnClear, báo kết quả bằng Win() / Lose().
/// UI (ModalLose, ModalBooster, ScreenInGame, BoosterManager) chỉ dùng các thành viên public ở đây, nên giữ nguyên chữ ký khi thay gameplay.
/// </summary>
public class Level : ABetterMonoBehaviour
{
    public LevelConfig Config { get; private set; }

    /// <summary>Bàn đã xong (thắng). Win() đặt true; GameManager.WinLevel chỉ chạy một lần mỗi màn.</summary>
    public bool IsCompleted { get; protected set; }

    /// <summary>Tiến độ hiện tại / tổng để HUD vẽ (ScreenInGame đọc khi dựng, sau đó nghe GameGlobalEvent.OnProgressChanged).</summary>
    public int Progress { get; private set; }
    public int ProgressTotal { get; private set; }

    public int ReviveByAdsCount { get; set; }
    public int ReviveByCoinCount { get; set; }
    public LoseReason CurLoseReason { get; protected set; } = LoseReason.None;

    private readonly Dictionary<ResourceType, int> boosterByAdsCount = new();
    private bool isClaimBooster = true;
    private bool isBoosterClaimFlowDone = true;

    private void OnDestroy()
    {
        Clear();
    }

    public async UniTask StartGame(LevelConfig config)
    {
        Config = config;
        IsCompleted = false;
        CurLoseReason = LoseReason.None;
        ReviveByAdsCount = 0;
        ReviveByCoinCount = 0;
        boosterByAdsCount.Clear();
        await OnStartGame(config);
    }

    public void Clear()
    {
        OnClear();
    }

    /// <summary>Play On sau khi kẹt (ModalLose trả coin / xem ads). Mặc định chỉ xoá lý do thua; gameplay tự gỡ thế kẹt.</summary>
    public virtual UniTask Revive()
    {
        CurLoseReason = LoseReason.None;
        return UniTask.CompletedTask;
    }

    /// <summary>Dựng bàn theo config. Chạy trong màn loading: lỗi ở đây làm LevelManager huỷ vào level.</summary>
    protected virtual UniTask OnStartGame(LevelConfig config) => UniTask.CompletedTask;

    protected virtual void OnClear()
    {
    }

    /// <summary>Bàn báo thắng. Gọi một lần; các lần sau bị bỏ qua.</summary>
    protected void Win()
    {
        if (IsCompleted) return;
        IsCompleted = true;
        GameManager.Instance.PrepareWin();
        GameManager.Instance.WinLevel().Forget();
    }

    /// <summary>Bàn kẹt: mở ModalLose (Play On). Người chơi từ chối thì GameManager chốt thua.</summary>
    protected void Lose(LoseReason reason)
    {
        if (IsCompleted) return;
        CurLoseReason = reason;
        GameManager.Instance.LoseLevel().Forget();
    }

    protected void SetProgress(int current, int total)
    {
        Progress = current;
        ProgressTotal = total;
        Core.GamePlay.GameGlobalEvent.OnProgressChanged?.Invoke(current, total);
    }

    // ===== Input (InputManager gọi khi đang chơi, không pause; Down / Click bỏ qua khi con trỏ đè UI) =====
    public virtual void OnPointerDown(Vector2 screenPosition)
    {
    }

    public virtual void OnPointerUp(Vector2 screenPosition)
    {
    }

    public virtual void OnClick(Vector2 screenPosition)
    {
    }

    // ===== Booster =====
    public virtual bool CanUseBooster(ResourceType boosterType) => !IsCompleted;

    public virtual BoosterBlockReason GetBoosterBlockReason(ResourceType boosterType)
    {
        return CanUseBooster(boosterType) ? BoosterBlockReason.None : BoosterBlockReason.NotAvailable;
    }

    /// <summary>Thực hiện booster lên bàn. true = đã dùng (BoosterManager mới trừ tài nguyên). Template chỉ log.</summary>
    public virtual UniTask<bool> UseBooster(ResourceType boosterType)
    {
        Debug.Log($"[Level] UseBooster {boosterType}: chưa có hiệu ứng trong template");
        return UniTask.FromResult(true);
    }

    public int GetBoosterByAdsCount(ResourceType boosterType) => boosterByAdsCount.GetValueOrDefault(boosterType);

    public void IncrementBoosterByAdsCount(ResourceType boosterType)
    {
        boosterByAdsCount[boosterType] = GetBoosterByAdsCount(boosterType) + 1;
    }

    public void SetIsClaimBooster(bool value) => isClaimBooster = value;
    public bool GetIsClaimBooster() => isClaimBooster;
    public void SetIsBoosterClaimFlowDone(bool value) => isBoosterClaimFlowDone = value;
    public bool GetIsBoosterClaimFlowDone() => isBoosterClaimFlowDone;

    /// <summary>Vị trí hiện bàn tay hướng dẫn / hiệu ứng nhận thưởng trên bàn.</summary>
    public virtual Vector3 GetTutSpawnPoint() => transform.position;
}
