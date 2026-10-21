using System;
using System.Collections.Generic;
using Core.Manager;
using CoreData;
using Cysharp.Threading.Tasks;
using TW.Utility.CustomComponent;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(BoardManager))]
public class Level : ABetterMonoBehaviour
{
    [field: SerializeField] public LevelRect LevelRectQueue {get; private set;}
    [field: SerializeField] public BubbleView BubblePrefab { get; private set; }

    [field: SerializeField, Tooltip("Prefab bóng đá (variant của SoftBubble: config cứng hơn + model đá + collider bấm); trống thì dùng BubblePrefab")]
    public BubbleView RockBubblePrefab { get; private set; }

    [field: SerializeField, Tooltip("Config vật lý cứng (SoftBubbleConfig_Ice) dùng cho bóng khoá chìa / khoá đơn; luật va chạm cứng đặt cho world theo config này")]
    public SoftBubbleConfig IceConfig { get; private set; }

    [field: SerializeField, UnityEngine.Serialization.FormerlySerializedAs("<InkConfig>k__BackingField"), Tooltip("Bóng khói (isIce): config vật lý khi còn khói (SoftBubbleConfig_Smoke: mềm như bóng thường, nổi mạnh hơn để ham lên đỉnh); trống = giữ config nền")]
    public SoftBubbleConfig SmokeConfig { get; private set; }

    [field: SerializeField, Tooltip("Bóng khói (isIce): material lớp khói phủ lên bóng khi còn khói (M_SoftBubbleSmoke, shader Cartoon bật Smoke); trống = giữ material nền")]
    public Material IceMaterial { get; private set; }

    [field: SerializeField, Tooltip("MultiHpBubble: material vỏ HP đặt lên bóng khi Hp > 0 (che mờ cá); vỏ vỡ thì trả material nền. Trống = không đổi material")]
    public Material HpMaterial { get; private set; }

    [field: SerializeField, Tooltip("MultiHpBubble: còn vỏ thì cá bên trong chỉ hiện bóng đen bằng material này (một màu phẳng), vỏ vỡ thì trả material thật. Trống = cá vẫn hiện mờ qua vỏ")]
    public Material HpFishSilhouetteMaterial { get; private set; }

    /// <summary>Visual vỏ HP (MultiHpBubble): Frost = băng sương mờ; Snowflake = bóng tuyết (3 lớp ảnh matcap, bông tuyết rõ). Cả hai kiểu cá bên trong đều là bóng đen.</summary>
    public enum HpBubbleVisual
    {
        Frost = 0,
        Snowflake = 1,
    }

    [field: SerializeField, Tooltip("MultiHpBubble: kiểu visual vỏ HP. Frost = băng sương (HpMaterial). Snowflake = bóng tuyết (HpSnowMaterial, matcap 3 lớp). Cả hai: cá bên trong là bóng đen HpFishSilhouetteMaterial")]
    public HpBubbleVisual HpVisual { get; private set; } = HpBubbleVisual.Frost;

    [field: SerializeField, Tooltip("MultiHpBubble kiểu Snowflake: material vỏ bóng tuyết (shader bật Snowflake, fill đặc hơn bóng thường). Trống = dùng HpMaterial")]
    public Material HpSnowMaterial { get; private set; }

    [field: SerializeField, Tooltip("MultilayerBubble: material của bong bóng lồng bên trong khi còn lớp ẩn (BubbleView.SetHiddenLayers). Trống = dùng material nền của bóng")]
    public Material MultiLayerInnerMaterial { get; private set; }

    [field: SerializeField, Tooltip("FishSchoolBubble: material nền riêng của bóng đàn (bấm một con là cả đàn bay; giữ cả đời bóng, vỏ HP / băng vẫn phủ lên). Trống = dùng material của prefab")]
    public Material FishSchoolMaterial { get; private set; }

    [field: SerializeField, Tooltip("Lock & Key: material nền của bóng ổ khoá khi còn khoá (M_SoftBubbleCartoonLock: fill kính xanh navy mờ phủ đều để ổ khoá nổi hơn cá và nền phía sau; vỏ HP / băng vẫn phủ lên). Mở khoá thì trả material nền cũ. Trống = không phủ")]
    public Material KeyLockVeilMaterial { get; private set; }
    [field: SerializeField] public Transform HeadPoint { get; private set; }
    [field: SerializeField] public Transform SpawnPoint { get; private set; }
    [field: SerializeField] public Transform BubbleRoot { get; private set; }
    [field: SerializeField] public Transform FlyingRoot { get; private set; }
    [field: SerializeField] public TankSlotView[] Tanks { get; private set; } = Array.Empty<TankSlotView>();
    [field: SerializeField, Tooltip("Prefab ô chờ (HoldingSlot.prefab): ô chờ sinh động lúc StartGame và khi thêm ô, kiểu QueueHolder của WordSort")]
    public HoldingSlotView HoldingSlotPrefab { get; private set; }

