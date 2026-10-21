using Core;
using Core.Manager;
using CoreData;
using SDK;
using System.Collections.Generic;
using TW.Utility.DesignPattern;

/// <summary>
/// Stub analytics gameplay giữ nguyên chữ ký của FishHunt/AntFood. Chỉ log trong Editor; khi gắn Firebase thì bơm nội dung vào từng hàm.
/// </summary>
public class IngameFirebaseAnalystic : DDOLSingleton<IngameFirebaseAnalystic>
{
    #region Shared analytics context

    // "level" của các event ads/iap/booster_spend theo sheet Event Firebase: đang chơi round League thì = round.
    // Đọc round từ LevelConfig (không đọc LeagueManager.CurrentRound vì round đã advance ngay khi RoundCompleted,
    // trong khi ModalWin/ads sau đó vẫn thuộc round vừa chơi).
    public static string TrackedLevelId => Core.GameManager.Instance != null ? Core.GameManager.Instance.Level.Value.ToString() : "0";

    public static string CurrentGameMode => "Normal";
    public static AnalyticsParameter GameModeParam => new AnalyticsParameter("game_mode", CurrentGameMode);

    /// <summary>Biến thể level đang chơi (0 = bản gốc), xem LevelVariantRule.</summary>
    private static int CurrentVariant =>
        LevelManager.Instance != null && LevelManager.Instance.LevelConfig != null ? LevelManager.Instance.LevelConfig.Variant : 0;

    // Ngữ cảnh cho iap_*: status = kết quả màn chơi gần nhất ("start" khi chưa win/lose lần nào trong session),
    // is_after_inter = đã có inter hiển thị sau kết quả đó hay chưa.
    public static string LastLevelResult { get; private set; } = "start";
    public static bool IsAfterInter { get; private set; }
    public static void SetLastLevelResult(string result)
    {
        LastLevelResult = result;
        IsAfterInter = false;
    }
    public static void MarkInterstitialShown() => IsAfterInter = true;

    #endregion

    #region Gameplay

    // Winstreak chỉ đổi lúc win/lose nên giá trị lúc fail == giá trị lúc start;
    // phải chụp ở start vì ResetWinStreak chạy trước khi ModalConfirmLose bắn level_fail.
    private int winStreakAtLevelStart;
    // Đếm trong RAM cho check_point (heart/lose): sống qua các lần retry, reset khi qua level mới.
    private int currentLevelHeartUsed;
    private int currentLevelFailCount;

    public void TrackLevelStart()
    {
        winStreakAtLevelStart = 0;

        string level = GameManager.Instance.Level.ToString();
        int retry = LevelManager.Instance.CurrentRetryCount;
        string day = UserData.Instance.GetCheckPointDayRetention().ToString();
        int remaining_coins = PlayerResourceManager.Instance.GetResource(ResourceType.Money).Value;

        if (retry == 0)
        {
            // Level mới bắt đầu — xoá số liệu heart/lose còn sót nếu level trước bị bỏ ngang.
            currentLevelHeartUsed = 0;
            currentLevelFailCount = 0;
        }
        // Mỗi lần start (kể cả retry) tốn 1 Heart, trừ khi đang infinite life — đếm cho check_point.
        if (!PlayerResourceManager.Instance.HasInfiniteLife)
        {
            currentLevelHeartUsed++;
        }

        var parameters = new List<AnalyticsParameter>
        {
            new ("level", level),
            new ("day", day),
            new ("retry", retry),
            new ("variant", CurrentVariant),
            new ("remaining_coins", remaining_coins),
        };

        for (var type = ResourceType.AddSlot; type <= ResourceType.UnlockTank; type++)
        {
            int remaining = PlayerResourceManager.Instance.GetResource(type).Value;
            parameters.Add(new AnalyticsParameter("remaining_booster_" + type.ToString().ToLowerInvariant(), remaining));
        }

        Send("level_start", parameters.ToArray());
        TrackCheckPointStart(GameManager.Instance.Level.Value, UserData.Instance.GetCheckPointDayRetention());
    }

