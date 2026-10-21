using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using DataModule;
using R3;
using R3.Triggers;
using Sirenix.OdinInspector;
using TW.Utility.DesignPattern;
using UnityEngine;
using Manager;
using CoreData;
using Core;
using TW.Utility.Extension;

public class StarBlazeManager : Singleton<StarBlazeManager>
{
    public const int EventLoopDays = 3;
    public static readonly int[] BonusArray = { 1, 5, 10, 25, 100 };
    public static Action OnInitializeComplete { get; set; }
    public static Action OnResetData { get; set; }
    public DateTime LastStartStarBlazeTime { get; set; } 
    public DateTime LastStartDoublePointTime { get; set; }
    public DateTime DataLastStartStarBlazeTime { get; set; }
    private StarBlazeData StarBlazeData => StarBlazeData.Instance;
    public int CurrentPoint => StarBlazeData.CurrentPoint;
    public int CurrentStreak => StarBlazeData.CurrentWinStreak;
    public int LastStreak => StarBlazeData.LastWinStreak;
    public bool IsStartEvent => StarBlazeData.IsStartEvent;
    public bool IsEventUnlock => StarBlazeRemoteConfig.Instance.LevelUnlock > 0 && 
                                 StarBlazeRemoteConfig.Instance.LevelUnlock <= GameManager.Instance.Level;

    public TimeSpan StarBlazeTimeRemaining => LastStartStarBlazeTime.AddDays(EventLoopDays) - DateTimeManager.UtcNow;
    [ShowInInspector] public bool IsInitialize { get; private set; }

    // Mốc bắt đầu của chu kỳ hiện tại, đếm mỗi EventLoopDays ngày kể từ ngày bắt đầu event (StarBlazeStartDate).
    private DateTime GetCurrentPeriodStartTime(DateTime utcNow)
    {
        DateTime eventStartTime = StarBlazeRemoteConfig.Instance.StarBlazeStartDate;
        double elapsedDays = (utcNow - eventStartTime).TotalDays;
        int periodIndex = (int)Math.Floor(elapsedDays / EventLoopDays);
        if (periodIndex < 0) periodIndex = 0;
        return eventStartTime.AddDays(periodIndex * EventLoopDays);
    }

#if UNITY_EDITOR
    [ShowInInspector] private string DebugLastStartStarBlazeTime => LastStartStarBlazeTime.DateTimeToString();
    [ShowInInspector] private string DebugLastStartDoublePointTime => LastStartDoublePointTime.DateTimeToString();
#endif
    
    public async UniTask Initialize()
    {
        if (IsInitialize) return;
        await UniTask.Yield();
        DateTime utcNow = DateTimeManager.UtcNow;
        if (!StarBlazeRemoteConfig.Instance.IsBetweenEvent(DateTimeManager.UtcNow)) return;
        if (StarBlazeRemoteConfig.Instance.LevelUnlock <= 0) return;
        IsInitialize = true;
        LastStartStarBlazeTime = GetCurrentPeriodStartTime(utcNow);
        DataLastStartStarBlazeTime = StarBlazeData.LastStartStarBlazeTime.StringToDateTime();
        if (DataLastStartStarBlazeTime != LastStartStarBlazeTime)
        {
            DataLastStartStarBlazeTime = LastStartStarBlazeTime;
            ResetEventData();
        }
        OnInitializeComplete?.Invoke();
        
        this.UpdateAsObservable().Subscribe(CheckResetEventData).AddTo(this);
    }
    private void CheckResetEventData(Unit _)
    {
        DateTime utcNow = DateTimeManager.UtcNow;
        if (!StarBlazeRemoteConfig.Instance.IsBetweenEvent(DateTimeManager.UtcNow))
        {
            if (!IsInitialize) return;
            IsInitialize = false;
            OnInitializeComplete?.Invoke();
            return;
        }

        LastStartStarBlazeTime = GetCurrentPeriodStartTime(utcNow);
        if (DataLastStartStarBlazeTime != LastStartStarBlazeTime)
        {
            DataLastStartStarBlazeTime = LastStartStarBlazeTime;
            ResetEventData();
        }

        if (!IsInitialize)
        {
            IsInitialize = true;
            OnInitializeComplete?.Invoke();
        }

    }

