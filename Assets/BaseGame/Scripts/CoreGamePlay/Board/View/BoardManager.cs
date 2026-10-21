using System;
using System.Collections.Generic;
using System.Threading;
using Core.GamePlay;
using Core.Manager;
using Core.UI.Screens;
using CoreData;
using Cysharp.Threading.Tasks;
using LitMotion;
using Sirenix.OdinInspector;
using UnityEngine;
using Random = System.Random;

[RequireComponent(typeof(Level))]
public class BoardManager : MonoBehaviour
{
    [field: SerializeField]
    public Level Level { get; private set; }
    // Không serialize: Unity sẽ tạo BoardModel rỗng (không qua constructor, list null) cho instance prefab trước khi Build; chỉ hiện Inspector.
    [ShowInInspector]
    public BoardModel Model { get; private set; }
    [ShowInInspector]
    public IReadOnlyList<BubbleView> Views => views;
    public bool IsBusy => pendingFlights > 0 || pendingPops > 0 || pendingRefills > 0 || pendingShuffles > 0;
    public bool IsCompleted { get; private set; }
    public bool IsStuck { get; private set; }

    /// <summary>FTUE L1 (FtueLevelRules.HoldingEnabled): tắt thì cá không có bể đã đậu bị từ chối, không bay xuống ô chờ. Level đặt sau Build.</summary>
    public bool HoldingEnabled { get; set; } = true;

    /// <summary>Khoảng pitch của tiếng bóng trồi lên trong đợt spawn đầu màn: bóng đầu trầm, bóng cuối cao.</summary>
    private const float SpawnPitchLow = 0.85f;
    private const float SpawnPitchHigh = 1.25f;

    private readonly List<BubbleView> views = new();
    private readonly Random rng = new();
    private CancellationTokenSource session;
    private int pendingFlights;
    private readonly HashSet<BubbleView> keyLocksAwaitingKey = new();   // ổ đã mở ở model nhưng token chìa còn đang bay tới: RefreshLocks chưa mở hình
    private int pendingPops;
    private int pendingRefills;
    private int pendingShuffles;
    private int pendingBagArrivals;
    private bool winDecided;   // đã báo Level.OnBoardDecided cho màn này
    private float autoCollectTimer;
    private bool holdingWarned;
    private Fish hintFish;
    private MotionHandle hintHandle;
    private ResourceType selectingBooster = ResourceType.None;
    private Random boardRandom;
    private static GamePlayGlobalConfig Config => GamePlayGlobalConfig.Instance;
    private CancellationToken Token => session?.Token ?? this.GetCancellationTokenOnDestroy();
    private Transform FlyRoot => Level.FlyingRoot != null ? Level.FlyingRoot : transform;

    private void OnDestroy()
    {
        CancelSession();
    }

    private void CancelSession()
    {
        session?.Cancel();
        session?.Dispose();
        session = null;
    }

    private void Update()
    {
        TickIce(Time.deltaTime);
        if (autoCollectTimer <= 0f) return;
        autoCollectTimer -= Time.deltaTime;
        if (autoCollectTimer > 0f) return;
        TryAutoCollectOne();
        CheckBoardState();
    }

    public void Build(LevelData data)
    {
        Clear();
        boardRandom = new Random(1);
        session = new CancellationTokenSource();
        Model = new BoardModel(data, Level.ActiveTankCount, Config.MaxTankSlots, Config.DefaultHoldingSlots,
            Mathf.Max(DefaultGlobalConfig.Instance.MaxHoldingSlots, Config.BoosterHoldingSlotsMax), Config.VisibleBubbleCount);
        IsCompleted = false;
        IsStuck = false;
        autoCollectTimer = 0f;
        DropInitialTanksAsync().Forget();
        RaiseProgress();
        RefreshHoldingWarning();   // bắn OnHoldingChanged lần đầu cho tutorial / thống kê
    }

    public void Clear()
    {
        CancelSession();
        foreach (BubbleView view in views)
        {
            if (view != null) view.Unbind();
        }

        views.Clear();
        pendingFlights = 0;
        pendingPops = 0;
        pendingRefills = 0;
        pendingShuffles = 0;
        pendingBagArrivals = 0;
        winDecided = false;
        autoCollectTimer = 0f;
        holdingWarned = false;
        hintHandle.TryCancel();
        hintFish = null;
        Model = null;
    }

    public async UniTask SpawnFrontBubblesAsync()
    {
        if (Model == null) return;
        List<BubbleEntry> fronts = new List<BubbleEntry>(Model.FrontBubbles);
        for (int i = 0; i < fronts.Count; i++)
        {
            if (Model == null) return;
            BubbleEntry entry = fronts[i];
            SpawnBubbleView(entry, i / (float)fronts.Count);
        }

        // Màn loading còn che: mô phỏng trước cho bàn xếp xong ngay, tránh người chơi phải ngồi nhìn bóng bò lên từ SpawnPoint.
        Level.WarmUpBubbles();
    }

    private BubbleView SpawnBubbleView(BubbleEntry entry, float spawnProgress)
    {
        Model.TryAutoRevealAll(entry);   // HiddenFish: bóng toàn cá ẩn thì spawn thẳng model thật
        BubbleView view = Level.SpawnBubble(entry, boardRandom, spawnProgress);
        if (view == null) return null;
        view.Bind(this, entry);
        views.Add(view);
        return view;
    }
    private async UniTask DropInitialTanksAsync()
    {
        pendingRefills++;
        try
        {
            var tasks = new List<UniTask>();
            for (int i = 0; i < Model.Tanks.Count; i++) tasks.Add(SyncTankAsync(i, i * 0.1f));
            await UniTask.WhenAll(tasks);
            RequestAutoCollect();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            pendingRefills--;
            CheckBoardState();
        }
    }

    // ---------- Nước đi ----------

    /// <summary>FISH-03/04: bấm cá ở bóng đang hiện. Trả về true nếu có nước đi. Ô chờ đầy thì khóa bấm.</summary>
    public bool TryTapFish(Fish fish)
    {
        if (!TryResolveTap(fish, out BubbleView view, out int index, out TankEntry tank, out int holdingIndex)) return false;
        // Phản hồi bấm vào bóng bị chặn (đá / băng / vỏ HP) đang tắt; nếu mở lại thì đặt ở đây (view.PlayTapFeedback()),
        // không đặt trong TryResolveTap vì hàm đó chạy mỗi frame khi đè tay.
        if (view.Entry.IsFishSchool) return TapSchool(view, index);   // FishSchoolBubble: cả đàn bay (TryResolveTap đã chắc đủ chỗ)

        int fishId = view.Entry.FishIdAt(index);
        RemoveFishFromBubble(view, fish, index);
        if (tank != null) ReserveTank(tank);
        else Model.ReserveHolding(holdingIndex);

        AfterFishLeft(view);
        if (tank != null) FlyToTank(fish, fishId, tank).Forget();
        else FlyToHolding(fish, fishId, holdingIndex).Forget();
        PlaySfx(AudioKey.SfxFishTap);
        GameGlobalEvent.OnFishPicked?.Invoke(fish);
        return true;
    }

