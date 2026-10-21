using System;
using System.Collections.Generic;
using Core;
using CoreData;
using Cysharp.Threading.Tasks;
using DataModule;
using Game.Helper;
using Manager;
using Newtonsoft.Json;
using R3;
using R3.Triggers;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using TW.Utility.DesignPattern;
using UnityEngine;

public enum ShipRaceStatus
{
    NotStarted = 0,
    Running = 1,
    Finished = 2,
}

/// <summary>
/// ShipRace: limited-time racing event. The player races up to 4 bots over 3 stages.
/// Bots advance on a timer (seconds per level), the player advances by earning points from
/// core gameplay. Reaching the stage Target finishes the stage; the reward is claimed in the UI,
/// which then advances to the next stage. Finishing the last stage loops back to stage 0.
///
/// Follows the same lifecycle shape as AntLabManager / UfoManager: remote config window +
/// anchored period grid (ExistTime) for the run reset, all state in one IDataModule.
/// </summary>
public class ShipRaceManager : Singleton<ShipRaceManager>
{
    public static Action OnInitializeComplete { get; set; }
    public static Action OnRaceStateChanged { get; set; }
    public static Action<int> OnStageFinished { get; set; }
    public static Action<int> OnRewardClaimed { get; set; }
    /// <summary>
    /// A new run replaced the old one. Any open Ship Race UI is showing state that no longer exists
    /// - notably a join popup whose stage is baked in - so it has to close itself.
    /// </summary>
    public static Action OnRunReset { get; set; }

    private ShipRaceData Data => ShipRaceData.Instance;
    private ShipRaceGlobalConfig Config => ShipRaceGlobalConfig.Instance;
    [ShowInInspector] public bool IsInitialize { get; private set; }
    [field: SerializeField] public DateTime RunEndTimeUtc { get; private set; }

    public ShipRaceStatus Status => (ShipRaceStatus)Data.RaceStatus;
    public int CurrentStage => Data.CurrentStage;
    public int PlayerPoint => Data.PlayerPoint;
    public int PlayerPlace => Data.PlayerPlace;
    public bool IsRunning => Status == ShipRaceStatus.Running;
    // Only a win pays out: a race lost to a bot is finished, but has nothing to claim
    public bool HasUnclaimedReward => Status == ShipRaceStatus.Finished && Data.IsPlayerWin && !Data.IsRewardClaimed;
    public bool IsPlayerWin => Data.IsPlayerWin;
    public string WinnerName => Data.WinnerName;
    /// <summary>
    /// Derived from saved state, not a runtime flag: a resolved race stays pending until the player
    /// acknowledges it, so quitting before the popup appears does not swallow the result.
    /// A lost race is cleared by ConfirmLose, a won one by claiming the reward.
    /// </summary>
    public bool NeedShowWin => HasUnclaimedReward;
    public bool NeedShowLose => Status == ShipRaceStatus.Finished && !Data.IsPlayerWin;
    // Assigned by the UI layer so the manager does not depend on a concrete modal
    public static Func<int, UniTask> ShowWinAsync { get; set; }
    public static Func<int, UniTask> ShowLoseAsync { get; set; }
    private int[] _lastBotPoints = Array.Empty<int>();
    private bool _isUpdateSubscribed;
    private readonly Dictionary<ShipRaceBotData, ShipRaceBotState> _botStateCache = new();
    public ShipRaceStageConfig CurrentStageConfig => Config.GetStageConfig(Data.CurrentStage);
    public int CurrentTarget => CurrentStageConfig?.Target ?? 0;

    public bool IsEventUnlock => ShipRaceRemoteConfig.Instance.LevelUnlock > 0 &&
                                 ShipRaceRemoteConfig.Instance.LevelUnlock <= GameManager.Instance.Level;

    /// <summary>Time left in the current run. Locked in at run start so config changes can not shift it.</summary>
    public TimeSpan RunTimeRemaining => RunEndTimeUtc - DateTimeManager.UtcNow;

    #region Initialize / run lifecycle

    /// <summary>
    /// Period grid anchored to the remote start date, one run every ExistTime seconds, so every
    /// player's run starts and ends at the same global moment (same convention as AntLab/Ufo).
    /// </summary>
    /// <summary>
    /// Everything the event needs to be live, in one place so Initialize and the poll can never
    /// disagree about whether it should be running. Player level is deliberately not here: that is
    /// IsEventUnlock, which the UI and AddPoint read live, so it needs no re-init.
    /// </summary>
    private bool CanRunEvent(DateTime utcNow)
    {
        if (!ShipRaceRemoteConfig.Instance.IsBetweenEvent(utcNow)) return false;
        if (ShipRaceRemoteConfig.Instance.LevelUnlock <= 0) return false;
        return true;
    }

    private DateTime GetCurrentPeriodStartTime(DateTime utcNow)
    {
        DateTime eventStartTime = ShipRaceRemoteConfig.Instance.ShipRaceStartDate;
        float existTime = Mathf.Max(1f, Config.ExistTime);
        double elapsedSeconds = (utcNow - eventStartTime).TotalSeconds;
        int periodIndex = (int)Math.Floor(elapsedSeconds / existTime);
        if (periodIndex < 0) periodIndex = 0;
        return eventStartTime.AddSeconds(periodIndex * existTime);
    }

    public async UniTask Initialize()
    {
        if (IsInitialize) return;
        await UniTask.Yield();
        // Subscribed before the early returns, and only once: the poll is what notices the event
        // becoming available later (remote window opening, collection season starting), so it has
        // to run even on a boot where none of the conditions were met yet.
        if (!_isUpdateSubscribed)
        {
            _isUpdateSubscribed = true;
            this.UpdateAsObservable().Subscribe(OnUpdate).AddTo(this);
        }

        DateTime utcNow = DateTimeManager.UtcNow;
        if (!CanRunEvent(utcNow)) return;
        IsInitialize = true;
        LoadData(utcNow);
        OnInitializeComplete?.Invoke();
    }

