using System;
using System.Collections.Generic;
using System.Threading;
using Core.GamePlay;
using Core.Manager;
using Core.UI.Activities;
using Core.UI.Screens;
using Cysharp.Threading.Tasks;
using TW.UGUI.Core.Modals;
using UnityEngine;

namespace Core.Tutorial
{
    /// <summary>
    /// Bộ thao tác dùng chung cho kịch bản tutorial (spec FTUE đợt 1 mục 5.3), một instance mỗi phiên level. Kịch bản chỉ đụng UI / bàn
    /// chơi qua đây. Trạng thái chung (lớp tối, khóa bấm, khung chữ, bàn tay) chỉ đổi khi phiên này còn là phiên hiện tại của
    /// TutorialManager: kịch bản cũ chạy tiếp sau khi level đã đổi cũng không phá phiên mới.
    /// </summary>
    public sealed class TutorialStage
    {
        private readonly TutorialManager manager;
        private readonly List<Fish> litFish = new();
        private readonly List<TankView> litTanks = new();
        private readonly List<HoldingSlotView> litHolding = new();
        private readonly List<HudPart> litHud = new();
        private HoldingBagView litBag;
        private string scriptId;
        private int stepIndex;
        private float beginTime;
        private bool dimmed;
        private bool restricting;
        private bool guideBlocking;
        private int guideVersion;

        public TutorialStage(TutorialManager manager, Level level, int levelNumber)
        {
            this.manager = manager;
            Level = level;
            LevelNumber = levelNumber;
        }

        public Level Level { get; }
        public int LevelNumber { get; }
        public BoardManager Board => Level != null ? Level.Board : null;
        public BoardModel Model => Board != null ? Board.Model : null;
        public TutorialTexts Texts => manager.Texts;

        /// <summary>Phiên này còn là phiên hiện tại của TutorialManager.</summary>
        public bool IsActive => IsOwner(this, manager.CurrentStage);

        /// <summary>Đang làm tối, khóa bấm hay hiện khung chặn bấm: idle hint / gợi ý L1 không chen vào.</summary>
        public bool IsBusy => dimmed || restricting || guideBlocking;

        /// <summary>Luật chọn phiên (thuần, test ở FtueStageOwnershipTests).</summary>
        public static bool IsOwner(object stage, object current) => current != null && ReferenceEquals(stage, current);

        // ---------- Lớp tối + làm sáng ----------

        /// <summary>
        /// Bật / tắt lớp tối và áp lại mọi vật đang làm sáng. Layer TutMesh chỉ camera Tut vẽ (main camera không vẽ), mà camera Tut chỉ bật
        /// cùng lớp tối: không làm tối thì vật làm sáng phải ở layer thường (cá chỉ bật viền), nếu không nó biến mất.
        /// </summary>
        public void DimWorld(bool on)
        {
            if (!IsActive) return;
            dimmed = on;
            manager.SetDimActive(on);
            foreach (Fish fish in litFish) ApplyHighlight(fish);
            foreach (TankView tank in litTanks)
            {
                if (tank != null) tank.SetTut(dimmed);
            }

            foreach (HoldingSlotView slot in litHolding)
            {
                if (slot != null) slot.SetTut(dimmed);
            }

            if (litBag != null) litBag.SetTut(dimmed);
        }

        public void Highlight(Fish fish)
        {
            if (fish == null) return;
            litFish.Add(fish);
            ApplyHighlight(fish);
        }

        public void Highlight(TankSlotView slot)
        {
            TankView tank = slot != null ? slot.Tank : null;
            if (tank == null) return;
            tank.SetTut(dimmed);
            litTanks.Add(tank);
        }

        /// <summary>Cả hàng ô chờ và cá đang đậu trong đó.</summary>
        public void HighlightHolding()
        {
            if (Level == null) return;
            foreach (HoldingSlotView slot in Level.HoldingSlots)
            {
                if (slot == null) continue;
                slot.SetTut(dimmed);
                litHolding.Add(slot);
                if (slot.Fish != null) Highlight(slot.Fish);
            }

            if (Level.Bag != null)
            {
                Level.Bag.SetTut(dimmed);
                litBag = Level.Bag;
            }
        }

