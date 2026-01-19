using System;
using System.Collections.Generic;
using System.Globalization;
using CoreData;
using Cysharp.Threading.Tasks;
using Manager;
using R3;
using SDK;
using Sirenix.OdinInspector;
using TMPro;
using TW.Reactive.CustomComponent;
using TW.UGUI.Core.Activities;
using TW.UGUI.Core.Modals;
using TW.UGUI.Core.Screens;
using TW.UGUI.Core.Views;
using TW.Utility.DesignPattern;
using UnityEngine;
using GameManager = Manager.GameManager;

namespace Manager
{
    public partial class GameManager : Singleton<GameManager>
    {
#if CHEAT_ONLY
        [ShowInInspector] public BuildType BuildType => BuildType.Cheat;
        [ShowInInspector] public bool ShowBanner => false;
#elif RELEASE_ONLY
        [ShowInInspector] public BuildType BuildType => BuildType.Release;
        [ShowInInspector] public bool ShowBanner => true;
#else
        [field: SerializeField] public BuildType BuildType { get; set; }
#endif

        [field: SerializeField] public bool WinLastGame { get; set; }
        [field: SerializeField] private ReactiveValue<int> PlayedLevel { get; set; }
        [field: SerializeField] public ReactiveValue<int> Retry { get; set; }
        [field: SerializeField] public ReactiveValue<int> WinStreak { get; set; }
        [field: SerializeField] public ReactiveValue<int> Level { get; set; }
        [field: SerializeField] public ReactiveValue<bool> InGame { get; set; }
        [field: SerializeField] public bool TrackedLevelStart { get; set; }
        [field: SerializeField] public ReactiveValue<bool> IsLoading { get; set; }

        private void Start()
        {
#if !UNITY_EDITOR
            Application.targetFrameRate = 60;
#endif
            // PlayedLevel = UserProfileDataSave.Instance.PlayedLevel;
            // Retry = UserProfileDataSave.Instance.Retry;
            // WinStreak = UserProfileDataSave.Instance.WinStreak;
            // Level = UserProfileDataSave.Instance.Level;
            // SetUserProperty();
            // InGameDataManager.Instance.InGameData.SettingDataSave.GetLanguage().ReactiveProperty
            //     .Subscribe(OnLanguageChanged).AddTo(this);

        }


//         public async UniTask DoneLoading()
//         {
//             IsLoading.Value = false;
//             if (!InGame.Value)
//             {
//                 ScreenOptions screenOptions = new ScreenOptions(nameof(ScreenHome), stack: false);
//                 await ScreenContainer.Find(ContainerKey.Screens).PushAsync(screenOptions);
//                 EventOfferManager.Instance.DelayShowOffer().Forget();
//             }
//             else
//             {
//                 PlayerResourceManager.Instance.SubResourceValue(CurrencyType.Life, 1);
//                 GamePlayManager.Instance.InitLevel();
//                 InGameAdsController.EventShowBanner?.Invoke();
//                 ScreenOptions screenOptions = new ScreenOptions(nameof(ScreenInGame), stack: false);
//                 await ScreenContainer.Find(ContainerKey.Screens).PushAsync(screenOptions);
//             }
//             await UniTask.WaitForEndOfFrame();
//         }


//         private async UniTask UNI_TrackAtStartLevel()
//         {
//             TrackedLevelStart = false;
//             while (!TrackedLevelStart)
//             {
//                 await UniTask.Delay(1000);
//                 InGameAnalyticController.EventTrackLevelStart?.Invoke();
//                 TrackCheckPointStart();
//                 TrackedLevelStart = true;
//             }
//         }

//         public async UniTask TryPlayGame(Func<UniTask> onHasLife)
//         {
//             if (IsLoading.Value) return;
//             if (PlayerResourceManager.Instance.HasLife || PlayerResourceManager.Instance.HasInfiniteLife)
//             {
//                 if (onHasLife != null)
//                 {
//                     await onHasLife.Invoke();
//                 }
//                 await PlayGameSuccess();
//                 return;
//             }

//             ViewOptions viewOption = new ViewOptions(nameof(ModalFillHeart));
//             await ModalContainer.Find(ContainerKey.Modals).PushAsync(viewOption);
//         }

//         private async UniTask PlayGameSuccess()
//         {
//             if (GameDefaultConfig.CanUseWinStreak())
//             {
//                 ViewOptions options = new ViewOptions(nameof(ModalStartGame));
//                 await ModalContainer.Find(ContainerKey.Modals).PushAsync(options);
//             }
//             else
//             {
//                 PlayGame();
//             }
//         }

//         public async UniTask TryReplayGame(Func<UniTask> onHasLife)
//         {
//             if (IsLoading.Value) return;
//             if (PlayerResourceManager.Instance.HasLife || PlayerResourceManager.Instance.HasInfiniteLife)
//             {
//                 if (onHasLife != null)
//                 {
//                     await onHasLife.Invoke();
//                 }
//                 if (HotPotManager.Instance.NeedShowProgress)
//                 {
//                     await HotPotManager.Instance.ShowLoseProgress(PlayGameSuccess);
//                 }
//                 else
//                 {
//                     await PlayGameSuccess();
//                 }
//                 return;
//             }

//             ViewOptions viewOption = new ViewOptions(nameof(ModalFillHeart));
//             await ModalContainer.Find(ContainerKey.Modals).PushAsync(viewOption);
//         }

//         [Button]
//         public void PlayGame()
//         {
//             //PlayerResourceManager.Instance.SubResourceValue(CurrencyType.Life, 1);
//             InGame.Value = true;
//             ShowScreenLoading().Forget();
//             SetUserProperty();
//         }

//         public void LoadNextLevel()
//         {
//             //PlayerResourceManager.Instance.SubResourceValue(CurrencyType.Life, 1);
//             InGame.Value = true;
//             ShowScreenLoading().Forget();
//             SetUserProperty();
//         }

//         public void GameStartInitLevel()
//         {
//             //TrackAtStartLevel();
//             UNI_TrackAtStartLevel().Forget();
//         }


//         private void TrackCheckPointStart()
//         {
//             if (PlayedLevel.Value != Level.Value)
//             {
//                 InGameAnalyticController.EventTrackCheckPointStart?.Invoke();
//             }
//             else
//             {
//                 RetryLevel();
//             }
//             SetPlayedLevel();
//         }

//         [Button]
//         public async UniTask BackToMenu()
//         {
//             InGame.Value = false;
//             await ShowScreenLoading();
//             GamePlayManager.Instance.CleanLevel();
//             UIMainMenuTab.ActiveMainMenuTab?.Invoke();
//         }
//         public async UniTask ShowMiniShop()
//         {
//             ViewOptions options = new ViewOptions(nameof(ModalShop));
//             await ModalContainer.Find(ContainerKey.Modals).PushAsync(options);
//         }
//         #region UI

//         private async UniTask ShowScreenLoading()
//         {
//             ViewOptions activityResource = new ViewOptions(nameof(ActivityLoading));
//             await ActivityContainer.Find(ContainerKey.Activities).ShowAsync(activityResource);
//             IsLoading.Value = true;
//         }


//         #endregion