    private void ResetEventData()
    {
        StarBlazeData.LastWinStreak = 0;
        StarBlazeData.CurrentWinStreak = 0;
        StarBlazeData.CurrentPoint = 0;
        StarBlazeData.WillAddPoint = 0;
        StarBlazeData.IsStartEvent = true;
        StarBlazeData.IsDoublePoint = false;
        StarBlazeData.LastStartStarBlazeTime = LastStartStarBlazeTime.DateTimeToString();
        StarBlazeData.LastStartDoublePointTime = string.Empty;
        StarBlazeData.SaveData();
        OnResetData?.Invoke();
    }
    
    public StarBlazeState GetStarBlazeState(int point)
    {
        StarBlazeState state = new StarBlazeState
        {
            CurrentPoint = point,
        };
        int currentPoint = point;
        foreach (StarBlazeStageConfig starBlazeStageConfig in StarBlazeGlobalConfig.Instance.StarBlazeConfig.StageConfigs)
        {
            currentPoint -= starBlazeStageConfig.Point;
            if (currentPoint < 0)
            {
                state.CurrentStagePoint = currentPoint + starBlazeStageConfig.Point;
                state.CurrentMaxStagePoint = starBlazeStageConfig.Point;
                state.CurrentStageConfig = starBlazeStageConfig;
                break;
            }
        }

        if (currentPoint >= 0)
        {
            StarBlazeStageConfig starBlazeStageConfig = StarBlazeGlobalConfig.Instance.StarBlazeConfig.StageConfigs[^1];
            state.CurrentStageConfig = starBlazeStageConfig;
            state.CurrentStagePoint = starBlazeStageConfig.Point;
            state.CurrentMaxStagePoint = starBlazeStageConfig.Point;
        }
        return state;
    }

    public async UniTask TryClaimReward()
    {
        if (!IsInitialize) return;
        if (!IsStartEvent) return;
        if (!IsEventUnlock) return;
        if (StarBlazeData.WillAddPoint == 0) return;
        int willAddPoint = StarBlazeData.WillAddPoint;
        int currentPoint = CurrentPoint;
        int targetPoint = currentPoint + willAddPoint;
        StarBlazeData.CurrentPoint = targetPoint;
        StarBlazeData.WillAddPoint = 0;
        int lastStreak = StarBlazeData.LastWinStreak;
        int currentStreak = StarBlazeData.CurrentWinStreak;
        bool isStreakChanged = lastStreak != currentStreak;
        StarBlazeData.LastWinStreak = currentStreak;
        
        StarBlazeData.SaveData();
        bool hasReward = TryGetClaimGameResourceGroup(currentPoint, targetPoint, out ClaimGameResourceGroup claimGroup);
        if (hasReward)
        {
            GameManager.Instance.TrackStarBlazeClaimed(1);
            CheckPassStage(currentPoint, targetPoint);
            TrackSweetMealClaimStages(currentPoint, targetPoint);
            RewardManager.Instance.AddResource(claimGroup);

            if (HasReachedFinalStage(currentPoint, targetPoint))
            {
                GameManager.Instance.TrackSweetMealChampion();
            }
        }

        if (IsAlreadyCompleteEvent(currentPoint, targetPoint)) return;
        await (UIStreakClaimPoint.PlayClaimAnimAsync?.Invoke(willAddPoint) ?? UniTask.CompletedTask);
        await (UIStarBlazeProgress.PlayProgressAsync?.Invoke(currentPoint, targetPoint) ?? UniTask.CompletedTask);
        if (hasReward)
        {
            await RewardManager.Instance.ClaimResource(claimGroup);
            await UIManager.Instance.ClaimRewardComplete();
        }

        if (isStreakChanged)
        {
            UIStreakBar.PlayStreakAsync?.Invoke(lastStreak, currentStreak).Forget();
        }
        StarBlazeData.SaveData();
    }

