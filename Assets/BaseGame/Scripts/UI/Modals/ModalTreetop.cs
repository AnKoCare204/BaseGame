using System;
using System.Collections.Generic;
using Core.UI.Activities;
using Cysharp.Threading.Tasks;
using Game.Helper;
using DataModule;
using LitMotion;
using TW.UGUI.MVPPattern;
using UnityEngine;
using R3;
using R3.Triggers;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using TMPro;
using TW.UGUI.Core.Activities;
using TW.UGUI.Core.Modals;
using TW.UGUI.Core.Views;
using UnityEngine.UI;

namespace Core.UI.Modals
{
    
    public class ModalTreetop : Modal
    {
        public enum ModalState
        {
            ShowCurrent,
            ShowWin,
            ShowLose,
            ShowWinReward
        }
        [field: SerializeField] public ModalTreetopContext.UIPresenter UIPresenter { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            AddLifecycleEvent(UIPresenter, 1);
            AddLifecycleEvent(new MainMenuPopupLifecycle(), 2);
            AddLifecycleEvent(new PopupMoneyResourceEffectLifecycle(), 3);
        }

        public override async UniTask Initialize(Memory<object> args)
        {
            await base.Initialize(args);
        }
    }


    [Serializable]
    public class ModalTreetopContext
    {
        public static class Events
        {
            public static Action SampleEvent { get; set; }
            public static Func<UniTask> WaitAnim {get;set;}
        }

        [HideLabel]
        [Serializable]
        public class UIModel : IAModel
        {
            [field: Title(nameof(UIModel))]
            [field: SerializeField]
            public SerializableReactiveProperty<int> SampleValue { get; private set; }
            [field: SerializeField]
            public ModalTreetop.ModalState State { get; private set; }
            [field: SerializeField]
            public int Level { get; private set; }
            public UniTask Initialize(Memory<object> args)
            {
                State = args.Span[0] as ModalTreetop.ModalState? ?? ModalTreetop.ModalState.ShowCurrent;
                Level = (int)args.Span[1];
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
            [field: SerializeField]
            public Transform Container { get; private set; }
            [field: SerializeField]
            public Button BtnClose { get; private set; }
            [field: SerializeField]
            public Button BtnInfo { get; private set; }
            [field: SerializeField]
            public GameObject GOMainObject { get; private set; }
            [field: SerializeField]
            public Transform TfStartPoint { get; private set; }
            [field: SerializeField]
            public ScrollRect ScrollRect { get; private set; }
            [field: SerializeField]
            public GameObject GOBlockScroll { get; private set; }
            [field: SerializeField]
            public TextMeshProUGUI TxtTimeEndEvent { get; private set; }
            [field: SerializeField]
            public Button BtnCloseBg { get; private set; }

            public UniTask Initialize(Memory<object> args)
            {
                return UniTask.CompletedTask;
            }
        }

        [HideLabel]
        [Serializable]
        public class UIPresenter : IAPresenter, IModalLifecycleEventSimple
        {
            [field: SerializeField] public UIModel Model { get; private set; } = new();
            [field: SerializeField] public UIView View { get; set; } = new();
            [field: SerializeField] public List<UITreetopCheckpoint> ListTreetopCheckpoint { get; private set; } = new();
            private TreetopGlobalConfig GlobalConfig => TreetopGlobalConfig.Instance;
            [field: SerializeField] public UITreetopCheckpoint curUITreetopCheckpoint { get; private set; }
            [field: SerializeField] public UITreetopCheckpoint lastUITreetopCheckpoint { get; private set; }
            private bool IsAnimDone;
            private Vector3 startPosition;
            private Vector3 tarGetPostion;
            [field: SerializeField] public Transform TfLimitTop { get; private set; }
            [field: SerializeField] public Transform TfLimitBot { get; private set; }
            [field: SerializeField] public Transform TempContainer { get; private set; }
            [field: SerializeField] private UITreetopCheckpoint InitCheckpoint { get;  set; }