        #region Language

        public TMP_FontAsset GetFont()
        {
            return LanguageGlobalConfig.Instance.GetTextFont(
                InGameDataManager.Instance.InGameData.SettingDataSave.GetLanguage().Value);
        }

        private Dictionary<TextMeshProUGUI, int> localizeTxts1 = new Dictionary<TextMeshProUGUI, int>();

        public void GetLocalizeTxt(TextMeshProUGUI txt, int textId)
        {
            var data = LanguageGlobalConfig.Instance.GetTextData(
                InGameDataManager.Instance.InGameData.SettingDataSave.GetLanguage().Value, textId);
            txt.text = $"{data.textValue}";
            TMP_FontAsset font = GetFont();
            if (font != null)
            {
                txt.font = font;
            }

            if (localizeTxts1 == null)
                localizeTxts1 = new Dictionary<TextMeshProUGUI, int>();
            if (!localizeTxts1.ContainsKey(txt))
                localizeTxts1.Add(txt, textId);
        }

        private Dictionary<TextMeshProUGUI, string> localizeTxts2 = new Dictionary<TextMeshProUGUI, string>();

        public void GetLocalizeTxt(TextMeshProUGUI txt, string root)
        {
            var data = LanguageGlobalConfig.Instance.GetTextData(
                InGameDataManager.Instance.InGameData.SettingDataSave.GetLanguage().Value, root);
            txt.text = $"{data.textValue}";
            TMP_FontAsset font = GetFont();
            if (font != null)
            {
                txt.font = font;
            }

            if (localizeTxts2 == null)
                localizeTxts2 = new Dictionary<TextMeshProUGUI, string>();
            if (!localizeTxts2.ContainsKey(txt))
                localizeTxts2.Add(txt, root);
        }

        private void OnLanguageChanged(string language)
        {
            foreach (var keyValuePair in localizeTxts1)
            {
                if (keyValuePair.Key != null)
                {
                    GetLocalizeTxt(keyValuePair.Key, keyValuePair.Value);
                }
            }

            foreach (var keyValuePair in localizeTxts2)
            {
                if (keyValuePair.Key != null)
                {
                    GetLocalizeTxt(keyValuePair.Key, keyValuePair.Value);
                }
            }
        }

        public TextData GetTextData(string root)
        {
            var data = LanguageGlobalConfig.Instance.GetTextData(
                InGameDataManager.Instance.InGameData.SettingDataSave.GetLanguage().Value, root);
            return data;
        }

        public TextData GetTextData(int textId)
        {
            var data = LanguageGlobalConfig.Instance.GetTextData(
                InGameDataManager.Instance.InGameData.SettingDataSave.GetLanguage().Value, textId);
            return data;
        }

        #endregion
    }

    public enum BuildType
    {
        Release,
        Test,
        Cheat,
        Admin,
    }
}