    public void TrackLevelComplete(int playtime, int reviveCount, int retryCount)
    {
        string level = GameManager.Instance.Level.ToString();
        int retry = retryCount;
        int duration = playtime;
        int add_sot_revive = reviveCount;
        int remaining_coins = PlayerResourceManager.Instance.GetResource(ResourceType.Money).Value;


        // Giữ tên param cũ "duration_win" (sheet ghi duration) — data report đã build trên tên cũ.
        var parameters = new List<AnalyticsParameter>
        {
            new ("level", level),
            new ("duration_win", duration),
            new ("retry", retry),
            new ("variant", CurrentVariant),
            new ("revive", add_sot_revive),
            new ("remaining_coins", remaining_coins),
        };

        for (var type = ResourceType.AddSlot; type <= ResourceType.UnlockTank; type++)
        {
            int remaining = PlayerResourceManager.Instance.GetResource(type).Value;
            parameters.Add(new AnalyticsParameter("remaining_booster_" + type.ToString().ToLowerInvariant(), remaining));
        }

        AppendSessionStats(parameters);
        Send("level_complete", parameters.ToArray());
        EventTrackingManager.TrackEventAppsflyer?.Invoke("af_level_achieved", new AnalyticsParameter("level", level));
        AppsFlyerManager.TrackCompleteLevel(GameManager.Instance.Level);
    }

    public void TrackLevelFail(int playtime, int retryCount, string looseType, int reviveCount)
    {
        // Mọi đường thua đều qua đây (kể cả Ice/Cage đẩy thẳng ModalConfirmLose, không qua FailedLevel)
        // → cập nhật status cho iap_* tại đây cho đủ.
        SetLastLevelResult("lose");

        string level = GameManager.Instance.Level.ToString();
        int duration = playtime;
        int retry = retryCount;
        string lose_type = looseType;
        int remaining_coins = PlayerResourceManager.Instance.GetResource(ResourceType.Money).Value;
        int add_sot_revive = reviveCount;
        int revive = reviveCount;

        currentLevelFailCount++;

        var parameters = new List<AnalyticsParameter>
        {
            new ("level", level),
            new ("duration_fail", duration),
            new ("retry", retry),
            new ("variant", CurrentVariant),
            new ("revive", revive),
            new ("add_sot_revive", add_sot_revive),
            new ("lose_type", lose_type),
            new ("remaining_coins", remaining_coins),
        };

        for (var type = ResourceType.AddSlot; type <= ResourceType.UnlockTank; type++)
        {
            int remaining = PlayerResourceManager.Instance.GetResource(type).Value;
            parameters.Add(new AnalyticsParameter("remaining_booster_" + type.ToString().ToLowerInvariant(), remaining));
        }

        AppendSessionStats(parameters);
        Send("level_fail", parameters.ToArray());
    }


    public void TrackCheckPointEnd(int level, int retry_level, int duration, int booster_spend, int coin_spend, int ads_count, int day_retention)
    {
        AnalyticsParameter levelParam = new AnalyticsParameter("level", level.ToString());
        AnalyticsParameter heartParam = new AnalyticsParameter("heart", currentLevelHeartUsed.ToString());
        AnalyticsParameter retryLevelParam = new AnalyticsParameter("retry_level", retry_level);
        AnalyticsParameter durationParam = new AnalyticsParameter("duration", duration);
        AnalyticsParameter boosterSpendParam = new AnalyticsParameter("booster_spend", booster_spend);
        AnalyticsParameter coinSpendParam = new AnalyticsParameter("coin_spend", coin_spend);
        AnalyticsParameter adsCountParam = new AnalyticsParameter("ads_count", ads_count);
        AnalyticsParameter dayRetentionParam = new AnalyticsParameter("day_retention", day_retention);

        EventTrackingManager.TrackEventFirebase?.Invoke(
            "check_point_end",
            levelParam,
            heartParam,
            retryLevelParam,
            durationParam,
            boosterSpendParam,
            coinSpendParam,
            adsCountParam,
            dayRetentionParam);

        currentLevelHeartUsed = 0;
        currentLevelFailCount = 0;
    }

    public void TrackCheckPointStart(int level, int day_retention)
    {
        if (!UserData.Instance.TryMarkLevelCheckpoint(level)) return;

        AnalyticsParameter levelParam = new AnalyticsParameter("level", level.ToString());
        AnalyticsParameter dayRetentionParam = new AnalyticsParameter("day_retention", day_retention);
        // Giữ tên event cũ "check_point_start" (sheet ghi checkpoint_start) — data report đã build trên tên cũ.
        EventTrackingManager.TrackEventFirebase?.Invoke("check_point_start", levelParam, dayRetentionParam);
    }


    #endregion

    #region Revive