    public bool TryGetClaimGameResourceGroup(int currentProgress, int targetProgress, out ClaimGameResourceGroup claimGroup)
    {
        claimGroup = new ClaimGameResourceGroup();
        int point = 0;
        int reward = 0;
        for (int i = 0; i < StarBlazeGlobalConfig.Instance.StarBlazeConfig.StageConfigs.Length; i++)
        {
            point += StarBlazeGlobalConfig.Instance.StarBlazeConfig.StageConfigs[i].Point;
            if (point <= currentProgress) continue;
            if (point > targetProgress) break;
            
            // bool isHaveCollectionReward = CardCollectionManager.Instance.IsActiveForReward();
            bool isHaveCollectionReward = false;
            
            if (isHaveCollectionReward)
            {
                foreach (GameResource gameResource in StarBlazeGlobalConfig.Instance.StarBlazeConfig.StageConfigs[i].Reward.ItemList)
                {
                    claimGroup.GameResourceList.Add(gameResource);
                    reward++;
                }
            }
            else
            {
                foreach (GameResource gameResource in StarBlazeGlobalConfig.Instance.StarBlazeConfig.StageConfigs[i].DefaultReward.ItemList)
                {
                    claimGroup.GameResourceList.Add(gameResource);
                    reward++;
                }
            }
            
        }
        return reward > 0;
    }

    // Bắn sweet_meal_claim cho từng mốc (stage) vừa vượt qua trong lần claim này, 1 event / 1 reward item.
    private void TrackSweetMealClaimStages(int currentProgress, int targetProgress)
    {
        StarBlazeStageConfig[] stageConfigs = StarBlazeGlobalConfig.Instance.StarBlazeConfig.StageConfigs;
        if (stageConfigs == null || stageConfigs.Length == 0) return;
        string level = GameManager.Instance.Level.Value.ToString();
        int winstreak = StarBlazeData.CurrentWinStreak;
        int forkRemain = GetStarBlazeState(targetProgress).CurrentStagePoint;
        int point = 0;
        for (int i = 0; i < stageConfigs.Length; i++)
        {
            point += stageConfigs[i].Point;
            if (point <= currentProgress) continue;
            if (point > targetProgress) break;
            int requiredForks = stageConfigs[i].Point;

            // bool isHaveCollectionReward = CardCollectionManager.Instance.IsActiveForReward();
            bool isHaveCollectionReward = false;
            
            if (isHaveCollectionReward)
            {
                if (stageConfigs[i].Reward?.ItemList == null) continue;
                foreach (GameResource gameResource in stageConfigs[i].Reward.ItemList)
                {
                    if (gameResource == null) continue;
                    // IngameFirebaseAnalystic.Instance.TrackSweetMealClaim(
                    //     level,
                    //     stageConfigs[i].Index,
                    //     winstreak,
                    //     forkRemain,
                    //     requiredForks,
                    //     GetRewardType(gameResource.Type),
                    //     gameResource.Value.Value);
                }
            }
            else
            {
                foreach (GameResource gameResource in stageConfigs[i].DefaultReward.ItemList)
                {
                    if (stageConfigs[i].DefaultReward?.ItemList == null) continue;
                    if (gameResource == null) continue;
                    // IngameFirebaseAnalystic.Instance.TrackSweetMealClaim(
                    //     level,
                    //     stageConfigs[i].Index,
                    //     winstreak,
                    //     forkRemain,
                    //     requiredForks,
                    //     GetRewardType(gameResource.Type),
                    //     gameResource.Value.Value);
                }
            }
            
        }
    }

