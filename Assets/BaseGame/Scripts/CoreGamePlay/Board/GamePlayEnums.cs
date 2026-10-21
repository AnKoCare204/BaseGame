// Enum gameplay FishSort, giữ tên và giá trị trong Docs/GDD/03_Enums.tsv.
// Booster và tiền tệ dùng chung CoreData.ResourceType (khung AntFood), trạng thái game dùng Core.GameState.

public enum StuckType
{
    None = 0,
    HoldingFull = 1,
    TimeIsUp = 2
}

public enum LevelEndType
{
    None = 0,
    Win = 1,
    Lose = 2,
    Quit = 3
}

public enum FishMoveTarget
{
    None = 0,
    Tank = 1,
    Holding = 2,
    BackToBubble = 3
}

public enum ObstacleType
{
    None = 0,
    Ink = 1,
    Chain = 2,
    Ice = 3,
    Jellyfish = 4,
    HiddenFish = 5,
    Rock = 6,
    Spawner = 7
}

/// <summary>Lý do một booster không dùng được lúc này (BoardManager.GetBoosterBlockReason), UI chọn thông báo theo đó.</summary>
public enum BoosterBlockReason
{
    None = 0,
    NotAvailable = 1,
    /// <summary>Ô chờ đầy nên bấm bị khoá; Shuffle xếp lại xong vẫn không bấm được nên bị chặn.</summary>
    HoldingFull = 2,
    /// <summary>Bàn đang bận (cá bay, bể rơi, trộn...): chờ xong rồi bấm lại, UI không hiện MessageNoUse.</summary>
    Busy = 3
}

public enum PlayOnVariantType
{
    AddTwoSlots = 0,
    ClearHolding = 1,
    UndoLast = 2,
    AddTwoSlotsWithDoubleRewarded = 3,
    AddOneSlotWithLimit = 4,
    Disabled = 99
}
