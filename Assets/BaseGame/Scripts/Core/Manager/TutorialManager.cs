using System;
using System.Collections.Generic;
using System.Threading;
using Core.GamePlay;
using Core.Tutorial;
using Core.UI.Activities;
using Core.UI.Screens;
using CoreData;
using Cysharp.Threading.Tasks;
using DataModule;
using Manager;
using TW.UGUI.Core.Activities;
using TW.UGUI.Core.Views;
using TW.Utility.DesignPattern;
using UnityEngine;

namespace Core.Manager
{
    /// <summary>
    /// FTUE đợt 1 (spec docs/superpowers/specs/2026-10-05-ftue-phase1-design.md): áp luật HUD theo level, chạy kịch bản tutorial của level
    /// (ITutorialScript) trên một TutorialStage mỗi phiên, lưu bước đã xong vào TutData, giữ danh sách cá được bấm khi kịch bản khóa bấm.
    /// L1: bấm cá chưa có bể (bị từ chối) thì bàn tay chỉ con khớp bể. L1-L2: bàn kẹt được cứu miễn phí (Level) thì hiện chữ.
    /// </summary>
    public class TutorialManager : Singleton<TutorialManager>
    {
        [field: SerializeField] public TutData TutData { get; set; } = new TutData();
        [field: SerializeField, Tooltip("CanvasTut + camera Tut: lớp tối; vật đưa lên layer TutMesh vẽ trên nó")]
        public GameObject[] TutObjects { get; private set; }
        [field: SerializeField, Tooltip("Prefab bàn tay chỉ (Prefabs/Tutorial/WorldPointerHand.prefab), dùng chung cho kịch bản và idle hint")]
        public WorldPointerHand HandPrefab { get; private set; }
        [field: SerializeField] public TutorialTexts Texts { get; private set; } = new TutorialTexts();

        private readonly List<ITutorialScript> scripts = CreateScripts();
        private readonly HashSet<Fish> allowedFish = new();
        private bool restricting;
        private CancellationTokenSource levelCts;
        private TutorialStage stage;
        private WorldPointerHand hand;
        private bool guideShowing;
        private int deniedHintVersion;

        /// <summary>Kịch bản FTUE theo level. Thêm kịch bản mới vào đây.</summary>
        private static List<ITutorialScript> CreateScripts() => new List<ITutorialScript>
        {
            new TutMatchTankScript(),
            new TutHoldingScript(),
        };

        public bool IsRestrictingTaps => restricting;
        public bool IsStageBusy => stage != null && stage.IsBusy;
        internal TutorialStage CurrentStage => stage;

        public WorldPointerHand Hand
        {
            get
            {
                if (hand == null && HandPrefab != null) hand = Instantiate(HandPrefab, transform);
                return hand;
            }
        }

        public bool IsFishAllowed(Fish fish) => !restricting || (fish != null && allowedFish.Contains(fish));

        /// <summary>TutorialDimmerFilter: điểm màn hình trúng cá đang được phép bấm (kể cả FishPickPadding) thì lớp tối nhường click.</summary>
        public bool IsAllowedFishHole(Vector2 screenPoint)
        {
            if (!restricting || allowedFish.Count == 0 || CameraManager.Instance == null) return false;
            float padding = GamePlayGlobalConfig.Instance != null ? GamePlayGlobalConfig.Instance.FishPickPadding : 0f;
            return CameraManager.Instance.TryGetFishAt(screenPoint, padding, out Fish fish) && allowedFish.Contains(fish);
        }

        private void Start()
        {
            TutData = InGameDataManager.Instance.InGameData.TutData;
            GameGlobalEvent.OnFreeRescue += OnFreeRescue;
        }

        private void OnDestroy()
        {
            GameGlobalEvent.OnFreeRescue -= OnFreeRescue;
            // Thoát Play / đổi scene: container "Activities" có thể đã huỷ trước (thứ tự huỷ không cố định, ActivityContainer.Find ghi
            // log lỗi) và khung chữ huỷ theo nó, nên không đóng khung ở đây.
            EndLevel(false);
        }

        // ---------- Vòng đời level ----------

        /// <summary>Ngay sau ScreenInGame LoadLevel (trước popup intro): ẩn / hiện phần HUD theo FtueHudRules. Bể khóa đã áp lúc dựng bàn.</summary>
        public void PrepareLevel(int level)
        {
            EndLevel();
            List<string> done = TutData.DoneSteps ?? new List<string>();
            ScreenInGameContext.Events.SetHudPart?.Invoke(HudPart.Progress, FtueHudRules.IsVisible(HudPart.Progress, level, done), false);
            ScreenInGameContext.Events.SetHudPart?.Invoke(HudPart.BoosterBar, FtueHudRules.IsVisible(HudPart.BoosterBar, level, done), false);
            Level current = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevel : null;
            if (current != null) current.SetHoldingVisible(FtueHudRules.IsVisible(HudPart.Holding, level, done), false);
        }

        /// <summary>Sau popup intro, trước khi LevelManager bỏ pause: mở phiên, chạy kịch bản của level. Kịch bản BlocksAtStart khóa bấm trước khi trả về.</summary>
        public UniTask BeginLevelAsync(int level)
        {
            Level current = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevel : null;
            if (current == null) return UniTask.CompletedTask;
            levelCts = new CancellationTokenSource();
            stage = new TutorialStage(this, current, level);
            if (level == 1) GameGlobalEvent.OnFishTapDenied += OnDeniedTap;
            var ctx = new TutorialRunContext(level, TutData.DoneSteps);
            foreach (ITutorialScript script in scripts)
            {
                if (script.Level != level || !script.ShouldRun(ctx)) continue;
                if (script.BlocksAtStart) stage.BlockAll();
                RunScriptAsync(script, stage, levelCts.Token).Forget();
            }

            return UniTask.CompletedTask;
        }