    // Map ResourceType -> reward_type dùng cho analytics: heart, coin, booster_{name}.
    private static string GetRewardType(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Money:
                return "coin";
            case ResourceType.Life:
            case ResourceType.BonusLife:
            case ResourceType.InfiniteLife:
                return "heart";
            case ResourceType.AddSlot:
            case ResourceType.AutoFill:
            case ResourceType.PackedUp:
            case ResourceType.Shuffle:
            case ResourceType.UnlockTank:
                return $"booster_{type}";
            default:
                return type.ToString().ToLower();
        }
    }

    private bool HasReachedFinalStage(int currentProgress, int targetProgress)
    {
        StarBlazeStageConfig[] stageConfigs = StarBlazeGlobalConfig.Instance.StarBlazeConfig.StageConfigs;
        if (stageConfigs == null || stageConfigs.Length == 0) return false;

        int finalStagePoint = 0;
        for (int i = 0; i < stageConfigs.Length; i++)
        {
            finalStagePoint += stageConfigs[i].Point;
        }

        return currentProgress < finalStagePoint && targetProgress >= finalStagePoint;
    }

    private void CheckPassStage(int currentProgress, int targetProgress)
    {
        StarBlazeStageConfig[] stageConfigs = StarBlazeGlobalConfig.Instance.StarBlazeConfig.StageConfigs;
        if (stageConfigs == null || stageConfigs.Length == 0) return;
        int point = 0;
        for (int i = 0; i < stageConfigs.Length; i++)
        {
            point += stageConfigs[i].Point;
            if (point <= currentProgress) continue;
            if (point > targetProgress) break;
            // AntLabManager.NotifyHighest(AntLabQuestType.SweetMeal, stageConfigs[i].Index + 1);
            Debug.Log($"StarBlaze Claim Stage {stageConfigs[i].Index + 1}.");
        }
    }
    public int GetCurrentPassedStep()
    {
        StarBlazeStageConfig[] stageConfigs = StarBlazeGlobalConfig.Instance.StarBlazeConfig.StageConfigs;
        if (stageConfigs == null || stageConfigs.Length == 0) return 0;
        int point = 0;
        int passedStep = 0;
        for (int i = 0; i < stageConfigs.Length; i++)
        {
            point += stageConfigs[i].Point;
            if (point > CurrentPoint) break;
            passedStep = stageConfigs[i].Index + 1;
        }
        return passedStep;
    }

    public void AddProgressAndWinStreak()
    {
        if (!IsInitialize) return;
        if (!IsStartEvent) return;
        if (!IsEventUnlock) return;
        int forksEarned = GetCurrentBonusPoint();
        int eventWinStreak = StarBlazeData.CurrentWinStreak + 1;
        AddProgress();
        IncreaseWinStreak();
        
        int projectedPoint = CurrentPoint + StarBlazeData.WillAddPoint;
        int forkRemain = GetStarBlazeState(projectedPoint).CurrentStagePoint;
        int levelWon = GameManager.Instance.LastLevelPlayed;
        // IngameFirebaseAnalystic.Instance.TrackForkEarn(levelWon.ToString(), forksEarned, eventWinStreak, forkRemain);
    }
    public void ResetWinStreak()
    {
        if (!IsInitialize) return;
        if (!IsStartEvent) return;
        if (!IsEventUnlock) return;
        StarBlazeData.LastWinStreak = StarBlazeData.CurrentWinStreak;
        StarBlazeData.CurrentWinStreak = 0;
        StarBlazeData.SaveData();
    }

    private void AddProgress()
    {
        int currentStagePoint = GetCurrentBonusPoint();
        StarBlazeData.WillAddPoint += currentStagePoint;
        StarBlazeData.SaveData();
    }

    private void IncreaseWinStreak()
    {
        StarBlazeData.LastWinStreak = StarBlazeData.CurrentWinStreak;
        StarBlazeData.CurrentWinStreak = Mathf.Clamp(StarBlazeData.CurrentWinStreak+1, 0, BonusArray.Length-1);
        StarBlazeData.SaveData();
    }

    private int GetCurrentBonusPoint()
    {
        return BonusArray[StarBlazeData.CurrentWinStreak];
    }

    public bool IsStageUnlock(int index)
    {
        if (index < 0 || index >= StarBlazeGlobalConfig.Instance.StarBlazeConfig.StageConfigs.Length)
        {
            return false;
        }

        int requiredPoint = 0;
        for (int i = 0; i < StarBlazeGlobalConfig.Instance.StarBlazeConfig.StageConfigs.Length; i++)
        {
            requiredPoint += StarBlazeGlobalConfig.Instance.StarBlazeConfig.StageConfigs[i].Point;
            if (i == index)
            {
                break;
            }
        }
        return CurrentPoint >= requiredPoint;
    }

    public void TryAutoClaimReward()
    {
        if (StarBlazeData.WillAddPoint == 0) return;
        int willAddPoint = StarBlazeData.WillAddPoint;
        int currentProgress = CurrentPoint;
        int targetProgress = currentProgress + willAddPoint;
        StarBlazeData.CurrentPoint = targetProgress;
        StarBlazeData.WillAddPoint = 0;
        
        int currentStreak = StarBlazeData.CurrentWinStreak;
        StarBlazeData.LastWinStreak = currentStreak;
        
        StarBlazeData.SaveData();
        bool hasReward = TryGetClaimGameResourceGroup(currentProgress, targetProgress, out ClaimGameResourceGroup claimGroup);
        if (hasReward)
        {
            GameManager.Instance.TrackStarBlazeClaimed(1);
            CheckPassStage(currentProgress, targetProgress);
            TrackSweetMealClaimStages(currentProgress, targetProgress);
            foreach (GameResource gameResource in claimGroup.GameResourceList)
            {
                PlayerResourceManager.Instance.AddResourceValue(gameResource.Type, gameResource.Amount, claimGroup.ClaimType);
            }

            if (HasReachedFinalStage(currentProgress, targetProgress))
            {
                GameManager.Instance.TrackSweetMealChampion();
            }

        }
    }

    public void TryAutoResetStreak()
    {
        if (StarBlazeData.CurrentWinStreak == 0) return;
        Debug.Log("TryAutoResetStreak");
        StarBlazeData.LastWinStreak = StarBlazeData.CurrentWinStreak;
        Debug.Log("LastWinStreak:" + StarBlazeData.LastWinStreak);
        StarBlazeData.CurrentWinStreak = 0;
        StarBlazeData.SaveData();
        // UIStreakBar.SetStreakAction?.Invoke(StarBlazeData.CurrentWinStreak);
        UIStreakBar.PlayStreakAsync?.Invoke(StarBlazeData.LastWinStreak, StarBlazeData.CurrentWinStreak);
    }
    
    [Button]
    public void AddCustomPoint(int point)
    {
        StarBlazeData.WillAddPoint += point;
        StarBlazeData.SaveData();
        IncreaseWinStreak();
    }

