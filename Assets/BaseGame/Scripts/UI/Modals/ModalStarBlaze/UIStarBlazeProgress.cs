using System;
using AssetKits.ParticleImage;
using Cysharp.Threading.Tasks;
using LitMotion;
using Sirenix.OdinInspector;
using TMPro;
using TW.Utility.Extension;
using UnityEngine;
using UnityEngine.UI;

public class UIStarBlazeProgress : MonoBehaviour
{
    public static Func<int,int,UniTask> PlayProgressAsync;
    [field: SerializeField] private Slider ProgressBar { get; set; }
    [field: SerializeField] private UIItemGroupInfo ItemGroupInfo { get; set; }
    [field: SerializeField] private TextMeshProUGUI TextProgress { get; set; }
    [field: SerializeField] private AnimationCurve ProgressCurve { get; set; }
    [field: SerializeField] private AnimationCurve ResourceScaleCurve { get; set; }
    [field: SerializeField] private ParticleImage ResourceParticle { get; set; }
    [field: SerializeField] private TextMeshProUGUI TextLevelUnlock { get; set; }
    [field: SerializeField] private Button MainButton {get; set;}
    [field: SerializeField] private Button LockButton {get; set;}
    private StarBlazeState FromState { get; set; }
    private StarBlazeState ToState { get; set; }
    private StarBlazeState MotionState { get; set; }
    private MotionHandle MotionHandle { get; set; }

    private ItemGroup FromResourceGroup { get; set; }
    private ItemGroup ToResourceGroup { get; set; }
    private ItemGroup LastGroup { get; set; }
    private ItemGroup CurrentGroup { get; set; }
    private bool IsChangingResourceGroup { get; set; }
    private MotionHandle ResourceHandle { get; set; }

    private void Start()
    {
        MainButton.SetOnClickDestination(UIManager.Instance.ShowModalStreakInfo);
        LockButton.SetOnClickDestination(ShowToast);
        PlayProgressAsync = PlayProgress;
        StarBlazeManager.OnInitializeComplete += OnInitializeComplete;
        StarBlazeManager.OnResetData += OnResetData;
        TextLevelUnlock.SetText($"Level {StarBlazeRemoteConfig.Instance.LevelUnlock}");
        OnInitializeComplete();
    }

    private void OnDestroy()
    {
        PlayProgressAsync = null;
        StarBlazeManager.OnInitializeComplete -= OnInitializeComplete;
        StarBlazeManager.OnResetData -= OnResetData;
    }

    private void ShowToast()
    {
        UIManager.Instance.ShowInfo($"Reach Level {StarBlazeRemoteConfig.Instance.LevelUnlock } to unlock Treasure Dive!");
    }
    private void OnInitializeComplete()
    {
        gameObject.SetActive(false);
        if (!StarBlazeManager.Instance.IsInitialize) return;
        TextLevelUnlock.SetText($"Level {StarBlazeRemoteConfig.Instance.LevelUnlock}");
        if (!StarBlazeManager.Instance.IsStartEvent) return;
        if (!StarBlazeManager.Instance.IsEventUnlock)
        {
            LockButton.SetActive(true);
            MainButton.SetActive(false);
            gameObject.SetActive(true);

            return;
        }
        MainButton.SetActive(true);
        LockButton.SetActive(false);
        gameObject.SetActive(true);
        SetProgress(StarBlazeManager.Instance.CurrentPoint);

    }

    private void OnResetData()
    {
        SetProgress(StarBlazeManager.Instance.CurrentPoint);
    }
    
    [Button]
    public void SetProgress(int progress)
    {
        StarBlazeState state = StarBlazeManager.Instance.GetStarBlazeState(progress);
        ProgressBar.value = (float)state.CurrentStagePoint / state.CurrentMaxStagePoint;
        
        // bool isHaveCollectionReward = CardCollectionManager.Instance.IsActiveForReward();
        bool isHaveCollectionReward = false;
        
        if (isHaveCollectionReward)
        {
            ItemGroupInfo.Init(state.CurrentStageConfig.Reward);
        }
        else
        {
            ItemGroupInfo.Init(state.CurrentStageConfig.DefaultReward);
        }
        
        TextProgress.text = $"{state.CurrentStagePoint}/{state.CurrentMaxStagePoint}";
    }

    [Button]
    public async UniTask PlayProgress(int fromProgress, int toProgress)
    {
       
        SetProgress(fromProgress);
        FromState = StarBlazeManager.Instance.GetStarBlazeState(fromProgress);
        ToState = StarBlazeManager.Instance.GetStarBlazeState(toProgress);
        MotionState = FromState;
        
        // bool isHaveCollectionReward = CardCollectionManager.Instance.IsActiveForReward();
        bool isHaveCollectionReward = false;
        
        if (isHaveCollectionReward)
        {
            CurrentGroup = MotionState.CurrentStageConfig.Reward;
        }
        else
        {
            CurrentGroup = MotionState.CurrentStageConfig.DefaultReward;
        }
        
        
        MotionHandle.TryCancel();
        MotionHandle = LMotion.Create(0f, 1f, 0.5f)
            .WithEase(Ease.Linear)
            .Bind(OnProgressUpdate)
            .AddTo(this);
        await UniTask.Delay(500, cancellationToken: this.GetCancellationTokenOnDestroy());
        SetProgress(toProgress);
    }

    private void OnProgressUpdate(float value)
    {
        float progress = ProgressCurve.Evaluate(value);
        float floatPoint = Mathf.Lerp(FromState.CurrentPoint, ToState.CurrentPoint, progress);
        int point = Mathf.FloorToInt(floatPoint + 0.1f);

        MotionState = StarBlazeManager.Instance.GetStarBlazeState(point);
        float offset = floatPoint - MotionState.CurrentPoint;

        ProgressBar.value = (offset + MotionState.CurrentStagePoint) / MotionState.CurrentMaxStagePoint;
        
        LastGroup = CurrentGroup;
        
        // bool isHaveCollectionReward = CardCollectionManager.Instance.IsActiveForReward();
        bool isHaveCollectionReward = false;
        
        if (isHaveCollectionReward)
        {
            CurrentGroup = MotionState.CurrentStageConfig.Reward;
        }
        else
        {
            CurrentGroup = MotionState.CurrentStageConfig.DefaultReward;
        }

        if (LastGroup != CurrentGroup)
        {
            ChangeResourceGroup(LastGroup, CurrentGroup);
            AudioManager.Instance.PlaySoundFx(StarBlazeSfx.StreakIncrease);
        }
        TextProgress.text = $"{MotionState.CurrentStagePoint}/{MotionState.CurrentMaxStagePoint}";
    }

    private void ChangeResourceGroup(ItemGroup from, ItemGroup to)
    {
        FromResourceGroup = from;
        ToResourceGroup = to;
        ItemGroupInfo.Init(from);
        IsChangingResourceGroup = false;
        
        ResourceHandle.TryCancel();
        ResourceHandle = LMotion.Create(0f, 1f, 0.5f)
            .WithEase(Ease.Linear)
            .Bind(OnChangeResourceGroupUpdate)
            .AddTo(this);
    }

    private void OnChangeResourceGroupUpdate(float value)
    {
        ItemGroupInfo.transform.localScale = Vector3.one * ResourceScaleCurve.Evaluate(value);
        if (value < 0.5f || IsChangingResourceGroup) return;
        IsChangingResourceGroup = true;
        ItemGroupInfo.Init(CurrentGroup);
        ResourceParticle.SetActive(true);
        ResourceParticle.Play();
    }
}