        /// <summary>Mọi order đang mở (ô đang hiện, không khóa, đã có bể).</summary>
        public void HighlightOrders()
        {
            if (Level == null) return;
            foreach (TankSlotView slot in Level.Tanks)
            {
                if (slot != null && slot.gameObject.activeInHierarchy && !slot.IsLocked) Highlight(slot);
            }
        }

        /// <summary>Phần HUD (bộ đếm) giữ sáng khi HUD tối.</summary>
        public void HighlightHud(HudPart part)
        {
            if (!IsActive) return;
            litHud.Add(part);
            ScreenInGameContext.Events.SetHudPartLit?.Invoke(part, true);
        }

        private void ApplyHighlight(Fish fish)
        {
            if (fish == null) return;
            if (dimmed)
            {
                fish.SetTut(true);
                return;
            }

            fish.SetTut(false);
            fish.SetHighlighted(true);
        }

        public void ClearHighlights()
        {
            foreach (Fish fish in litFish)
            {
                if (fish != null) fish.SetTut(false);
            }

            foreach (TankView tank in litTanks)
            {
                if (tank != null) tank.SetTut(false);
            }

            foreach (HoldingSlotView slot in litHolding)
            {
                if (slot != null) slot.SetTut(false);
            }

            if (litBag != null) litBag.SetTut(false);
            foreach (HudPart part in litHud) ScreenInGameContext.Events.SetHudPartLit?.Invoke(part, false);
            litFish.Clear();
            litTanks.Clear();
            litHolding.Clear();
            litHud.Clear();
            litBag = null;
        }

        // ---------- Khóa bấm ----------

        public void AllowOnly(IEnumerable<Fish> fishes)
        {
            if (!IsActive) return;
            restricting = true;
            manager.SetAllowedFish(fishes);
        }

        public void AllowAll()
        {
            if (!IsActive) return;
            restricting = false;
            manager.ClearTapRestriction();
        }

        public void BlockAll()
        {
            if (!IsActive) return;
            restricting = true;
            manager.SetAllowedFish(Array.Empty<Fish>());
        }

        // ---------- Bàn tay + khung chữ ----------

        public void ShowHand(Transform worldTarget)
        {
            WorldPointerHand hand = IsActive ? manager.Hand : null;
            if (hand != null) hand.PointAt(worldTarget, this);
        }

        public void ShowHand(RectTransform uiTarget)
        {
            WorldPointerHand hand = IsActive ? manager.Hand : null;
            if (hand != null) hand.PointAt(uiTarget, this);
        }

        public void HideHand()
        {
            WorldPointerHand hand = manager.Hand;
            if (hand != null) hand.Hide(this);
        }

        /// <summary>Khung chữ đặt theo phần đang làm sáng (Highlight*), tránh hàng order / ô chờ / bộ đếm / thanh booster. blocking = chặn bấm + "Tap to continue".</summary>
        public async UniTask ShowGuide(string text, bool blocking) => await ShowGuideCore(text, CollectLitRects, blocking);

        /// <summary>Khung không chặn bấm đặt cạnh near (không làm sáng gì), tự ẩn sau seconds giây nếu chưa có khung khác thay.</summary>
        public async UniTask ShowToast(string text, Fish near, float seconds, CancellationToken token)
        {
            int version = await ShowGuideCore(text, () => FishRects(near), false);
            if (version < 0) return;
            await UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: token);
            if (version == guideVersion) HideGuide();
        }

        /// <summary>Trả về version của khung vừa hiện; -1 nếu không hiện (phiên cũ, hoặc khung khác đã thay trong lúc nạp activity).</summary>
        private async UniTask<int> ShowGuideCore(string text, Func<List<Rect>> lit, bool blocking)
        {
            if (!IsActive) return -1;
            int version = ++guideVersion;
            guideBlocking = blocking;
            await manager.EnsureGuideActivityAsync();
            if (!IsActive || version != guideVersion) return -1;
            ActivityFirstTutContext.Events.AppearGuide?.Invoke(text, lit(), CollectAvoidRects(), blocking);
            return version;
        }