    [field: SerializeField, Tooltip("Prefab túi PackedUp (HoldingBag.prefab), đứng cuối hàng ô chờ")]
    public HoldingBagView HoldingBagPrefab { get; private set; }

    /// <summary>Túi PackedUp đang hiện (null = không có).</summary>
    public HoldingBagView Bag { get; private set; }

    [field: SerializeField, Tooltip("Cha của hàng ô chờ (dưới hàng bể)")] public Transform HoldingRoot { get; private set; }
    [field: SerializeField, Min(0f)] public float TankSpacing { get; private set; } = 2.7f;
    [field: SerializeField, Min(0f)] public float HoldingSpacing { get; private set; } = 1.55f;

    [field: SerializeField, Min(0f), Tooltip("Bán kính lệch ngẫu nhiên quanh SpawnPoint khi sinh bóng (mọi bóng đều sinh tại SpawnPoint)")]
    public float SpawnRadius { get; private set; } = 1f;

    [field: SerializeField, Min(0f), Tooltip("Vận tốc đẩy bóng mới từ SpawnPoint về giữa bàn (m/s); 0 = không đẩy")]
    public float SpawnPushSpeed { get; private set; } = 5f;

    [field: SerializeField, Range(0f, 90f), Tooltip("Góc lệch ngẫu nhiên (độ) của hướng đẩy quanh hướng lên trên")]
    public float SpawnPushSpread { get; private set; } = 25f;

    [field: SerializeField, Min(0f), Tooltip("Khi sinh hàng loạt: chờ tối đa bấy nhiêu giây cho bóng trước rời khỏi SpawnPoint rồi mới sinh bóng kế (tránh dồn cục)")]
    public float SpawnClearTimeout { get; private set; } = 1f;

    [field: SerializeField, Min(0f), Tooltip("Gia tốc đẩy lên (m/s²) cho bóng đang nằm dưới mép dưới màn hình, để bóng sinh ở SpawnPoint nổi vào bàn nhanh; 0 = tắt")]
    public float RiseAccel { get; private set; } = 12f;

    [field: SerializeField, Min(0f), Tooltip("Bóng có tâm thấp hơn (mép dưới màn hình + RiseMargin) thì được đẩy lên")]
    public float RiseMargin { get; private set; } = 1f;

    [field: SerializeField, Min(0f), Tooltip("Khoảng cách thời gian giữa hai lần spawn (giây)")]
    public float SpawnInterval { get; private set; } = 0.1f;

    [field: SerializeField, Min(0f), Tooltip("Lúc loading: sau khi sinh bóng đầu màn, mô phỏng trước ngay trong frame đó (thời gian mô phỏng) cho bàn xếp xong như lúc " +
                                              "chơi, tối đa bấy nhiêu giây (dừng sớm khi mọi bóng chậm hơn SpawnWarmUpSettleSpeed), để khi màn loading đóng bóng đã nằm " +
                                              "sẵn trên bàn; 0 = tắt, bóng nổi lên từ SpawnPoint trước mắt người chơi như cũ")]
    public float SpawnWarmUpSeconds { get; private set; } = 6f;

    [field: SerializeField, Min(0f), Tooltip("Warm-up dừng sớm khi bóng nhanh nhất chậm hơn ngưỡng này (m/s), kiểm tra sau mỗi 0,25 s mô phỏng kể từ 0,75 s; hàng chờ dưới màn bị RiseAccel " +
                                              "ép liên tục nên không bao giờ về 0 hẳn, để quá thấp thì warm-up chạy hết SpawnWarmUpSeconds")]
    public float SpawnWarmUpSettleSpeed { get; private set; } = 0.75f;

    [field: SerializeField] public bool ClampToGamePlayRect { get; private set; } = true;

    [field: SerializeField, Range(1, 32), Tooltip("Số lần thử vị trí spawn để không đè bóng đang có")]
    public int SpawnAttempts { get; private set; } = 8;

    [field: SerializeField, Tooltip("BoardManager cùng GameObject (gán sẵn trên prefab Level)")]
    public BoardManager Board { get; private set; }

    [field: SerializeField] public LevelData Data { get; private set; }

    public IReadOnlyList<BubbleView> Bubbles => bubbles;

    public IReadOnlyList<HoldingSlotView> HoldingSlots => holdingSlots;

    public int MaxHoldingSlots => DefaultGlobalConfig.Instance != null ? Mathf.Max(1, DefaultGlobalConfig.Instance.MaxHoldingSlots) : 7;

    /// <summary>Level của người chơi (GameManager), không phải số trong file data (level vòng lặp dùng file khác). Không có GameManager (test) thì coi như level cao: luật thường.</summary>
    public int PlayerLevel => Core.GameManager.Instance != null ? Core.GameManager.Instance.Level.Value : int.MaxValue;