    /// <summary>
    /// Gửi mỗi lần popup lose (có lựa chọn revive) được chốt kết quả.
    /// </summary>
    /// <param name="result">Phản hồi của user với offer revive: "coin", "ads_reward" hoặc "lose".</param>
    public void TrackReviveOfferResult(string result)
    {
        string loseType = LevelManager.Instance != null && LevelManager.Instance.CurrentLevel != null
            ? LevelManager.Instance.CurrentLevel.CurLoseReason.ToString()
            : "";
        EventTrackingManager.TrackEventFirebase?.Invoke("REVIVE_offer_popup",
            new AnalyticsParameter("level", TrackedLevelId),
            new AnalyticsParameter("retry", LevelManager.Instance.CurrentRetryCount),
            new AnalyticsParameter("result", result),
            new AnalyticsParameter("lose_type", loseType));
    }

    #endregion

    #region Resource

    public void TrackBoosterEarn(string placement, string boosterName, int value, string claimType = "free")
    {
        string level = GameManager.Instance.Level.ToString();
        AnalyticsParameter levelParam = new AnalyticsParameter("level", level);
        AnalyticsParameter placementParam = new AnalyticsParameter("placement", placement);
        AnalyticsParameter boosterNameParam = new AnalyticsParameter("booster_name", boosterName);
        AnalyticsParameter valueParam = new AnalyticsParameter("value", value);

        EventTrackingManager.TrackEventFirebase?.Invoke("booster_earn", levelParam, placementParam, boosterNameParam, valueParam);
    }

    public void TrackBoosterSpeend(string placement, string boosterName, int value = 1)
    {
        AnalyticsParameter levelParam = new AnalyticsParameter("level", TrackedLevelId);
        AnalyticsParameter placementParam = new AnalyticsParameter("placement", placement);
        AnalyticsParameter boosterNameParam = new AnalyticsParameter("booster_name", boosterName);

        EventTrackingManager.TrackEventFirebase?.Invoke("booster_spend", levelParam, GameModeParam, placementParam, boosterNameParam);
        // Google Ads value bidding: đếm booster đã dùng cho booster_spend_10
        AdValueBiddingAnalytics.OnBoosterSpent();
    }

    public void TrackResourceEarn(string placement, string resourceName, int value)
    {
        string level = GameManager.Instance.Level.ToString();
        AnalyticsParameter levelParam = new AnalyticsParameter("level", level);
        AnalyticsParameter resourceNameParam = new AnalyticsParameter("resource_name", resourceName);
        AnalyticsParameter valueParam = new AnalyticsParameter("value", value);
        AnalyticsParameter placementParam = new AnalyticsParameter("placement", placement);

        EventTrackingManager.TrackEventFirebase?.Invoke("resource_earn", levelParam, valueParam, resourceNameParam, placementParam);
    }

    public void TrackResourceSpend(string placement, string resourceName, int value, string spendType)
    {
        string level = GameManager.Instance.Level.ToString();
        AnalyticsParameter levelParam = new AnalyticsParameter("level", level);
        AnalyticsParameter resourceNameParam = new AnalyticsParameter("resource_name", resourceName);
        AnalyticsParameter valueParam = new AnalyticsParameter("value", value);
        AnalyticsParameter spendTypeParam = new AnalyticsParameter("spend_type", spendType);
        AnalyticsParameter placementParam = new AnalyticsParameter("placement", placement);

        EventTrackingManager.TrackEventFirebase?.Invoke("resource_spend", levelParam, valueParam, resourceNameParam, placementParam, spendTypeParam);
    }

    public void TrackCoinEarn(string placement, string claim_type, int value)
    {
        string level = GameManager.Instance.Level.ToString();
        AnalyticsParameter levelParam = new AnalyticsParameter("level", level);
        AnalyticsParameter placementParam = new AnalyticsParameter("placement", placement);
        AnalyticsParameter claimTypeParam = new AnalyticsParameter("claim_type", claim_type); // ads, iap, free
        AnalyticsParameter valueParam = new AnalyticsParameter("value", value);

        EventTrackingManager.TrackEventFirebase?.Invoke("coins_earn", levelParam, placementParam, claimTypeParam, valueParam);
    }

    public void TrackCoinSpend(string placement, string spend_type, int value)
    {
        string level = GameManager.Instance.Level.ToString();
        AnalyticsParameter levelParam = new AnalyticsParameter("level", level);
        AnalyticsParameter placementParam = new AnalyticsParameter("placement", placement);
        AnalyticsParameter spendTypeParam = new AnalyticsParameter("spend_type", spend_type);
        AnalyticsParameter valueParam = new AnalyticsParameter("value", value);

        EventTrackingManager.TrackEventFirebase?.Invoke("coins_spend", levelParam, placementParam, spendTypeParam, valueParam);
    }