        /// <summary>
        /// Bước giải thích chặn bấm (spec bản sửa mục 2.2): khóa bấm, làm tối, lightUp làm sáng phần cần nhìn, khung chữ "Tap to continue",
        /// chờ chạm (tối đa 3 s), rồi tắt khung, bỏ làm sáng, trả tối. Bấm vẫn khóa: bên gọi tự AllowAll / AllowOnly khi thả.
        /// </summary>
        public async UniTask ExplainAsync(string text, Action lightUp, CancellationToken token)
        {
            BlockAll();
            DimWorld(true);
            lightUp?.Invoke();
            await ShowGuide(text, true);
            await WaitTapToContinue(3f, token);
            HideGuide();
            ClearHighlights();
            DimWorld(false);
        }

        public void HideGuide()
        {
            if (!IsActive) return;
            guideVersion++;
            guideBlocking = false;
            ActivityFirstTutContext.Events.HideGuide?.Invoke();
        }

        public void SetHudPart(HudPart part, bool visible, bool animate)
        {
            if (!IsActive) return;
            if (part == HudPart.Holding)
            {
                if (Level != null) Level.SetHoldingVisible(visible, animate);
                return;
            }

            ScreenInGameContext.Events.SetHudPart?.Invoke(part, visible, animate);
        }

        // ---------- Chờ sự kiện ----------
        // Mọi hàm chờ đăng ký ngay lúc được gọi (trước await đầu tiên). Kịch bản phải gọi chúng TRƯỚC khi mở bấm / trước khi await
        // ShowGuide (nạp khung chữ có thể mất cả giây), giữ task rồi await sau, để không lỡ lần bấm của người chơi nhanh tay.

        /// <summary>Chờ mọi con trong remaining được bấm (list bị rút dần, onPicked(con vừa bấm, số còn lại) chạy mỗi lần).</summary>
        public async UniTask WaitPickedAll(List<Fish> remaining, Action<Fish, int> onPicked, CancellationToken token)
        {
            var tcs = new UniTaskCompletionSource();
            void OnPicked(Fish fish)
            {
                if (!remaining.Remove(fish)) return;
                onPicked?.Invoke(fish, remaining.Count);
                if (remaining.Count == 0) tcs.TrySetResult();
            }

            GameGlobalEvent.OnFishPicked += OnPicked;
            try
            {
                if (remaining.Count == 0) return;
                using (token.Register(() => tcs.TrySetCanceled()))
                    await tcs.Task;
            }
            finally
            {
                GameGlobalEvent.OnFishPicked -= OnPicked;
            }
        }

        public async UniTask<Fish> WaitFishPicked(Func<Fish, bool> match, CancellationToken token)
        {
            var tcs = new UniTaskCompletionSource<Fish>();
            void OnPicked(Fish fish)
            {
                if (match == null || match(fish)) tcs.TrySetResult(fish);
            }

            GameGlobalEvent.OnFishPicked += OnPicked;
            try
            {
                using (token.Register(() => tcs.TrySetCanceled()))
                    return await tcs.Task;
            }
            finally
            {
                GameGlobalEvent.OnFishPicked -= OnPicked;
            }
        }

        /// <summary>Chờ bể kế tiếp chốt; trả về loài của bể đó.</summary>
        public async UniTask<int> WaitTankCompleted(CancellationToken token)
        {
            var tcs = new UniTaskCompletionSource<int>();
            void OnCompleted(int tankIndex, int fishId) => tcs.TrySetResult(fishId);
            GameGlobalEvent.OnTankCompleted += OnCompleted;
            try
            {
                using (token.Register(() => tcs.TrySetCanceled()))
                    return await tcs.Task;
            }
            finally
            {
                GameGlobalEvent.OnTankCompleted -= OnCompleted;
            }
        }