    /// <summary>Ô bể khóa đang được dựng (FtueLevelRules.ShowLockedTanks). false thì UnlockNextTank / booster UnlockTank không dùng được.</summary>
    public bool LocksShown { get; private set; } = true;

    /// <summary>Hàng ô chờ đang dồn lại (tween) sau khi thêm ô.</summary>
    public bool IsHoldingLayoutMoving
    {
        get
        {
            foreach (HoldingSlotView slot in holdingSlots)
            {
                if (slot != null && slot.IsMoving) return true;
            }

            return false;
        }
    }
    public bool IsCompleted => Board != null && Board.IsCompleted;
    public int ReviveByAdsCount { get; set; }
    public int ReviveByCoinCount { get; set; }
    public int ActiveTankCount { get; private set; }
    public int ActiveHoldingCount { get; private set; }
    public Core.GamePlay.GlobalEnum.LoseReason CurLoseReason { get; private set; } = Core.GamePlay.GlobalEnum.LoseReason.None;

    public event Action<BubbleView, BubbleEntry> OnBubbleSpawned;
    public event Action OnSpawnCompleted;

    /// <summary>Bước mô phỏng lúc warm-up: bằng một frame 60 fps để độ cứng / số substep giống lúc chơi.</summary>
    private const float WarmUpStepDt = 1f / 60f;

    /// <summary>Warm-up chạy từng đợt bấy nhiêu giây rồi mới xét ngưỡng dừng; đợt đầu tối thiểu WarmUpMinSeconds để bóng nở xong (BubbleGrowDuration) và bắt đầu nổi.</summary>
    private const float WarmUpChunkSeconds = 0.25f;
    private const float WarmUpMinSeconds = 0.75f;

    private readonly List<BubbleView> bubbles = new();
    private readonly List<HoldingSlotView> holdingSlots = new();
    private readonly Dictionary<ResourceType, int> boosterByAdsCount = new();
    private GamePlayRectManager rectManager;
    private bool isClaimBooster = true;
    private bool isBoosterClaimFlowDone = true;

    private static GamePlayGlobalConfig Config => GamePlayGlobalConfig.Instance;

    private void OnDestroy()
    {
        Clear();
    }

    public async UniTask StartGame(LevelData data)
    {
        Clear();
        Data = data;
        ReviveByAdsCount = 0;
        ReviveByCoinCount = 0;
        CurLoseReason = Core.GamePlay.GlobalEnum.LoseReason.None;
        boosterByAdsCount.Clear();
        if (data == null)
        {
            Debug.LogError("Level: LevelData null.", this);
            return;
        }
        // FishModelGlobalConfig.Instance.ShuffleFishModelConfigs();
        ApplyCheatFishPriority(data);
        rectManager = GamePlayRectManager.Instance;
        rectManager.FitCameraToQueue(LevelRectQueue);

        InstallContactRules();
        
        int playerLevel = PlayerLevel;
        LayoutTanks(Core.Tutorial.FtueLevelRules.OpenOrderSlots(playerLevel, Config.DefaultTankSlots),
            Core.Tutorial.FtueLevelRules.ShowLockedTanks(playerLevel));
        LayoutHolding(Config.DefaultHoldingSlots);
        Board.Build(data);
        Board.HoldingEnabled = Core.Tutorial.FtueLevelRules.HoldingEnabled(playerLevel);
        await Board.SpawnFrontBubblesAsync();
        OnSpawnCompleted?.Invoke();
    }

    /// <summary>Bản cheat: cá chọn ở panel cheat (CheatFishPriority) được ưu tiên làm các loài của level, thiếu thì lấy cá khác như thường.</summary>
    private static void ApplyCheatFishPriority(LevelData data)
    {
        if (Core.GameManager.Instance == null || Core.GameManager.Instance.BuildType != BuildType.Cheat) return;

        FishModelGlobalConfig config = FishModelGlobalConfig.Instance;
        List<FishModel> preferred = CheatFishPriority.ResolveModels(config.GetPickableFishModels());
        if (preferred.Count == 0) return;
        config.ApplyPriorityModels(data.OrderQueue, preferred);

        // Log loài -> model theo thứ tự đơn để tester đối chiếu với list đã chọn.
        var species = new List<int>();
        var line = new System.Text.StringBuilder($"[CheatFishPriority] L{data.Level} ({preferred.Count} chọn):");
        foreach (int id in data.OrderQueue)
        {
            if (species.Contains(id)) continue;
            species.Add(id);
            FishModelConfig entry = config.GetFishModelConfigById(id);
            line.Append($" {id}={(entry?.FishModel != null ? entry.FishModel.name : "?")}");
        }
        Debug.Log(line.ToString());
    }