            public async UniTask Initialize(Memory<object> args)
            {
                await Model.Initialize(args);
                await View.Initialize(args);
                ListTreetopCheckpoint = new List<UITreetopCheckpoint>() {  };
                View.BtnClose.SetOnClickDestination(OnClose);
                View.BtnInfo.SetOnClickDestination(ShowInfo);
                View.BtnCloseBg.SetOnClickDestination(OnClose);
                IsAnimDone = false;
                View.GOBlockScroll.SetActive(true);
                View.BtnCloseBg.gameObject.SetActive(false);
                ShowCurrent();
                ShowWin();
                ShowLose();
                ShowWinReward();
                Events.WaitAnim = WaitDoneAnim;
                View.MainView.UpdateAsObservable()
                    .Subscribe(OnUpdate)
                    .AddTo(View.MainView);
            }

            private void OnUpdate(Unit _)
            {
                if (!TreetopManager.Instance.IsInitialize) return;
                if (!TreetopManager.Instance.IsEventUnlock) return;
                if(TreetopData.Instance.EventEndTimeUtc.IsNullOrWhitespace()) return;
                TimeSpan time = TreetopData.Instance.EventEndTimeUtc.StringToDateTime() - DateTimeManager.UtcNow;
                View.TxtTimeEndEvent.SetText(time.TimeSpanToStringUI());
            }

            private async UniTask WaitDoneAnim()
            {
                await UniTask.WaitUntil(() => IsAnimDone, cancellationToken: View.MainView.GetCancellationTokenOnDestroy());
            }

            public UniTask Cleanup(Memory<object> args)
            {
                Events.WaitAnim = null;
                return UniTask.CompletedTask;
            }
            public void DidPushEnter(Memory<object> args)
            {
                PlayAnimWin().Forget();
                PlayAnimLose().Forget();
                PlayAnimWinReward().Forget();
            }

            private void ShowCurrent()
            {
                if(Model.State != ModalTreetop.ModalState.ShowCurrent) return;
                View.GOBlockScroll.SetActive(false);
                View.BtnClose.gameObject.SetActive(true);
                View.BtnInfo.gameObject.SetActive(true);
                UITreetopCheckpoint prefab = PrefabGlobalConfig.Instance.UITreetopCheckpointPrefab;
                //UITreetopCheckpoint nextUiTreetopCheckpoint = null;
                int count = GlobalConfig.Streaks.Length;
                
                for (int i = 1; i < count; i++)
                {
                    TreetopStreakConfig config = GlobalConfig.Streaks[i];
                    UITreetopCheckpoint uiTreetopCheckpoint = i == 1 ? InitCheckpoint : prefab.Get(Vector3.zero, Quaternion.identity, View.Container);
                    uiTreetopCheckpoint.Init(config.Level).SetImgNormalAlter((i & 1) == 0).SetProcess(TreetopData.Instance.Streak);
                    ListTreetopCheckpoint.Add(uiTreetopCheckpoint);
                }

                if (TreetopData.Instance.Streak == 0)
                {
                    View.GOMainObject.transform.SetParent(ListTreetopCheckpoint[0].transform, false);
                    View.GOMainObject.transform.localPosition = Vector3.Lerp(
                        ListTreetopCheckpoint[0].TfLastLevelPos.localPosition,
                        ListTreetopCheckpoint[0].TfCurLevelPos.localPosition,
                        0
                    );
                }
                else
                {
                    for (int i = 0; i < ListTreetopCheckpoint.Count; i++)
                    {
                        UITreetopCheckpoint uiTreetopCheckpoint = ListTreetopCheckpoint[i];
                        if (uiTreetopCheckpoint.IsCurrentStreakInsideRange(TreetopData.Instance.Streak,out float process))
                        {
                            View.GOMainObject.transform.SetParent(uiTreetopCheckpoint.transform, false);
                            View.GOMainObject.transform.localPosition = Vector3.Lerp(
                                uiTreetopCheckpoint.TfLastLevelPos.localPosition,
                                uiTreetopCheckpoint.TfCurLevelPos.localPosition,
                                process
                            );
                            //nextUiTreetopCheckpoint = (i + 1 < ListTreetopCheckpoint.Count) ? ListTreetopCheckpoint[i + 1] : null;
                            break;
                        }
                    }
                }
                Canvas.ForceUpdateCanvases();
                
                RectTransform content = (RectTransform)View.Container;
                float contentHeight = content.rect.height;
                float viewportHeight = View.ScrollRect.viewport.rect.height;

// Convert GOMainObject world pos to content local space
                Vector2 localPos = content.InverseTransformPoint(View.GOMainObject.transform.position);

// Scroll so GOMainObject is at center of viewport
                float targetBottom = localPos.y - viewportHeight / 2f;
                View.ScrollRect.verticalNormalizedPosition = Mathf.Clamp01(targetBottom / (contentHeight - viewportHeight));
            }