        /// <summary>
        /// Chờ bể loài fishId đậu xong. true khi tới (hoặc đã đậu sẵn); false nếu đã có maxOtherArrivals bể loài khác đậu trước (data cũ,
        /// loài này còn xa trong hàng đợi).
        /// </summary>
        public async UniTask<bool> WaitTankArrived(int fishId, int maxOtherArrivals, CancellationToken token)
        {
            if (FindTankSlot(fishId) != null) return true;
            var tcs = new UniTaskCompletionSource<bool>();
            int others = 0;
            void OnArrived(int tankIndex, int id)
            {
                if (id == fishId) tcs.TrySetResult(true);
                else if (++others >= maxOtherArrivals) tcs.TrySetResult(false);
            }

            GameGlobalEvent.OnTankArrived += OnArrived;
            try
            {
                using (token.Register(() => tcs.TrySetCanceled()))
                    return await tcs.Task;
            }
            finally
            {
                GameGlobalEvent.OnTankArrived -= OnArrived;
            }
        }

        /// <summary>Chờ tới khi match(số ô trống, tổng số ô) đúng; đúng sẵn thì trả về ngay.</summary>
        public async UniTask WaitHolding(Func<int, int, bool> match, CancellationToken token)
        {
            BoardModel model = Model;
            if (model != null && match(model.HoldingFreeCount, model.HoldingSlots)) return;
            var tcs = new UniTaskCompletionSource();
            void OnChanged(int free, int slots)
            {
                if (match(free, slots)) tcs.TrySetResult();
            }

            GameGlobalEvent.OnHoldingChanged += OnChanged;
            try
            {
                using (token.Register(() => tcs.TrySetCanceled()))
                    await tcs.Task;
            }
            finally
            {
                GameGlobalEvent.OnHoldingChanged -= OnChanged;
            }
        }

        public async UniTask<Fish> WaitFishLandedInHolding(CancellationToken token)
        {
            var tcs = new UniTaskCompletionSource<Fish>();
            void OnLanded(Fish fish, int index) => tcs.TrySetResult(fish);
            GameGlobalEvent.OnFishLandedInHolding += OnLanded;
            try
            {
                using (token.Register(() => tcs.TrySetCanceled()))
                    return await tcs.Task;
            }
            finally
            {
                GameGlobalEvent.OnFishLandedInHolding -= OnLanded;
            }
        }

        /// <summary>Chờ người chơi chạm vùng "Tap to continue" của khung đang chặn bấm, tối đa maxSeconds giây (để không ai bị kẹt).</summary>
        public async UniTask WaitTapToContinue(float maxSeconds, CancellationToken token)
        {
            var tcs = new UniTaskCompletionSource();
            void OnTap() => tcs.TrySetResult();
            ActivityFirstTutContext.Events.TapContinued += OnTap;
            try
            {
                using (token.Register(() => tcs.TrySetCanceled()))
                    await UniTask.WhenAny(tcs.Task, UniTask.Delay(TimeSpan.FromSeconds(maxSeconds), cancellationToken: token));
            }
            finally
            {
                ActivityFirstTutContext.Events.TapContinued -= OnTap;
            }

            token.ThrowIfCancellationRequested();
        }

        // ---------- Tra cứu bàn chơi ----------

        /// <summary>Ô bể đã đậu đang nhận loài fishId, null nếu chưa có.</summary>
        public TankSlotView FindTankSlot(int fishId)
        {
            BoardModel model = Model;
            if (model == null || Level == null) return null;
            foreach (TankEntry tank in model.Tanks)
            {
                if (tank.FishId != fishId) continue;
                TankSlotView slot = Level.TankAt(tank.Index);
                if (slot != null && slot.IsReady) return slot;
            }

            return null;
        }