    public void Clear()
    {
        if (Board != null) Board.Clear();
        foreach (BubbleView bubble in bubbles)
        {
            if (bubble != null) Destroy(bubble.gameObject);
        }

        bubbles.Clear();
        foreach (TankSlotView tank in Tanks)
        {
            if (tank != null) tank.Clear();
        }

        foreach (HoldingSlotView slot in holdingSlots)
        {
            if (slot != null) Destroy(slot.gameObject);
        }

        holdingSlots.Clear();
        ActiveHoldingCount = 0;
        if (Bag != null)
        {
            Destroy(Bag.gameObject);
            Bag = null;
        }
    }

    public TankSlotView TankAt(int index) => index >= 0 && index < Tanks.Length ? Tanks[index] : null;

    public HoldingSlotView HoldingAt(int index) => index >= 0 && index < holdingSlots.Count ? holdingSlots[index] : null;

    /// <summary>
    /// Hiện bể trong prefab (tối đa GamePlayGlobalConfig.MaxTankSlots), openCount bể đầu mở, còn lại khóa. showLocked = false (FTUE L1-L3)
    /// thì chỉ dựng các ô đang mở, căn giữa, không có ô "Locked +".
    /// </summary>
    private void LayoutTanks(int openCount, bool showLocked = true)
    {
        int total = Mathf.Clamp(Config.MaxTankSlots, 1, Tanks.Length);
        ActiveTankCount = Mathf.Clamp(openCount, 0, total);
        LocksShown = showLocked;
        if (!showLocked) total = Mathf.Max(1, ActiveTankCount);
        for (int i = 0; i < Tanks.Length; i++)
        {
            TankSlotView tank = Tanks[i];
            if (tank == null) continue;
            bool shown = i < total;
            tank.gameObject.SetActive(shown);
            if (!shown) continue;
            Vector3 local = tank.transform.localPosition;
            local.x = (i - (total - 1) * 0.5f) * TankSpacing;
            tank.transform.localPosition = local;
            tank.Setup(i);
            tank.SetLocked(i >= ActiveTankCount);
        }
    }

    /// <summary>Booster UnlockTank: mở bể kế tiếp đang khóa. Trả về view vừa mở, null nếu không còn.</summary>
    public TankSlotView UnlockNextTank()
    {
        if (!LocksShown) return null;
        int total = Mathf.Clamp(Config.MaxTankSlots, 1, Tanks.Length);
        if (ActiveTankCount >= total) return null;
        TankSlotView tank = Tanks[ActiveTankCount];
        ActiveTankCount++;
        if (tank != null) tank.SetLocked(false);
        return tank;
    }

    /// <summary>
    /// Đúng count ô chờ kiểu QueueCard của WordSort: ô mới sinh từ HoldingSlotPrefab lệch phải HoldingSpawnOffset rồi cả hàng
    /// trượt về vị trí căn giữa quanh x = 0 trong HoldingRelayoutDuration. Lần đầu (chưa có ô) đặt ngay. Cá đang đậu giữ nguyên.
    /// </summary>
    public void LayoutHolding(int count)
    {
        count = Mathf.Clamp(count, 0, MaxHoldingSlots);
        if (HoldingSlotPrefab == null)
        {
            Debug.LogError("Level: chưa gán HoldingSlotPrefab (HoldingSlot.prefab).", this);
            return;
        }

        Transform root = HoldingRoot != null ? HoldingRoot : transform;
        bool first = holdingSlots.Count == 0;
        while (holdingSlots.Count > count)
        {
            HoldingSlotView last = holdingSlots[^1];
            holdingSlots.RemoveAt(holdingSlots.Count - 1);
            if (last != null) Destroy(last.gameObject);
        }

        while (holdingSlots.Count < count)
        {
            int i = holdingSlots.Count;
            HoldingSlotView slot = Instantiate(HoldingSlotPrefab, root);
            slot.name = $"Slot_{i}";
            slot.transform.localRotation = Quaternion.identity;
            slot.transform.localScale = Vector3.one;
            Vector3 local = HoldingLocalPosition(i, count + (Bag != null ? 1 : 0));
            if (!first) local.x += Config.HoldingSpawnOffset;
            slot.transform.localPosition = local;
            slot.Setup(i);
            holdingSlots.Add(slot);
        }

        ActiveHoldingCount = holdingSlots.Count;
        float duration = first ? 0f : Config.HoldingRelayoutDuration;
        // Túi PackedUp (nếu có) chiếm vị trí cuối: ô chờ đứng cạnh nhau phía trước, cả hàng cách đều và căn giữa.
        int total = holdingSlots.Count + (Bag != null ? 1 : 0);
        for (int i = 0; i < holdingSlots.Count; i++)
        {
            if (holdingSlots[i] != null) holdingSlots[i].MoveTo(HoldingLocalPosition(i, total), duration);
        }

        if (Bag != null) Bag.MoveTo(HoldingLocalPosition(holdingSlots.Count, total), duration);
    }