            private void ShowWin()
            {
                if(Model.State != ModalTreetop.ModalState.ShowWin) return;
                curUITreetopCheckpoint = null;
                View.BtnClose.gameObject.SetActive(false);
                View.BtnInfo.gameObject.SetActive(false);
                UITreetopCheckpoint prefab = PrefabGlobalConfig.Instance.UITreetopCheckpointPrefab;
                //UITreetopCheckpoint nextUiTreetopCheckpoint = null;
                int count = GlobalConfig.Streaks.Length;
                
                for (int i = 1; i < count; i++)
                {
                    TreetopStreakConfig config = GlobalConfig.Streaks[i];
                    UITreetopCheckpoint uiTreetopCheckpoint = i == 1 ? InitCheckpoint : prefab.Get(Vector3.zero, Quaternion.identity, View.Container);
                    uiTreetopCheckpoint.Init(config.Level).SetImgNormalAlter((i & 1) == 0).SetProcess(TreetopData.Instance.Streak);
                    ListTreetopCheckpoint.Add(uiTreetopCheckpoint);
                }
                
                for (int i = 0; i < ListTreetopCheckpoint.Count; i++)
                {
                    UITreetopCheckpoint uiTreetopCheckpoint = ListTreetopCheckpoint[i];
                    if (uiTreetopCheckpoint.IsCurrentStreakInsideRange(TreetopData.Instance.Streak,out float process))
                    {
                        curUITreetopCheckpoint = uiTreetopCheckpoint;
                        View.GOMainObject.transform.SetParent(curUITreetopCheckpoint.transform, false);
                        float lastProcess = curUITreetopCheckpoint.GetLastStreakProcess(Model.Level);
                        startPosition = Vector3.Lerp(
                            curUITreetopCheckpoint.TfLastLevelPos.localPosition,
                            curUITreetopCheckpoint.TfCurLevelPos.localPosition,
                            lastProcess
                        );
                        View.GOMainObject.transform.localPosition = startPosition;
                        tarGetPostion = Vector3.Lerp(
                            curUITreetopCheckpoint.TfLastLevelPos.localPosition,
                            curUITreetopCheckpoint.TfCurLevelPos.localPosition,
                            process
                        );
                        curUITreetopCheckpoint.CachedWinProcesses();
                        //nextUiTreetopCheckpoint = (i + 1 < ListTreetopCheckpoint.Count) ? ListTreetopCheckpoint[i + 1] : null;
                        break;
                    }
                }
                Canvas.ForceUpdateCanvases();
                
                RectTransform content = (RectTransform)View.Container;
                float contentHeight = content.rect.height;
                float viewportHeight = View.ScrollRect.viewport.rect.height;

// Convert GOMainObject world pos to content local space
                Vector2 localPos = content.InverseTransformPoint(View.GOMainObject.transform.position);

// Scroll so GOMainObject is at center of viewport
                float targetBottom = localPos.y - viewportHeight / 2f;
                View.ScrollRect.verticalNormalizedPosition = Mathf.Clamp01(targetBottom / (contentHeight - viewportHeight));
            }

            private async UniTask PlayAnimWin()
            {
                if(Model.State != ModalTreetop.ModalState.ShowWin) return;
                await UniTask.WaitForSeconds(0.5f, cancellationToken: View.MainView.GetCancellationTokenOnDestroy());
                curUITreetopCheckpoint.PlayAnimProcess(0.5f);
                LMotion.Create(0f, 1f, 0.5f)
                    .Bind(process =>
                    {
                        View.GOMainObject.transform.localPosition = Vector3.Lerp(startPosition, tarGetPostion, process);
                    }).AddTo(View.MainView);
                //await UniTask.WaitForSeconds(1f, cancellationToken: View.MainView.GetCancellationTokenOnDestroy());
                await UniTask.WaitForSeconds(1f, cancellationToken: View.MainView.GetCancellationTokenOnDestroy());
                if (!TreetopData.Instance.IsShowTut)
                {
                    TreetopData.Instance.IsShowTut = true;
                    TreetopData.Instance.SaveData();
                    await ShowInfo();
                }
                View.BtnClose.gameObject.SetActive(true);
                View.BtnInfo.gameObject.SetActive(true);
                View.GOBlockScroll.SetActive(false);
                IsAnimDone = true;
            }

