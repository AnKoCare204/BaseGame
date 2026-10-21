using System;
using Cysharp.Threading.Tasks;
using Game.Helper;
using LitMotion;
using MoreMountains.Feedbacks;
using Sirenix.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UITreetopProgress : MonoBehaviour
{
    [field: SerializeField] public TextMeshProUGUI TxtLastLevel {get; private set;}
    [field: SerializeField] public TextMeshProUGUI TxtCurLevel {get; private set;}
    [field: SerializeField] public TextMeshProUGUI TxtNextLevel {get; private set;}
    [field: SerializeField] public Slider SliderProcess {get; private set;}
    [field: SerializeField] public GameObject GOMainObject {get; private set;}
    [field: SerializeField] public Transform TfBotSlider {get; private set;}
    [field: SerializeField] public Transform TfTopSlider {get; private set;}
    [field: SerializeField] public GameObject RootObject {get; private set;}
    [field: SerializeField] public GameObject GoProcessLastLevel {get; private set;}
    [field: SerializeField] public GameObject GoNextLevel {get; private set;}
    private TreetopGlobalConfig GlobalConfig => TreetopGlobalConfig.Instance;
    [field: SerializeField] public TreetopStreakConfig Config {get; private set;}
    [field: SerializeField] public TreetopStreakConfig NextConfig {get; private set;}
    [field: SerializeField] public TreetopStreakConfig PrevConfig {get; private set;}
    [field: SerializeField] public GameObject ImgCurPassed {get; private set;}
    [field: SerializeField] public GameObject ImgNextLevelPassed {get; private set;}
    [field: SerializeField] public GameObject Partical { get; private set; }
    [field: SerializeField] public GameObject ImgCurChest { get; private set; }
    [field: SerializeField] public GameObject ImgNextChest { get; private set; }
    [field: SerializeField] public TextMeshProUGUI TxtTimeEndEvent {get; private set;}
    private Vector3 StartPos;
    private Vector3 EndPos;
    private float StartValue;
    private float EndValue;
    [field: SerializeField] public MMF_Player AnimEnter { get; private set; }
    [field: SerializeField] public MMF_Player AnimCloud1 { get; private set; }
    [field: SerializeField] public MMF_Player AnimCloud2 { get; private set; }

    private void Start() => Refresh();

    public void Refresh()
    {
        RootObject.SetActive(false);
        if(!TreetopManager.Instance.IsInitialize) return;
        if(!TreetopManager.Instance.IsEventUnlock) return;
        if(TreetopData.Instance.EventEndTimeUtc.IsNullOrWhitespace()) return;
        if(TreetopData.Instance.Streak == 0) return;
        if(TreetopManager.Instance.LastShowUILevel == GlobalConfig.Streaks[^1].Level) return;
        Init();
        PlayAnim().Forget();
    }

    private void Init()
    {
        int curLevel = TreetopData.Instance.Streak;
        int lastLevel = curLevel - 1;

        Config = GlobalConfig.GetNextConfigByLevel(lastLevel);
        TxtCurLevel.SetText(Config.Level.ToString());

        PrevConfig = GlobalConfig.GetLastConfigByLevel(Config.Level);
        TxtLastLevel.SetText(PrevConfig.Level.ToString());

        NextConfig =GlobalConfig.GetNextConfigByLevel(Config.Level);
        if (NextConfig != null)
        {
            TxtNextLevel.SetText(NextConfig.Level.ToString());
            ImgNextLevelPassed.gameObject.SetActive(TreetopData.Instance.MaxStreak >= NextConfig.Level);
            ImgNextChest.gameObject.SetActive(TreetopData.Instance.MaxStreak < NextConfig.Level);
        }
        

        GoProcessLastLevel.gameObject.SetActive(PrevConfig.Level != 0);
        GoNextLevel.gameObject.SetActive(Config.Level != GlobalConfig.Streaks[^1].Level);

        StartValue = (float)(lastLevel - PrevConfig.Level) / (Config.Level - PrevConfig.Level);
        EndValue = (float)(curLevel - PrevConfig.Level)/ (Config.Level - PrevConfig.Level);
        SliderProcess.value = StartValue;
        
        StartPos = Vector3.Lerp(TfBotSlider.position, TfTopSlider.position, StartValue);
        EndPos = Vector3.Lerp(TfBotSlider.position, TfTopSlider.position, EndValue);
        GOMainObject.transform.position = StartPos;
        
        ImgCurPassed.gameObject.SetActive(TreetopData.Instance.MaxStreak > TreetopData.Instance.Streak);
        if (!TreetopManager.Instance.NeedShowUIWin)
        {
            ImgCurChest.gameObject.SetActive(TreetopData.Instance.MaxStreak < Config.Level);
        }
        
    }

    private void Update()
    {
        if(!TreetopManager.Instance.IsInitialize) return;
        if(!TreetopManager.Instance.IsEventUnlock) return;
        if(TreetopData.Instance.EventEndTimeUtc.IsNullOrWhitespace()) return;
        TimeSpan time = TreetopData.Instance.EventEndTimeUtc.StringToDateTime() - DateTimeManager.UtcNow;
        TxtTimeEndEvent.SetText(time.TimeSpanToStringUI());
    }

    private async UniTask PlayAnim()
    {
        AnimEnter.PlayFeedbacks();
        RootObject.SetActive(true);
        await UniTask.Yield(cancellationToken: this.GetCancellationTokenOnDestroy());
        await UniTask.Delay(600, cancellationToken: this.GetCancellationTokenOnDestroy());
        AnimCloud1.PlayFeedbacks();
        AnimCloud2.PlayFeedbacks();
        LMotion.Create(0f, 1f, 0.5f)
            .WithOnComplete(() =>
            {
                bool isMaxProcess = EndValue == 1;
                if (!ImgCurPassed.activeSelf)
                {
                    ImgCurPassed.gameObject.SetActive(isMaxProcess);
                }
                if (isMaxProcess)
                {
                    Partical.SetActive(true);
                    AudioManager.Instance.PlaySoundFx(AudioKey.SfxComplete);
                }
            })
            .Bind(process =>
            {
                SliderProcess.value = Mathf.Lerp(StartValue, EndValue, process);
                GOMainObject.transform.position = Vector3.Lerp(StartPos, EndPos, process);
            })
            .AddTo(this);
    }
}
