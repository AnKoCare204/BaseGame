using System;
using Core.GamePlay;
using Core.Manager;
using Core.UI.Activities;
using CoreData;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using TW.UGUI.MVPPattern;
using UnityEngine;
using R3;
using Sirenix.OdinInspector;
using Spine.Unity;
using TMPro;
using TW.UGUI.Core.Activities;
using TW.UGUI.Core.Modals;
using TW.UGUI.Core.Views;
using UnityEngine.UI;

namespace Core.UI.Modals
{
    public class ModalConfirmLose : Modal
    {
        [field: SerializeField] public ModalConfirmLoseContext.UIPresenter UIPresenter { get; private set; }

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
    public class ModalConfirmLoseContext
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

            [field: SerializeField] public TextMeshProUGUI TextLevel { get; private set; }
            [field: SerializeField] public TextMeshProUGUI TextDefaultReward { get; private set; }

            [field: SerializeField] public Button BtnClose { get; private set; }
            [field: SerializeField] public Button ButtonTryAgain { get; private set; }

            public UniTask Initialize(Memory<object> args)
            {
                EventAdsManager.ShowBannerAds?.Invoke(false);
                TextLevel.SetTextFormat(MyCacheUI.textFormat, "Level " + GameManager.Instance.Level.Value);
                LevelDifficulty levelDifficulty = LevelManager.Instance.LevelConfig.LevelType;
                TextDefaultReward.text = $"{EndGameRewardRemoteConfigData.Instance.GetRewardCoin(levelDifficulty)}";
                return UniTask.CompletedTask;
            }
            

            public void PLayAnimSpine()
            {
            }
        }

        [HideLabel]
        [Serializable]
        public class UIPresenter : IAPresenter, IModalLifecycleEventSimple
        {
            [field: SerializeField] public UIModel Model { get; private set; } = new();
            [field: SerializeField] public UIView View { get; set; } = new();

            public async UniTask Initialize(Memory<object> args)
            {
                await Model.Initialize(args);
                await View.Initialize(args);

                View.BtnClose.SetOnClickDestination(OnButtonCloseClick);
                View.ButtonTryAgain.SetOnClickDestination(OnButtonTryAgainClick);
                
                int playTime = LevelManager.Instance.GetPlayTimeToNow();
                int retryCount = LevelManager.Instance.CurrentRetryCount;
                int reviveCount = LevelManager.Instance.CurrentReviveCount;
                string looseType = LevelManager.Instance.CurrentLevel.CurLoseReason.ToString();
                IngameFirebaseAnalystic.Instance.TrackLevelFail(playTime, retryCount, looseType, reviveCount);
                View.PLayAnimSpine();
            }

            private async UniTask OnButtonCloseClick()
            {
                if (GameManager.Instance.CanShowInterLose())
                {
                    AnalyticsParameter[] parameters = new AnalyticsParameter[] {
                        new AnalyticsParameter("level", IngameFirebaseAnalystic.TrackedLevelId),
                        IngameFirebaseAnalystic.GameModeParam,
                        new AnalyticsParameter("placement", ResourcePos.lose_level.ToString()),
                    };
                    EventAdsManager.ShowInterstitialAds?.Invoke(null, null, OnShowInterstitialAdsSuccess, null, false, false, "ads_inter_show", parameters);
                }
                await GameManager.Instance.TryBackToMainMenu();
            }
            
            private void OnShowInterstitialAdsSuccess()
            {
                NoAdsPopupManager.MarkInterstitialWatched();
            }

            private async UniTask OnButtonTryAgainClick()
            {
                if (PlayerResourceManager.Instance.HasLife ||
                    PlayerResourceManager.Instance.HasInfiniteLife)
                {
                    // ADsManager.ShowInterstitial(ResourcePos.replay_level);
                    // ADSAction.OnAdsInterEvent?.Invoke(ResourcePos.replay_level);
                    AnalyticsParameter[] parameters = new AnalyticsParameter[] {
                        new AnalyticsParameter("level", GameManager.Instance.Level.Value.ToString()),
                        new AnalyticsParameter("placement", ResourcePos.replay_level.ToString()),
                    };
                    // if (GameManager.Instance.CanShowAdsInter())
                    // {
                    //     EventAdsManager.ShowInterstitialAds?.Invoke(null, null, null, false, false, "ads_inter_show", parameters);
                    // }
                    await OnRetry();
                }
                else
                {
                    ViewOptions viewOptions = new ViewOptions(nameof(ModalFillHeart));
                    await ModalContainer.Find(ContainerKey.Modals).PushAsync(viewOptions);
                }
            }

            private async UniTask OnRetry()
            {
                await GameManager.Instance.TryRetryCurrentLevel();
            }
        }
    }
}