    private void LoadData(DateTime utcNow)
    {
        DateTime periodEnd = GetCurrentPeriodStartTime(utcNow).AddSeconds(Config.ExistTime);
        if (Data.RunEndTimeUtc.IsNullOrWhitespace())
        {
            OpenNewRun(periodEnd);
            return;
        }

        RunEndTimeUtc = Data.RunEndTimeUtc.StringToDateTime();
        // Expired, or saved by an older cadence so it no longer sits on the grid
        if (utcNow >= RunEndTimeUtc || RunEndTimeUtc != periodEnd)
        {
            // Yesterday's race has to be acknowledged before a new run opens: resolve it as a
            // timeout loss and leave it pending, rather than wiping it behind the player's back
            if (TryResolveByRunEnd() || NeedShowLose || NeedShowWin)
            {
                CacheBotPoints();
                return;
            }

            OpenNewRun(periodEnd);
            return;
        }

        // The stage clock keeps running while the game is closed, so a race left open can already
        // be over on return. Settle that before any UI reads the state.
        TryResolveByDeadline();
        CacheBotPoints();
    }

    private void OnUpdate(Unit _)
    {
        DateTime utcNow = DateTimeManager.UtcNow;
        if (!IsInitialize)
        {
            // The event became available after boot. LoadData, not OpenNewRun: a run saved earlier
            // in the same period is still valid, and wiping it would throw away an in progress race
            // or a win the player has not claimed yet.
            if (!CanRunEvent(utcNow)) return;
            IsInitialize = true;
            LoadData(utcNow);
            OnInitializeComplete?.Invoke();
            OnRaceStateChanged?.Invoke();
            return;
        }

        if (!CanRunEvent(utcNow))
        {
            IsInitialize = false;
            OnInitializeComplete?.Invoke();
            return;
        }

        if (DateTimeManager.UtcNow >= RunEndTimeUtc)
        {
            // A running race becomes a timeout loss, and any pending result must be acknowledged
            // before the next run can open - so the player never loses a race silently
            if (TryResolveByRunEnd())
            {
                CacheBotPoints();
                OnRaceStateChanged?.Invoke();
                return;
            }

            if (NeedShowLose || NeedShowWin) return;

            OpenNewRun(GetCurrentPeriodStartTime(DateTimeManager.UtcNow).AddSeconds(Config.ExistTime));
            OnRaceStateChanged?.Invoke();
            return;
        }

        if (!IsRunning) return;
        // The stage can run out with the player idle on any screen, so the deadline is polled here
        if (TryResolveByDeadline())
        {
            CacheBotPoints();
            return;
        }

        // Rival and filler bots drift between player actions, so their points are polled here
        if (!HasBotPointChanged()) return;
        CacheBotPoints();
        OnRaceStateChanged?.Invoke();
    }

    /// <summary>
    /// Bot points are a pure function of elapsed time, so nothing needs ticking - this only
    /// detects when a displayed value would change and lets the UI refresh.
    /// </summary>
    private bool HasBotPointChanged()
    {
        if (Data.Bots == null) return false;
        if (_lastBotPoints.Length != Data.Bots.Count) return true;
        for (int i = 0; i < Data.Bots.Count; i++)
        {
            if (_lastBotPoints[i] != GetBotPoint(Data.Bots[i])) return true;
        }

        return false;
    }

    private void CacheBotPoints()
    {
        if (Data.Bots == null)
        {
            _lastBotPoints = Array.Empty<int>();
            return;
        }

        if (_lastBotPoints.Length != Data.Bots.Count)
        {
            _lastBotPoints = new int[Data.Bots.Count];
        }

        for (int i = 0; i < Data.Bots.Count; i++)
        {
            _lastBotPoints[i] = GetBotPoint(Data.Bots[i]);
        }
    }

    /// <summary>Fresh run: back to stage 0, nothing joined, no bots.</summary>
    private void OpenNewRun(DateTime runEndTimeUtc)
    {
        // A reward won but never claimed would be wiped with the rest of the run, so it is paid out
        // here instead. The player earned it; only the ceremony is skipped.
        GrantUnclaimedRewardOnReset();
        Data.SetDefault();
        RunEndTimeUtc = runEndTimeUtc;
        Data.RunEndTimeUtc = RunEndTimeUtc.DateTimeToString();
        Data.SaveData();
        _lastBotPoints = Array.Empty<int>();
        _botStateCache.Clear();
        // Fired last, with the new run already in place: listeners close stale UI and read fresh state
        OnRunReset?.Invoke();
    }

    /// <summary>
    /// Pays a won-but-unclaimed stage reward straight into the player's resources before the run is
    /// wiped. Uses PlayerResourceManager rather than the claim flow, since there is no popup here.
    /// </summary>
    private void GrantUnclaimedRewardOnReset()
    {
        if (!HasUnclaimedReward) return;
        if (!TryGetClaimReward(out ClaimGameResourceGroup claimGroup)) return;
        foreach (GameResource gameResource in claimGroup.GameResourceList)
        {
            PlayerResourceManager.Instance.AddResourceValue(gameResource, "tidal_dash", "tidal_dash");
        }
    }

    #endregion

    #region Join

    public bool CanJoinRace()
    {
        if (!IsInitialize) return false;
        if (!IsEventUnlock) return false;
        if (IsAllStageWon) return false;
        if (Status != ShipRaceStatus.NotStarted) return false;
        if (CurrentStageConfig == null) return false;
        // A race can still be joined in the last minutes: it just ends as a timeout loss when the
        // run closes, which the player has to acknowledge like any other loss.
        return !IsRunExpired;
    }

    /// <summary>The run clock has passed. A pending result still has to be acknowledged first.</summary>
    public bool IsRunExpired => DateTimeManager.UtcNow >= RunEndTimeUtc;

    /// <summary>Lost because the run clock ran out rather than to a rival.</summary>
    public bool IsLoseByRunEnd => Data.IsLoseByRunEnd;

    /// <summary>
    /// Every stage of this run is won: nothing left to race until the next run opens, so the home
    /// button hides itself.
    /// </summary>
    public bool IsAllStageWon => Data.IsAllStageWon;
    public float WinChancePercent => Data.WinChancePercent;