    #endregion

    #region IAP
    public void TrackIAPShow(string packID, string placement)
    {
        AnalyticsParameter[] parameters = new AnalyticsParameter[]
        {
            new ("level", GameManager.Instance.Level.Value.ToString()),
            new ("pack_id", packID),
            new ("placement", placement)
        };
        EventTrackingManager.TrackEventFirebase?.Invoke("iap_show", parameters);
    }
    #endregion

    #region Tutorial (FTUE)

    public void TrackTutorialBegin(string tutorialId, int level)
    {
        Send("tutorial_begin", new AnalyticsParameter("tutorial_id", tutorialId), new AnalyticsParameter("level", level.ToString()));
    }

    public void TrackTutorialStep(string tutorialId, int step, string stepName, int elapsedMs)
    {
        Send("tutorial_step", new AnalyticsParameter("tutorial_id", tutorialId), new AnalyticsParameter("step", step),
            new AnalyticsParameter("step_name", stepName), new AnalyticsParameter("elapsed_ms", elapsedMs));
    }

    public void TrackTutorialComplete(string tutorialId, int level, int durationSec)
    {
        Send("tutorial_complete", new AnalyticsParameter("tutorial_id", tutorialId), new AnalyticsParameter("level", level.ToString()),
            new AnalyticsParameter("duration_sec", durationSec));
    }

    public void TrackFtueRescue(int level)
    {
        Send("ftue_rescue", new AnalyticsParameter("level", level.ToString()));
    }

    public void TrackLevelBackground(Core.Manager.LevelSessionStats stats, float now)
    {
        Send("level_background", new AnalyticsParameter("level", TrackedLevelId), new AnalyticsParameter("duration_sec", stats.DurationSec(now)),
            new AnalyticsParameter("taps", stats.Taps), new AnalyticsParameter("holding_used", stats.HoldingUsed),
            new AnalyticsParameter("idle_sec", stats.IdleSec(now)));
    }

    /// <summary>Tham số phiên level thêm vào level_complete / level_fail (chỉ thêm, không đổi tham số cũ).</summary>
    private static void AppendSessionStats(List<AnalyticsParameter> parameters)
    {
        Core.Manager.LevelSessionStats stats = LevelManager.Instance != null ? LevelManager.Instance.SessionStats : null;
        if (stats == null) return;
        parameters.Add(new AnalyticsParameter("taps", stats.Taps));
        parameters.Add(new AnalyticsParameter("denied_taps", stats.DeniedTaps));
        parameters.Add(new AnalyticsParameter("holding_max", stats.HoldingMax));
        parameters.Add(new AnalyticsParameter("idle_hints", stats.IdleHints));
    }

    /// <summary>Gửi Firebase; trong Editor in thêm "[Analytics] tên k=v ..." để kiểm Play mode (Firebase chưa sẵn sàng trong Editor).</summary>
    private static void Send(string eventName, params AnalyticsParameter[] parameters)
    {
        EventTrackingManager.TrackEventFirebase?.Invoke(eventName, parameters);
#if UNITY_EDITOR
        var sb = new System.Text.StringBuilder("[Analytics] ").Append(eventName);
        foreach (AnalyticsParameter p in parameters)
            sb.Append(' ').Append(p.Name).Append('=').Append(p.StringValue ?? p.LongValue.ToString());
        UnityEngine.Debug.Log(sb.ToString());
#endif
    }

    #endregion

    #region Ship Race

    public void TrackShipRaceStart(string level, int stage)
    {
        EventTrackingManager.TrackEventFirebase?.Invoke(
            "tidal_dash_start",
            new AnalyticsParameter("level", level),
            new AnalyticsParameter("stage", stage.ToString()));
    }

    public void TrackShipRaceWin(string level, int stage, int duration)
    {
        EventTrackingManager.TrackEventFirebase?.Invoke(
            "tidal_dash_win",
            new AnalyticsParameter("level", level),
            new AnalyticsParameter("stage", stage.ToString()),
            new AnalyticsParameter("duration", duration));
    }

    public void TrackShipRaceLose(string level, int stage, int duration, string loseType)
    {
        EventTrackingManager.TrackEventFirebase?.Invoke(
            "tidal_dash_lose",
            new AnalyticsParameter("level", level),
            new AnalyticsParameter("stage", stage.ToString()),
            new AnalyticsParameter("duration", duration),
            new AnalyticsParameter("lose_type", loseType));
    }

    #endregion
}