    /// <summary>FTUE: ẩn / hiện cả hàng ô chờ (L1 ẩn hẳn, L2 kịch bản bật có animation). Model ô chờ không đổi.</summary>
    public void SetHoldingVisible(bool visible, bool animate)
    {
        if (HoldingRoot == null) return;   // không có root riêng thì không ẩn (ẩn transform của Level là ẩn cả bàn)
        bool wasActive = HoldingRoot.gameObject.activeSelf;
        HoldingRoot.gameObject.SetActive(visible);
        if (!visible || !animate || wasActive) return;
        for (int i = 0; i < holdingSlots.Count; i++)
        {
            if (holdingSlots[i] != null) holdingSlots[i].PlayReveal(i * 0.06f);
        }
    }

    /// <summary>
    /// Túi PackedUp: chưa có thì sinh ở cuối hàng ô chờ (lệch phải HoldingSpawnOffset rồi trượt vào, scale từ 0), có rồi thì trả túi
    /// hiện có. Hàng ô chờ xếp lại cách đều cùng túi.
    /// </summary>
    public HoldingBagView ShowBag()
    {
        if (Bag != null) return Bag;
        if (HoldingBagPrefab == null)
        {
            Debug.LogError("Level: chưa gán HoldingBagPrefab (HoldingBag.prefab).", this);
            return null;
        }

        Transform root = HoldingRoot != null ? HoldingRoot : transform;
        Bag = Instantiate(HoldingBagPrefab, root);
        Bag.name = "HoldingBag";
        Bag.transform.localRotation = Quaternion.identity;
        Vector3 local = HoldingLocalPosition(holdingSlots.Count, holdingSlots.Count + 1);
        local.x += Config.HoldingSpawnOffset;
        Bag.transform.localPosition = local;
        Bag.Show(Config.HoldingRelayoutDuration);
        LayoutHolding(holdingSlots.Count);
        return Bag;
    }

    /// <summary>Túi hết cá: co về 0 và huỷ, hàng ô chờ căn giữa lại.</summary>
    public void HideBag()
    {
        if (Bag == null) return;
        HoldingBagView bag = Bag;
        Bag = null;
        bag.HideAndDestroy(Config.HoldingRelayoutDuration);
        LayoutHolding(holdingSlots.Count);
    }

    /// <summary>Vị trí local của ô chờ thứ index trong hàng count ô, căn giữa quanh x = 0; y / z lấy theo prefab.</summary>
    private Vector3 HoldingLocalPosition(int index, int count)
    {
        Vector3 basePos = HoldingSlotPrefab != null ? HoldingSlotPrefab.transform.localPosition : Vector3.zero;
        return new Vector3((index - (count - 1) * 0.5f) * HoldingSpacing, basePos.y, basePos.z);
    }

    /// <summary>
    /// Spawn bóng cho một BubbleEntry tại SpawnPoint (lệch ngẫu nhiên, đẩy lên bàn). Bán kính: seed Radius > 0 thì dùng,
    /// không thì ngẫu nhiên trong [BubbleRadiusMin, BubbleRadiusMax] (không phụ thuộc số cá) và ghi lại vào entry.
    /// Prefab là BubbleView đã gán sẵn Bubble / FishContainer.
    /// </summary>
    public BubbleView SpawnBubble(BubbleEntry entry, System.Random boardRandom, float spawnProgress)
    {
        float startRadius = Config.BubbleStartRadius;
        float targetRadius = entry.Radius;
        Vector3 offset = Vector3.zero;
        offset.x = (float)boardRandom.NextDouble() * 3;
        offset.y = (float)boardRandom.NextDouble() * 3;
        Vector3 spawnPos = Vector3.Lerp(HeadPoint.position, SpawnPoint.position, spawnProgress)  + offset;

        Transform root = BubbleRoot != null ? BubbleRoot : transform;
        BubbleView prefab = entry.IsRock && RockBubblePrefab != null ? RockBubblePrefab : BubblePrefab;   // nền; băng phủ lên sau
        BubbleView view = Instantiate(prefab, spawnPos, Quaternion.identity, root);
#if UNITY_EDITOR
        string kind = entry.IsRock ? "Rock" : entry.Hp > 0 ? "Hp" : entry.HasSpawner ? "Spawner" : entry.HasLayers ? "MultiLayer" : "Bubble";
        view.name = entry.IsIce ? $"Ice{kind}_{entry.Index}" : $"{kind}_{entry.Index}";
#endif
        SoftBubble bubble = view.Bubble;
        bubble.SetRadius(startRadius);
        
        if (!entry.IsRock) view.FishContainer.Spawn(entry.Fishes);
        if (entry.HasSpawner) view.FishContainer.ShowSpawner(entry.SpawnerCount);   // con spawner vào cuối đàn; băng che luôn cùng đàn
        if (entry.HasLayers) view.SetHiddenLayers(entry.Layers.Count, MultiLayerInnerMaterial);   // còn lớp ẩn thì có một bong bóng lồng (hết lớp = bóng thường); trước Freeze để băng tắt luôn
        if (entry.IsFishSchool) view.SetBaseMaterial(FishSchoolMaterial);   // bóng đàn: material riêng cả đời (viền cầu vồng); đặt trước vỏ HP / băng để vỡ xong trả về đúng nó
        if (entry.Hp > 0)   // vỏ HP trước, băng phủ lên sau (băng vỡ trả về vỏ HP)
        {
            bool snow = HpVisual == HpBubbleVisual.Snowflake && HpSnowMaterial != null;
            view.ShowShell(entry.Hp, snow ? HpSnowMaterial : HpMaterial, HpFishSilhouetteMaterial);   // cả hai kiểu vỏ: cá bên trong là bóng đen
        }
        if (entry.IsFrozen) view.Freeze(SmokeConfig, IceMaterial);   // bóng khói: lớp khói phủ lên, config mềm riêng
        if (entry.IsLock) view.ShowKeyLock(IceConfig, Config.GetKeyLockMaterial(entry.KeyPairIndex), KeyLockVeilMaterial);   // ổ khoá tô màu theo cặp, bóng phủ kính mờ
        if (entry.IsKey) view.ShowKey(Config.GetKeyLockMaterial(entry.KeyPairIndex));                 // chìa cùng màu ổ nó mở
        if (entry.IsOrderBubble) view.ShowOrder(entry.FishOrderId, IceConfig);   // khoá đơn trước băng, để băng che huy hiệu

        bubble.SetRadius(targetRadius, Config.BubbleGrowDuration);
        bubbles.Add(view);
        OnBubbleSpawned?.Invoke(view, entry);
        return view;
    }