    /// <summary>Scene test không có AudioManager thì bỏ qua; key chưa gán clip trong AudioManager.prefab thì PlaySoundFx tự bỏ qua.</summary>
    private static void PlaySfx(AudioKey key, float delay = 0f)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySoundFx(key, delay);
    }

    private static void PlaySfxPitched(AudioKey key, float pitch)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySoundFxPitched(key, pitch);
    }

    /// <summary>Cá bấm được ngay lúc này (đủ mọi điều kiện của TryTapFish, không đổi gì). InputManager dùng để viền cá khi đè tay.</summary>
    public bool CanTapFish(Fish fish) => TryResolveTap(fish, out _, out _, out _, out _);

    /// <summary>
    /// InputManager đè tay: nhóm cá sẽ cùng bay nếu bấm con này (FishSchoolBubble: cả đàn; bóng thường: chính nó) và có đủ chỗ không.
    /// Không đổi state. Cá không bấm được (băng / vỏ / khoá / ô chờ đầy / đang shuffle...) thì false và group rỗng; bóng đàn bấm được
    /// nhưng bể + ô chờ không chứa hết đàn thì false, group = cả đàn và denied = true để InputManager viền đỏ.
    /// </summary>
    public bool TryGetPickGroup(Fish fish, List<Fish> group, out bool denied)
    {
        group.Clear();
        denied = false;
        if (!TryResolveSource(fish, out BubbleView view, out int index)) return false;
        if (!view.Entry.IsFishSchool)
        {
            if (TryResolveTap(fish, out _, out _, out _, out _))
            {
                group.Add(fish);
                return true;
            }

            // FTUE L1 (ô chờ tắt): cá không có bể nào mang đơn loài của nó bị từ chối: viền đỏ + tiếng từ chối của InputManager, nhả tay
            // bắn OnFishTapDenied. Có bể của loài đó nhưng đang đủ người nhận / đang thay đơn thì chỉ là chưa bấm được, không báo sai.
            if (!HoldingEnabled && Model.CanTap && !Model.HasOrderFor(view.Entry.FishIdAt(index)))
            {
                group.Add(fish);
                denied = true;
            }

            return false;
        }

        group.AddRange(view.FishContainer.Fishes);
        if (Model.TryPlanSchool(view.Entry, index, null, IsTankReady)) return true;
        denied = true;
        return false;
    }

    /// <summary>FishSchoolBubble thiếu chỗ (InputManager): mọi ô chờ nhuộm SchoolDenyColor (on) hoặc trả màu gốc (off), đổi màu trong SchoolDenyFade giây.</summary>
    public void SetHoldingDeny(bool on)
    {
        if (Model == null) return;
        for (int i = 0; i < Model.HoldingSlots; i++)
        {
            HoldingSlotView slot = Level.HoldingAt(i);
            if (slot != null) slot.SetDeny(on, Config.SchoolDenyColor, Config.SchoolDenyFade);
        }
    }

    /// <summary>
    /// FishSchoolBubble: bấm một con là cả đàn bay. Đặt chỗ cho cả đàn theo TryPlanSchool (con được bấm trước, rồi các con còn lại theo
    /// thứ tự đàn), gỡ hết khỏi bóng ngay (bóng rỗng nổ như thường), rồi từng con cất cánh cách nhau FishSchoolFlyStagger giây tới đích
    /// riêng (FlyToTank / FlyToHolding như bấm lẻ). OnFishPicked bắn từng con để tutorial đếm đúng.
    /// </summary>
    private bool TapSchool(BubbleView view, int tappedIndex)
    {
        var plan = new List<SchoolMove>();
        if (!Model.TryPlanSchool(view.Entry, tappedIndex, plan, IsTankReady)) return false;
        var flights = new List<SchoolFlight>(plan.Count);
        foreach (SchoolMove m in plan)
        {
            flights.Add(new SchoolFlight
            {
                Fish = view.FishContainer.Fishes[m.FishIndex], FishIndex = m.FishIndex, FishId = view.Entry.FishIdAt(m.FishIndex),
                Tank = m.Tank, HoldingIndex = m.HoldingIndex,
            });
            if (m.Tank != null) ReserveTank(m.Tank);
            else Model.ReserveHolding(m.HoldingIndex);
        }

        // Gỡ theo index giảm dần để index của các con còn lại không lệch (RemoveFishFromBubble gỡ theo index ở cả model lẫn view).
        var byIndex = new List<SchoolFlight>(flights);
        byIndex.Sort((a, b) => b.FishIndex.CompareTo(a.FishIndex));
        foreach (SchoolFlight f in byIndex) RemoveFishFromBubble(view, f.Fish, f.FishIndex);
        AfterFishLeft(view);
        FlySchoolAsync(flights).Forget();
        PlaySfx(AudioKey.SfxFishTap);
        return true;
    }

    private struct SchoolFlight
    {
        public Fish Fish;
        public int FishIndex;
        public int FishId;
        public TankEntry Tank;
        public int HoldingIndex;
    }

    /// <summary>Cả đàn cất cánh lần lượt (con được bấm trước) cách nhau FishSchoolFlyStagger; bàn bận tới khi con cuối cất cánh (mỗi chuyến bay tự đếm tiếp).</summary>
    private async UniTaskVoid FlySchoolAsync(List<SchoolFlight> flights)
    {
        pendingFlights++;
        try
        {
            for (int i = 0; i < flights.Count; i++)
            {
                if (i > 0 && Config.FishSchoolFlyStagger > 0f)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(Config.FishSchoolFlyStagger), cancellationToken: Token);
                }

                if (Model == null) return;
                SchoolFlight f = flights[i];
                if (f.Tank != null) FlyToTank(f.Fish, f.FishId, f.Tank).Forget();
                else FlyToHolding(f.Fish, f.FishId, f.HoldingIndex).Forget();
                GameGlobalEvent.OnFishPicked?.Invoke(f.Fish);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            pendingFlights--;
            CheckBoardState();
        }
    }

    public bool TryGetFirstOpenTank(out int tankIndex, out int fishId)
    {
        tankIndex = -1;
        fishId = -1;
        if (Model == null) return false;
        foreach (TankEntry tank in Model.Tanks)
        {
            if (!tank.HasOrder) continue;
            tankIndex = tank.Index;
            fishId = tank.FishId;
            return true;
        }

        return false;
    }

    /// <summary>Cá bấm được nếu bỏ qua khóa bấm của tutorial (kịch bản chọn cá trước khi mở khóa cho đúng con đó).</summary>
    public bool IsTappableIgnoringTutorial(Fish fish) => TryResolveTap(fish, out _, out _, out _, out _, false);

    public bool IsFirstOpenTankReady()
    {
        if (Model == null) return false;
        foreach (TankEntry tank in Model.Tanks)
        {
            if (!tank.HasOrder) continue;
            return IsTankReady(tank);
        }

        return false;
    }

    public bool TryCollectTappableFish(int fishId, int count, List<Fish> results)
    {
        results.Clear();
        if (Model == null || count <= 0) return false;
        foreach (BubbleView view in views)
        {
            if (view == null || view.FishContainer == null || !view.IsFront || view.Entry == null || !view.Entry.IsTappable) continue;
            IReadOnlyList<Fish> fishes = view.FishContainer.Fishes;
            for (int i = 0; i < fishes.Count; i++)
            {
                Fish fish = fishes[i];
                if (fish == null || fish.FishId != fishId || fish.IsHidden) continue;
                if (!IsTappableIgnoringTutorial(fish)) continue;
                results.Add(fish);
                if (results.Count >= count) return true;
            }
        }

        return results.Count > 0;
    }

    /// <summary>
    /// Kiểm tra mọi điều kiện bấm cá và tìm đích: bể đã đậu nhận cá, không thì ô chờ trống. Không đổi model. Chỉ bể đã đậu mới nhận cá;
    /// bể đang rơi vào / bay đi (model đã có đơn mới) thì coi như không có bể: cá lên ô chờ, bể đậu xong tự thu (TryAutoCollectOne).
    /// </summary>
    private bool TryResolveTap(Fish fish, out BubbleView view, out int index, out TankEntry tank, out int holdingIndex, bool checkTutorial = true)
    {
        tank = null;
        holdingIndex = -1;
        if (!TryResolveSource(fish, out view, out index, checkTutorial)) return false;
        // FishSchoolBubble: cả đàn phải đủ chỗ (bể + ô chờ); đích từng con tính lại lúc bấm (TapSchool), tank / holdingIndex không dùng.
        if (view.Entry.IsFishSchool) return Model.TryPlanSchool(view.Entry, index, null, IsTankReady);

        tank = Model.FindTankFor(view.Entry.FishIdAt(index), IsTankReady);
        if (tank != null) return true;
        if (!HoldingEnabled) return false;   // FTUE L1: không có ô chờ
        holdingIndex = Model.FindFreeHoldingIndex();
        return holdingIndex >= 0;
    }

    /// <summary>
    /// Mọi điều kiện bấm cá trừ chỗ đến: bàn còn chơi, tutorial cho phép, không khoá bay / shuffle, ô chờ còn trống, cá thuộc bóng đang
    /// hiện và bấm được, lớp phủ đã tan xong. Không đổi model.
    /// </summary>
    private bool TryResolveSource(Fish fish, out BubbleView view, out int index, bool checkTutorial = true)
    {
        view = null;
        index = -1;
        if (Model == null || IsCompleted || IsStuck || fish == null) return false;
        if (checkTutorial && TutorialManager.Instance != null && !TutorialManager.Instance.IsFishAllowed(fish)) return false;
        if (Config.InputLockDuringFlight && pendingFlights > 0) return false;
        if (pendingShuffles > 0) return false;   // Shuffle đang bay: đàn chưa sắp lại khớp model, chỉ số bấm sẽ sai
        if (!Model.CanTap) return false;
        view = FindViewOf(fish);
        if (view == null || !view.IsFront) return false;
        index = view.IndexOf(fish);
        if (index < 0 || !view.Entry.IsTappable) return false;
        if (view.IsKeyLockShown) return false;   // ổ khoá: model đã mở nhưng token chìa còn đang bay tới, đợi ổ mở hình mới bấm được
        return !view.IsCoverDissolving;   // băng / vỏ HP vừa vỡ, lớp phủ còn đang tan: đợi tan xong mới bấm được (model đã mở khoá từ lúc vỡ)
    }

    /// <summary>Bể của entry đã đậu yên trong ô cắm (có TankView và không đang rơi / bay đi) nên nhận cá được.</summary>
    private bool IsTankReady(TankEntry tank)
    {
        TankSlotView slot = Level.TankAt(tank.Index);
        return slot != null && slot.IsReady;
    }

    /// <summary>
    /// Gỡ một con cá khỏi bóng (model + view), cá giữ GameObject cho bên gọi bay đi. Bóng có spawner: model chèn con đầu hàng đợi vào
    /// đúng index (RemoveFishAt), view sinh con đó bay từ chỗ con spawner tới điểm vừa trống và giảm số trên spawner (hết thì spawner ẩn).
    /// MultilayerBubble: con vừa gỡ là con cuối của lớp đang hiện thì model đã đưa lớp kế lên (newLayer), view sinh cả lớp mới bay từ tâm
    /// bóng ra, bóng giữ nguyên cỡ. Dùng chung cho bấm tay, AutoFill và ép ô chờ để không đường nào quên spawner / lớp kế.
    /// </summary>
    private void RemoveFishFromBubble(BubbleView view, Fish fish, int index)
    {
        Model.RemoveFishAt(view.Entry, index, out FishEntry refill, out bool newLayer);
        view.FishContainer.Detach(fish, out int pointIndex);
        fish.transform.SetParent(FlyRoot, true);
        fish.SetTappable(false);
        if (hintFish == fish) ClearHint();
        if (newLayer)
        {
            view.FishContainer.SpawnLayer(view.Entry.Fishes, Config.LayerRevealDuration, Config.LayerRevealStagger);
            RefreshMultiLayer(view);
            return;
        }

        if (refill == null) return;
        view.FishContainer.SpawnFishFromSpawner(refill, index, pointIndex, Config.SpawnerSpawnDuration);
        view.FishContainer.UpdateSpawner(view.Entry.SpawnerCount, Config.SpawnerHideDuration);
    }

    /// <summary>
    /// MultilayerBubble: báo số lớp còn ẩn cho view sau khi lộ lớp (SpawnLayer) hoặc AutoFill rút bớt / rút hết cá của một lớp ẩn: quả
    /// lồng bung ra tan, còn lớp ẩn thì quả mới phình lên, hết lớp ẩn thì không còn quả (bóng thường). Không phải bóng nhiều lớp thì không làm gì.
    /// </summary>
    private static void RefreshMultiLayer(BubbleView view)
    {
        if (view == null || view.Entry == null || !view.Entry.IsMultiLayer) return;
        view.SetHiddenLayers(view.Entry.Layers.Count);
    }

    /// <summary>
    /// Sau khi một con cá rời bóng: bóng giữ nguyên cỡ; hết cá thì nổ và sinh bóng kế từ hàng đợi (bóng có spawner chỉ hết khi hàng
    /// đợi spawner cũng rỗng, bóng nhiều lớp chỉ hết khi qua lớp cuối: RemoveFishAt đã chèn cá thay / đưa lớp kế lên); các con còn lại
    /// toàn cá ẩn thì cả đàn lộ hình thật (HiddenFish).
    /// </summary>
    private void AfterFishLeft(BubbleView view)
    {
        if (view.Entry.IsEmpty && view.Entry.IsKey) LaunchKeyFlight(view);   // trước RefreshLocks: ổ có chìa đang bay chưa được mở hình
        RefreshLocks();
        if (view.Entry.IsEmpty)
        {
            PopAndSpawnNextAsync(view).Forget();
            return;
        }

        if (!Model.TryAutoRevealAll(view.Entry)) return;
        foreach (Fish fish in view.FishContainer.Fishes) fish.Reveal();
    }

    /// <summary>
    /// Bóng hết cá: phình lên BubblePopScale trong BubblePopDuration rồi biến mất (VFX bóng vỡ tại tâm, BubbleView.PlayBreakVfx);
    /// bóng kế trong hàng đợi sinh ra.
    /// </summary>
    private async UniTask PopAndSpawnNextAsync(BubbleView view)
    {
        pendingPops++;
        try
        {
            if (Config.BubblePopDelay > 0f) await UniTask.Delay(TimeSpan.FromSeconds(Config.BubblePopDelay), cancellationToken: Token);
            if (view == null || Model == null) return;
            float popRadius = Mathf.Max(view.Bubble.TargetRadius, view.Bubble.CurrentRadius) * Mathf.Max(1f, Config.BubblePopScale);
            view.Bubble.SetRadius(popRadius, Config.BubblePopDuration);
            if (Config.BubblePopDuration > 0f) await UniTask.Delay(TimeSpan.FromSeconds(Config.BubblePopDuration), cancellationToken: Token);
            if (view == null || Model == null) return;
            views.Remove(view);
            view.Unbind();
            view.PlayBreakVfx();
            Level.DespawnBubble(view);
            PlaySfx(AudioKey.SfxBubblePop);
            BubbleEntry next = Model.TakeNextFromQueue();
            if (next != null)
            {
                BubbleView nextView = SpawnBubbleView(next, 1);
                if (nextView != null)
                {
                    PlaySfxPitched(AudioKey.SfxBubbleSpawn, UnityEngine.Random.Range(0.95f, 1.05f));
                    await UniTask.Delay(TimeSpan.FromSeconds(Config.BubbleGrowDuration), cancellationToken: Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            
        }
        finally
        {
            pendingPops--;
            CheckBoardState();
        }
    }

    /// <summary>
    /// Cá bay vào bể. levelLanding = tới bể nằm ngang theo chiều bay (AutoFill: nhiều con về cùng lúc, chúi đầu rồi xoay lại nhìn khựng);
    /// mặc định chúi xuống như bấm tay / tự thu.
    /// </summary>
    private async UniTaskVoid FlyToTank(Fish fish, int fishId, TankEntry tank, bool levelLanding = false)
    {
        pendingFlights++;
        try
        {
            fish.Reveal();   // HiddenFish lộ hình thật khi bay đi (cá thường: không làm gì); phải trước PrepareArrival (đọc Fish.BodySize)
            TankSlotView slot = Level.TankAt(tank.Index);
            if (slot == null)
            {
                Debug.LogError($"BoardManager: bể {tank.Index} không có ô cắm trong Level.", this);
                return;
            }

            // Lưới an toàn: bấm tay và tự thu đã lọc IsTankReady nên bình thường bể đã đậu. Nếu vẫn chưa (race) thì đợi bể đậu
            // rồi mới dựng đường bay; return ngay như trước sẽ bỏ cá đứng giữa trời và Incoming của bể kẹt mãi.
            await UniTask.WaitUntil(() => slot.IsReady, cancellationToken: Token);
            if (Model == null) return;
            Transform anchor = slot.PrepareArrival(fish);
            await FishFlight.Fly(fish, slot.Tank.FishIcon, Config.TankFishScale, Token);
            
            fish.HideGraphic();
            if (Model == null) return;
            bool complete = Model.ArriveAtTank(tank);
            if (slot != null) slot.Park(fish, anchor);
            PlaySfx(AudioKey.SfxFishLand);
            slot.Tank.PlayEffectRaiseProgress();
            await UniTask.Delay(TimeSpan.FromSeconds(0.2f),  cancellationToken: Token);
            if (complete)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(0.3f),  cancellationToken: Token);
                CompleteTankAsync(tank, slot).Forget();
            }
            RequestAutoCollect();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            pendingFlights--;
            CheckBoardState();
        }
    }

    private async UniTaskVoid FlyToHolding(Fish fish, int fishId, int holdingIndex)
    {
        pendingFlights++;
        try
        {
            fish.Reveal();   // HiddenFish lộ hình thật khi bay đi (cá thường: không làm gì)
            HoldingSlotView slot = Level.HoldingAt(holdingIndex);
            // Ô chờ giữ trước điểm ở tâm và trả anchor bám theo điểm (đi theo ô khi dồn hàng) để cá bay thẳng tới.
            Transform anchor = slot != null ? slot.PrepareArrival(fish) : transform;
            await FishFlight.Fly(fish, anchor, Config.HoldingFishScale, Token);
            if (Model == null) return;
            Model.ArriveAtHolding(holdingIndex, fishId);
            if (slot != null) slot.Place(fish, anchor);
            GameGlobalEvent.OnFishLandedInHolding?.Invoke(fish, holdingIndex);
            PlaySfx(AudioKey.SfxFishLand);
            RefreshHoldingWarning();
            RequestAutoCollect();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            pendingFlights--;
            CheckBoardState();
        }
    }

    /// <summary>TANK-03: bể đủ cá → tiến độ, bể mang cá bay đi, bể kế (đơn kế, FIFO) rơi xuống rồi tự thu cá.</summary>
    private async UniTaskVoid CompleteTankAsync(TankEntry tank, TankSlotView slot)
    {
        pendingRefills++;
        try
        {
            int completedFishId = tank.FishId;
            Model.CompleteTank(tank);
            RefreshShells();
            RefreshLocks();
            RaiseProgress();
            GameGlobalEvent.OnTankCompleted?.Invoke(tank.Index, completedFishId);
            PlaySfx(AudioKey.SfxTankComplete);
            PlaySfx(AudioKey.SfxTankLeave);
            if (slot != null) await slot.PlayComplete(Token);
            if (Config.TankRefillDelay > 0f) await UniTask.Delay(TimeSpan.FromSeconds(Config.TankRefillDelay), cancellationToken: Token);
            if (Model == null) return;
            await SyncTankAsync(tank.Index);
            RequestAutoCollect();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            pendingRefills--;
            CheckBoardState();
        }
    }

    /// <summary>Bể trong ô cắm theo model: có đơn thì bể rơi xuống (sau delay), đổi đơn thì bể cũ bay đi trước, hết đơn thì bể bay đi.</summary>
    private async UniTask SyncTankAsync(int tankIndex, float delay = 0f)
    {
        TankSlotView slot = Level.TankAt(tankIndex);
        if (slot == null || Model == null || tankIndex >= Model.Tanks.Count || slot.IsLocked) return;
        if (delay > 0f) await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: Token);
        if (Model == null) return;
        TankEntry tank = Model.Tanks[tankIndex];
        int fishId = tank.FishId;
        await slot.SetOrderAsync(fishId, tank.Goal, Token);
        GameGlobalEvent.OnTankArrived?.Invoke(tankIndex, fishId);
    }

    /// <summary>Ô cắm vừa mở (UnlockTank) hoặc vừa đổi đơn (Swap): bể rơi xuống rồi tự thu cá; giữ bàn "bận" tới khi bể đậu.</summary>
    private async UniTaskVoid RefreshTankAsync(int tankIndex)
    {
        pendingRefills++;
        try
        {
            await SyncTankAsync(tankIndex);
            RequestAutoCollect();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            pendingRefills--;
            CheckBoardState();
        }
    }

    // ---------- Tự thu cá (HOLD-02) ----------

    /// <summary>Xin tự thu (bể vừa đậu / cá vừa hạ / ô cắm vừa mở). Đang trong khoảng nghỉ thì Update gọi lại khi hết.</summary>
    private void RequestAutoCollect()
    {
        if (autoCollectTimer > 0f) return;
        TryAutoCollectOne();
    }

    /// <summary>
    /// Mỗi lần chỉ một con: cá đầu tiên trong ô chờ (trái sang phải) đã nằm yên (cá đang bay lên ô chờ thì đợi nó hạ xong,
    /// FlyToHolding sẽ xin lại) và có bể cần đã đậu yên (bể đang rơi thì bỏ qua, bể đậu xong xin lại); con kế sau AutoCollectInterval.
    /// </summary>
    private void TryAutoCollectOne()
    {
        if (Model == null || !Config.AutoCollectEnabled || IsCompleted) return;
        if (!Model.TryFindAutoCollect(out int holdingIndex, out TankEntry tank, IsTankReady))
        {
            TryCollectFromBag();   // túi PackedUp đứng sau các ô chờ
            return;
        }
        HoldingSlotView holdingSlot = Level.HoldingAt(holdingIndex);
        Fish fish = holdingSlot != null ? holdingSlot.Take() : null;
        int fishId = Model.TakeFromHolding(holdingIndex);
        RefreshHoldingWarning();
        if (fish == null)
        {
            Debug.LogWarning($"BoardManager: ô chờ {holdingIndex} có cá {fishId} trong model nhưng không có view.", this);
            return;
        }

        ReserveTank(tank);
        fish.transform.SetParent(FlyRoot, true);
        FlyToTank(fish, fishId, tank).Forget();
        autoCollectTimer = Mathf.Max(0.01f, Config.AutoCollectInterval);
    }

    private void RefreshHoldingWarning()
    {
        if (Model == null) return;
        int free = Model.HoldingFreeCount;
        bool warn = free <= Config.HoldingWarnRemaining;
        for (int i = 0; i < Model.HoldingSlots; i++)
        {
            HoldingSlotView slot = Level.HoldingAt(i);
            if (slot != null) slot.SetWarning(warn && !Model.Holding[i].HasFish);
        }
        ScreenInGameContext.Events.UpdateTxtWarning?.Invoke(free);
        ScreenInGameContext.Events.SetActiveWarning?.Invoke(warn);
        if (warn && !holdingWarned) PlaySfx(AudioKey.SfxHoldingWarning);
        holdingWarned = warn;
        GameGlobalEvent.OnHoldingChanged?.Invoke(free, Model.HoldingSlots);
    }

    private void RaiseProgress()
    {
        if (Model == null) return;
        GameGlobalEvent.OnProgressChanged?.Invoke(Model.Collected, Model.TotalFish);
        ScreenInGameContext.Events.UpdateProgress?.Invoke(Model.Collected, Model.TotalFish);
    }

    /// <summary>HOLD-03 / WIN-01: chỉ phán khi không còn cá bay, bóng nổ, bể đang rơi / bay.</summary>
    private void CheckBoardState()
    {
        if (Model == null || IsCompleted || IsStuck || IsBusy) return;
        if (Model.HasPendingMoves) return;
        GameGlobalEvent.OnBoardSettled?.Invoke();
        if (Model.IsWin)
        {
            IsCompleted = true;
            Level.OnBoardCompleted();
            return;
        }

        if (Config.StuckCheckAfterResolve && Model.IsStuck)
        {
            IsStuck = true;
            Level.OnBoardStuck();
        }
    }

    /// <summary>
    /// Đặt chỗ cá bay vào bể (mọi đường: bấm tay, đàn, tự thu, booster). Nếu đây là con quyết định thắng (Model.IsWinDecided) thì báo
    /// Level ngay để GameManager khoá HUD / input trước khi animation xong (cỡ 1.5 s); WinLevel vẫn đợi CheckBoardState như cũ.
    /// </summary>
    private void ReserveTank(TankEntry tank)
    {
        Model.ReserveTank(tank);
        if (winDecided || IsCompleted || !Model.IsWinDecided) return;
        winDecided = true;
        Level.OnBoardDecided();
    }

    // ---------- Bóng vỏ HP (MultiHpBubble) ----------

    /// <summary>Bể vừa hoàn thành: model đã trừ HP cho bóng HP đang hiện (BoardModel.DamageShells); view cập nhật số và vỡ vỏ khi về 0.</summary>
    private void RefreshShells()
    {
        foreach (BubbleView view in views)
        {
            if (view != null && view.Entry != null && view.Entry.MaxHp > 0) view.UpdateHp(view.Entry.Hp);
        }
    }

    // ---------- Bóng khóa (LockHpBubble) ----------

    /// <summary>
    /// Bể vừa hoàn thành / bóng chìa nổ: model đã bỏ cờ. Khoá chìa và khoá đơn mỗi loại chạy animation mở của riêng nó. Ổ đang chờ token
    /// chìa bay tới (keyLocksAwaitingKey) thì chưa mở hình, FlyKeyAsync mở khi chìa tới nơi.
    /// </summary>
    private void RefreshLocks()
    {
        bool unlocked = false;
        foreach (BubbleView view in views)
        {
            if (view == null || view.Entry == null) continue;
            if (!keyLocksAwaitingKey.Contains(view) && view.UpdateKeyLock(view.Entry.IsLock)) unlocked = true;
            if (view.UpdateOrderLock(view.Entry.IsOrderBubble)) unlocked = true;
        }

        if (unlocked) CheckBoardState();
    }

    /// <summary>
    /// Bóng chìa vừa hết cá (model đã mở ổ trong RemoveFishAt): rút token chìa khỏi đàn (BubbleFishContainer.DetachKey) và cho bay sang
    /// ổ vừa mở mà view còn hiện ổ; tới nơi ổ mới mở hình (UpdateKeyLock) và cá trong ổ mới bấm được (TryResolveSource chặn theo
    /// IsKeyLockShown). Ổ không có view (còn trong hàng đợi) thì không bay, spawn sau đã mở sẵn. Model mở nhiều ổ một lúc (không xảy ra
    /// với data hợp lệ) thì chỉ ổ đầu nhận chìa, ổ khác mở ngay trong RefreshLocks.
    /// </summary>
    private void LaunchKeyFlight(BubbleView keyView)
    {
        KeyView key = keyView.FishContainer != null ? keyView.FishContainer.DetachKey() : null;
        BubbleView target = null;
        foreach (BubbleView view in views)
        {
            if (view == null || view == keyView || view.Entry == null || view.Entry.IsLock || !view.IsKeyLockShown || keyLocksAwaitingKey.Contains(view)) continue;
            target = view;
            break;
        }

        if (key == null) return;
        if (target == null)
        {
            Destroy(key.gameObject);
            return;
        }

        keyLocksAwaitingKey.Add(target);
        FlyKeyAsync(key, target).Forget();
    }

    /// <summary>
    /// Token chìa bay cong lên rồi hạ vào icon ổ khoá (KeyLockVisual bám tâm bóng) trong KeyFlyDuration giây, giữ nguyên cỡ; tới nơi thì
    /// huỷ token và mở hình ổ. Tính là một chuyến bay (pendingFlights) nên bàn bận và CheckBoardState đợi tới khi ổ mở.
    /// </summary>
    private async UniTaskVoid FlyKeyAsync(KeyView key, BubbleView target)
    {
        pendingFlights++;
        try
        {
            key.transform.SetParent(FlyRoot, true);
            Transform anchor = target.KeyLockVisual != null ? target.KeyLockVisual : target.transform;
            Vector3 start = key.transform.position;
            Vector3 end = anchor.position;
            Vector3 mid = Vector3.Lerp(start, end, 0.5f) + Vector3.up * 1.2f;
            float scale = key.transform.localScale.x;
            await FishFlight.TestFly(key.Fish, anchor, new[] { start, mid, end }, scale, Config.KeyFlyDuration, Token, end.x >= start.x ? Vector3.right : Vector3.left);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            pendingFlights--;
            keyLocksAwaitingKey.Remove(target);
            if (key != null) Destroy(key.gameObject);
            if (Model != null && target != null && target.Entry != null && target.UpdateKeyLock(target.Entry.IsLock)) PlaySfx(AudioKey.SfxFishTap);
            if (Model != null) CheckBoardState();
        }
    }
    // ---------- Bóng băng (IceBubble) ----------

    /// <summary>
    /// Bóng băng chỉ vỡ khi mép trên chạm đỉnh vùng chơi (cách yMax không quá IceBreakTopMargin) liên tục IceBreakHoldTime giây: bóng
    /// nổi lên, còn bóng khác nằm trên thì chưa tới đỉnh nên chưa vỡ; bóng trên nổ dần thì bóng băng trồi lên đỉnh và vỡ. Bóng đá ở đỉnh
    /// cũng chắn như bóng thường (bóng băng phải trôi sang cạnh đá mới lên được); chưa có rect thì không vỡ. Vỡ thì model bỏ cờ, view
    /// đổi material / config, bàn kiểm tra lại.
    /// </summary>
    private void TickIce(float dt)
    {
        if (Model == null || IsCompleted || IsStuck) return;
        GamePlayRectManager rectManager = GamePlayRectManager.Instance;
        if (rectManager == null || !rectManager.HasWorldRect) return;
        float topY = rectManager.WorldRect.yMax - Config.IceBreakTopMargin;
        bool broke = false;
        foreach (BubbleView view in views)
        {
            if (view == null || view.Entry == null || !view.Entry.IsFrozen || view.Bubble == null) continue;
            bool touchingTop = view.Bubble.Center.y + view.Bubble.CurrentRadius >= topY;
            if (!view.TickTopContact(touchingTop, Config.IceBreakHoldTime, dt)) continue;
            if (!Model.TryBreakIce(view.Entry)) continue;
            view.BreakIce();
            PlaySfx(AudioKey.SfxSmokeClear);   // key chưa gán clip thì AudioManager tự bỏ qua
            broke = true;
        }

        if (!broke) return;
        CheckBoardState();
    }

    // ---------- Play On ----------

    /// <summary>Thêm ô chờ (ExtraSlot, Play On) tới trần. Trả về false nếu đã chạm trần.</summary>
    public bool SetHoldingSlots(int count)
    {
        if (Model == null) return false;
        count = Mathf.Min(count, Model.MaxHoldingSlots, Level.MaxHoldingSlots);
        if (count <= Model.HoldingSlots) return false;
        while (Model.HoldingSlots < count && Model.AddHoldingSlot())
        {
        }

        Level.LayoutHolding(Model.HoldingSlots);
        IsStuck = false;
        RefreshHoldingWarning();
        return true;
    }

    // ---------- Booster ----------

    public bool CanUseBooster(ResourceType type)
    {
        if (Model == null || IsCompleted) return false;
        return type switch
        {
            ResourceType.AddSlot => Model.HoldingSlots < Mathf.Min(Config.BoosterHoldingSlotsMax, Level.MaxHoldingSlots),
            ResourceType.Shuffle => Model.FrontFishBubbleCount >= 2 && Model.CanTap && !IsBusy,   // ô chờ đầy: bấm bị khoá, trộn vô ích
            ResourceType.PackedUp => Model.HoldingFishCount > 0 && !IsBusy,   // có cá đang đậu trong ô chờ để gom
            ResourceType.UnlockTank => Level.LocksShown && Model.CanAddTank && Level.ActiveTankCount < Mathf.Min(Config.MaxTankSlots, Level.Tanks.Length) && !IsBusy,
            ResourceType.AutoFill => AnyFillableTank() && !IsBusy,
            ResourceType.Swap => Model.OrderQueue.Count > 0 && !IsBusy,
            _ => false
        };
    }

    /// <summary>Chờ cá tự bấm lên ô chờ hạ xong tối đa bấy nhiêu giây (PrefillHoldingAsync) rồi trả về dù bàn còn bận.</summary>
    private const float PrefillWaitSeconds = 5f;

    /// <summary>
    /// Ép dùng thử PackedUp khi mở khóa (BoosterSelector.GetBoosterOnTutorial): tự bấm cá lên ô chờ cho tới khi có count con (tính cả
    /// con đang có), cách nhau HoldingPrefillInterval như bấm tay, để lần dùng thử có cá để gom. Luôn chừa ít nhất một ô trống nên bàn
    /// vẫn bấm được, không kẹt. Chọn cá (BoardModel.TryFindHoldingPrefillSource): loài không bể nào đang mở nhận (khỏi bị tự thu lên bể),
    /// ưu tiên loài khác nhau và bóng khác nhau, cá thường trong bóng bấm được không spawner còn ≥ 2 con; thiếu thì nới dần: cùng bóng →
    /// trùng loài → cá ẩn / spawner / con cuối. Xong thì chờ cá hạ hết (bàn hết bận, tối đa PrefillWaitSeconds) vì CanUseBooster đòi
    /// bàn không bận.
    /// </summary>
    public async UniTask PrefillHoldingAsync(int count)
    {
        if (Model == null || IsCompleted || IsStuck) return;
        int target = Mathf.Min(count, Model.HoldingSlots - 1);
        int need = target - (Model.HoldingSlots - Model.HoldingFreeCount);   // ô đã có cá hoặc đang giữ chỗ cho cá bay tới
        var usedSpecies = new HashSet<int>();
        var usedBubbles = new HashSet<BubbleEntry>();
        try
        {
            for (int n = 0; n < need; n++)
            {
                if (Model == null || !TryPrefillOne(usedSpecies, usedBubbles)) break;
                if (Config.HoldingPrefillInterval > 0f) await UniTask.Delay(TimeSpan.FromSeconds(Config.HoldingPrefillInterval), cancellationToken: Token);
            }

            float deadline = Time.time + PrefillWaitSeconds;
            while (Model != null && IsBusy && Time.time < deadline) await UniTask.Yield(Token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>Một con cho PrefillHoldingAsync: nới điều kiện chọn dần tới khi tìm được, rồi gỡ khỏi bóng và bay lên ô chờ trống trái nhất.</summary>
    private bool TryPrefillOne(HashSet<int> usedSpecies, HashSet<BubbleEntry> usedBubbles)
    {
        int holdingIndex = Model.FindFreeHoldingIndex();
        if (holdingIndex < 0) return false;
        if (!Model.TryFindHoldingPrefillSource(usedSpecies, usedBubbles, true, out BubbleEntry entry, out int index)
            && !Model.TryFindHoldingPrefillSource(usedSpecies, null, true, out entry, out index)
            && !Model.TryFindHoldingPrefillSource(null, usedBubbles, true, out entry, out index)
            && !Model.TryFindHoldingPrefillSource(null, null, true, out entry, out index)
            && !Model.TryFindHoldingPrefillSource(null, null, false, out entry, out index)) return false;
        BubbleView view = FindView(entry);
        if (view == null || index >= view.FishContainer.Fishes.Count) return false;
        Fish fish = view.FishContainer.Fishes[index];
        int fishId = entry.FishIdAt(index);
        usedSpecies.Add(fishId);
        usedBubbles.Add(entry);
        ClearHint();
        RemoveFishFromBubble(view, fish, index);
        Model.ReserveHolding(holdingIndex);
        AfterFishLeft(view);
        FlyToHolding(fish, fishId, holdingIndex).Forget();
        PlaySfx(AudioKey.SfxFishTap);
        return true;
    }

    /// <summary>Booster UnlockTank: mở ô cắm khóa kế tiếp, bể (đơn kế) rơi vào và tự thu cá từ ô chờ.</summary>
    public bool TryUnlockTank()
    {
        if (!CanUseBooster(ResourceType.UnlockTank)) return false;
        TankEntry tank = Model.AddTank();
        if (tank == null) return false;
        RefreshLocks();
        Level.UnlockNextTank();
        RefreshTankAsync(tank.Index).Forget();
        IsStuck = false;
        return true;
    }

    /// <summary>AutoFill đổ cho bấy nhiêu bể, duyệt trái sang phải qua mọi ô cắm; bể đã giải xong / không đổ được thì xét bể kế tiếp.</summary>
    private const int AutoFillTankCount = 2;

    /// <summary>Có bể (bất kỳ ô cắm nào) đang đậu yên và còn thiếu cá.</summary>
    private bool AnyFillableTank()
    {
        for (int t = 0; t < Model.Tanks.Count; t++)
        {
            TankEntry tank = Model.Tanks[t];
            if (tank.HasOrder && tank.Missing > 0 && IsTankReady(tank)) return true;
        }

        return false;
    }

    /// <summary>MOVE-02: tô sáng (phóng to nhịp) một con cá nên bấm trong HintHighlightDuration giây.</summary>
    public bool TryUseHint()
    {
        // Hint không còn là booster (AddSlot = thêm ô chờ); giữ hàm cho tutorial / debug.
        if (Model == null || IsCompleted || IsBusy || !Model.CanTap || !Model.FindHint(out BubbleEntry bubble, out int fishIndex)) return false;
        return StartHintPulse(bubble, fishIndex) != null;
    }

    /// <summary>Idle hint / tutorial L1: phóng nhịp một con cá khớp bể đã đậu (BoardModel.FindMatchingHint). false = không có con nào.</summary>
    public bool TryShowMatchingHint(out Fish fish)
    {
        fish = null;
        if (Model == null || IsCompleted || !Model.FindMatchingHint(IsTankReady, out BubbleEntry bubble, out int fishIndex)) return false;
        fish = StartHintPulse(bubble, fishIndex);
        return fish != null;
    }

    public void ClearHintPulse() => ClearHint();

    private Fish StartHintPulse(BubbleEntry bubble, int fishIndex)
    {
        BubbleView view = FindView(bubble);
        if (view == null || view.FishContainer == null || fishIndex >= view.FishContainer.Fishes.Count) return null;
        ClearHint();
        hintFish = view.FishContainer.Fishes[fishIndex];
        Transform t = hintFish.Graphic != null ? hintFish.Graphic : hintFish.transform;
        Vector3 baseScale = t.localScale;
        hintHandle = LMotion.Create(0f, 1f, Config.HintHighlightDuration)
            .Bind(u => { if (t != null) t.localScale = baseScale * (1f + 0.18f * Mathf.Abs(Mathf.Sin(u * Config.HintHighlightDuration * Mathf.PI * 2f))); })
            .AddTo(this);
        return hintFish;
    }

    private void ClearHint()
    {
        hintHandle.TryCancel();
        if (hintFish != null)
        {
            Transform t = hintFish.Graphic != null ? hintFish.Graphic : hintFish.transform;
            if (t != null) t.localScale = Vector3.one;
        }

        hintFish = null;
    }

    /// <summary>Lý do booster không dùng được (BoosterSelector chọn thông báo). None = dùng được.</summary>
    public BoosterBlockReason GetBoosterBlockReason(ResourceType type)
    {
        if (CanUseBooster(type)) return BoosterBlockReason.None;
        if (Model != null && type == ResourceType.Shuffle && !Model.CanTap) return BoosterBlockReason.HoldingFull;
        if (Model != null && IsBusy && type != ResourceType.AddSlot) return BoosterBlockReason.Busy;   // AddSlot không phụ thuộc IsBusy
        return BoosterBlockReason.NotAvailable;
    }

    /// <summary>
    /// Booster Shuffle: model xếp lại cá có chủ đích (BoardModel.ShuffleFrontBubbles: bể đang mở có đủ cá trên bàn thì gom cụm cá của
    /// bể đó vào bóng trên cùng còn chứa được cả cụm, tính theo độ cao tâm bóng lúc bấm), rồi cá đổi bóng bay theo đường cong
    /// (FishFlight) sang điểm đã giữ trong đàn đích; cá ở lại bóng cũ giữ nguyên chỗ. Trong lúc bay bàn bận (IsBusy) và bấm bị chặn vì
    /// đàn chưa khớp model. Bay xong mỗi đàn sắp lại thứ tự theo Entry.Fishes và cá dựng lại model theo cờ ẩn mới.
    /// </summary>
    public bool TryUseShuffle()
    {
        if (!CanUseBooster(ResourceType.Shuffle)) return false;
        var shuffled = new List<BubbleView>();
        foreach (BubbleView view in views)
        {
            if (view == null || !view.IsFront || !view.Entry.IsTappable || view.Entry.FishCount == 0) continue;   // đá / băng / vỏ HP không tham gia
            if (view.Entry.IsFishSchool) continue;   // bóng đàn giữ nguyên cá (model cũng bỏ qua)
            shuffled.Add(view);
        }

        // Bóng đích của cụm cá gom cho bể là bóng trên cùng: model không biết vị trí nên view đưa độ cao tâm bóng lúc bấm.
        var heights = new Dictionary<BubbleEntry, float>();
        foreach (BubbleView view in shuffled)
        {
            if (view.Bubble != null) heights[view.Entry] = view.Bubble.Center.y;
        }

        if (!Model.ShuffleFrontBubbles(rng, Config.ShuffleGroupCount, entry => heights.TryGetValue(entry, out float y) ? y : float.MinValue)) return false;
        ClearHint();
        ShuffleFlightsAsync(shuffled).Forget();
        return true;
    }

    private async UniTaskVoid ShuffleFlightsAsync(List<BubbleView> shuffled)
    {
        pendingShuffles++;
        try
        {
            var targetOf = new Dictionary<FishEntry, BubbleView>();
            foreach (BubbleView view in shuffled)
            {
                foreach (FishEntry f in view.Entry.Fishes) targetOf[f] = view;
            }

            // Xuất phát lệch dần từng con, không chia đợt: xáo thứ tự ngẫu nhiên, con thứ i đợi i × bước cộng lệch ngẫu nhiên dưới một
            // bước; bước co lại để tổng lệch không quá ShuffleStaggerMax. Bước nhỏ hơn thời gian bay nên con sau cất cánh khi con trước
            // còn đang bay. Cá chưa tới lượt vẫn bơi trong đàn cũ (bấm đã bị chặn nên lệch chỉ số không sao), chỉ rời đàn lúc xuất phát.
            var moving = new List<(Fish fish, BubbleView from, BubbleView to)>();
            foreach (BubbleView from in shuffled)
            {
                foreach (Fish fish in from.FishContainer.Fishes)
                {
                    if (fish == null || fish.Entry == null) continue;
                    if (!targetOf.TryGetValue(fish.Entry, out BubbleView to) || to == from) continue;   // ở lại bóng cũ
                    moving.Add((fish, from, to));
                }
            }

            for (int i = moving.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (moving[i], moving[j]) = (moving[j], moving[i]);
            }

            float step = Config.ShuffleStaggerStep;
            if (moving.Count > 1) step = Mathf.Min(step, Config.ShuffleStaggerMax / (moving.Count - 1));

            // Báo trước cho đàn cũ con nào sắp đi (đứng trôi tại chỗ tới lượt cất cánh, không tính vào số điểm, nhường điểm của mình), rồi
            // giữ chỗ cho mọi con ngay từ đầu: bộ điểm của bóng đích dựng một lần cho đúng số cá cuối cùng (cá ở lại + cá tới), nên cá tới
            // hạ thẳng vào bố cục đúng, xong shuffle là chuẩn ngay. Giữ chỗ rải rác theo lúc xuất phát thì mỗi lần thêm chỗ bộ điểm đổi
            // mẫu, mọi điểm dời và con đã hạ cánh bị kéo đi chỗ khác, nhìn khựng.
            foreach (var (fish, from, _) in moving) from.FishContainer.MarkLeaving(fish);
            var anchors = new Transform[moving.Count];
            for (int i = 0; i < moving.Count; i++) anchors[i] = moving[i].to.FishContainer.ReserveArrival(moving[i].fish);

            // Thời gian bay mỗi con khác nhau: xa bay lâu hơn gần (0.75..1.25 lần theo quãng đường so với con xa nhất) rồi lệch ngẫu
            // nhiên thêm ±ShuffleFlyDurationVariance, để cả đàn không cất cánh và về đích đều tăm tắp.
            var distances = new float[moving.Count];
            float maxDistance = 0.01f;
            for (int i = 0; i < moving.Count; i++)
            {
                distances[i] = Vector3.Distance(moving[i].fish.transform.position, anchors[i] != null ? anchors[i].position : moving[i].to.Bubble.Center);
                maxDistance = Mathf.Max(maxDistance, distances[i]);
            }

            var flights = new List<UniTask>();
            for (int i = 0; i < moving.Count; i++)
            {
                float delay = i * step + (float)rng.NextDouble() * step;
                float distanceFactor = Mathf.Lerp(0.75f, 1.25f, distances[i] / maxDistance);
                float randomFactor = 1f + ((float)rng.NextDouble() * 2f - 1f) * Config.ShuffleFlyDurationVariance;
                float duration = Mathf.Max(0.15f, Config.ShuffleFlyDuration * distanceFactor * randomFactor);
                flights.Add(FlyToBubble(moving[i].fish, moving[i].from, moving[i].to, anchors[i], delay, duration));
            }

            await UniTask.WhenAll(flights);
            if (Model == null) return;
            foreach (BubbleView view in shuffled)
            {
                if (view == null || view.Entry == null) continue;
                view.FishContainer.ReorderToMatch(view.Entry.Fishes);
                foreach (Fish fish in view.FishContainer.Fishes) fish.RefreshGraphic();   // cờ ẩn có thể đã đổi (TryAutoRevealAll)
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            pendingShuffles--;
            CheckBoardState();
        }
    }

    /// <summary>
    /// Cá bay sang bóng khác khi Shuffle: đợi tới lượt (delay) rồi rời đàn cũ, bay cong lên và hạ vào điểm đã giữ trước (anchor) trong
    /// đàn đích, trong duration giây (mỗi con một thời gian riêng).
    /// </summary>
    private async UniTask FlyToBubble(Fish fish, BubbleView from, BubbleView to, Transform anchor, float delay, float duration)
    {
        if (delay > 0f) await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: Token);
        if (Model == null || fish == null || from == null || to == null || to.FishContainer == null || anchor == null) return;
        from.FishContainer.Detach(fish);
        fish.transform.SetParent(FlyRoot, true);
        fish.SetTappable(false);
        Vector3 start = fish.transform.position;
        Vector3 end = anchor.position;
        Vector3 mid = Vector3.Lerp(start, end, 0.5f) + Vector3.up * 1.2f;
        // Tới đích nằm ngang theo chiều bay (tư thế nghỉ của đàn là nhìn ±X) để FishSchool nhận cá không phải xoay lại; đích bám theo bóng trôi.
        Vector3 endForward = end.x >= start.x ? Vector3.right : Vector3.left;
        await FishFlight.TestFly(fish, anchor, new[] { start, mid, end }, 1f, duration, Token, endForward);
        if (Model == null || to == null || to.FishContainer == null) return;
        to.FishContainer.Arrive(fish, anchor);
    }

    public bool TryAddHoldingSlot() => Model != null && SetHoldingSlots(Model.HoldingSlots + 1);

    public void BeginSelectTank(ResourceType type) => selectingBooster = type;

    public void CancelSelectTank() => selectingBooster = ResourceType.None;

    /// <summary>Booster hai pha chọn bể. Swap: đưa đơn xuống cuối hàng đợi. (Magnet cũ đã thay bằng AutoFill chạy ngay.)</summary>
    public bool TryUseTankBooster(ResourceType type, TankSlotView slot)
    {
        selectingBooster = ResourceType.None;
        if (Model == null || slot == null || slot.Index >= Model.Tanks.Count) return false;
        TankEntry tank = Model.Tanks[slot.Index];
        if (!tank.HasOrder || !slot.IsReady) return false;
        return type switch
        {
            ResourceType.Swap => UseSwap(tank, slot),
            _ => false
        };
    }

    /// <summary>
    /// Booster AutoFill: đổ đủ cá còn thiếu cho AutoFillTankCount bể, duyệt trái sang phải qua mọi ô cắm (kể cả bể mở thêm bằng
    /// UnlockTank); bể đã giải xong / hết đơn / đang rơi vào / bay đi thì xét bể kế tiếp, chỉ bể thật sự được đổ mới tính vào số bể. Mỗi con lấy
    /// theo ưu tiên: ô chờ → bóng bấm được → bóng vỏ HP → bóng băng → bóng khoá chìa / khoá đơn (trong mỗi hạng: cá thường → cá ẩn → hàng đợi spawner); không
    /// còn đâu trên bàn thì ứng một con từ đáy màn và ghi nợ (BoardModel.FishDebt, trả khi bóng có loài đó lên bàn). Cá bay vào bể
    /// bằng đường bay thường, bể đủ cá tự chốt; băng, vỏ HP và khoá giữ nguyên. Trả về true nếu có ít nhất một con được đưa đi.
    /// </summary>
    public bool TryUseAutoFill()
    {
        if (!CanUseBooster(ResourceType.AutoFill)) return false;
        ClearHint();
        int moved = 0;
        int filledTanks = 0;
        for (int t = 0; t < Model.Tanks.Count && filledTanks < AutoFillTankCount; t++)
        {
            TankEntry tank = Model.Tanks[t];
            if (!tank.HasOrder || tank.Missing <= 0 || !IsTankReady(tank)) continue;
            int movedForTank = 0;
            int guard = 0;
            while (tank.Missing > 0 && guard++ < 16)
            {
                if (!AutoFillOne(tank)) break;
                moved++;
                movedForTank++;
            }

            if (movedForTank > 0) filledTanks++;
        }

        return moved > 0;
    }

    /// <summary>Một con cho bể: ô chờ → bóng theo ưu tiên → ứng nợ. Trả về false nếu không đưa được con nào (không nên xảy ra).</summary>
    private bool AutoFillOne(TankEntry tank)
    {
        int fishId = tank.FishId;

        // 1) Ô chờ: lấy trước để giải phóng ô.
        int holdingIndex = Model.FindHoldingFish(fishId);
        if (holdingIndex >= 0)
        {
            HoldingSlotView holdingSlot = Level.HoldingAt(holdingIndex);
            Fish held = holdingSlot != null ? holdingSlot.Take() : null;
            Model.TakeFromHolding(holdingIndex);
            RefreshHoldingWarning();
            if (held == null) return false;
            ReserveTank(tank);
            held.transform.SetParent(FlyRoot, true);
            FlyToTank(held, fishId, tank, true).Forget();
            return true;
        }

        // 2) Bóng trên bàn theo ưu tiên (model quyết định, view làm theo).
        if (Model.TryFindAutoFillSource(fishId, out BubbleEntry entry, out int index, out AutoFillSourceKind kind, out int layer))
        {
            BubbleView view = FindView(entry);
            if (view == null) return false;
            if (kind == AutoFillSourceKind.Visible)
            {
                if (index >= view.FishContainer.Fishes.Count) return false;
                Fish fish = view.FishContainer.Fishes[index];
                RemoveFishFromBubble(view, fish, index);   // cá ẩn lộ lúc bay (FlyToTank); bóng spawner refill / bóng nhiều lớp đổi lớp như bấm tay
                ReserveTank(tank);
                AfterFishLeft(view);
                FlyToTank(fish, fishId, tank, true).Forget();
                return true;
            }

            if (kind == AutoFillSourceKind.Layer)
            {
                // Lớp còn ẩn của MultilayerBubble: gỡ khỏi model, sinh cá tại tâm bóng (kể cả đang bị băng che) rồi bay thẳng vào bể; đàn đang hiện không đổi.
                FishEntry hidden = Model.RemoveLayerFishAt(entry, layer, index);
                RefreshMultiLayer(view);   // vừa rút nốt cá của lớp ẩn cuối thì bóng thành bóng thường
                Fish fromLayer = CreateLooseFish(hidden, view.Bubble.Center, Config.AutoFillSpawnScale);
                if (fromLayer == null) return false;
                ReserveTank(tank);
                FlyToTank(fromLayer, fishId, tank, true).Forget();
                return true;
            }

            // Hàng đợi spawner: gỡ khỏi model, sinh cá tại chỗ con spawner (kể cả đang bị băng che) rồi bay thẳng vào bể.
            FishEntry taken = Model.RemoveSpawnerFishAt(entry, index);
            Vector3 origin = view.FishContainer.Spawner != null ? view.FishContainer.Spawner.transform.position : view.Bubble.Center;
            view.FishContainer.UpdateSpawner(entry.SpawnerCount, Config.SpawnerHideDuration);
            Fish fromQueue = CreateLooseFish(taken, origin, Config.AutoFillSpawnScale);
            if (fromQueue == null) return false;
            ReserveTank(tank);
            FlyToTank(fromQueue, fishId, tank, true).Forget();
            return true;
        }

        // 3) Không còn đâu trên bàn: ứng một con từ đáy màn và ghi nợ.
        Vector3 bottom = Level.SpawnPoint != null ? Level.SpawnPoint.position : transform.position + Vector3.down * 10f;
        Fish debtFish = CreateLooseFish(new FishEntry(fishId), bottom, Config.AutoFillSpawnScale);
        if (debtFish == null) return false;
        Model.AddDebt(fishId);
        ReserveTank(tank);
        FlyToTank(debtFish, fishId, tank, true).Forget();
        return true;
    }

    /// <summary>Cá không thuộc đàn nào (AutoFill: từ hàng đợi spawner hoặc ứng từ đáy màn): sinh dưới FlyRoot tại worldPosition, đợi FlyToTank.</summary>
    private Fish CreateLooseFish(FishEntry entry, Vector3 worldPosition, float scale)
    {
        Fish prefab = Level.BubblePrefab != null && Level.BubblePrefab.FishContainer != null ? Level.BubblePrefab.FishContainer.FishPrefab : null;
        if (prefab == null)
        {
            Debug.LogError("BoardManager: không có Fish prefab (Level.BubblePrefab.FishContainer.FishPrefab) để sinh cá AutoFill.", this);
            return null;
        }

        Fish fish = Instantiate(prefab, worldPosition, Quaternion.identity, FlyRoot);
        fish.name = $"Fish_{entry.FishId}";
        fish.Initialize(entry);
        if (fish.Swimmer != null)
        {
            fish.Swimmer.StopSwimming();
            fish.Swimmer.enabled = false;
        }

        fish.SetTappable(false);
        fish.transform.localScale = Vector3.one * scale;
        return fish;
    }

    // ---------- Túi PackedUp ----------

    /// <summary>
    /// Booster PackedUp: gom mọi cá đang đậu trong ô chờ vào túi ở cuối hàng (chưa có túi thì sinh, có rồi thì gom thêm). Model gom
    /// ngay nên ô chờ trống lại và bấm được; view rút cá từ các ô, bay và thu nhỏ vào túi rồi huỷ, túi nhún theo từng con. Cá đang bay
    /// từ ô chờ vào bể đã rời ô nên không tính; cá đang bay tới ô chờ vẫn hạ vào ô. Túi không nhận cá bấm thêm: đầy ô lại thì phải
    /// dùng booster lần nữa. Trả về true nếu gom được ít nhất một con (tính phí).
    /// </summary>
    public bool TryUsePackedUp()
    {
        if (!CanUseBooster(ResourceType.PackedUp)) return false;
        return PackHoldingIntoBag();
    }

    /// <summary>
    /// Play On khi kẹt vì đầy ô chờ: gom mọi cá đang đậu trong ô chờ vào túi như booster PackedUp (không tính phí booster), bỏ cờ kẹt và
    /// chơi tiếp. Không cộng thêm ô chờ như trước. Trả về false nếu không có cá để gom.
    /// </summary>
    public bool ReviveWithBag()
    {
        if (Model == null || IsCompleted) return false;
        IsStuck = false;
        bool packed = PackHoldingIntoBag();
        RefreshHoldingWarning();
        return packed;
    }

    /// <summary>Gom cá ô chờ vào túi: model gom ngay, view rút cá từ các ô bay vào túi. Dùng chung cho booster PackedUp và Play On.</summary>
    private bool PackHoldingIntoBag()
    {
        List<int> packed = Model.PackHolding();
        if (packed.Count == 0) return false;
        HoldingBagView bag = Level.ShowBag();
        if (bag == null) return false;
        ClearHint();
        RefreshHoldingWarning();
        PlaySfx(AudioKey.SfxBoosterPackedUp);
        bag.SetCount(Model.Bag.Count);
        foreach (int index in packed)
        {
            HoldingSlotView slot = Level.HoldingAt(index);
            Fish fish = slot != null ? slot.Take() : null;
            if (fish == null) continue;
            fish.transform.SetParent(FlyRoot, true);
            fish.SetTappable(false);
            FlyToBag(fish, bag).Forget();
        }

        return true;
    }

    /// <summary>Cá từ ô chờ bay vào túi: bay cong tới Anchor túi (túi đang trượt thì đích bám theo), thu nhỏ rồi huỷ view; túi nhún.</summary>
    private async UniTask FlyToBag(Fish fish, HoldingBagView bag)
    {
        pendingFlights++;
        pendingBagArrivals++;
        try
        {
            Transform anchor = bag.AnchorTransform;
            Vector3 start = fish.transform.position;
            Vector3 mid = Vector3.Lerp(start, anchor.position, 0.5f) + Vector3.up * 0.8f;
            await FishFlight.TestFly(fish, anchor, new[] { start, mid, anchor.position }, 0.05f, Config.FishMoveDuration, Token, Vector3.down);
            if (fish != null) Destroy(fish.gameObject);
            if (bag != null) bag.Push();
            PlaySfxPitched(AudioKey.SfxBagSuck, UnityEngine.Random.Range(0.95f, 1.1f));
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            pendingFlights--;
            pendingBagArrivals--;
            if (pendingBagArrivals == 0 && Model != null) RequestAutoCollect();   // cá vào túi xong mới xét nhả vào bể
            CheckBoardState();
        }
    }

    /// <summary>
    /// Tự thu từ túi (sau khi ô chờ không còn con nào khớp): con đầu tiên trong túi có bể đang đậu nhận thì sinh cá tại Anchor túi, bay
    /// vào bể kiểu nằm ngang; túi nhún hụt, đổi số; hết cá thì túi biến mất. Đợi cá đang bay vào túi hạ hết mới nhả.
    /// </summary>
    private void TryCollectFromBag()
    {
        if (Model == null || !Model.HasBag || Level.Bag == null || pendingBagArrivals > 0) return;
        if (!Model.TryFindBagCollect(out int bagIndex, out TankEntry tank, IsTankReady)) return;
        HoldingBagView bag = Level.Bag;
        int fishId = Model.TakeFromBag(bagIndex);
        Fish fish = CreateLooseFish(new FishEntry(fishId), bag.AnchorTransform.position, Config.AutoFillSpawnScale);
        bag.Pop();
        bag.SetCount(Model.Bag.Count);
        if (Model.Bag.Count == 0) Level.HideBag();
        if (fish == null) return;
        ReserveTank(tank);
        FlyToTank(fish, fishId, tank, true).Forget();
        autoCollectTimer = Mathf.Max(0.01f, Config.AutoCollectInterval);
    }

    /// <summary>Swap: cá đã đậu trả về ô chờ (phải đủ chỗ), bể cũ bay đi, bể kế rơi xuống, rồi tự thu cá.</summary>
    private bool UseSwap(TankEntry tank, TankSlotView slot)
    {
        if (Model.OrderQueue.Count == 0 || tank.Incoming > 0) return false;
        if (slot.Fishes.Count > Model.HoldingFreeCount) return false;
        List<Fish> parked = slot.TakeAll();
        int fishId = tank.FishId;
        Model.SwapOrder(tank);
        RefreshLocks();
        for (int i = 0; i < parked.Count; i++)
        {
            int holdingIndex = Model.FindFreeHoldingIndex();
            if (holdingIndex < 0) break;
            Model.ReserveHolding(holdingIndex);
            parked[i].transform.SetParent(FlyRoot, true);
            FlyToHolding(parked[i], fishId, holdingIndex).Forget();
        }

        RefreshTankAsync(tank.Index).Forget();
        return true;
    }


    private BubbleView FindView(BubbleEntry entry)
    {
        foreach (BubbleView v in views)
        {
            if (v != null && v.Entry == entry) return v;
        }

        return null;
    }

    /// <summary>Bóng đang chứa con cá này (thay GetComponentInParent): quét tối đa VisibleBubbleCount view.</summary>
    private BubbleView FindViewOf(Fish fish)
    {
        foreach (BubbleView v in views)
        {
            if (v != null && v.IndexOf(fish) >= 0) return v;
        }

        return null;
    }
}
