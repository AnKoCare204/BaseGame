using System;
using System.Collections.Generic;
using Core.GamePlay;
using Core.Manager;
using Core.UI.Activities;
using CoreData;
using Cysharp.Threading.Tasks;
using DataModule;
using LitMotion;
using Manager;
using TW.UGUI.MVPPattern;
using UnityEngine;
using R3;
using Sirenix.OdinInspector;
using Spine.Unity;
using TMPro;
using TW.UGUI.Core.Modals;
using TW.UGUI.Core.Views;
using UnityEngine.UI;

namespace Core.UI.Modals
{
    public class ModalWin : Modal
    {
        [field: SerializeField] public ModalWinContext.UIPresenter UIPresenter { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            AddLifecycleEvent(UIPresenter, 1);
        }

        public override async UniTask Initialize(Memory<object> args)
        {
            await base.Initialize(args);
        }
    }


    [Serializable]
    public class ModalWinContext
    {
        public static class Events
        {
            public static Action SampleEvent { get; set; }
        }

        [HideLabel]
        [Serializable]
        public class UIModel : IAModel
        {
            [field: Title(nameof(UIModel))]
            [field: SerializeField]
            public SerializableReactiveProperty<int> SampleValue { get; private set; }

            public UniTask Initialize(Memory<object> args)
            {
                return UniTask.CompletedTask;
            }
        }

        [HideLabel]
        [Serializable]
        public class UIView : IAView
        {
            [field: Title(nameof(UIView))]
            [field: SerializeField]
            public CanvasGroup MainView { get; private set; }
            // [field: SerializeField] public Button ButtonClaimWithAds {get; private set;}
            // [field: SerializeField] public Button ButtonClaim {get; private set;}
            [field: SerializeField] public Button ButtonContinue {get; private set;}
            [field: SerializeField] public List<StarGroup> StarGroups {get; private set;}
            [field: SerializeField] public TextMeshProUGUI DefaultRewardTxt {get; private set;}
            // [field: SerializeField] public TextMeshProUGUI AdsRewardTxt {get; private set;}
            [field: SerializeField] public TextMeshProUGUI LevelTxt {get; private set;}
            [field: SerializeField] public Transform GlowTF {get; private set;}
            
            [field: SerializeField] public Transform CoinGroup {get; private set;}
            [field: SerializeField] public CoinEffectUI CoinEffectUI {get; private set;}
            [field: SerializeField] public UIMoneyResource UIMoneyResource {get; private set;}
            [field: SerializeField] public Transform CoinClaimTF {get; private set;}
            [field: SerializeField] public Transform CoinClaimWithAdsTF {get; private set;}

            private const string Idle = "idle";
            private const string Start = "start";

            public MotionHandle RotateHandle;
            public UniTask Initialize(Memory<object> args)
            {
                EventAdsManager.ShowBannerAds?.Invoke(false);
                LevelDifficulty levelDifficulty = LevelManager.Instance.LevelConfig.LevelType;
                DefaultRewardTxt.text = $"{EndGameRewardRemoteConfigData.Instance.GetRewardCoin(levelDifficulty)}";
                // AdsRewardTxt.text = $"{EndGameRewardRemoteConfigData.Instance.GetAdsRewardCoin(levelDifficulty)}";
                LevelTxt.text = $"Level {GameManager.Instance.Level.Value - 1}";

                RunAnim().Forget();
               
                return UniTask.CompletedTask;
            }
            