    public void DespawnBubble(BubbleView view)
    {
        if (view == null) return;
        bubbles.Remove(view);
        Destroy(view.gameObject);
    }

    /// <summary>
    /// Bóng nằm dưới mép dưới màn hình (vùng SpawnPoint) được đẩy lên đều mỗi frame cho tới khi vào tầm nhìn; warm-up lúc loading gọi
    /// PushRising từng bước mô phỏng để giống hệt lúc chơi.
    /// </summary>
    private void Update()
    {
        if (LevelManager.Instance != null && LevelManager.Instance.IsSimulationFrozen) return;
        PushRising(Time.deltaTime);
    }

    private void PushRising(float dt)
    {
        if (RiseAccel <= 0f || bubbles.Count == 0 || !TryGetVisibleBottom(out float visibleBottom)) return;
        float line = visibleBottom + RiseMargin;
        Vector3 push = Vector3.up * (RiseAccel * dt);
        foreach (BubbleView view in bubbles)
        {
            SoftBubble bubble = view != null ? view.Bubble : null;
            if (bubble != null && bubble.Center.y < line) bubble.AddVelocity(push);
        }
    }

    /// <summary>Mép dưới màn hình (world Y) trên mặt phẳng bóng theo camera chính.</summary>
    private bool TryGetVisibleBottom(out float visibleBottom)
    {
        visibleBottom = 0f;
        Camera cam = CameraManager.Instance != null ? CameraManager.Instance.MainCamera : null;
        if (cam == null) return false;
        if (cam.orthographic) visibleBottom = cam.transform.position.y - cam.orthographicSize;
        else
        {
            float distance = Mathf.Abs(cam.transform.position.z - transform.position.z);
            visibleBottom = cam.transform.position.y - distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        }

        return true;
    }

    /// <summary>
    /// BoardManager gọi ngay sau khi sinh xong bóng đầu màn (màn loading đang che): mô phỏng trước ngay trong frame này, với tường bàn
    /// và lực đẩy như lúc chơi, tới khi bàn đứng yên hoặc hết SpawnWarmUpSeconds. Nhờ vậy khi màn loading đóng bóng đã nằm sẵn trên
    /// bàn, không phải ngồi nhìn cột bóng bò lên từ SpawnPoint (đo 2026-09-22: sau khi loading tắt còn mất 2 đến 3 giây), và không
    /// phụ thuộc fps lúc loading (SoftBubbleWorld.MaxDeltaTime chặn mỗi frame chỉ tiến được một chút).
    /// </summary>
    public void WarmUpBubbles()
    {
        if (SpawnWarmUpSeconds <= 0f || bubbles.Count == 0) return;
        SoftBubbleWorld world = SoftBubbleWorld.Get();
        if (world == null) return;

        // Mô phỏng từng đợt tới khi bàn đứng yên: thời gian xếp xong tùy lực nổi, số bóng và độ cao cột spawn nên không dùng một hằng số.
        float simulated = 0f;
        while (simulated < SpawnWarmUpSeconds)
        {
            float chunk = Mathf.Min(WarmUpChunkSeconds, SpawnWarmUpSeconds - simulated);
            world.Simulate(chunk, WarmUpStepDt, PushRising);
            simulated += chunk;
            if (simulated >= WarmUpMinSeconds && MaxBubbleSpeed() <= SpawnWarmUpSettleSpeed) break;
        }
    }