    /// <summary>
    /// Joins the current stage: rolls the bot line-up, the rival and this attempt's win chance.
    /// </summary>
    [Button]
    public void JoinRace()
    {
        if (!CanJoinRace()) return;
        ShipRaceStageConfig stageConfig = CurrentStageConfig;

        Data.RaceStartTimeUtc = DateTimeManager.UtcNow.DateTimeToString();
        Data.PlayerPoint = 0;
        Data.PlayerPlace = 0;
        Data.IsRewardClaimed = false;
        Data.RaceStatus = (int)ShipRaceStatus.Running;
        Data.IsPlayerWin = false;
        Data.WinnerName = string.Empty;
        Data.IsLoseByRunEnd = false;
        Data.IsEndGameResultShown = false;
        Data.FinishElapsedSeconds = 0f;
        // Snapshotted now rather than read at the finish, so a config change mid-race can not move
        // the odds the player committed to
        Data.WinChancePercent = Mathf.Clamp(
            ShipRaceRemoteConfig.Instance.GetWinChancePercent(stageConfig), 0f, 100f);
        Data.Bots = CreateBots(stageConfig);
        _botStateCache.Clear();
        CacheBotPoints();

        Data.SaveData();
        TrackRaceStart();
        OnRaceStateChanged?.Invoke();
    }

    /// <summary>
    /// Bots are rolled once and stored, so their names and pace survive a restart.
    /// Names are drawn without repeats from a per-stage slice of the codename pool.
    /// </summary>
    private List<ShipRaceBotData> CreateBots(ShipRaceStageConfig stageConfig)
    {
        List<ShipRaceBotData> bots = new List<ShipRaceBotData>();
        int botCount = stageConfig.BotCount;
        if (botCount <= 0) return bots;
        List<string> names = PickBotNames(botCount);
        if (names.Count == 0) return bots;

        float timePassLvMin = Mathf.Min(stageConfig.TimePassLvMin, stageConfig.TimePassLvMax);
        float timePassLvMax = Mathf.Max(stageConfig.TimePassLvMin, stageConfig.TimePassLvMax);
        for (int i = 0; i < names.Count; i++)
        {
            bots.Add(new ShipRaceBotData
            {
                Name = names[i],
                SecondsPerLevel = UnityEngine.Random.Range(timePassLvMin, timePassLvMax),
                // Small head start spread so the bots do not move in lockstep
                StartOffsetSeconds = UnityEngine.Random.Range(0f, timePassLvMin * 0.5f),
                // Drives this bot's win/lose rolls. Saved, so replaying the timeline after a
                // restart produces exactly the same run of results.
                Seed = UnityEngine.Random.Range(1, int.MaxValue),
                AvatarIndex = ProfileGlobalConfig.Instance.GetRandomAvatarIndex(),
                FrameIndex = ProfileGlobalConfig.Instance.GetRandomFrameIndex(),
                BadgeIndex = ProfileGlobalConfig.Instance.GetRandomBadgeIndex(),
            });
        }

        // Exactly one of them chases the player and is the only bot that can win
        if (bots.Count > 0)
        {
            bots[UnityEngine.Random.Range(0, bots.Count)].IsRival = true;
        }

        return bots;
    }

    /// <summary>
    /// Draws the line-up's codenames. Prefixes and suffixes are each drawn without repeats, so a
    /// race never fields two bots sharing a half of their name (no NovaRex next to NovaBolt, and no
    /// NovaRex next to CyberRex) - lanes that rhyme read as a bug.
    /// Falls back to the explicit BotNamePool when one is authored, where only whole names exist.
    /// </summary>
    private List<string> PickBotNames(int count)
    {
        List<string> names = new List<string>();
        if (Config.BotNamePool != null && Config.BotNamePool.Length > 0)
        {
            List<string> pool = new List<string>(Config.BotNamePool);
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int pick = UnityEngine.Random.Range(0, pool.Count);
                names.Add(pool[pick]);
                pool.RemoveAt(pick);
            }

            return names;
        }

        List<string> prefixes = Config.BotNamePrefixes == null
            ? new List<string>()
            : new List<string>(Config.BotNamePrefixes);
        List<string> suffixes = Config.BotNameSuffixes == null
            ? new List<string>()
            : new List<string>(Config.BotNameSuffixes);
        if (prefixes.Count == 0 || suffixes.Count == 0) return names;

        for (int i = 0; i < count; i++)
        {
            // Refill when the config has fewer parts than racers: repeats are better than no name
            if (prefixes.Count == 0) prefixes.AddRange(Config.BotNamePrefixes);
            if (suffixes.Count == 0) suffixes.AddRange(Config.BotNameSuffixes);

            int prefixPick = UnityEngine.Random.Range(0, prefixes.Count);
            int suffixPick = UnityEngine.Random.Range(0, suffixes.Count);
            string prefix = prefixes[prefixPick];
            string suffix = suffixes[suffixPick];
            prefixes.RemoveAt(prefixPick);
            suffixes.RemoveAt(suffixPick);

            // "NovaNova" reads as a mistake, so retry that pairing once against what is left
            if (prefix == suffix && suffixes.Count > 0)
            {
                int retryPick = UnityEngine.Random.Range(0, suffixes.Count);
                suffixes.Add(suffix);
                suffix = suffixes[retryPick];
                suffixes.RemoveAt(retryPick);
            }

            names.Add(prefix + suffix);
        }