#if CHEAT_ONLY || UNITY_EDITOR
    public bool IsCheatEventRunning => IsInitialize && IsStartEvent && IsEventUnlock;
    public int WillAddPoint => StarBlazeData.WillAddPoint;

    // Cheat: cộng điểm chờ claim như khi thắng level nhưng giữ nguyên streak; về Home sẽ claim qua TryClaimReward.
    // Event chưa chạy thì bỏ qua, nếu không điểm sẽ nằm trong WillAddPoint và bị TryAutoClaimReward cộng lén lần mở game sau.
    public bool CheatAddPoint(int point)
    {
        if (point <= 0) return false;
        if (!IsCheatEventRunning) return false;
        StarBlazeData.WillAddPoint += point;
        StarBlazeData.SaveData();
        return true;
    }
#endif

    public bool IsAlreadyCompleteEvent(int currentProgress, int targetProgress)
    {
        StarBlazeStageConfig[] stageConfigs = StarBlazeGlobalConfig.Instance.StarBlazeConfig.StageConfigs;
        if (stageConfigs == null || stageConfigs.Length == 0) return false;
        int finalStagePoint = stageConfigs.Sum(t => t.Point);
        return  currentProgress >= finalStagePoint && targetProgress >= finalStagePoint;
    }

    public bool IsCompleted()
    {
        StarBlazeStageConfig[] stageConfigs = StarBlazeGlobalConfig.Instance.StarBlazeConfig.StageConfigs;
        if (stageConfigs == null || stageConfigs.Length == 0) return false;
        int finalStagePoint = stageConfigs.Sum(t => t.Point);
        return  CurrentPoint >= finalStagePoint;
    }
}
[System.Serializable]
public class StarBlazeState
{
    public int CurrentPoint;
    public int CurrentStagePoint;
    public int CurrentMaxStagePoint;
    public StarBlazeStageConfig CurrentStageConfig;
}