            private void ShowLose()
            {
                if(Model.State != ModalTreetop.ModalState.ShowLose) return;
                curUITreetopCheckpoint = null;
                lastUITreetopCheckpoint = null;
                View.BtnClose.gameObject.SetActive(false);
                View.BtnInfo.gameObject.SetActive(false);
                UITreetopCheckpoint prefab = PrefabGlobalConfig.Instance.UITreetopCheckpointPrefab;
                //UITreetopCheckpoint nextUiTreetopCheckpoint = null;
                int count = GlobalConfig.Streaks.Length;
                
                for (int i = 1; i < count; i++)
                {
                    TreetopStreakConfig config = GlobalConfig.Streaks[i];
                    UITreetopCheckpoint uiTreetopCheckpoint = i == 1 ? InitCheckpoint : prefab.Get(Vector3.zero, Quaternion.identity, View.Container);
                    uiTreetopCheckpoint.Init(config.Level).SetImgNormalAlter((i & 1) == 0).SetProcess(Model.Level);
                    ListTreetopCheckpoint.Add(uiTreetopCheckpoint);
                }
                
                for (int i = ListTreetopCheckpoint.Count-1; i >= 0; i--)
                {
                    UITreetopCheckpoint uiTreetopCheckpoint = ListTreetopCheckpoint[i];
                    if (uiTreetopCheckpoint.IsCurrentStreakInsideRange(Model.Level,out float process1))
                    {
                        Debug.Log(process1);
                        lastUITreetopCheckpoint = uiTreetopCheckpoint;
                        View.GOMainObject.transform.SetParent(lastUITreetopCheckpoint.transform, false);
                        startPosition = Vector3.Lerp(
                            lastUITreetopCheckpoint.TfLastLevelPos.localPosition,
                            lastUITreetopCheckpoint.TfCurLevelPos.localPosition,
                            process1
                        );
                        View.GOMainObject.transform.localPosition = startPosition;
                        tarGetPostion = lastUITreetopCheckpoint.TfLastLevelPos.localPosition;
                        lastUITreetopCheckpoint.GetLastStreakProcess(Model.Level);
                        lastUITreetopCheckpoint.CachedFailedProcesses();
                        // The natural fall target is this segment's own floor (PrevConfig, e.g. 11 for
                        // a segment spanning 11->17) - only step back one more segment if THAT floor
                        // isn't itself a checkpoint. This used to test Config.IsCheckPoint (this
                        // segment's ceiling, e.g. 17) instead, which is true for nearly every level in
                        // the real data, so it walked one segment too far even when PrevConfig was
                        // already a valid checkpoint - e.g. streak 12 (segment 11->17, both checkpoints)
                        // landed the animation on 8 instead of stopping at 11, even though
                        // TreetopManager/GetLastCheckpointByLevel already correctly saved Data.Streak = 11.
                        if (lastUITreetopCheckpoint.PrevConfig.IsCheckPoint)
                        {
                            break;
                        }
                        if(lastUITreetopCheckpoint == ListTreetopCheckpoint[0]) return;
                        int indexLast = ListTreetopCheckpoint.IndexOf(lastUITreetopCheckpoint);
                        int currentIndex = indexLast-1;
                        curUITreetopCheckpoint = ListTreetopCheckpoint[currentIndex];
                        // Vector3 worldPos = View.GOMainObject.transform.position;
                        // View.GOMainObject.transform.SetParent(curUITreetopCheckpoint.transform, false);
                        // startPosition = curUITreetopCheckpoint.transform.InverseTransformPoint(worldPos);
                        // View.GOMainObject.transform.localPosition = startPosition;
                        tarGetPostion = curUITreetopCheckpoint.TfLastLevelPos.localPosition;
                        curUITreetopCheckpoint.GetLastStreakProcess(curUITreetopCheckpoint.Config.Level);
                        curUITreetopCheckpoint.CachedFailedProcesses();
                        break;
                    }
                }
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)View.Container);
                Canvas.ForceUpdateCanvases();
                