        return names;
    }

    private ShipRaceBotData GetRivalBot()
    {
        if (Data.Bots == null) return null;
        foreach (ShipRaceBotData bot in Data.Bots)
        {
            if (bot.IsRival) return bot;
        }

        return null;
    }

    public bool IsRivalBot(ShipRaceRacer racer)
    {
        return racer is { IsPlayer: false } && racer.Bot != null && racer.Bot.IsRival;
    }

    #endregion

    #region Progress

    /// <summary>Core gameplay hook: one point per level won while a race is running.</summary>
    public void AddPoint(int point = 1)
    {
        if (!IsInitialize) return;
        if (!IsEventUnlock) return;
        if (!IsRunning) return;
        if (point <= 0) return;
        ShipRaceStageConfig stageConfig = CurrentStageConfig;
        if (stageConfig == null) return;

        // The stage may have run out while the player was mid level: that loss takes priority
        if (TryResolveByDeadline()) return;

        Data.PlayerPoint = Mathf.Clamp(Data.PlayerPoint + point, 0, stageConfig.Target);
        if (Data.PlayerPoint >= stageConfig.Target)
        {
            // Only the player crossing the line ends a race, so this is the single resolve point
            ResolveRace(stageConfig);
        }
        else
        {
            Data.SaveData();
            OnRaceStateChanged?.Invoke();
        }
    }

    /// <summary>
    /// The rival runs purely on the clock, paced to arrive at the target exactly when the stage
    /// deadline (LimitLongestTime) is reached. Nothing about it reacts to the player, so it never
    /// reads as a shadow following them.
    /// Held at Target-1 until the deadline: crossing the line is what ends the stage, and that is
    /// decided in TryResolveByDeadline, not here.
    /// </summary>
    public int GetRivalPoint(ShipRaceBotData rival, ShipRaceStageConfig stageConfig)
    {
        if (rival == null || stageConfig == null) return 0;
        if (rival.SecondsPerLevel <= 0f) return 0;

        // Paces itself on the clock, like the filler bots
        int point = (int)(GetElapsedSeconds() / rival.SecondsPerLevel + 1e-4d);

        // ...but held inside a gap band around the player, so the chase stays tight instead of the
        // rival running away or falling out of the race when the player is fast or slow
        int minOffset = Mathf.Min(stageConfig.RivalOffsetRange.x, stageConfig.RivalOffsetRange.y);
        int maxOffset = Mathf.Max(stageConfig.RivalOffsetRange.x, stageConfig.RivalOffsetRange.y);
        point = Mathf.Clamp(point, Data.PlayerPoint + minOffset, Data.PlayerPoint + maxOffset);

        // Never on the line: only the deadline or a lost roll puts the rival on the target
        return Mathf.Clamp(point, 0, stageConfig.Target - 1);
    }

    /// <summary>
    /// Stage timing comes from remote config when it carries an entry for the stage, otherwise from
    /// the values authored in ShipRaceGlobalConfig. Read through here rather than off the stage
    /// config directly, so a remote change lands everywhere at once.
    /// </summary>
    public float GetLimitFastestTime(ShipRaceStageConfig stageConfig)
    {
        return stageConfig == null ? 0f : ShipRaceRemoteConfig.Instance.GetLimitFastestTime(stageConfig);
    }

    public float GetLimitLongestTime(ShipRaceStageConfig stageConfig)
    {
        return stageConfig == null ? 0f : ShipRaceRemoteConfig.Instance.GetLimitLongestTime(stageConfig);
    }

    /// <summary>Seconds left before the rival crosses the line and the stage is lost.</summary>
    public TimeSpan StageTimeRemaining
    {
        get
        {
            ShipRaceStageConfig stageConfig = CurrentStageConfig;
            if (stageConfig == null || !IsRunning) return TimeSpan.Zero;
            double left = GetLimitLongestTime(stageConfig) - GetElapsedSeconds();
            return TimeSpan.FromSeconds(left < 0d ? 0d : left);
        }
    }

    /// <summary>
    /// The stage lasts exactly LimitLongestTime. When that passes the rival crosses the line and
    /// the race is lost, without waiting for the player to finish a level - so a player who stops
    /// playing still finds the result waiting the next time they open the game.
    /// Returns true when the race is no longer running.
    /// </summary>
    /// <summary>
    /// The run clock ran out while a race was still going: the event simply ends. Nobody wins - no
    /// rival is credited and no badge is shown - and the player has to acknowledge the loss before
    /// a new run opens. Returns true when it resolved a race here.
    /// </summary>
    private bool TryResolveByRunEnd()
    {
        if (!IsRunning) return false;
        ShipRaceStageConfig stageConfig = CurrentStageConfig;
        if (stageConfig == null) return false;

        Data.IsLoseByRunEnd = true;
        // No winner name: the race was called off, not lost to a rival
        FinishRace(stageConfig, false, string.Empty, GetElapsedSeconds());
        return true;
    }

    private bool TryResolveByDeadline()
    {
        if (!IsRunning) return true;
        ShipRaceStageConfig stageConfig = CurrentStageConfig;
        if (stageConfig == null) return false;
        if (GetElapsedSeconds() < GetLimitLongestTime(stageConfig)) return false;

        ShipRaceBotData rival = GetRivalBot();
        if (rival != null) rival.CurPoint = stageConfig.Target;
        // Frozen on the deadline, not on now: an offline gap must not carry the board past it
        FinishRace(stageConfig, false, rival == null ? string.Empty : rival.Name, GetLimitLongestTime(stageConfig));
        return true;
    }

    /// <summary>
    /// Decides the race the moment the player finishes it, from how long the stage took:
    /// under LimitFastestTime is an automatic win, over LimitLongestTime is an automatic loss, and
    /// in between it comes down to the WinChancePercent rolled when the stage was joined.
    /// </summary>
    private void ResolveRace(ShipRaceStageConfig stageConfig)
    {
        double completeTime = GetElapsedSeconds();
        bool isPlayerWin;
        if (completeTime < GetLimitFastestTime(stageConfig))
        {
            isPlayerWin = true;
        }
        else if (completeTime > GetLimitLongestTime(stageConfig))
        {
            isPlayerWin = false;
        }
        else
        {
            isPlayerWin = UnityEngine.Random.Range(0f, 100f) < Data.WinChancePercent;
        }

        ShipRaceBotData rival = GetRivalBot();
        if (!isPlayerWin)
        {
            // Beaten to the line: the rival crosses and the player is held one short, so the board
            // reads as a race lost by a length rather than two ships sharing the finish
            Data.PlayerPoint = Mathf.Max(0, stageConfig.Target - 1);
            if (rival != null) rival.CurPoint = stageConfig.Target;
        }

        FinishRace(stageConfig, isPlayerWin, isPlayerWin || rival == null ? string.Empty : rival.Name, completeTime);
    }

    private void FinishRace(ShipRaceStageConfig stageConfig, bool isPlayerWin, string winnerName, double finishElapsedSeconds)
    {
        Data.RaceStatus = (int)ShipRaceStatus.Finished;
        Data.IsPlayerWin = isPlayerWin;
        Data.WinnerName = winnerName;
        // Set before the place is worked out: GetElapsedSeconds clamps to it, so every bot is read
        // at the instant the race ended
        Data.FinishElapsedSeconds = (float)finishElapsedSeconds;
        _botStateCache.Clear();
        Data.PlayerPlace = isPlayerWin ? CalculatePlayerPlace(stageConfig) : CalculateLosingPlace(stageConfig);
        Data.IsRewardClaimed = false;
        Data.SaveData();
        TrackRaceFinished(isPlayerWin, finishElapsedSeconds);
        OnStageFinished?.Invoke(Data.CurrentStage);
        OnRaceStateChanged?.Invoke();
    }

    /// <summary>Place when the rival took the race: it is 1st, the player 2nd.</summary>
    private int CalculateLosingPlace(ShipRaceStageConfig stageConfig)
    {
        int racersAhead = 0;
        foreach (ShipRaceBotData bot in Data.Bots)
        {
            if (GetBotPoint(bot) > Data.PlayerPoint) racersAhead++;
        }

        return racersAhead + 1;
    }

    /// <summary>Player win is always 1st: no filler bot can reach the target.</summary>
    private int CalculatePlayerPlace(ShipRaceStageConfig stageConfig)
    {
        double elapsed = GetElapsedSeconds();
        int botsAhead = 0;
        foreach (ShipRaceBotData bot in Data.Bots)
        {
            if (GetBotFinishSeconds(bot, stageConfig) <= elapsed) botsAhead++;
        }

        return botsAhead + 1;
    }

    /// <summary>
    /// Seconds of race time to read the bots at. Once the race is decided this stops at the winning
    /// moment, so every other ship holds the point it had when the line was crossed instead of
    /// carrying on while the result popup waits to be seen.
    /// </summary>
    private double GetElapsedSeconds()
    {
        if (Data.RaceStartTimeUtc.IsNullOrWhitespace()) return 0d;
        double elapsed = (DateTimeManager.UtcNow - Data.RaceStartTimeUtc.StringToDateTime()).TotalSeconds;
        if (elapsed < 0d) elapsed = 0d;
        if (Status == ShipRaceStatus.Finished && Data.FinishElapsedSeconds > 0f)
        {
            elapsed = Math.Min(elapsed, Data.FinishElapsedSeconds);
        }

        return elapsed;
    }

    private double GetBotFinishSeconds(ShipRaceBotData bot, ShipRaceStageConfig stageConfig)
    {
        return SimulateBot(bot, stageConfig).FinishSeconds;
    }

    /// <summary>A bot's point total right now.</summary>
    public int GetBotPoint(ShipRaceBotData bot)
    {
        ShipRaceStageConfig stageConfig = CurrentStageConfig;
        if (stageConfig == null) return 0;
        return SimulateBot(bot, stageConfig).Point;
    }

    /// <summary>
    /// Replays a bot's whole timeline from the race start: one attempt every SecondsPerLevel, each
    /// either a pass (+1) or a fail (BotLoseChance). Kept as a replay instead of stored points so
    /// the bots stay correct across app restarts and offline gaps - the timeline is a function of
    /// (Seed, elapsed time), so it reconstructs identically every time.
    /// Results are cached per attempt index, so a frame that changes nothing costs one division.
    /// </summary>
    private ShipRaceBotState SimulateBot(ShipRaceBotData bot, ShipRaceStageConfig stageConfig)
    {
        ShipRaceBotState state = new ShipRaceBotState { Point = 0, FinishSeconds = double.MaxValue };
        if (bot == null || bot.SecondsPerLevel <= 0f) return state;

        // The rival is paced by the stage deadline, not by attempts. Once the race is over its
        // stored point is the truth (the deadline or a lost roll may have put it on the target).
        if (bot.IsRival)
        {
            state.Point = Status == ShipRaceStatus.Finished
                ? Mathf.Clamp(bot.CurPoint, 0, stageConfig.Target)
                : GetRivalPoint(bot, stageConfig);
            state.AttemptCount = int.MaxValue;
            state.FinishSeconds = state.Point >= stageConfig.Target ? Data.FinishElapsedSeconds : double.MaxValue;
            if (Status != ShipRaceStatus.Finished) bot.CurPoint = state.Point;
            _botStateCache[bot] = state;
            return state;
        }

        double elapsed = GetElapsedSeconds() - bot.StartOffsetSeconds;
        // Epsilon: elapsed is clamped to an exact multiple of SecondsPerLevel when the race is
        // frozen, and a bare truncation can land one attempt short of it
        int attemptCount = elapsed <= 0d ? 0 : (int)(elapsed / bot.SecondsPerLevel + 1e-4d);
        if (_botStateCache.TryGetValue(bot, out ShipRaceBotState cached) && cached.AttemptCount == attemptCount)
        {
            return cached;
        }

        // Filler bots exist to make the lane busy: they drift on the clock but are held one short of
        // the target, so they can never finish and never win
        int maxPoint = Mathf.Max(0, stageConfig.Target - 1);
        float loseChance = Mathf.Clamp01(stageConfig.BotLoseChance);
        int point = 0;
        for (int attempt = 1; attempt <= attemptCount; attempt++)
        {
            if (loseChance > 0f && Hash01(bot.Seed, attempt) < loseChance)
            {
                // BotLosePoint 0 wipes the bot back to the start, same as the player losing a level
                point = stageConfig.BotLosePoint <= 0
                    ? 0
                    : Mathf.Max(0, point - stageConfig.BotLosePoint);
                continue;
            }

            if (point >= maxPoint) break;
            point++;
        }

        state.Point = Mathf.Clamp(point, 0, maxPoint);
        state.AttemptCount = attemptCount;
        _botStateCache[bot] = state;
        return state;
    }

    /// <summary>True for the saved bot recorded as the winner of the finished race.</summary>
    private bool IsWinnerBotData(ShipRaceBotData bot)
    {
        if (bot == null) return false;
        if (Status != ShipRaceStatus.Finished) return false;
        if (Data.IsPlayerWin) return false;
        return !string.IsNullOrEmpty(Data.WinnerName) && bot.Name == Data.WinnerName;
    }

    /// <summary>Stable hash -> [0,1). Deterministic across platforms and sessions, unlike Random.</summary>
    private static float Hash01(int seed, int attempt)
    {
        unchecked
        {
            uint hash = (uint)seed * 2654435761u ^ (uint)attempt * 2246822519u;
            hash ^= hash >> 13;
            hash *= 3266489917u;
            hash ^= hash >> 16;
            return (hash & 0xFFFFFF) / (float)0x1000000;
        }
    }

    /// <summary>
    /// The player quit mid level and relaunched on the same level, so that attempt counts as a loss.
    /// Called from GameManager.OnFirebaseInitialized, alongside the other events' equivalents.
    /// Also settles the stage deadline, since the clock kept running while the game was closed.
    /// </summary>
    public void HandleFailOnLastProgress()
    {
        OnPlayerLoseLevel();
    }

    /// <summary>Wipes the player's progress for this race. Called when a level is lost.</summary>
    public void OnPlayerLoseLevel()
    {
        if (!IsInitialize) return;
        if (!IsEventUnlock) return;
        if (!IsRunning) return;
        // Losing a level can not save a race whose deadline already passed
        if (TryResolveByDeadline()) return;
        if (Data.PlayerPoint <= 0) return;
        // Only the player falls back: the rival is on its own clock and keeps its pace
        Data.PlayerPoint = 0;
        Data.SaveData();
        OnRaceStateChanged?.Invoke();
    }

    /// <summary>
    /// Player + bots sorted for the leaderboard: more points first, then whoever is pacing faster.
    /// The player wins ties, so a shared point total never reads as the player losing.
    /// </summary>
    public List<ShipRaceRacer> GetRacers()
    {
        List<ShipRaceRacer> racers = new List<ShipRaceRacer>
        {
            new ShipRaceRacer { IsPlayer = true, Name = string.Empty, Point = Data.PlayerPoint },
        };
        foreach (ShipRaceBotData bot in Data.Bots)
        {
            racers.Add(new ShipRaceRacer { IsPlayer = false, Name = bot.Name, Point = GetBotPoint(bot), Bot = bot });
        }

        racers.Sort((a, b) =>
        {
            if (a.Point != b.Point) return b.Point.CompareTo(a.Point);
            if (a.IsPlayer != b.IsPlayer) return a.IsPlayer ? -1 : 1;
            if (a.Bot == null || b.Bot == null) return 0;
            return a.Bot.SecondsPerLevel.CompareTo(b.Bot.SecondsPerLevel);
        });
        return racers;
    }

    #endregion

    #region Claim reward / stage advance

    public bool TryGetClaimReward(out ClaimGameResourceGroup claimGroup)
    {
        claimGroup = new ClaimGameResourceGroup();
        if (!HasUnclaimedReward) return false;
        ShipRaceStageConfig stageConfig = CurrentStageConfig;
        if (stageConfig == null) return false;
        // Same rule as Ufo: the collection reward is only handed out while a card collection event
        // is running, otherwise the stage pays its default set
        ClaimGameResourceGroup rewards = stageConfig.Rewards;
        if (rewards == null) return false;
        claimGroup.TitleName = "Congratulation!";
        claimGroup.GameResourceList.AddRange(rewards.GameResourceList);
        claimGroup.ClaimType = "tidal_dash";
        claimGroup.Placement = "tidal_dash";
        return claimGroup.GameResourceList.Count > 0;
    }

    /// <summary>
    /// Claims the finished stage's reward, then opens the next stage (or loops back to stage 0
    /// after the last one). The player has to join again to start the next race.
    /// </summary>
    public async UniTask TryClaimReward()
    {
        if (!IsInitialize) return;
        if (!HasUnclaimedReward) return;
        int finishedStage = Data.CurrentStage;
        bool hasReward = TryGetClaimReward(out ClaimGameResourceGroup claimGroup);

        Data.IsRewardClaimed = true;
        AdvanceStage();
        // A win claimed after the run clock expired is the same acknowledgement as a confirmed loss:
        // the run was held open for it, so the next one opens here
        if (IsRunExpired)
        {
            OpenNewRun(GetCurrentPeriodStartTime(DateTimeManager.UtcNow).AddSeconds(Config.ExistTime));
        }
        else
        {
            Data.SaveData();
        }
        OnRewardClaimed?.Invoke(finishedStage);
        OnRaceStateChanged?.Invoke();

        if (!hasReward) return;
        RewardManager.Instance.AddResource(claimGroup);
        await RewardManager.Instance.ClaimResource(claimGroup);
    }

    private void AdvanceStage()
    {
        int nextStage = Data.CurrentStage + 1;
        if (Config.GetStageConfig(nextStage) == null)
        {
            // Circuit cleared. It restarts at stage 0 until the run has been cleared MaxCompleteLoop
            // times; only then does IsAllStageWon close the event for the rest of the run.
            Data.CompleteLoopCount++;
            Data.CurrentStage = 0;
            Data.IsAllStageWon = Data.CompleteLoopCount >= Mathf.Max(1, Config.MaxCompleteLoop);
        }
        else
        {
            Data.CurrentStage = nextStage;
        }

        ResetRaceState();
    }

    /// <summary>Back to the joinable state, keeping the current stage.</summary>
    private void ResetRaceState()
    {
        Data.RaceStatus = (int)ShipRaceStatus.NotStarted;
        Data.PlayerPoint = 0;
        // Must follow PlayerPoint back to 0, or the next race would animate the player's ship
        // backwards from the point it ended the last one on. Bot equivalents go with the bot list.
        Data.PlayerLastShownPoint = 0;
        Data.PlayerLastShownPointEndGame = 0;
        Data.PlayerPlace = 0;
        Data.IsRewardClaimed = false;
        Data.IsPlayerWin = false;
        Data.WinnerName = string.Empty;
        // Belongs to the race that just ended: leaving it set would hide PanelTime and show the
        // event ended text on the next race
        Data.IsLoseByRunEnd = false;
        Data.IsEndGameResultShown = false;
        Data.FinishElapsedSeconds = 0f;
        Data.WinChancePercent = 0f;
        Data.Bots = new List<ShipRaceBotData>();
        _lastBotPoints = Array.Empty<int>();
        _botStateCache.Clear();
    }

    #endregion

    #region Show result

    public async UniTask TryShowWin()
    {
        if (!IsInitialize) return;
        if (!IsEventUnlock) return;
        if (!NeedShowWin) return;
        await (ShowWinAsync?.Invoke(Data.CurrentStage) ?? UniTask.CompletedTask);
    }

    /// <summary>
    /// Shows the lost race. The state is left untouched so the popup can still display the final
    /// board (points, bots, who won) - the UI calls ConfirmLose when the player acknowledges it.
    /// </summary>
    public async UniTask TryShowLose()
    {
        if (!IsInitialize) return;
        if (!IsEventUnlock) return;
        if (!NeedShowLose) return;
        await (ShowLoseAsync?.Invoke(Data.CurrentStage) ?? UniTask.CompletedTask);
    }

    /// <summary>
    /// Player acknowledged the loss: the race is cleared back to NotStarted on the same stage,
    /// ready to be joined again. Until this is called the loss stays pending, so closing the game
    /// on the popup just shows it again next session.
    /// </summary>
    public const string PlayerDisplayName = "You";

    public int GetStageTarget(int stageId)
    {
        return Config.GetStageConfig(stageId)?.Target ?? 0;
    }

    /// <summary>
    /// Player + bots in the order they were rolled, NOT sorted by progress: the board keeps every
    /// ship in its own lane for the whole race. Use GetRacers for a ranked list.
    /// </summary>
    public List<ShipRaceRacer> GetRacersInJoinOrder()
    {
        List<ShipRaceRacer> racers = new List<ShipRaceRacer>
        {
            new ShipRaceRacer { IsPlayer = true, Name = PlayerDisplayName, Point = Data.PlayerPoint },
        };
        if (Data.Bots == null) return racers;
        foreach (ShipRaceBotData bot in Data.Bots)
        {
            racers.Add(new ShipRaceRacer { IsPlayer = false, Name = bot.Name, Point = GetBotPoint(bot), Bot = bot });
        }

        return racers;
    }

    /// <summary>
    /// Same racers as GetRacers, ranked by the points the end game board last showed instead of the
    /// live ones. It uses the two orders together to animate cards from their old rank to the new.
    /// </summary>
    public List<ShipRaceRacer> GetRacersRankedByLastShownEndGame()
    {
        List<ShipRaceRacer> racers = GetRacersInJoinOrder();
        racers.Sort((a, b) =>
        {
            int lastShownA = GetLastShownPointEndGame(a);
            int lastShownB = GetLastShownPointEndGame(b);
            if (lastShownA != lastShownB) return lastShownB.CompareTo(lastShownA);
            if (a.IsPlayer != b.IsPlayer) return a.IsPlayer ? -1 : 1;
            if (a.Bot == null || b.Bot == null) return 0;
            return a.Bot.SecondsPerLevel.CompareTo(b.Bot.SecondsPerLevel);
        });
        return racers;
    }

    /// <summary>
    /// Point each ship was last drawn at. The stage modal animates from here to the live point,
    /// so a player who was away sees the ships travel the ground they missed.
    /// </summary>
    public int GetLastShownPoint(ShipRaceRacer racer)
    {
        if (racer == null) return 0;
        return racer.IsPlayer ? Data.PlayerLastShownPoint : racer.Bot?.LastShownPoint ?? 0;
    }

    /// <summary>
    /// True for the bot that took the race. Always false for the player: a player win is shown by
    /// the win panel, not by a badge on the board.
    /// </summary>
    public bool IsWinnerBot(ShipRaceRacer racer)
    {
        if (racer == null || racer.IsPlayer) return false;
        return IsWinnerBotData(racer.Bot);
    }

    /// <summary>
    /// Same idea as GetLastShownPoint but for the end game board, which keeps its own marker: both
    /// surfaces replay the same gain once each, so watching it on the win screen does not rob the
    /// stage board of its animation.
    /// </summary>
    public int GetLastShownPointEndGame(ShipRaceRacer racer)
    {
        if (racer == null) return 0;
        return racer.IsPlayer ? Data.PlayerLastShownPointEndGame : racer.Bot?.LastShownPointEndGame ?? 0;
    }

    /// <summary>Called by the stage board once the ships have finished animating.</summary>
    public void MarkRacersShown()
    {
        Data.PlayerLastShownPoint = Data.PlayerPoint;
        if (Data.Bots != null)
        {
            foreach (ShipRaceBotData bot in Data.Bots)
            {
                bot.LastShownPoint = GetBotPoint(bot);
            }
        }

        Data.SaveData();
    }

    /// <summary>
    /// True while a decided race still has its result to show on the end game board. Flips false
    /// once the board has played it, so the win screen only carries the result the first time.
    /// </summary>
    public bool NeedShowEndGameResult => Status == ShipRaceStatus.Finished && !Data.IsEndGameResultShown;

    /// <summary>
    /// Burns the one-shot for a decided race. Called by whichever end game surface showed it first -
    /// the win screen board, or the confirm lose screen - so the result is never shown twice.
    /// </summary>
    public void MarkEndGameResultShown()
    {
        if (Status != ShipRaceStatus.Finished) return;
        if (Data.IsEndGameResultShown) return;
        Data.IsEndGameResultShown = true;
        Data.SaveData();
    }

    /// <summary>Called by the end game board once its cards have finished re-ranking.</summary>
    public void MarkRacersShownEndGame()
    {
        // A decided race is only worth showing once: after this the win screen gives the slot to
        // another event instead of replaying a race nobody can still affect
        MarkEndGameResultShown();
        Data.PlayerLastShownPointEndGame = Data.PlayerPoint;
        if (Data.Bots != null)
        {
            foreach (ShipRaceBotData bot in Data.Bots)
            {
                bot.LastShownPointEndGame = GetBotPoint(bot);
            }
        }

        Data.SaveData();
    }

    public void ConfirmLose()
    {
        if (!NeedShowLose) return;
        ResetRaceState();
        // The run was held open just for this acknowledgement, so the next one starts now
        if (IsRunExpired)
        {
            OpenNewRun(GetCurrentPeriodStartTime(DateTimeManager.UtcNow).AddSeconds(Config.ExistTime));
        }
        else
        {
            Data.SaveData();
        }

        OnRaceStateChanged?.Invoke();
    }

    #endregion

    #region Analytics

    /// <summary>
    /// Stage number as the player sees it: the config is 0 based, the UI and the dashboards are not.
    /// </summary>
    private int TrackedStage => Data.CurrentStage + 1;

    private void TrackRaceStart()
    {
        IngameFirebaseAnalystic.Instance.TrackShipRaceStart(GameManager.Instance.Level.ToString(), TrackedStage);
    }

    /// <summary>
    /// One event per decided race, sent from the resolve point rather than from the result popup:
    /// a race settled on relaunch - the stage deadline or the run clock passed while the game was
    /// closed - is still counted, and the duration stays the real race time instead of including
    /// however long the player was away.
    /// </summary>
    private void TrackRaceFinished(bool isPlayerWin, double finishElapsedSeconds)
    {
        string level = GameManager.Instance.Level.ToString();
        int duration = Mathf.Max(0, Mathf.RoundToInt((float)finishElapsedSeconds));
        if (isPlayerWin)
        {
            IngameFirebaseAnalystic.Instance.TrackShipRaceWin(level, TrackedStage, duration);
            return;
        }

        // Set by TryResolveByRunEnd before it finishes the race, so the two ways to lose stay apart
        IngameFirebaseAnalystic.Instance.TrackShipRaceLose(
            level, TrackedStage, duration, Data.IsLoseByRunEnd ? "run_end" : "rival");
    }

    #endregion

    [Button]
    public void AddCustomPoint(int point)
    {
        AddPoint(point);
    }
}