        /// <summary>Kịch bản L2: một con cá bấm được ngay mà loài chưa có bể nào đang mở; ưu tiên loài đứng đầu hàng đợi (bể của nó tới sớm nhất).</summary>
        public Fish FindHoldingDemoFish()
        {
            BoardModel model = Model;
            BoardManager board = Board;
            if (model == null || board == null) return null;
            int preferred = model.OrderQueue.Count > 0 ? model.OrderQueue[0] : -1;
            Fish fallback = null;
            foreach (BubbleView view in board.Views)
            {
                if (view == null || !view.IsFront || view.Entry == null || view.Entry.IsFishSchool || view.FishContainer == null) continue;
                foreach (Fish fish in view.FishContainer.Fishes)
                {
                    if (fish == null || fish.IsHidden || HasTankFor(model, fish.FishId) || !board.IsTappableIgnoringTutorial(fish)) continue;
                    if (fish.FishId == preferred) return fish;
                    if (fallback == null) fallback = fish;
                }
            }

            return fallback;
        }

        private static bool HasTankFor(BoardModel model, int fishId)
        {
            foreach (TankEntry tank in model.Tanks)
            {
                if (tank.HasOrder && tank.FishId == fishId) return true;
            }

            return false;
        }

        /// <summary>Có thể chen ngang để mở booster (L3): bàn còn chơi, không booster nào chạy, không modal nào mở.</summary>
        public bool CanInterruptPlay()
        {
            BoardManager board = Board;
            if (board == null || board.IsCompleted || board.IsStuck) return false;
            if (BoosterManager.Instance != null && BoosterManager.Instance.IsUsingBooster) return false;
            if (GameManager.Instance == null || !GameManager.Instance.IsState(GameState.InGame)) return false;
            ModalContainer modals = ModalContainer.Find(ContainerKey.Modals);
            return modals == null || (!modals.IsInTransition && modals.Modals.Count == 0);
        }

        // ---------- Hình chữ nhật màn hình cho chỗ đặt khung chữ ----------

        private static Camera WorldCamera => CameraManager.Instance != null ? CameraManager.Instance.GetTutCamera() : null;

        private static Bounds FishBounds(Fish fish) =>
            fish.TapCollider != null ? fish.TapCollider.bounds : new Bounds(fish.transform.position, Vector3.one * 0.5f);

        private static List<Rect> FishRects(Fish fish)
        {
            var rects = new List<Rect>();
            Camera cam = WorldCamera;
            if (cam != null && fish != null) rects.Add(ScreenRects.FromBounds(FishBounds(fish), cam));
            return rects;
        }

        private List<Rect> CollectLitRects()
        {
            var rects = new List<Rect>();
            Camera cam = WorldCamera;
            if (cam != null)
            {
                foreach (Fish fish in litFish)
                {
                    if (fish != null) rects.Add(ScreenRects.FromBounds(FishBounds(fish), cam));
                }

                foreach (TankView tank in litTanks)
                {
                    if (tank != null && TryGetTankBounds(tank, out Bounds b)) rects.Add(ScreenRects.FromBounds(b, cam));
                }

                foreach (HoldingSlotView slot in litHolding)
                {
                    if (slot != null && slot.Body != null) rects.Add(ScreenRects.FromBounds(slot.Body.bounds, cam));
                }

                if (litBag != null && TryGetRendererBounds(litBag.transform, out Bounds bag)) rects.Add(ScreenRects.FromBounds(bag, cam));
            }

            foreach (HudPart part in litHud) AddHudRect(rects, part);
            return rects;
        }

        /// <summary>Phần khung chữ không được đè: hàng order, hàng ô chờ (khi hiện), bộ đếm, thanh booster (khi hiện).</summary>
        private List<Rect> CollectAvoidRects()
        {
            var rects = new List<Rect>();
            Camera cam = WorldCamera;
            if (cam != null && Level != null)
            {
                if (TryGetOrderRowBounds(out Bounds orders)) rects.Add(ScreenRects.FromBounds(orders, cam));
                Transform holding = Level.HoldingRoot;
                if (holding != null && holding.gameObject.activeInHierarchy && TryGetRendererBounds(holding, out Bounds row))
                    rects.Add(ScreenRects.FromBounds(row, cam));
            }

            AddHudRect(rects, HudPart.Progress);
            AddHudRect(rects, HudPart.BoosterBar);
            return rects;
        }