                if (curUITreetopCheckpoint != null)
                {
                    Vector3 worldPos = View.GOMainObject.transform.position; // accurate after layout
                    View.GOMainObject.transform.SetParent(curUITreetopCheckpoint.transform, false);
                    startPosition = curUITreetopCheckpoint.transform.InverseTransformPoint(worldPos);
                    View.GOMainObject.transform.localPosition = startPosition;
                }
                
                RectTransform content = (RectTransform)View.Container;
                float contentHeight = content.rect.height;
                float viewportHeight = View.ScrollRect.viewport.rect.height;
                float scrollableHeight = contentHeight - viewportHeight;
                if (scrollableHeight <= 0f) return;

// Reset to 0 so TfLimitBot's content-local Y is measured at a stable baseline
                View.ScrollRect.verticalNormalizedPosition = 0f;
                Canvas.ForceUpdateCanvases();

                Transform referenceTransform = curUITreetopCheckpoint != null
                    ? curUITreetopCheckpoint.TfLastLevelPos
                    : lastUITreetopCheckpoint.TfLastLevelPos;

                float refY      = content.InverseTransformPoint(referenceTransform.position).y;
                float limitBotY = content.InverseTransformPoint(TfLimitBot.position).y;
                View.ScrollRect.verticalNormalizedPosition = Mathf.Clamp01((refY - limitBotY) / scrollableHeight);
            }

            private async UniTask PlayAnimLose()
            {
                if(Model.State != ModalTreetop.ModalState.ShowLose) return;
                await UniTask.WaitForSeconds(0.5f, cancellationToken: View.MainView.GetCancellationTokenOnDestroy());
                lastUITreetopCheckpoint.PlayAnimProcess(0.5f);
                float delay = 0f;
                if (curUITreetopCheckpoint != null)
                {
                    delay = 0.5f;
                    curUITreetopCheckpoint.PlayAnimProcess(0.5f,delay);
                }
                LMotion.Create(0f, 1f, 0.5f + delay)
                    .Bind(process =>
                    {
                        View.GOMainObject.transform.localPosition = Vector3.Lerp(startPosition, tarGetPostion, process);
                    }).AddTo(View.MainView);
                //await UniTask.WaitForSeconds(1f, cancellationToken: View.MainView.GetCancellationTokenOnDestroy());
                await UniTask.WaitForSeconds(1f + delay, cancellationToken: View.MainView.GetCancellationTokenOnDestroy());
                if (!TreetopData.Instance.IsShowTut)
                {
                    TreetopData.Instance.IsShowTut = true;
                    TreetopData.Instance.SaveData();
                    await ShowInfo();
                }
                View.BtnClose.gameObject.SetActive(true);
                View.BtnInfo.gameObject.SetActive(true);
                View.GOBlockScroll.SetActive(false);
                IsAnimDone = true;
            }
            private void ShowWinReward()
            {
                if(Model.State != ModalTreetop.ModalState.ShowWinReward) return;
                curUITreetopCheckpoint = null;
                View.BtnClose.gameObject.SetActive(false);
                View.BtnInfo.gameObject.SetActive(false);
                UITreetopCheckpoint prefab = PrefabGlobalConfig.Instance.UITreetopCheckpointPrefab;
                //UITreetopCheckpoint nextUiTreetopCheckpoint = null;
                int count = GlobalConfig.Streaks.Length;
                
                for (int i = 1; i < count; i++)
                {
                    TreetopStreakConfig config = GlobalConfig.Streaks[i];
                    UITreetopCheckpoint uiTreetopCheckpoint = i == 1 ? InitCheckpoint : prefab.Get(Vector3.zero, Quaternion.identity, View.Container);
                    uiTreetopCheckpoint.Init(config.Level).SetImgNormalAlter((i & 1) == 0).SetProcess(TreetopData.Instance.Streak);
                    ListTreetopCheckpoint.Add(uiTreetopCheckpoint);
                }
                
                for (int i = 0; i < ListTreetopCheckpoint.Count; i++)
                {
                    UITreetopCheckpoint uiTreetopCheckpoint = ListTreetopCheckpoint[i];
                    if (uiTreetopCheckpoint.IsCurrentStreakInsideRange(TreetopData.Instance.Streak,out float process))
                    {
                        curUITreetopCheckpoint = uiTreetopCheckpoint;
                        View.GOMainObject.transform.SetParent(curUITreetopCheckpoint.transform, false);
                        float lastProcess = curUITreetopCheckpoint.GetLastStreakProcess(Model.Level);
                        startPosition = Vector3.Lerp(
                            curUITreetopCheckpoint.TfLastLevelPos.localPosition,
                            curUITreetopCheckpoint.TfCurLevelPos.localPosition,
                            lastProcess
                        );
                        View.GOMainObject.transform.localPosition = startPosition;
                        tarGetPostion = Vector3.Lerp(
                            curUITreetopCheckpoint.TfLastLevelPos.localPosition,
                            curUITreetopCheckpoint.TfCurLevelPos.localPosition,
                            process
                        );
                        curUITreetopCheckpoint.CachedWinProcesses();
                        curUITreetopCheckpoint.CloseAnim();
                        //nextUiTreetopCheckpoint = (i + 1 < ListTreetopCheckpoint.Count) ? ListTreetopCheckpoint[i + 1] : null;
                        break;
                    }
                }
                foreach (var uiTreetopCheckpoint in ListTreetopCheckpoint)
                {
                    uiTreetopCheckpoint.SetActiveCurrentPassed();
                }
                Canvas.ForceUpdateCanvases();
                
                RectTransform content = (RectTransform)View.Container;
                float contentHeight = content.rect.height;
                float viewportHeight = View.ScrollRect.viewport.rect.height;

// Convert GOMainObject world pos to content local space
                Vector2 localPos = content.InverseTransformPoint(View.GOMainObject.transform.position);

// Scroll so GOMainObject is at center of viewport
                float targetBottom = localPos.y - viewportHeight / 2f;
                View.ScrollRect.verticalNormalizedPosition = Mathf.Clamp01(targetBottom / (contentHeight - viewportHeight));
            }