        /// <summary>Level kết thúc (dọn bàn, thắng): huỷ kịch bản, trả lớp tối / khóa bấm / khung chữ / bàn tay.</summary>
        public void EndLevel() => EndLevel(true);

        private void EndLevel(bool closeGuide)
        {
            GameGlobalEvent.OnFishTapDenied -= OnDeniedTap;
            if (levelCts != null)
            {
                // Không Dispose: kịch bản đang await còn giữ token; Cancel là đủ, GC dọn sau.
                levelCts.Cancel();
                levelCts = null;
            }

            if (stage != null)
            {
                stage.Restore();
                stage = null;
            }

            ClearTapRestriction();
            SetDimActive(false);
            if (closeGuide) CloseGuideActivity();
        }

        private async UniTaskVoid RunScriptAsync(ITutorialScript script, TutorialStage owner, CancellationToken token)
        {
            owner.Begin(script.Id);
            try
            {
                await script.RunAsync(owner, token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                // Xong, lỗi hay bị huỷ: không bao giờ để người chơi kẹt trong lớp tối / khóa bấm. Phiên cũ thì Restore chỉ dọn phần của nó.
                owner.Restore();
            }
        }

        // ---------- Phục vụ TutorialStage ----------

        internal void SetDimActive(bool on)
        {
            ScreenInGameContext.Events.SetHudDim?.Invoke(on);   // HUD (Overlay) không nằm dưới lớp tối của thế giới: làm tối riêng
            if (TutObjects == null) return;
            for (int i = 0; i < TutObjects.Length; i++)
            {
                if (TutObjects[i] != null) TutObjects[i].SetActive(on);
            }
        }

        internal void SetAllowedFish(IEnumerable<Fish> fishes)
        {
            restricting = true;
            allowedFish.Clear();
            foreach (Fish fish in fishes)
            {
                if (fish != null) allowedFish.Add(fish);
            }
        }

        internal void ClearTapRestriction()
        {
            restricting = false;
            allowedFish.Clear();
        }

        internal bool MarkDone(string id)
        {
            if (!TutData.MarkDone(id)) return false;
            TutData.SaveData();
            return true;
        }

        internal async UniTask EnsureGuideActivityAsync()
        {
            ActivityContainer container = ActivityContainer.Find(ContainerKey.Activities);
            if (container == null) return;
            if (guideShowing)
            {
                await UniTask.WaitUntil(() => !guideShowing);
                return;
            }

            if (container.Activities.ContainsKey(nameof(ActivityFirstTut))) return;
            guideShowing = true;
            try
            {
                await container.ShowAsync(new ViewOptions(nameof(ActivityFirstTut)));
            }
            finally
            {
                guideShowing = false;
            }
        }

        internal void CloseGuideActivity()
        {
            ActivityContainer container = ActivityContainer.Find(ContainerKey.Activities);
            if (container != null && container.Activities.ContainsKey(nameof(ActivityFirstTut))) ActivityFirstTutContext.Events.CloseActivity?.Invoke();
        }

        // ---------- L1: gợi ý khi bấm sai; L1-L2: cứu miễn phí ----------

        private void OnDeniedTap(Fish _)
        {
            if (stage == null || stage.IsBusy || levelCts == null) return;
            ShowDeniedHintAsync(stage, ++deniedHintVersion, levelCts.Token).Forget();
        }

        private async UniTaskVoid ShowDeniedHintAsync(TutorialStage owner, int version, CancellationToken token)
        {
            BoardManager board = owner.Board;
            if (board == null || !board.TryShowMatchingHint(out Fish target)) return;
            owner.ShowHand(target.transform);
            try
            {
                await owner.ShowToast(Texts.L1Denied, target, 2f, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (version != deniedHintVersion) return;
            owner.HideHand();
            if (owner.Board != null) owner.Board.ClearHintPulse();
        }

        private void OnFreeRescue()
        {
            int level = GameManager.Instance != null ? GameManager.Instance.Level.Value : 0;
            if (IngameFirebaseAnalystic.Instance != null) IngameFirebaseAnalystic.Instance.TrackFtueRescue(level);
            if (stage == null || levelCts == null) return;
            TutorialStage owner = stage;
            TutorialTexts texts = Texts;
            CancellationToken token = levelCts.Token;
            TutorialGuard.RunAsync(() => ExplainRescueAsync(owner, texts, token), owner.Restore, Debug.LogException).Forget();
        }

        /// <summary>
        /// Câu cứu miễn phí là bước chặn (spec bản sửa mục 2.2): chờ bàn xong việc gom túi và bước khác (nếu có) xong, làm sáng ô chờ + túi,
        /// chạm để tiếp tục, rồi thả bấm. Chạy trong TutorialGuard: lỗi thì Restore, không để người chơi kẹt.
        /// </summary>
        private static async UniTask ExplainRescueAsync(TutorialStage owner, TutorialTexts texts, CancellationToken token)
        {
            await UniTask.WaitUntil(() => !owner.IsBusy && (owner.Board == null || !owner.Board.IsBusy), cancellationToken: token);
            await owner.ExplainAsync(texts.Rescue, owner.HighlightHolding, token);
            owner.AllowAll();
        }
    }
}