            [Button]
            public async UniTask RunAnim()
            {
                // ButtonClaimWithAds.transform.localScale = Vector3.zero;
                // ButtonClaim.transform.localScale = Vector3.zero;
                // CoinGroup.localScale = Vector3.zero;
                //
                // textSpine.AnimationState.SetAnimation(0, Start, false);
                // textSpine.AnimationState.AddAnimation(0, Idle, true, 0);
                //
                // charSpine.AnimationState.SetAnimation(0, Start, false);
                // charSpine.AnimationState.AddAnimation(0, Idle, true, 0);
                await UniTask.Delay(350);
                // bool isShowX2 = EndGameRewardRemoteConfigData.Instance.CanShowX2Reward(GameManager.Instance.Level.Value - 1);
                // if (isShowX2)
                // {
                //     ButtonClaimWithAds.gameObject.SetActive(true);
                //     await LMotion.Create(0f, 1f, 0.2f).WithEase(Ease.OutBack).Bind(x => ButtonClaimWithAds.transform.localScale = x * Vector3.one).AddTo(MainView);
                //     await UniTask.Delay(100, cancellationToken: MainView.GetCancellationTokenOnDestroy());
                // }
                // else
                // {
                //     ButtonClaimWithAds.gameObject.SetActive(false);
                // }
                // await LMotion.Create(0f, 1f, 0.2f).WithEase(Ease.OutBack).Bind(x => ButtonClaim.transform.localScale = x * Vector3.one).AddTo(MainView);
                if (GameManager.Instance.CanShowRate())
                {
                    RateData.Instance.IsShowRate = true;
                    RateData.Instance.SaveData();
                    ViewOptions options = new ViewOptions(nameof(ModalRate));
                    ModalContainer.Find(ContainerKey.Modals).PushAsync(options).Forget();
                }
            }

            private async UniTask PlayAnimCoinOrFeature()
            {
                await PlayAnimCoin();
            }
            
            private async UniTask PlayAnimCoin()
            {
                await LMotion.Create(0f, 1f, 0.3f)
                    .WithEase(Ease.OutBack)
                    .Bind(t =>
                    {
                        CoinGroup.localScale = Vector3.one * t;
                    }).AddTo(MainView);
            }
            public void PlayRotateLoop()
            {
                RotateHandle = LMotion.Create(0, 360, 2f)
                    .WithEase(Ease.Linear)
                    .WithLoops(-1)
                    .Bind(t =>
                    {
                        GlowTF.localRotation = Quaternion.Euler(0, 0, t);
                    }).AddTo(MainView); 
            }
        }

        [HideLabel]
        [Serializable]
        public class UIPresenter : IAPresenter, IModalLifecycleEventSimple
        {
            [field: SerializeField] public UIModel Model { get; private set; } = new();
            [field: SerializeField] public UIView View { get; set; } = new();
            private bool isRewardClaimed;

            public async UniTask Initialize(Memory<object> args)
            {

                await Model.Initialize(args);
                await View.Initialize(args);
                // View.ButtonClaim.SetOnClickDestination(OnClickButtonClaim);
                // View.ButtonClaimWithAds.SetOnClickDestination(OnClickButtonClaimWithAds);
                View.ButtonContinue.SetOnClickDestination(OnClickButtonContinue);
                AudioManager.Instance.PlaySoundFx(AudioKey.SfxWin);
            }
            public void DidPushEnter(Memory<object> args)
            {
                
            }

            private void OnClickButtonClaimWithAds()
            {
                View.MainView.interactable = false;
                if (GameManager.Instance.BuildType == BuildType.Cheat)
                {
                    OnClaimWithAdsSuccess();
                    return;
                }
                AnalyticsParameter[] parameters = new AnalyticsParameter[] {
                    new AnalyticsParameter("level", GameManager.Instance.Level.Value.ToString()),
                    new AnalyticsParameter("button_name", "win_level_x2_reward"),
                    new AnalyticsParameter("reward_name", "coin"),
                    new AnalyticsParameter("reward_type", "coin"),
                    new AnalyticsParameter("value", GetAdsRewardCoin()),
                    new AnalyticsParameter("placement", ResourcePos.ingame.ToString()),
                };
                EventAdsManager.ShowRewardAds?.Invoke(OnClaimWithAdsSuccess, OnClaimWithAdsFail, OnClaimWithAdsFail, true, "ads_reward_complete", parameters);
            }

            private void OnClaimWithAdsSuccess()
            {
                ClaimReward(
                    GetAdsRewardCoin(),
                    ResourcePos.win_level.ToString(),
                    ResourcePos.ads.ToString());
            }
            private void OnClaimWithAdsFail()
            {
                if (View.MainView != null)
                {
                    View.MainView.interactable = true;
                }
            }
            