        private static void AddHudRect(List<Rect> rects, HudPart part)
        {
            RectTransform rect = ScreenInGameContext.Events.GetHudPartRect?.Invoke(part);
            if (rect != null && rect.gameObject.activeInHierarchy) rects.Add(ScreenRects.FromRectTransform(rect));
        }

        private bool TryGetOrderRowBounds(out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (TankSlotView slot in Level.Tanks)
            {
                TankView tank = slot != null && slot.gameObject.activeInHierarchy ? slot.Tank : null;
                if (tank == null || !TryGetTankBounds(tank, out Bounds b)) continue;
                if (!any)
                {
                    bounds = b;
                    any = true;
                }
                else bounds.Encapsulate(b);
            }

            return any;
        }

        /// <summary>Bounds của bể: TankView.Body nếu prefab có gán, không thì gộp các renderer đang bật của bể (prefab hiện không gán Body).</summary>
        private static bool TryGetTankBounds(TankView tank, out Bounds bounds)
        {
            if (tank.Body != null && tank.Body.enabled && tank.Body.gameObject.activeInHierarchy)
            {
                bounds = tank.Body.bounds;
                return true;
            }

            return TryGetRendererBounds(tank.transform, out bounds);
        }

        /// <summary>Gộp bounds mọi renderer đang bật dưới root (bỏ renderer tắt / object ẩn và hệ hạt). false nếu không có renderer nào.</summary>
        public static bool TryGetRendererBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            if (root == null) return false;
            bool any = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || renderer is ParticleSystemRenderer) continue;
                if (!any)
                {
                    bounds = renderer.bounds;
                    any = true;
                }
                else bounds.Encapsulate(renderer.bounds);
            }

            return any;
        }

        // ---------- Bước, hoàn tất, analytics ----------

        /// <summary>Tiếng bước tutorial: cùng một clip, mỗi bước cao hơn 2 nửa cung.</summary>
        public void PlayStepSound(int step)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySoundFxPitched(AudioKey.SfxTutorialStep, Mathf.Pow(1.1225f, Mathf.Max(0, step)));
        }

        public void Begin(string id)
        {
            scriptId = id;
            stepIndex = 0;
            beginTime = Time.realtimeSinceStartup;
            if (IngameFirebaseAnalystic.Instance != null) IngameFirebaseAnalystic.Instance.TrackTutorialBegin(id, LevelNumber);
        }

        public void Step(string name)
        {
            stepIndex++;
            int elapsedMs = Mathf.RoundToInt((Time.realtimeSinceStartup - beginTime) * 1000f);
            if (IngameFirebaseAnalystic.Instance != null) IngameFirebaseAnalystic.Instance.TrackTutorialStep(scriptId, stepIndex, name, elapsedMs);
        }

        /// <summary>Ghi kịch bản đã xong (TutData + lưu), tiếng hoàn tất, gửi tutorial_complete. Gọi nhiều lần vô hại.</summary>
        public void Complete()
        {
            if (string.IsNullOrEmpty(scriptId) || !manager.MarkDone(scriptId)) return;
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySoundFx(AudioKey.SfxTutorialDone);
            int durationSec = Mathf.RoundToInt(Time.realtimeSinceStartup - beginTime);
            if (IngameFirebaseAnalystic.Instance != null) IngameFirebaseAnalystic.Instance.TrackTutorialComplete(scriptId, LevelNumber, durationSec);
        }

        /// <summary>Trả mọi thứ về trạng thái thường (kịch bản xong, lỗi, bị huỷ, level kết thúc).</summary>
        public void Restore()
        {
            HideHand();
            HideGuide();
            ClearHighlights();
            DimWorld(false);
            AllowAll();
            if (Board != null) Board.ClearHintPulse();
        }
    }
}
