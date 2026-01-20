using System;
using System.Threading;
using Core.UI;
using Cysharp.Threading.Tasks;
using LitMotion;
using R3;
using SDK;
using Sirenix.OdinInspector;
using TMPro;
using TW.ACacheEverything;
using TW.UGUI.Core.Activities;
using TW.UGUI.Core.Screens;
using TW.UGUI.Core.Views;
using TW.Utility.DesignPattern;
using UnityEngine;
using UnityEngine.UI;

namespace Manager
{
    public partial class LoadingManager : Singleton<LoadingManager>
    {
        [field: SerializeField, ReadOnly]
        private SerializableReactiveProperty<float> LoadProgress { get; set; } = new(0f);

        [field: SerializeField] private GameObject CanvasFirstLoading { get; set; }
        [field: SerializeField] private Slider LoadProgressSlider { get; set; }
        [field: SerializeField] private TextMeshProUGUI LoadProgressText { get; set; }
        [field: SerializeField] private TextMeshProUGUI LoadDescriptionText { get; set; }

        private bool IsLoadingComplete { get; set; }
        private bool IsBackGroundLoadComplete { get; set; }
        private MotionHandle LoadProgressMotion { get; set; }

        private void Start()
        {
            LoadProgressAsync().Forget();
        }

        private async UniTask LoadProgressAsync()
        {
            CanvasFirstLoading.SetActive(true);
            IsLoadingComplete = false;
            LoadDescriptionText.text = "Loading Assets...";
            UpdateProgress(0.2f);

            await UniTask.Delay(1000);

            LoadDescriptionText.text = "Loading UI...";
            UpdateProgress(0.7f);
            
            // Tìm ActivityContainer với null check
            if (ActivityContainer.TryFind(ContainerKey.Activities, out var activityContainer))
            {
                ViewOptions activityBlock = new ViewOptions(nameof(ActivityBlock));
                await activityContainer.ShowAsync(activityBlock);
            }
            else
            {
                Debug.LogError($"ActivityContainer '{ContainerKey.Activities}' chưa được khởi tạo. Vui lòng đảm bảo container được tạo trước khi sử dụng.");
            }
            
            await DoneFirstLoading();
            UpdateProgress(1);
            await WaitLoadingProgressCompleteAsync(this.GetCancellationTokenOnDestroy());
            IsLoadingComplete = true;
            CanvasFirstLoading.SetActive(false);
        }

        private void UpdateProgress(float targetProgress)
        {
            LoadProgressMotion.TryCancel();
            float currentProgressClamped = LoadProgress.CurrentValue;
            LoadProgressMotion = LMotion.Create(currentProgressClamped, targetProgress,
                    targetProgress - currentProgressClamped)
                .Bind(OnUpdateProgressCache)
                .AddTo(this);
        }

        [ACacheMethod]
        private void OnUpdateProgress(float progress)
        {
            LoadProgress.Value = progress;
            LoadProgressSlider.value = progress;
            LoadProgressText.text = $"{(int)(progress * 100)}%";
        }

        private async UniTask WaitLoadingProgressCompleteAsync(CancellationToken ct)
        {
            if (LoadProgress.CurrentValue >= 0.99f) return;
            await UniTask.WaitUntil(IsLoadingProgressComplete, cancellationToken: ct);
        }

        private bool IsLoadingProgressComplete()
        {
            return LoadProgress.CurrentValue >= 0.99f;
        }

        public async UniTask WaitForLoadingCompleteAsync(CancellationToken ct)
        {
            if (IsLoadingComplete)
            {
                return;
            }

            await UniTask.WaitUntil(IsLoadingCompletePredicateCache, cancellationToken: ct);
        }

        [ACacheMethod]
        private bool IsLoadingCompletePredicate()
        {
            return IsLoadingComplete;
        }

        private async UniTask DoneFirstLoading()
        {
            ScreenOptions screenOptions = new ScreenOptions(nameof(ScreenHome), stack: false);
            await ScreenContainer.Find(ContainerKey.Screens).PushAsync(screenOptions);
            SoundManager.Instance.PlayMusic(SoundType.BgMainMenu);
        }
    }
}