/// <summary>Replayed bot progress. Runtime only, rebuilt from (Seed, elapsed time).</summary>
public struct ShipRaceBotState
{
    public int Point;
    public int AttemptCount;
    // double.MaxValue while the bot has not reached the target yet
    public double FinishSeconds;
}

/// <summary>Runtime row for the leaderboard UI. Not saved.</summary>
public class ShipRaceRacer
{
    public bool IsPlayer;
    public string Name;
    public int Point;
    public ShipRaceBotData Bot;
}

[System.Serializable]
public class ShipRaceBotData
{
    public string Name;
    public float SecondsPerLevel;
    public float StartOffsetSeconds;
    // Seeds this bot's pass/fail rolls (see ShipRaceManager.SimulateBot)
    public int Seed;
    // Where the UI last drew this racer. Tracked per surface so the stage board and the end game
    // board each replay the same gain once, independently of the other.
    public int LastShownPoint;
    public int LastShownPointEndGame;
    // Profile look, rolled once at join so the bot keeps the same face all race
    public int AvatarIndex;
    public int FrameIndex;
    public int BadgeIndex = -1;
    // The one bot that chases the player and can take the race. Its point is driven by the player's
    // progress, not by the clock, and it is capped below the target until the race resolves.
    public bool IsRival;
    // Rival only: its stored point. Filler bots derive theirs from elapsed time instead.
    public int CurPoint;
}