            private void OnClickButtonClaim()
            {
                View.MainView.interactable = false;
                ClaimReward(
                    GetRewardCoin(),
                    ResourcePos.win_level.ToString(),
                    ResourcePos.free.ToString());
            }
            
            private void OnClickButtonContinue()
            {
                View.MainView.interactable = false;
                ClaimReward(
                    GetRewardCoin(),
                    ResourcePos.win_level.ToString(),
                    ResourcePos.free.ToString());
            }

            private int GetRewardCoin()
            {
                return EndGameRewardRemoteConfigData.Instance.GetRewardCoin(LevelManager.Instance.LevelConfig.LevelType);
            }

            private int GetAdsRewardCoin()
            {
                return EndGameRewardRemoteConfigData.Instance.GetAdsRewardCoin(LevelManager.Instance.LevelConfig.LevelType);
            }

            private async UniTask ClaimReward(int amount, string placement, string claimType)
            {
                if (isRewardClaimed) return;
                isRewardClaimed = true;
                if (GameManager.Instance.Level > DefaultGlobalConfig.Instance.DefaultBackToMenuLevel)
                {
                    ClaimGameResourceGroup claimGameResourceGroup = new();
                    claimGameResourceGroup.GameResourceList.Add(new GameResource(ResourceType.Money, amount));
                    claimGameResourceGroup.ClaimGameResourceType = ClaimGameResourceType.GamePlay;
                    claimGameResourceGroup.ClaimType = "gameplay";
                    claimGameResourceGroup.Placement = "win_level";
                    ResourceData.Instance.AddClaimGameResourceGroup(claimGameResourceGroup);
                }
                else
                {
                    PlayerResourceManager.Instance.AddResourceValue(ResourceType.Money, amount, placement, claimType);
                }
                await DoneGetReward();
            }

            private async UniTask DoneGetReward()
            {
                await ShowInterstitialAdsComplete();
                if (GameManager.Instance.Level.Value <= DefaultGlobalConfig.Instance.DefaultBackToMenuLevel)
                {
                    await LoadNextLevel();
                }
                else
                {
                    await BackToMainMenu();
                }
            }
            
            private async UniTask ShowInterstitialAdsComplete()
            {
                bool isShowingInterstitial = false;
                if (GameManager.Instance.Level.Value > InterLevelRemoteConfigData.Instance.LevelShowInter)
                {
                    AnalyticsParameter[] parameters = new AnalyticsParameter[]
                    {
                        new AnalyticsParameter("level", IngameFirebaseAnalystic.TrackedLevelId),
                        IngameFirebaseAnalystic.GameModeParam,
                        new AnalyticsParameter("placement", ResourcePos.win_level.ToString()),
                    };
                    isShowingInterstitial = true;
                    EventAdsManager.ShowInterstitialAds?.Invoke(null, OnShowInterstitialFail, OnCloseInterstitial, OnSkipInterstitial, false, false, "ads_inter_show",
                        parameters);
                }

                if (isShowingInterstitial)
                {
                    ActivityNotificationOverlayContext.Events.ShowAdsBreak?.Invoke();
                    await UniTask.WaitUntil(IsShowAdsComplete, cancellationToken: View.MainView.GetCancellationTokenOnDestroy());
                    ActivityNotificationOverlayContext.Events.HideAdsBreak?.Invoke();
                }

                
                return;

                void OnShowInterstitialFail()
                {
                    isShowingInterstitial = false;
                }
                void OnCloseInterstitial()
                {
                    NoAdsPopupManager.MarkInterstitialWatched();
                    isShowingInterstitial = false;
                }
                void OnSkipInterstitial()
                {
                    isShowingInterstitial = false;
                }
                bool IsShowAdsComplete()
                {
                    return !isShowingInterstitial;
                }
            }
            private void OnShowInterstitialAdsSuccess()
            {

            }
            
            public async UniTask BackToMainMenu()
            {
                await GameManager.Instance.TryBackToMainMenu();
            }

            public async UniTask LoadNextLevel()
            {
                await GameManager.Instance.TryLoadNextLevel();
            }
        }
    }
}