            private async UniTask PlayAnimWinReward()
            {
                if(Model.State != ModalTreetop.ModalState.ShowWinReward) return;
                await UniTask.WaitForSeconds(0.5f, cancellationToken: View.MainView.GetCancellationTokenOnDestroy());
                curUITreetopCheckpoint.PlayAnimProcess(0.5f);
                LMotion.Create(0f, 1f, 0.5f)
                    .Bind(process =>
                    {
                        View.GOMainObject.transform.localPosition = Vector3.Lerp(startPosition, tarGetPostion, process);
                    }).AddTo(View.MainView);
                //await UniTask.WaitForSeconds(1f, cancellationToken: View.MainView.GetCancellationTokenOnDestroy());
                await UniTask.WaitForSeconds(0.5f, cancellationToken: View.MainView.GetCancellationTokenOnDestroy());
                foreach (var uiTreetopCheckpoint in ListTreetopCheckpoint)
                {
                    uiTreetopCheckpoint.ActiveCurrentPassed();
                }
                AudioManager.Instance.PlaySoundFx(AudioKey.SfxRewardClaim);
                await curUITreetopCheckpoint.OpenAnim();
                //await UniTask.WaitForSeconds(0.5f, cancellationToken: View.MainView.GetCancellationTokenOnDestroy());
                await TreetopManager.Instance.TryClaimReward();
                await UIManager.Instance.ClaimRewardComplete();
                if (!TreetopData.Instance.IsShowTut)
                {
                    TreetopData.Instance.IsShowTut = true;
                    TreetopData.Instance.SaveData();
                    await ShowInfo();
                }
                View.BtnClose.gameObject.SetActive(true);
                View.BtnInfo.gameObject.SetActive(true);
                View.GOBlockScroll.SetActive(false);
                View.BtnCloseBg.gameObject.SetActive(true);
                IsAnimDone = true;
            }

            private async UniTask OnClose()
            {
                ActivityShowRewardTreetopContext.Events.CloseActivity?.Invoke();
                await ModalContainer.Find(ContainerKey.Modals).PopAsync(true);
            }

            private async UniTask ShowInfo()
            {
                ActivityShowRewardTreetopContext.Events.CloseActivity?.Invoke();
                ViewOptions view = new ViewOptions(nameof(ActivityTreetopInfo));
                await ActivityContainer.Find(ContainerKey.Activities).ShowAsync(view);
            }
        }
    }
}