[System.Serializable]
[DataGenerate(16)]
public class ShipRaceData : IDataModule
{
    public static ShipRaceData Instance => InGameDataManager.Instance.InGameData.ShipRaceData;
    public string Key => nameof(ShipRaceData);
    public int TargetVersion => 1;
    [field: SerializeField] public int Version { get; set; } = 1;

    public string RunEndTimeUtc = string.Empty;
    public string RaceStartTimeUtc = string.Empty;
    // Race time at which the race was decided; bots are frozen at this instant afterwards
    public float FinishElapsedSeconds;
    public int CurrentStage;
    // 0 = not started, 1 = running, 2 = finished (see ShipRaceStatus)
    public int RaceStatus;
    public int PlayerPoint;
    public int PlayerLastShownPoint;
    public int PlayerLastShownPointEndGame;
    public int PlayerPlace;
    public bool IsRewardClaimed;
    // Rolled once when the stage is joined: the odds used when the finish lands between the two
    // time limits. Saved so the outcome can not be re-rolled by replaying the finish.
    public float WinChancePercent;
    // Every stage of this run won: the event is done for the day and the home button hides
    public bool IsAllStageWon;
    // How many times the full circuit has been cleared this run (see ShipRaceGlobalConfig.MaxCompleteLoop)
    public int CompleteLoopCount;
    [field: SerializeField] public bool IsShowTut { get; set; } = false;
    // A finished race remembers who took it: the player, or the bot named here
    public bool IsPlayerWin;
    public string WinnerName = string.Empty;
    // Lost because the run clock expired, not to a rival: no winner, and the stage timer is hidden
    public bool IsLoseByRunEnd;
    // The end game board has already played this race's result, so it is not shown again
    public bool IsEndGameResultShown;
    public List<ShipRaceBotData> Bots = new List<ShipRaceBotData>();

    public void SetDefault()
    {
        RunEndTimeUtc = string.Empty;
        RaceStartTimeUtc = string.Empty;
        FinishElapsedSeconds = 0f;
        CurrentStage = 0;
        RaceStatus = (int)ShipRaceStatus.NotStarted;
        PlayerPoint = 0;
        PlayerLastShownPoint = 0;
        PlayerLastShownPointEndGame = 0;
        PlayerPlace = 0;
        IsRewardClaimed = false;
        WinChancePercent = 0f;
        IsAllStageWon = false;
        CompleteLoopCount = 0;
        IsPlayerWin = false;
        WinnerName = string.Empty;
        IsLoseByRunEnd = false;
        IsEndGameResultShown = false;
        Bots = new List<ShipRaceBotData>();
    }

    public void Migrate()
    {

    }

    public string ToJson()
    {
        return DataSerializer.ToJson(this);
    }

    public IDataModule FromJson(string json)
    {
        return DataSerializer.FromJson<TreetopData>(json);
    }
}
