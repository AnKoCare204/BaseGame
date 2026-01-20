using System;
using System.Collections.Generic;
using AssetKits.ParticleImage;
using BaseGame.Scripts.Common;
using Core.UI;
using Cysharp.Threading.Tasks;
using Manager;
using R3;
using SDK;
using Sirenix.OdinInspector;
using TMPro;
using TW.Reactive.CustomComponent;
using TW.UGUI.Core.Modals;
using TW.UGUI.Core.Views;
using TW.UGUI.MVPPattern;
using UnityEngine;
using UnityEngine.Events;
using Button = UnityEngine.UI.Button;

[Serializable]
public class ModalLevelSelectContext
{
    public static class Events
    {
        public static Action ShowLevelUpdateInfo { get; set; }
    }

    [HideLabel]
    [Serializable]
    public class UIModel : IAModel
    {
        [field: Title(nameof(UIModel))]
        [field: SerializeField] public ReactiveValue<int> CurrentLevel { get; private set; }


        public UniTask Initialize(Memory<object> args)
        {
            // LevelConfigs = LevelGlobalConfig.Instance.LevelConfigs;
            CurrentLevel = GameManager.Instance.Level;
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
        // [field: SerializeField] public Button BtnPlay { get; private set; }
        // [field: SerializeField] public Button BtnSetting { get; private set; }
        // [field: SerializeField] public Button BtnProfile { get; private set; }
        // [field: SerializeField] public Transform BtnCapy { get; private set; }
        // [field: SerializeField] public Transform BtnTeamTreasure{ get; private set; }
        // [field: SerializeField] public Transform BattlePassTransform{ get; private set; }
        // [field: SerializeField] public Transform HalloweenTransform{ get; private set; }
        // [field: SerializeField] public Transform JigsawFoodieTransform{ get; private set; }
        // [field: SerializeField] public GameObject LevelHuntPassRequire { get; private set; }
        // [field: SerializeField] public TextMeshProUGUI TxtLevel { get; private set; }
        // [field: SerializeField] public TextMeshProUGUI TxtEventPackRemain { get; private set; }
        // [field: SerializeField] public MiniPool<MenuBtnAlert> MenuBtnAlerts { get; private set; }
        // [field: SerializeField] public MenuBtnAlert MenuBtnAlertPrefab { get; private set; }
        // [field: SerializeField] public Transform AlertParrent { get; private set; }
        // [field: SerializeField] public GameObject btnLevelHard { get; private set; }
        // [field: SerializeField] public GameObject btnLevelSuperHard { get; private set; }

        public UniTask Initialize(Memory<object> args)
        {
            InitLevelSprite();
            // MenuBtnAlerts.OnInit(MenuBtnAlertPrefab, 3, AlertParrent);
            return UniTask.CompletedTask;
        }

        private void InitLevelSprite()
        {
            // int level = GameManager.Instance.Level.Value;
            // LevelConfig levelConfig = LevelGlobalConfig.Instance.GetLevelConfig(GameManager.Instance.Level.Value);
            // btnLevelHard.SetActive(levelConfig is { levelType: LevelType.Hard });
            // btnLevelSuperHard.SetActive(levelConfig is { levelType: LevelType.SuperHard });
        }

        public RectTransform rootRect;

        // public void CheckNoAdsPack(float amount)
        // {
        //     BtnNoAds.gameObject.SetActive(amount == 0);
        // }

        public void ChangeLanguage(string language)
        {
                // TxtLevel.text = $"{GameManager.Instance.GetTextData(13).textValue} {GameManager.Instance.Level.Value}";
                // BtnPlay.interactable = true;
                // TxtLevel.font = GameManager.Instance.GetFont();
        }
        

        // public async UniTask<int> ShowClaimedFeaturePoint()
        // {
        //     int countAlert = 1;
        //     if (!GameManager.Instance.WinLastGame) return countAlert;
        //     if (GameFeatureManager.Instance.IsFeatureActive(FeatureName.Capybara))
        //     {
        //         for (int i = 0; i < 3; i++)
        //         {
        //             SpawnAlert(FeatureName.Capybara, BtnCapy
        //                 , 0.15f * (i + countAlert) + 0.25f);
        //         }
        //         countAlert++;
        //     }
        //     if (GameFeatureManager.Instance.IsFeatureActive(FeatureName.TeamTreasure))
        //     {
        //         for (int i = 0; i < 3; i++)
        //         {
        //             SpawnAlert(FeatureName.TeamTreasure, BtnTeamTreasure
        //                 , 0.15f * (i + countAlert) + 0.25f);
        //         }
        //         countAlert++;
        //     }
        //     if (GameFeatureManager.Instance.IsFeatureActive(FeatureName.BattlePass))
        //     {
        //         for (int i = 0; i < 3; i++)
        //         {
        //             SpawnAlert(FeatureName.BattlePass, BattlePassTransform, 0.15f * (i + countAlert) + 0.25f);
        //         }
        //         countAlert++;
        //     }
        //     if (GameFeatureManager.Instance.IsFeatureActive(FeatureName.Halloween))
        //     {
        //         for (int i = 0; i < 3; i++)
        //         {
        //             SpawnAlert(FeatureName.Halloween, HalloweenTransform, 0.15f * (i + countAlert) + 0.25f);
        //         }
        //         countAlert++;
        //     }
        //     if (GameFeatureManager.Instance.IsFeatureActive(FeatureName.JigsawFoodie) && JigsawFoodieManager.Instance.IsBetweenEvent(DateTimeManager.UtcNow))
        //     {
        //         for (int i = 0; i < 3; i++)
        //         {
        //             SpawnAlert(FeatureName.JigsawFoodie, JigsawFoodieTransform, 0.15f * (i + countAlert) + 0.25f);
        //         }
        //         countAlert++;
        //     }
        //     await UniTask.Delay((int)((countAlert * 0.75f + 0.25f) * 1000), cancellationToken: MainView.GetCancellationTokenOnDestroy());
        //     UICurrencyCoin.ClaimFakeCoin?.Invoke();
            
        //     return countAlert;
        // }

        // private void SpawnAlert(FeatureName featureName, Transform target, float delay)
        // {
        //     MenuBtnAlert alert = MenuBtnAlerts.Spawn();
        //     alert.transform.position = AlertParrent.position;
        //     alert.Setup(GameFeatureManager.Instance.GetFeatureIcon(featureName),target
        //         , delay, () => {MenuBtnAlerts.Despawn(alert);});
        // }
    }

