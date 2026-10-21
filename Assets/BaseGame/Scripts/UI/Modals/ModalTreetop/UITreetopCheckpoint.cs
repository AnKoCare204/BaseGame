using System.Collections.Generic;
using Core.UI.Activities;
using Cysharp.Threading.Tasks;
using LitMotion;
using Spine.Unity;
using TMPro;
using TW.UGUI.Core.Activities;
using TW.UGUI.Core.Views;
using TW.Utility.CustomComponent;
using UnityEngine;
using UnityEngine.UI;
using CoreData;

public class UITreetopCheckpoint : ALinkedPoolMonoBehaviour<UITreetopCheckpoint>
{
    [field: SerializeField] public TextMeshProUGUI TxtStartLevel { get; private set; }
    [field: SerializeField] public TextMeshProUGUI TxtCurLevel { get; private set; }
    [field: SerializeField] public GameObject GOLastLevel { get; private set; }
    [field: SerializeField] public GameObject GOCurLevel { get; private set; }
    [field: SerializeField] public GameObject ImgPrevLevelPassed { get; private set; }
    [field: SerializeField] public GameObject ImgCurLevelPassed { get; private set; }
    [field: SerializeField] public GameObject ImgPrevLevelNotPassed  { get; private set; }
    [field: SerializeField] public GameObject ImgCurLevelNotPassed { get; private set; }
    [field: SerializeField] public GameObject ImgCheckPoint { get; private set; }
    [field: SerializeField] public GameObject ImgNormal {get; private set; }
    [field: SerializeField] public GameObject ImgNormalAlter {get; private set; }
    [field: SerializeField] public Slider Slider { get; private set; }
    [field: SerializeField] public GameObject GORoof {get; private set; }
    [field: SerializeField] public TreetopStreakConfig Config { get; private set; }
    [field: SerializeField] public TreetopStreakConfig PrevConfig { get; private set; }
    [field: SerializeField] public Transform TfCurLevelPos { get; private set; }
    [field: SerializeField] public Transform TfLastLevelPos { get; private set; }
    [field: SerializeField] public float StartProcess {get; private set; }
    [field: SerializeField] public float EndProcess {get; private set; }
    [field: SerializeField] public Button BtnShowInfo { get; private set; }
    [field: SerializeField] public Transform TfShowInfo { get; private set; }
    [field: SerializeField] public SkeletonGraphic Skeleton { get; private set; }
    private int skeletonId;
    private bool isLastConfig;
    [field : SerializeField] public GameObject PrevPartical {get; private set;}
    [field: SerializeField] public GameObject CurPartical {get; private set;}

    private void Start()
    {
        BtnShowInfo.SetOnClickDestination(OnClickBtnShowInfo);
    }

    public UITreetopCheckpoint Init(int currentLevel)
    {
        Config = TreetopGlobalConfig.Instance.GetConfigByLevel(currentLevel);
        PrevConfig = TreetopGlobalConfig.Instance.GetLastConfigByLevel(currentLevel);
        
        TxtStartLevel.SetText(PrevConfig.Level.ToString());
        TxtCurLevel.SetText(Config.Level.ToString());
        
        ImgCheckPoint.gameObject.SetActive(Config.IsCheckPoint);
        
        isLastConfig = Config == TreetopGlobalConfig.Instance.Streaks[^1];
        //GOLastLevel.SetActive(isLastReward);
        GOCurLevel.SetActive(isLastConfig);
        GORoof.SetActive(isLastConfig);

        bool isPassedLastReward = TreetopData.Instance.MaxStreak >= PrevConfig.Level;
        ImgPrevLevelPassed.SetActive(isPassedLastReward);
        //ImgPrevLevelNotPassed.SetActive(!isPassedLastReward);
        
        bool isPassedCurrent = TreetopData.Instance.MaxStreak >= Config.Level;
        ImgCurLevelPassed.SetActive(isPassedCurrent);
        ImgCurLevelNotPassed.SetActive(!isPassedCurrent);
        SetUpAnimation();
        return this;
    }

    public UITreetopCheckpoint SetImgNormalAlter(bool isEnable)
    {
        ImgNormal.gameObject.SetActive(!isEnable);
        if (ImgNormalAlter) ImgNormalAlter.SetActive(isEnable);
        return this;
    }