    private float MaxBubbleSpeed()
    {
        float max = 0f;
        foreach (BubbleView view in bubbles)
        {
            SoftBubble bubble = view != null ? view.Bubble : null;
            if (bubble != null) max = Mathf.Max(max, bubble.Velocity.magnitude);
        }

        return max;
    }

    /// <summary>
    /// Thắng đã chắc ngay lúc đặt chỗ cho cá cuối (BoardModel.IsWinDecided), cá / bể còn đang bay: GameManager khoá HUD + input ngay,
    /// còn WinLevel vẫn đợi bàn hết bận (OnBoardCompleted) như cũ.
    /// </summary>
    public void OnBoardDecided()
    {
        if (Core.GameManager.Instance != null) Core.GameManager.Instance.PrepareWin();
    }

    public void OnBoardCompleted()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySoundFx(AudioKey.SfxBoardClear);
        if (Core.GameManager.Instance != null) Core.GameManager.Instance.WinLevel().Forget();
    }

    public void OnBoardStuck()
    {
        // FTUE L1-L2: không thua, gom ô chờ vào túi miễn phí; TutorialManager hiện chữ và gửi ftue_rescue.
        if (Core.Tutorial.FtueLevelRules.FreeRescue(PlayerLevel) && Board != null && Board.ReviveWithBag())
        {
            Core.GamePlay.GameGlobalEvent.OnFreeRescue?.Invoke();
            return;
        }

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySoundFx(AudioKey.SfxHoldingFull);
        CurLoseReason = Core.GamePlay.GlobalEnum.LoseReason.HoldingFull;
        if (Core.GameManager.Instance != null) Core.GameManager.Instance.LoseLevel().Forget();
    }

    // ---------- API cho manager / UI ----------

    /// <summary>
    /// Người chơi bấm vào bóng đá, bóng còn băng hoặc bóng còn vỏ HP (InputManager): không có nước đi. Phản hồi nhún + rung tạm tắt,
    /// bỏ comment để mở lại.
    /// </summary>
    public void OnBubbleTapped(BubbleView view)
    {
        if (view == null || (!view.IsRock && !view.IsFrozen && !view.IsShelled)) return;
        // view.PlayTapFeedback();
        // if (VibrationManager.Instance != null) VibrationManager.Instance.CallHaptic(HapticType.LightImpact);
    }

    /// <summary>
    /// Va chạm với bóng cứng (đá, băng): cặp nào có một bên dùng config cứng thì lấy Contact của config cứng đó (hai bên đều cứng
    /// thì lấy bên cứng hơn), không lấy trung bình với bóng thường như mặc định của SoftBubbleWorld. Nhận diện qua Config đang gắn
    /// trên bóng (config prefab đá, IceConfig); băng vỡ trả config nền nên tự về luật nền. Đặt một lần cho world (world sống qua
    /// nhiều level), bảng luật tự dựng lại khi có bóng đăng ký mới.
    /// </summary>
    private void InstallContactRules()
    {
        SoftBubbleWorld world = SoftBubbleWorld.Get();
        if (world == null) return;
        SoftBubbleConfig defaultConfig = BubblePrefab != null && BubblePrefab.Bubble != null ? BubblePrefab.Bubble.Config : null;
        SoftBubbleConfig rockConfig = RockBubblePrefab != null && RockBubblePrefab.Bubble != null ? RockBubblePrefab.Bubble.Config : null;
        var hard = new Dictionary<SoftBubbleConfig, ContactRule>();
        foreach (SoftBubbleConfig cfg in new[] { rockConfig, IceConfig })
        {
            if (cfg != null && cfg != defaultConfig && !hard.ContainsKey(cfg)) hard[cfg] = cfg.Contact.ToRule();
        }

        if (defaultConfig == null || hard.Count == 0)
        {
            world.ContactRuleResolver = null;
            world.MarkContactRulesDirty();
            return;
        }

        world.ContactRuleResolver = (a, b) =>
        {
            ContactRule ra = default;
            ContactRule rb = default;
            bool hardA = a.Config != null && hard.TryGetValue(a.Config, out ra);
            bool hardB = b.Config != null && hard.TryGetValue(b.Config, out rb);
            if (hardA && hardB) return ra.Compliance <= rb.Compliance ? ra : rb;
            if (hardA) return ra;
            if (hardB) return rb;
            SoftBubbleConfig ca = a.Config != null ? a.Config : defaultConfig;
            SoftBubbleConfig cb = b.Config != null ? b.Config : defaultConfig;
            return ContactRule.Average(ca.Contact.ToRule(), cb.Contact.ToRule());
        };
        world.MarkContactRulesDirty();
    }

    /// <summary>Người chơi bấm một con cá (InputManager). Trả về true nếu có nước đi.</summary>
    public bool OnFishTapped(Fish fish)
    {
        if (Board == null) return false;
        bool moved = Board.TryTapFish(fish);
        return moved;
    }

    /// <summary>Cá bấm được ngay lúc này (InputManager viền cá khi đè tay). Không đổi trạng thái.</summary>
    public bool CanTapFish(Fish fish) => Board != null && Board.CanTapFish(fish);

    /// <summary>
    /// InputManager đè tay: nhóm cá sẽ cùng bay nếu bấm con này (FishSchoolBubble: cả đàn; bóng thường: chính nó). denied = đàn bấm được
    /// nhưng bể + ô chờ không đủ chỗ (viền đỏ, bấm sẽ bị từ chối). Không đổi trạng thái.
    /// </summary>
    public bool TryGetPickGroup(Fish fish, List<Fish> group, out bool denied)
    {
        denied = false;
        group.Clear();
        return Board != null && Board.TryGetPickGroup(fish, group, out denied);
    }

    /// <summary>FishSchoolBubble thiếu chỗ (InputManager): mọi ô chờ nhuộm đỏ (on) hoặc trả màu gốc (off).</summary>
    public void SetHoldingDeny(bool on)
    {
        if (Board != null) Board.SetHoldingDeny(on);
    }

    /// <summary>
    /// Play On khi kẹt vì đầy ô chờ: gom cá ô chờ vào túi (như booster PackedUp) rồi chơi tiếp. Không cộng thêm ô chờ nữa
    /// (DefaultGlobalConfig.ReviveSlot không còn dùng).
    /// </summary>
    public UniTask Revive()
    {
        if (Board != null) Board.ReviveWithBag();
        CurLoseReason = Core.GamePlay.GlobalEnum.LoseReason.None;
        return UniTask.CompletedTask;
    }

    public void SetIsClaimBooster(bool value) => isClaimBooster = value;
    public bool GetIsClaimBooster() => isClaimBooster;

    /// <summary>
    /// Trọn luồng nhận booster mở khóa đã xong (popup → bay về nút → ép dùng thử). Khác IsClaimBooster: cờ kia bật ngay khi booster
    /// bay tới nút, lúc tutorial ép dùng còn chưa bắt đầu.
    /// </summary>
    public void SetIsBoosterClaimFlowDone(bool value) => isBoosterClaimFlowDone = value;
    public bool GetIsBoosterClaimFlowDone() => isBoosterClaimFlowDone;

    public Vector3 GetTutSpawnPoint()
    {
        BubbleView first = null;
        foreach (BubbleView view in bubbles)
        {
            if (view == null || view.IsRock) continue;   // bóng đá không bấm được, tutorial không trỏ vào
            first = view;
            break;
        }

        return first != null && first.Bubble != null ? first.Bubble.Center : transform.position;
    }

    public int GetBoosterByAdsCount(ResourceType boosterType) => boosterByAdsCount.GetValueOrDefault(boosterType);

    public void IncrementBoosterByAdsCount(ResourceType boosterType) => boosterByAdsCount[boosterType] = GetBoosterByAdsCount(boosterType) + 1;

    public bool CanUseBooster(ResourceType boosterType) => Board != null && Board.CanUseBooster(boosterType);
    public BoosterBlockReason GetBoosterBlockReason(ResourceType boosterType) => Board != null ? Board.GetBoosterBlockReason(boosterType) : BoosterBlockReason.NotAvailable;
    public bool TryUseHint() => Board != null && Board.TryUseHint();
    public bool TryUseShuffle() => Board != null && Board.TryUseShuffle();
    public bool TryUseAutoFill() => Board != null && Board.TryUseAutoFill();
    public bool TryUsePackedUp() => Board != null && Board.TryUsePackedUp();
    /// <summary>Ép dùng thử PackedUp: tự bấm cá lên ô chờ cho đủ count con rồi chờ hạ xong (BoardManager.PrefillHoldingAsync).</summary>
    public UniTask PrefillHoldingAsync(int count) => Board != null ? Board.PrefillHoldingAsync(count) : UniTask.CompletedTask;
    public bool TryAddHoldingSlot() => Board != null && Board.TryAddHoldingSlot();
    public bool TryUnlockTank() => Board != null && Board.TryUnlockTank();
    public void BeginSelectTank(ResourceType boosterType) => Board?.BeginSelectTank(boosterType);
    public void CancelSelectTank() => Board?.CancelSelectTank();
    public bool TryUseTankBooster(ResourceType boosterType, TankSlotView tank) => Board != null && Board.TryUseTankBooster(boosterType, tank);

    public void HideTankSlotAndHolding()
    {
        for (int i = 0; i < Tanks.Length; i++)
        {
            Tanks[i].Hide();
        }

        for (int i = 0; i < HoldingSlots.Count; i++)
        {
            HoldingSlots[i].Hide();
        }
    }
    private void OnDrawGizmosSelected()
    {
        Vector3 origin = SpawnPoint != null ? SpawnPoint.position : transform.position;
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(origin, SpawnRadius);
    }
}
