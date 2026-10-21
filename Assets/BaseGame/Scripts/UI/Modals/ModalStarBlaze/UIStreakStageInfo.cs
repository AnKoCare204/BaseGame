using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIStreakStageInfo : MonoBehaviour
{
    [field: SerializeField] private TextMeshProUGUI[] TextStage {get; set;}
    [field: SerializeField] private GameObject LockGroup {get; set;}
    [field: SerializeField] private GameObject UnlockGroup {get; set;}
    [field: SerializeField] private GameObject CurrentGroup {get; set;}
    // Vùng đặt LightSelect của ModalStarBlazeInfo khi stage này là current.
    [field: SerializeField] public RectTransform LightAnchor {get; private set;}
    [field: SerializeField] private UIItemGroupInfo UIItemGroupInfo {get; set;}
    [field: SerializeField] private Slider Slider {get; set;}
    [field: SerializeField] private GameObject GoProcess {get; set;}
    private StarBlazeStageConfig StarBlazeStageConfig { get; set; }
    public bool IsUnlock { get; private set; }
    public bool IsCurrent { get; private set; }
    public RectTransform CurrentGroupRect => (RectTransform)CurrentGroup.transform;
    public void Init(StarBlazeStageConfig starBlazeStageConfig)
    {
        StarBlazeStageConfig = starBlazeStageConfig;
        
        // bool isHaveCollectionReward = CardCollectionManager.Instance.IsActiveForReward();
        bool isHaveCollectionReward = false;
        
        if (isHaveCollectionReward)
        {
            UIItemGroupInfo.Init(StarBlazeStageConfig.Reward);
        }
        else
        {
            UIItemGroupInfo.Init(StarBlazeStageConfig.DefaultReward);
        }
        
        for (int i = 0; i < TextStage.Length; i++)
        {
            TextStage[i].text = $"{StarBlazeStageConfig.Index + 1}";
        }
        IsUnlock= StarBlazeManager.Instance.IsStageUnlock(StarBlazeStageConfig.Index);
        // Mục tiêu hiện tại: stage đầu tiên chưa đạt đủ điểm (chưa nhận thưởng).
        IsCurrent = !IsUnlock && StarBlazeStageConfig.Index == StarBlazeManager.Instance.GetCurrentPassedStep();
        LockGroup.SetActive(!IsUnlock && !IsCurrent);
        UnlockGroup.SetActive(IsUnlock);
        CurrentGroup.SetActive(IsCurrent);
        SetSliderToNextValue();
    }
    private void SetSliderToNextValue()
    {
       int nextIndex = StarBlazeStageConfig.Index + 1;
       if (nextIndex > StarBlazeGlobalConfig.Instance.StarBlazeConfig.StageConfigs[^1].Index)
       {
           GoProcess.SetActive(false);
           return;
       }
       else
       {
           GoProcess.SetActive(true);
           // Fill tới stage đã mở khóa hoặc stage mục tiêu hiện tại.
           bool isNextReached = nextIndex <= StarBlazeManager.Instance.GetCurrentPassedStep();
           Slider.value = isNextReached ? 1 : 0;
       }
    }
}