    private void SetUpAnimation()
    {
        int index = System.Array.IndexOf(TreetopGlobalConfig.Instance.Streaks, Config);
        skeletonId = index%3 + 1;
        if (TreetopData.Instance.MaxStreak >= Config.Level)
        {
            Skeleton?.AnimationState?.SetAnimation(0, $"{skeletonId}_open_idle", true);
        }
        else
        {
            Skeleton?.AnimationState?.SetAnimation(0, $"{skeletonId}_close_idle", true);
            
        }
    }

    public void SetProcess(int streak)
    {
        //int streak = TreetopData.Instance.Streak;
        if (streak >= Config.Level)
        {
            Slider.value = 1;
        }
        else
        {
            if (streak <= PrevConfig.Level)
            {
                Slider.value = 0;
            }
            else
            {
                Slider.value = (float)(streak - PrevConfig.Level) / (Config.Level - PrevConfig.Level);
            }
        }
    }

    public bool IsCurrentStreakInsideRange(int streak,out float process)
    {
        //int streak = TreetopData.Instance.Streak;
        process = (float)(streak - PrevConfig.Level) / (Config.Level - PrevConfig.Level);
        return streak <= Config.Level && streak > PrevConfig.Level;
    }

    public float GetLastStreakProcess(int lastStreak)
    {
        StartProcess = (float)(lastStreak - PrevConfig.Level) / (Config.Level - PrevConfig.Level);
        return StartProcess;
    }

    public void CachedWinProcesses()
    {
        EndProcess = Slider.value;
        Slider.value = StartProcess;
    }

    public void CachedFailedProcesses()
    {
        Slider.value = StartProcess;
        EndProcess = 0f;
    }

    public void SetActiveCurrentPassed()
    {
        if (isLastConfig)
        {
            ImgCurLevelPassed.gameObject.SetActive(false);
            CurPartical.gameObject.SetActive(false);
        }
        ImgPrevLevelPassed.gameObject.SetActive(TreetopManager.Instance.LastShowUILevel >= PrevConfig.Level);
        PrevPartical.gameObject.SetActive(false);
    }

    public void ActiveCurrentPassed()
    {
        if (isLastConfig)
        {
            ImgCurLevelPassed.gameObject.SetActive(TreetopData.Instance.Streak >= Config.Level);
            CurPartical.gameObject.SetActive(TreetopData.Instance.Streak >= Config.Level);
        }
        ImgPrevLevelPassed.gameObject.SetActive(TreetopData.Instance.Streak >= PrevConfig.Level);
        PrevPartical.gameObject.SetActive(TreetopData.Instance.Streak == PrevConfig.Level);
    }

    public void CloseAnim()
    {
        Skeleton?.AnimationState?.SetAnimation(0, $"{skeletonId}_close_idle", true);
    }

    public async UniTask OpenAnim()
    {
        Skeleton?.AnimationState?.ClearTrack(0);
        Skeleton?.AnimationState?.SetAnimation(0, $"{skeletonId}_open", false);
        Skeleton?.AnimationState?.AddAnimation(0, $"{skeletonId}_open_idle", true,1.5f);
        await UniTask.WaitForSeconds(1.5f, cancellationToken: this.GetCancellationTokenOnDestroy());
    }

    public void PlayAnimProcess(float duration, float delay = 0f)
    {
        LMotion.Create(StartProcess, EndProcess, duration)
            .WithDelay(delay)
            .Bind(process =>
            {
                Slider.value = process;
            }).AddTo(this);
    }

    private async UniTask OnClickBtnShowInfo()
    {
        if(ActivityContainer.Find(ContainerKey.Activities).Activities.ContainsKey(nameof(ActivityShowRewardTreetop)))
        {
            ActivityShowRewardTreetopContext.Events.CloseActivity?.Invoke();
            return;
        }
        ViewOptions options = new ViewOptions(nameof(ActivityShowRewardTreetop));
        if (TreetopData.Instance.MaxStreak >= Config.Level)
        {
            await ActivityContainer.Find(ContainerKey.Activities).ShowAsync(options,new List<GameResource>(),TfShowInfo.position);
        }
        else
        {
            await ActivityContainer.Find(ContainerKey.Activities).ShowAsync(options, Config.DefaultRewards, TfShowInfo.position);
            
        }
    }
    
}