    [HideLabel]
    [Serializable]
    public class UIPresenter : IAPresenter, IModalLifecycleEventSimple
    {
        [field: SerializeField] public UIModel Model { get; private set; } = new();
        [field: SerializeField] public UIView View { get; set; } = new();
        public bool isAddListener;

        public async UniTask Initialize(Memory<object> args)
        {
            await Model.Initialize(args);
            await View.Initialize(args);

            // Model.CurrentLevel.ReactiveProperty.Skip(1).Subscribe(View.ChangeLevel).AddTo(View.MainView);
            // Init Level List
            //InitData(Model.LevelConfigs);

            // View.BtnPlay.interactable = true; // !GameManager.Instance.IsMaxLevel();

            if (isAddListener) return;
            isAddListener = true;
            // View.BtnPlay.SetOnClickDestination(Play);
            // View.BtnSetting.SetOnClickDestination(OpenModalSetting);
            // View.BtnProfile.SetOnClickDestination(OpenModalProfile);

            InGameDataManager.Instance.InGameData.SettingDataSave.GetLanguage()
                .ReactiveProperty.Subscribe(View.ChangeLanguage).AddTo(View.MainView);
        }


        public UniTask Cleanup(Memory<object> args)
        {
            Events.ShowLevelUpdateInfo = null;
            return UniTask.CompletedTask;
        }

        // private async UniTask Play()
        // {
        //     await GameManager.Instance.TryPlayGame(null);
        // }

        // private async UniTask OpenModalSetting()
        // {
        //     ViewOptions options = new ViewOptions(nameof(ModalSetting));
        //     await ModalContainer.Find(ContainerKey.Modals).PushAsync(options);
        // }

        // private async UniTask OpenModalProfile()
        // {
        //     ViewOptions options = new ViewOptions(nameof(ModalProfile));
        //     await ModalContainer.Find(ContainerKey.Modals).PushAsync(options);
        // }
    }
}