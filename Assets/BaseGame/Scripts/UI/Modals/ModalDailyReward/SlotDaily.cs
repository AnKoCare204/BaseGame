using Core;
using CoreData;
using CoreGame.GameGlobalConfig;
using Cysharp.Threading.Tasks;
using LitMotion;
using R3;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SlotDaily : MonoBehaviour
{
    private int slotData = -1;
    [SerializeField] private GameObject objLock;
    [SerializeField] private GameObject objClaimed;
    [SerializeField] private GameObject lineProgressNoDone;
    [SerializeField] private Image lineProgressDone;
    [SerializeField] private Transform dotDone;
    [SerializeField] private Transform dotCurrent;
    [SerializeField] private Transform dotNoDone;
    [SerializeField] private UIItemInfo uiItemInfo;
    [SerializeField] private Button btnClaimFree;
    [SerializeField] private Button btnClaimAds;
    [SerializeField] private GameResource rewardData;
    [SerializeField] private bool ads;
    [SerializeField] private int milestone;

    private bool isEnd = false;

    private void Start()
    {
        var dailyRewardCount = DailyRewardManager.Instance.dailyRewardClaimCount;
        dailyRewardCount.Skip(1).Subscribe(OnChangeDailyRewardCount).AddTo(this);
        DailyDataSave.Instance.DayClaimed.Subscribe(OnChangeDailyRewardCount).AddTo(this);
        btnClaimFree.onClick.AddListener(OnClickClaimFree);
        btnClaimAds.onClick.AddListener(OnClickClaimAds);
    }

    private void OnClickClaimAds()
    {
        if (ads)
        {
            AnalyticsParameter[] parameters = new AnalyticsParameter[] {
                    new AnalyticsParameter("level", GameManager.Instance.Level.Value.ToString()),
                    new AnalyticsParameter("button_name", $"daily_reward_{milestone}"),
                    new AnalyticsParameter("placement", "daily_reward"),
                    new AnalyticsParameter("reward_name", rewardData.Type == ResourceType.Money ? "coins" : rewardData.Type.ToString()),
                    new AnalyticsParameter("reward_type", rewardData.Type.ToString()),
                    new AnalyticsParameter("value", rewardData.Value),
                };
            EventAdsManager.ShowRewardAds(() => ClaimReward(ResourcePos.ads.ToString()), null, null, true, "ads_reward_complete", parameters);
        }
    }

    private void OnClickClaimFree()
    {
        ClaimReward(ResourcePos.free.ToString());
    }

    private void ClaimReward(string claimType)
    {
        var result = DailyRewardManager.Instance.ClaimReward(slotData);
        if (!result) return;
        ClaimGameResourceGroup claimGameResourceGroup = new()
        {
            ClaimType = claimType,
            GameResourceList = new List<GameResource>()
            {
                rewardData
            },
            Placement = "DailyReward",
        };
        RewardManager.Instance.AddResource(claimGameResourceGroup);
        RewardManager.Instance.ClaimResource(claimGameResourceGroup).Forget();
    }

    private void OnChangeDailyRewardCount(int dailyRewardCount)
    {
        InitData(slotData, isEnd, dailyRewardCount);
    }

    public void InitData(int i, bool isEnd, int dailyRewardCount)
    {
        Debug.Log("SlotData: " + slotData +"--------" + "DailyRewardCount: " + dailyRewardCount);
        bool isInit = dailyRewardCount == -1 || dailyRewardCount - 1 != slotData;
        
        slotData = i;
        this.isEnd = isEnd;

        if (isInit)
        {
            InitImmediate();
        }
        else
        {
            InitEffect().Forget();
        }
    }

    private async UniTask InitEffect()
    {
        var isFirstReward = slotData == 0;
        var isCurrentReward = DailyRewardManager.Instance.IsCurrentReward(slotData);
        var isCanClaim = DailyRewardManager.Instance.IsCanClaimThisReward(slotData);
        var isClaimed = DailyRewardManager.Instance.IsClaimed(slotData);

        objLock.SetActive(!isCanClaim && !isClaimed);

        dotDone.gameObject.SetActive(isClaimed);
        dotNoDone.gameObject.SetActive(!isClaimed && !isCurrentReward);
        dotCurrent.gameObject.SetActive(!isClaimed && isCurrentReward);

        objClaimed.SetActive(isClaimed);

        btnClaimFree.gameObject.SetActive(isCanClaim && isFirstReward);

        btnClaimAds.gameObject.SetActive(isCanClaim && !isFirstReward);

        if (!isEnd)
        {
            await PlayProgressAsync();
            
        }

        lineProgressNoDone.gameObject.SetActive(!isClaimed && !isEnd);
        
        rewardData = DailyGlobalConfig.Instance.GetDailyResourceConfig(DailyDataSave.Instance.DayClaimed).GetGameResource(slotData);
        if (rewardData == null) return;
        // Init tự chọn icon, tự format số lượng và tự bật/tắt nút info của card pack - slot không
        // cần biết gì về ResourceType nữa.
        if (uiItemInfo != null) uiItemInfo.Init(rewardData);
    }

    private void InitImmediate()
    {
        var isFirstReward = slotData == 0;
        var isCurrentReward = DailyRewardManager.Instance.IsCurrentReward(slotData);
        var isCanClaim = DailyRewardManager.Instance.IsCanClaimThisReward(slotData);
        var isClaimed = DailyRewardManager.Instance.IsClaimed(slotData);

        objLock.SetActive(!isCanClaim && !isClaimed);

        dotDone.gameObject.SetActive(isClaimed);
        dotNoDone.gameObject.SetActive(!isClaimed && !isCurrentReward);
        dotCurrent.gameObject.SetActive(!isClaimed && isCurrentReward);

        objClaimed.SetActive(isClaimed);

        btnClaimFree.gameObject.SetActive(isCanClaim && isFirstReward);

        btnClaimAds.gameObject.SetActive(isCanClaim && !isFirstReward);
        
        lineProgressDone.fillAmount = (isClaimed && !isEnd) ? 1 : 0;
        
        lineProgressNoDone.gameObject.SetActive(!isClaimed && !isEnd);
        
        rewardData = DailyGlobalConfig.Instance.GetDailyResourceConfig(DailyDataSave.Instance.DayClaimed).GetGameResource(slotData);
        if (rewardData == null) return;
        // Init tự chọn icon, tự format số lượng và tự bật/tắt nút info của card pack - slot không
        // cần biết gì về ResourceType nữa.
        if (uiItemInfo != null) uiItemInfo.Init(rewardData);
    }
    
    private MotionHandle lineDoneMotionHandle;
    private async UniTask PlayProgressAsync()
    {
        lineDoneMotionHandle.TryCancel();

        lineProgressDone.fillAmount = 0;

        lineDoneMotionHandle = LMotion.Create(0f, 1f, 0.3f)
            .WithEase(Ease.OutBack)
            .Bind(value =>
            {
                lineProgressDone.fillAmount = value;
            }).AddTo(this);
        await lineDoneMotionHandle;
    }
}
