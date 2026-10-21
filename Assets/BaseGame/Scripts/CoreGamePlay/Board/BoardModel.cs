using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;

/// <summary>
/// Một con cá trong model: loài (FishId) và các cờ vật cản gắn trên chính con cá (đọc từ FishData của seed). Model và view (Fish)
/// giữ chung một tham chiếu nên đổi cờ ở đâu cũng thấy. IsHidden = cá ẩn (HiddenFish): hiện model chung ID HiddenModelId cho tới
/// khi lộ; luật bấm / vào bể / ô chờ không khác cá thường.
/// </summary>
[Serializable]
public class FishEntry
{
    /// <summary>ID trong FishModelGlobalConfig của model "cá ẩn" dùng chung cho mọi loài.</summary>
    public const int HiddenModelId = -1;

    /// <summary>ID model của con spawner (SpawnerView) trong FishModelGlobalConfig; không phải loài cá, không bấm được.</summary>
    public const int SpawnerModelId = -2;

    /// <summary>Token chìa (Lock & Key) bơi trong bóng chìa: model chung key_bubble (KeyView), không phải cá, không nằm trong Fishes.</summary>
    public const int KeyModelId = -3;

    public int FishId;
    public bool IsHidden;

    /// <summary>ID model cần hiện lúc này: model ẩn khi chưa lộ, model thật khi đã lộ.</summary>
    public int ModelId => IsHidden ? HiddenModelId : FishId;

    public FishEntry()
    {
    }

    public FishEntry(int fishId, bool isHidden = false)
    {
        FishId = fishId;
        IsHidden = isHidden;
    }

    public FishEntry(FishData data) : this(data.FishId, data.isHidden)
    {
    }

    public FishEntry Clone() => (FishEntry)MemberwiseClone();
}

/// <summary>Một bóng trong model: cá theo thứ tự ô, thứ tự sinh, bán kính (0 = ngẫu nhiên khi spawn).</summary>
public class BubbleEntry
{
    public int Index;
    public int SpawnOrder;
    public float Radius;

    /// <summary>Bóng đá (RockBubble): không có cá, không bao giờ nổ, chiếm một suất trong VisibleCount tới hết level, chỉ để chiếm chỗ.</summary>
    public bool IsRock;

    /// <summary>
    /// Bóng băng: sinh ra với lớp băng phủ lên bóng nền (bóng thường hoặc bóng đá). Nội dung bên trong bị che và không bấm được tới khi
    /// băng vỡ (bóng chạm đỉnh vùng chơi); vỡ ra là cá hay là đá tuỳ nền.
    /// </summary>
    public bool IsIce;

    /// <summary>Còn lớp băng. Chỉ true với bóng băng; TryBreakIce bỏ cờ khi view thấy bóng chạm đỉnh.</summary>
    public bool IsFrozen;

    /// <summary>
    /// MultiHpBubble: vỏ có HP che mờ cá bên trong; mỗi bể hoàn thành trừ 1 cho mọi bóng HP đang hiện trên bàn (DamageShells), HP về
    /// 0 thì vỏ vỡ và cá bấm được. 0 = không có vỏ hoặc đã vỡ. Băng phủ lên được như mọi bóng nền.
    /// </summary>
    public int Hp;

    /// <summary>HP lúc sinh (0 = không phải bóng HP).</summary>
    public int MaxHp;

    /// <summary>Cá trong bóng bấm được: không phải bóng đá, không còn băng, vỏ HP đã vỡ, không còn khoá chìa / khoá đơn.</summary>
    public bool IsTappable => !IsRock && !IsFrozen && Hp <= 0 && !IsGated;

    /// <summary>Khoá chìa (isLock): không bấm được tới khi bóng chìa (isKey, keyId = SoftBubbleId) nổ.</summary>
    public bool IsLock;

    /// <summary>Bóng chìa: KeyId = SoftBubbleId của ổ khoá. Nổ thì mở ổ đó.</summary>
    public bool IsKey;
    public int KeyId;

    /// <summary>Số thứ tự cặp ổ khoá / chìa (0 = cặp đầu, theo thứ tự ổ khoá trong level data), chỉ để view chọn màu (GamePlayGlobalConfig.KeyLockMaterials); -1 = không thuộc cặp nào. Đặt lúc dựng từ data, không đổi khi ổ mở.</summary>
    public int KeyPairIndex = -1;

    /// <summary>Khoá đơn (isOrder): không bấm được tới khi hoàn thành bể loài OrderFishId, hoặc hết đơn khớp thì tự mở.</summary>
    public bool IsOrderBubble;

    /// <summary>FishId của đơn cần hoàn thành để mở khoá. 0 = chưa gán.</summary>
    public int FishOrderId;

    /// <summary>Còn khoá chìa hoặc khoá đơn.</summary>
    public bool IsGated => IsLock || IsOrderBubble;

    public readonly List<FishEntry> Fishes = new();

    /// <summary>
    /// Spawner: hàng đợi cá (FIFO) nằm trong con spawner bơi cùng đàn. Mỗi con cá rời bóng (RemoveFishAt) thì con đầu hàng chèn vào
    /// đúng ô vừa trống, nên số cá hiện giữ nguyên tới khi hàng đợi rỗng; bóng chỉ nổ khi cả Fishes và hàng đợi đều hết. Rỗng = không
    /// có spawner (hoặc đã hết, view ẩn con spawner).
    /// </summary>
    public readonly List<FishEntry> SpawnerFishes = new();

    /// <summary>
    /// MultilayerBubble theo data (giữ nguyên cả đời bóng). Visual nhiều lớp chỉ khi còn lớp ẩn (HasLayers): lộ lớp cuối, hoặc AutoFill /
    /// trừ nợ rút hết cá của các lớp ẩn, thì bóng thành bóng thường.
    /// </summary>
    public bool IsMultiLayer;

    /// <summary>
    /// MultilayerBubble: các lớp cá còn ẩn theo thứ tự lộ (lớp đang hiện là Fishes, không nằm trong đây). Cá hiện hết (RemoveFishAt) thì
    /// lớp đầu danh sách chuyển sang Fishes, bóng giữ nguyên cỡ; bóng chỉ nổ khi Fishes và mọi lớp đều hết. Rỗng = không còn lớp ẩn.
    /// </summary>
    public readonly List<List<FishEntry>> Layers = new();

    /// <summary>
    /// FishSchoolBubble (data: isBundleBubble): bấm một con là cả đàn (mọi cá đang hiện) bay cùng lúc, mỗi con vào bể nhận nó hoặc ô
    /// chờ; không đủ chỗ cho cả đàn thì không bấm được (BoardModel.TryPlanSchool). Giữ cả đời bóng; Shuffle bỏ qua bóng này, AutoFill
    /// vẫn rút lẻ từng con. Không dùng chung với spawner / nhiều lớp (LevelData.Validate).
    /// </summary>
    public bool IsFishSchool;

    public bool Popped;

    public int FishCount => Fishes.Count;
    public bool IsEmpty => Fishes.Count == 0;
    public bool HasSpawner => SpawnerFishes.Count > 0;
    public int SpawnerCount => SpawnerFishes.Count;

    public bool HasLayers => Layers.Count > 0;

    /// <summary>Số cá trong các lớp còn ẩn.</summary>
    public int LayerFishCount
    {
        get
        {
            int n = 0;
            foreach (List<FishEntry> layer in Layers) n += layer.Count;
            return n;
        }
    }

    /// <summary>Tổng cá còn phải lấy khỏi bóng (cá hiện + hàng đợi spawner + các lớp còn ẩn).</summary>
    public int TotalFishCount => Fishes.Count + SpawnerFishes.Count + LayerFishCount;
    public int FishIdAt(int index) => Fishes[index].FishId;
    public bool Contains(int fishId) => IndexOfFish(fishId) >= 0;

    /// <summary>Vị trí con cá đầu tiên thuộc loài fishId, −1 nếu không có.</summary>
    public int IndexOfFish(int fishId)
    {
        for (int i = 0; i < Fishes.Count; i++)
        {
            if (Fishes[i].FishId == fishId) return i;
        }

        return -1;
    }
}

/// <summary>FishSchoolBubble: đích của một con trong đàn khi bấm (BoardModel.TryPlanSchool): bể (Tank khác null) hoặc ô chờ (HoldingIndex).</summary>
public struct SchoolMove
{
    public int FishIndex;
    public TankEntry Tank;
    public int HoldingIndex;
}

/// <summary>Nơi AutoFill tìm thấy cá trong một bóng: cá đang hiện, hàng đợi spawner, hay một lớp còn ẩn của MultilayerBubble.</summary>
public enum AutoFillSourceKind
{
    Visible,
    Spawner,
    Layer
}

/// <summary>Bể: đơn hiện tại (FishId, Goal), cá đã vào (Count) và cá đang bay tới (Incoming).</summary>
[Serializable]
public class TankEntry
{
    public int Index;
    public int FishId = -1;
    public int Goal;
    public int Count;
    public int Incoming;

    public bool HasOrder => FishId >= 0;
    public int Missing => HasOrder ? Math.Max(0, Goal - Count - Incoming) : 0;
    public bool IsComplete => HasOrder && Count >= Goal;
    public bool Accepts(int fishId) => HasOrder && FishId == fishId && Missing > 0;
}

/// <summary>Ô chờ: cá đang nằm (FishId) hoặc đang được giữ chỗ cho cá bay tới (Reserved).</summary>
[Serializable]
public class HoldingEntry
{
    public int Index;
    public int FishId = -1;
    public bool Reserved;

    public bool HasFish => FishId >= 0;
    public bool IsFree => FishId < 0 && !Reserved;
}

/// <summary>
/// Trạng thái bàn chơi thuần C# (nguồn sự thật cho gameplay, Hint, Shuffle và bộ giải). Không tham chiếu Unity.
/// Bóng: một hàng đợi theo thứ tự seed; VisibleCount bóng đầu hiện ngay khi bắt đầu, mỗi bóng nổ (hết cá) thì bóng kế
/// trong hàng đợi được sinh thêm (SpawnedCount tăng). Bóng "trước cùng" = đã sinh và chưa nổ, bấm được mọi con cá bên trong.
/// Bể: tankSlots bể mở sẵn, booster UnlockTank mở thêm tới MaxTankSlots. TankGoal cố định = 3.
/// Luật theo Docs/GDD/02_CoreGameplay.tsv, chỉnh theo WordSort (2026-09-13): FISH-04 chọn bể; TANK-03 đơn kế lấy FIFO ngay khi
/// bể trống (bỏ TANK-04, cho trùng loài giữa các bể); HOLD-02 tự thu từng con (TryFindAutoCollect); ô chờ đầy thì khóa mọi
/// cú bấm (CanTap) và kẹt khi không cá nào trong ô chờ vào được bể; WIN-01 thắng.
/// </summary>
[Serializable]
public class BoardModel
{
    [ShowInInspector]
    public int TankGoal { get; }
    [ShowInInspector]
    public int MaxTankSlots { get; }
    [ShowInInspector]
    public int MaxHoldingSlots { get; }
    [ShowInInspector]
    public int TotalFish { get; }
    [ShowInInspector]
    public int VisibleCount { get; }
    [ShowInInspector]
    public int Collected { get; private set; }

    /// <summary>
    /// AutoFill: các con cá đã "ứng" từ đáy màn vì trên bàn không còn con nào của loài đó (mỗi phần tử một con, giá trị = loài).
    /// Bóng lên bàn từ hàng đợi có loài đang nợ thì bị bớt đúng con đó (TakeNextFromQueue → SettleDebt) để tổng cá vẫn khớp đơn.
    /// </summary>
    public readonly List<int> FishDebt = new();

    /// <summary>
    /// Túi (booster PackedUp): loài của các con cá đã gom từ ô chờ (PackHolding). Túi không nhận cá bấm thêm, chỉ nhả vào bể qua tự thu
    /// sau khi duyệt hết ô chờ (TryFindBagCollect); rỗng = không có túi (view huỷ túi khi con cuối bay ra).
    /// </summary>
    public readonly List<int> Bag = new();

    public bool HasBag => Bag.Count > 0;
    [ShowInInspector]
    public int SpawnedCount { get; private set; }
    [ShowInInspector]
    public readonly List<TankEntry> Tanks = new();
    [ShowInInspector]
    public readonly List<HoldingEntry> Holding = new();
    [ShowInInspector]
    public readonly List<int> OrderQueue = new();
    [ShowInInspector]
    public readonly List<BubbleEntry> Bubbles = new();

    public int HoldingSlots => Holding.Count;
    public int QueueCount => Math.Max(0, Bubbles.Count - SpawnedCount);
    public bool CanAddTank => Tanks.Count < MaxTankSlots && OrderQueue.Count > 0;

    public BoardModel(LevelData data, int tankSlots, int maxTankSlots, int holdingSlots, int maxHoldingSlots, int visibleCount)
    {
        TankGoal = GamePlayGlobalConfig.TankGoal;
        MaxTankSlots = Math.Max(tankSlots, maxTankSlots);
        MaxHoldingSlots = Math.Max(holdingSlots, maxHoldingSlots);
        TotalFish = data.TotalFish;
        VisibleCount = Math.Max(1, visibleCount);
        if (data.OrderQueue != null) OrderQueue.AddRange(data.OrderQueue);

        SoftBubbleData[] array = data.SoftBubbleDataArray ?? Array.Empty<SoftBubbleData>();
        for (int i = 0; i < array.Length; i++)
        {
            SoftBubbleData d = array[i];
            if (d == null) continue;
            // Băng là lớp phủ: bật cùng đá thì thành đá đóng băng, vỡ băng ra đá.
            var entry = new BubbleEntry
            {
                Index = i,
                SpawnOrder = Bubbles.Count,
                Radius = d.Radius,
                IsRock = d.isRock,
                IsIce = d.isIce,
                IsFrozen = d.isIce,
                IsLock = d.isLock,
                IsKey = d.isKey,
                KeyId = d.isKey ? d.keyId : 0,
                IsOrderBubble = d.isOrderBubble,
                FishOrderId = d.isOrderBubble && !d.isRock ? d.fishOrderId : 0,
                IsFishSchool = d.isBundleBubble && !d.isRock && !d.isSpawner && !d.isMultiLayer   // dùng chung spawner / nhiều lớp thì bỏ cờ (Validate đã báo)
            };
            if (d.isBubbleHp && !d.isRock)   // đá không có vỏ HP
            {
                entry.Hp = Math.Max(1, d.bubbleHp);
                entry.MaxHp = entry.Hp;
            }
            if (!d.isRock && d.FishDataArray != null)   // bóng đá không nuôi cá; có trong data cũng bỏ (LevelData.Validate đã báo lỗi)
            {
                foreach (FishData f in d.FishDataArray)
                {
                    if (f != null) entry.Fishes.Add(new FishEntry(f));
                }
            }

            if (!d.isRock && d.isSpawner && d.fishOnSpawner != null)   // hàng đợi spawner theo thứ tự data
            {
                foreach (FishData f in d.fishOnSpawner)
                {
                    if (f != null) entry.SpawnerFishes.Add(new FishEntry(f));
                }
            }

            // MultilayerBubble: FishDataArray là lớp đầu, layers xếp theo index tăng dần (trùng index thì giữ thứ tự data). Không dùng chung
            // với spawner và đá (LevelData.Validate báo lỗi), khi đó các lớp bị bỏ qua. Lớp không có cá thì bỏ.
            if (!d.isRock && !d.isSpawner && d.isMultiLayer && d.layers != null)
            {
                entry.IsMultiLayer = true;
                var ordered = new List<BubbleLayerData>();
                foreach (BubbleLayerData layer in d.layers)
                {
                    if (layer == null || layer.FishDataArray == null) continue;
                    int at = ordered.Count;
                    while (at > 0 && ordered[at - 1].index > layer.index) at--;
                    ordered.Insert(at, layer);
                }

                foreach (BubbleLayerData layer in ordered)
                {
                    var fishes = new List<FishEntry>();
                    foreach (FishData f in layer.FishDataArray)
                    {
                        if (f != null) fishes.Add(new FishEntry(f));
                    }

                    if (fishes.Count > 0) entry.Layers.Add(fishes);
                }

                PromoteNextLayer(entry);   // data không có lớp đầu (FishDataArray rỗng): lớp kế lên thay để bóng không sinh ra rỗng
            }

            Bubbles.Add(entry);
        }

        AssignKeyPairs();
        SpawnedCount = Math.Min(VisibleCount, Bubbles.Count);

        for (int t = 0; t < Math.Max(1, tankSlots); t++) Tanks.Add(new TankEntry { Index = t, Goal = TankGoal });
        foreach (TankEntry tank in Tanks) AssignNextOrder(tank);
        UnlockOrphanOrderLocks();

        for (int h = 0; h < Math.Max(1, holdingSlots); h++) Holding.Add(new HoldingEntry { Index = h });
    }

    // ---------- Truy vấn ----------

    /// <summary>Bóng đang hiện trên bàn (đã sinh, chưa nổ): mọi con cá trong đó đều bấm được.</summary>
    public bool IsFront(BubbleEntry entry) => entry != null && !entry.Popped && entry.SpawnOrder < SpawnedCount;

    public IEnumerable<BubbleEntry> FrontBubbles
    {
        get
        {
            for (int i = 0; i < SpawnedCount && i < Bubbles.Count; i++)
            {
                if (!Bubbles[i].Popped) yield return Bubbles[i];
            }
        }
    }

    public int FrontBubbleCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < SpawnedCount && i < Bubbles.Count; i++)
            {
                if (!Bubbles[i].Popped) n++;
            }

            return n;
        }
    }

    /// <summary>Số bóng đang hiện có cá bấm được (bỏ bóng đá và bóng còn băng): Shuffle cần ít nhất 2 bóng loại này.</summary>
    public int FrontFishBubbleCount
    {
        get
        {
            int n = 0;
            foreach (BubbleEntry front in FrontBubbles)
            {
                if (front.IsTappable && front.FishCount > 0 && !front.IsFishSchool) n++;   // bóng đàn không tham gia Shuffle
            }

            return n;
        }
    }

    /// <summary>Bóng kế trong hàng đợi (chưa sinh), null nếu hết.</summary>
    public BubbleEntry PeekQueue() => SpawnedCount < Bubbles.Count ? Bubbles[SpawnedCount] : null;

    /// <summary>Lấy bóng kế ra khỏi hàng đợi để sinh (sau khi một bóng nổ). Null nếu hết.</summary>
    /// <summary>
    /// Bóng kế trong hàng đợi (null nếu hết). Đang nợ cá (AutoFill) thì bóng lên bàn bị bớt cá đúng loài nợ (SettleDebt); bớt xong không
    /// còn con nào thì coi như đã nổ và lấy tiếp bóng kế. Không nợ thì hành vi như cũ (bộ giải dùng chung).
    /// </summary>
    public BubbleEntry TakeNextFromQueue()
    {
        while (SpawnedCount < Bubbles.Count)
        {
            BubbleEntry next = Bubbles[SpawnedCount++];
            if (FishDebt.Count > 0 && !next.IsRock && SettleDebt(next) && next.IsEmpty)
            {
                next.Popped = true;   // bóng chỉ toàn cá nợ: không lên bàn
                continue;
            }

            return next;
        }

        return null;
    }

    /// <summary>
    /// Trừ nợ cá vào bóng sắp lên: cá hiện trước, rồi hàng đợi spawner, rồi các lớp còn ẩn (MultilayerBubble); mỗi con bớt xoá một khoản
    /// nợ cùng loài. Cá hiện bị bớt hết mà hàng đợi spawner còn thì kéo một con lên làm cá hiện để spawner vẫn kích được; còn lớp ẩn thì
    /// lớp kế lên thay. Trả về true nếu có bớt.
    /// </summary>
    private bool SettleDebt(BubbleEntry entry)
    {
        bool any = false;
        for (int i = entry.Fishes.Count - 1; i >= 0 && FishDebt.Count > 0; i--)
        {
            int debt = FishDebt.IndexOf(entry.Fishes[i].FishId);
            if (debt < 0) continue;
            FishDebt.RemoveAt(debt);
            entry.Fishes.RemoveAt(i);
            any = true;
        }

        for (int k = entry.SpawnerFishes.Count - 1; k >= 0 && FishDebt.Count > 0; k--)
        {
            int debt = FishDebt.IndexOf(entry.SpawnerFishes[k].FishId);
            if (debt < 0) continue;
            FishDebt.RemoveAt(debt);
            entry.SpawnerFishes.RemoveAt(k);
            any = true;
        }

        for (int l = entry.Layers.Count - 1; l >= 0 && FishDebt.Count > 0; l--)
        {
            List<FishEntry> layer = entry.Layers[l];
            for (int k = layer.Count - 1; k >= 0 && FishDebt.Count > 0; k--)
            {
                int debt = FishDebt.IndexOf(layer[k].FishId);
                if (debt < 0) continue;
                FishDebt.RemoveAt(debt);
                layer.RemoveAt(k);
                any = true;
            }

            if (layer.Count == 0) entry.Layers.RemoveAt(l);
        }

        if (entry.Fishes.Count == 0 && entry.SpawnerFishes.Count > 0)
        {
            entry.Fishes.Add(entry.SpawnerFishes[0]);
            entry.SpawnerFishes.RemoveAt(0);
        }

        PromoteNextLayer(entry);
        return any;
    }

    /// <summary>
    /// MultilayerBubble: cá hiện đã hết mà còn lớp ẩn thì lớp kế thành cá hiện (lớp toàn cá ẩn tự lộ như lúc bóng spawn). Trả về true khi
    /// vừa đổi lớp (view sinh cá lớp mới từ tâm bóng). Bóng còn cá hiện thì không làm gì.
    /// </summary>
    private bool PromoteNextLayer(BubbleEntry entry)
    {
        if (entry.Fishes.Count > 0 || entry.Layers.Count == 0) return false;
        while (entry.Fishes.Count == 0 && entry.Layers.Count > 0)
        {
            entry.Fishes.AddRange(entry.Layers[0]);
            entry.Layers.RemoveAt(0);
        }

        if (entry.Fishes.Count == 0) return false;
        TryAutoRevealAll(entry);
        return true;
    }

    /// <summary>
    /// FISH-04: bể cần loài này còn thiếu (kể cả cá đang bay), ưu tiên thiếu ít nhất rồi trái sang phải. filter (tùy chọn) loại
    /// bể không được nhận cá lúc này (view: bể đang rơi vào / bay đi chưa đậu).
    /// </summary>
    public TankEntry FindTankFor(int fishId, Predicate<TankEntry> filter = null)
    {
        TankEntry best = null;
        foreach (TankEntry tank in Tanks)
        {
            if (!tank.Accepts(fishId)) continue;
            if (filter != null && !filter(tank)) continue;
            if (best == null || tank.Missing < best.Missing) best = tank;
        }

        return best;
    }

    /// <summary>
    /// FishSchoolBubble: xếp chỗ cho cả đàn khi bấm con ở tappedIndex, không đổi state. Con được bấm đi trước rồi các con còn lại theo
    /// thứ tự trong đàn; mỗi con vào bể khớp còn thiếu (tính cả cá đang bay và các con xếp trước trong đàn; thiếu ít ưu tiên, trái sang
    /// phải), không thì ô chờ trống trái nhất. Thiếu chỗ cho bất kỳ con nào thì false và plan rỗng. tankFilter (view): bể chưa đậu không
    /// nhận cá. plan null = chỉ hỏi có xếp được không (HasLegalMove, viền khi đè tay).
    /// </summary>
    public bool TryPlanSchool(BubbleEntry entry, int tappedIndex, List<SchoolMove> plan, Predicate<TankEntry> tankFilter = null)
    {
        plan?.Clear();
        if (entry == null || entry.Fishes.Count == 0) return false;
        // Sức chứa còn lại tính cục bộ để không đụng model (Incoming / Reserved chỉ đổi khi cá thật sự bay).
        var tankRoom = new int[Tanks.Count];
        for (int t = 0; t < Tanks.Count; t++) tankRoom[t] = Tanks[t].Missing;
        var holdingTaken = new bool[Holding.Count];
        int count = entry.Fishes.Count;
        for (int step = 0; step < count; step++)
        {
            int index = SchoolOrderIndex(step, tappedIndex, count);
            int fishId = entry.Fishes[index].FishId;
            int bestTank = -1;
            for (int t = 0; t < Tanks.Count; t++)
            {
                TankEntry tank = Tanks[t];
                if (!tank.HasOrder || tank.FishId != fishId || tankRoom[t] <= 0) continue;
                if (tankFilter != null && !tankFilter(tank)) continue;
                if (bestTank < 0 || tankRoom[t] < tankRoom[bestTank]) bestTank = t;
            }

            if (bestTank >= 0)
            {
                tankRoom[bestTank]--;
                plan?.Add(new SchoolMove { FishIndex = index, Tank = Tanks[bestTank], HoldingIndex = -1 });
                continue;
            }

            int holding = -1;
            for (int h = 0; h < Holding.Count; h++)
            {
                if (!Holding[h].IsFree || holdingTaken[h]) continue;
                holding = h;
                break;
            }

            if (holding < 0)
            {
                plan?.Clear();
                return false;
            }

            holdingTaken[holding] = true;
            plan?.Add(new SchoolMove { FishIndex = index, Tank = null, HoldingIndex = Holding[holding].Index });
        }

        return true;
    }

    /// <summary>Thứ tự bay của đàn: con được bấm trước (tappedIndex ngoài khoảng = theo thứ tự đàn), rồi các con còn lại theo thứ tự trong đàn.</summary>
    private static int SchoolOrderIndex(int step, int tappedIndex, int count)
    {
        if (tappedIndex < 0 || tappedIndex >= count) return step;
        if (step == 0) return tappedIndex;
        return step <= tappedIndex ? step - 1 : step;
    }

    /// <summary>HOLD-01: ô chờ trống trái nhất, −1 nếu đầy.</summary>
    public int FindFreeHoldingIndex()
    {
        foreach (HoldingEntry h in Holding)
        {
            if (h.IsFree) return h.Index;
        }

        return -1;
    }

    public int HoldingFreeCount
    {
        get
        {
            int n = 0;
            foreach (HoldingEntry h in Holding)
            {
                if (h.IsFree) n++;
            }

            return n;
        }
    }

    public int HoldingFishCount
    {
        get
        {
            int n = 0;
            foreach (HoldingEntry h in Holding)
            {
                if (h.HasFish) n++;
            }

            return n;
        }
    }


    public bool AnyTankAcceptsFrontFish()
    {
        foreach (BubbleEntry front in FrontBubbles)
        {
            if (!front.IsTappable) continue;
            foreach (FishEntry f in front.Fishes)
            {
                if (FindTankFor(f.FishId) != null) return true;
            }
        }

        return false;
    }

    public bool AnyTankAcceptsHoldingFish()
    {
        foreach (HoldingEntry h in Holding)
        {
            if (h.HasFish && FindTankFor(h.FishId) != null) return true;
        }

        foreach (int id in Bag)   // cá trong túi cũng sẽ tự thu vào bể
        {
            if (FindTankFor(id) != null) return true;
        }

        return false;
    }

    public bool HasPendingMoves
    {
        get
        {
            foreach (TankEntry t in Tanks)
            {
                if (t.Incoming > 0) return true;
            }

            foreach (HoldingEntry h in Holding)
            {
                if (h.Reserved) return true;
            }

            return false;
        }
    }

    /// <summary>Kiểu WordSort: ô chờ đầy thì khóa mọi cú bấm (kể cả cá khớp bể); chỉ tự thu cá mới giải phóng ô.</summary>
    public bool CanTap => HoldingFreeCount > 0;

    /// <summary>
    /// Còn nước đi: còn ô chờ trống, hoặc có cá trong ô chờ sắp được tự thu vào bể. Không xét bóng trên bàn: bóng băng / vỏ HP / khoá rồi
    /// sẽ mở (bàn không bao giờ chỉ còn băng), nên thua chỉ có một đường là ô chờ đầy (LoseReason.HoldingFull).
    /// </summary>
    public bool HasLegalMove => CanTap || AnyTankAcceptsHoldingFish();

    /// <summary>WIN-01: hết đơn và mọi bể đã xong (đủ cá đã thu).</summary>
    public bool IsWin => OrderQueue.Count == 0 && !AnyTankHasOrder() && Collected >= TotalFish;

    /// <summary>
    /// Thắng đã chắc dù cá còn đang bay: hết đơn chờ, mọi bể đang mở đã đủ cá (đậu + đang bay tới), và toàn bộ cá của màn đã thu hoặc
    /// đang nằm / bay vào bể. Đúng ngay lúc đặt chỗ cho cá cuối (ReserveTank), sớm hơn IsWin cỡ 1.5 s animation (cá bay 0.7 s, bể bay
    /// đi 0.5 s); BoardManager dùng để khoá HUD / input trước khi WinLevel chạy.
    /// </summary>
    public bool IsWinDecided
    {
        get
        {
            if (OrderQueue.Count > 0) return false;
            int inTanks = 0;
            foreach (TankEntry t in Tanks)
            {
                if (t.HasOrder && t.Missing > 0) return false;
                inTanks += t.Count + t.Incoming;
            }

            return Collected + inTanks >= TotalFish;
        }
    }

    /// <summary>Kẹt kiểu WordSort (chỉ đúng khi bàn đã dừng, không cá bay): ô chờ đầy và không cá nào trong ô chờ vào được bể. Đây là điều kiện thua duy nhất.</summary>
    public bool IsStuck => !IsWin && !HasPendingMoves && !HasLegalMove;

    private bool AnyTankHasOrder()
    {
        foreach (TankEntry t in Tanks)
        {
            if (t.HasOrder) return true;
        }

        return false;
    }

    // ---------- Thay đổi ----------

    /// <summary>Bỏ cá ở vị trí index khỏi bóng; bóng hết cá thì đánh dấu nổ. Trả về con cá vừa gỡ (view giữ tiếp tham chiếu này).</summary>
    public FishEntry RemoveFishAt(BubbleEntry entry, int index) => RemoveFishAt(entry, index, out _);

    /// <summary>
    /// Như trên, kèm con cá spawner vừa chèn vào đúng index (refill; null nếu bóng không có spawner hoặc hàng đợi đã hết). Chèn ngay
    /// trong model để mọi đường gỡ cá (bấm, Magnet, bộ giải) đều giữ luật: bóng có spawner không nổ khi hàng đợi còn cá.
    /// </summary>
    public FishEntry RemoveFishAt(BubbleEntry entry, int index, out FishEntry refill) => RemoveFishAt(entry, index, out refill, out _);

    /// <summary>
    /// Như trên, kèm newLayer = true khi con vừa gỡ là con cuối của lớp đang hiện và lớp kế của MultilayerBubble vừa lên thay (Fishes
    /// lúc này là cả lớp mới; view sinh đàn mới từ tâm bóng). Đổi lớp ngay trong model để mọi đường gỡ cá đều giữ luật: bóng nhiều lớp
    /// chỉ nổ khi hết lớp cuối.
    /// </summary>
    public FishEntry RemoveFishAt(BubbleEntry entry, int index, out FishEntry refill, out bool newLayer)
    {
        FishEntry fish = entry.Fishes[index];
        entry.Fishes.RemoveAt(index);
        refill = null;
        if (entry.SpawnerFishes.Count > 0)
        {
            refill = entry.SpawnerFishes[0];
            entry.SpawnerFishes.RemoveAt(0);
            entry.Fishes.Insert(index, refill);
        }

        newLayer = PromoteNextLayer(entry);
        if (entry.IsEmpty)
        {
            entry.Popped = true;
            if (entry.IsKey) UnlockKeyLocks(entry.KeyId);
        }
        return fish;
    }

    /// <summary>
    /// AutoFill: gỡ một con khỏi lớp còn ẩn thứ layer của bóng (cá bay thẳng vào bể, người chơi không thấy lớp đổi). Lớp hết cá thì bỏ
    /// luôn để không lộ ra một lớp rỗng. Bóng không rỗng vì cá hiện giữ nguyên.
    /// </summary>
    public FishEntry RemoveLayerFishAt(BubbleEntry entry, int layer, int index)
    {
        List<FishEntry> fishes = entry.Layers[layer];
        FishEntry fish = fishes[index];
        fishes.RemoveAt(index);
        if (fishes.Count == 0) entry.Layers.RemoveAt(layer);
        return fish;
    }

    /// <summary>AutoFill: gỡ một con khỏi hàng đợi spawner của bóng (cá bay thẳng vào bể, không chèn vào đàn). Bóng không rỗng vì cá hiện giữ nguyên.</summary>
    public FishEntry RemoveSpawnerFishAt(BubbleEntry entry, int index)
    {
        FishEntry fish = entry.SpawnerFishes[index];
        entry.SpawnerFishes.RemoveAt(index);
        return fish;
    }

    /// <summary>AutoFill: ghi nợ một con loài fishId đã ứng từ đáy màn; trả khi bóng có loài này lên bàn (TakeNextFromQueue).</summary>
    public void AddDebt(int fishId) => FishDebt.Add(fishId);

    /// <summary>
    /// PackedUp: gom mọi cá đang đậu trong ô chờ vào túi (ô chỉ đang giữ chỗ cho cá bay tới thì bỏ qua, cá đó hạ vào ô như thường).
    /// Trả về chỉ số các ô đã gom, trái sang phải (view rút cá từ đúng các ô này). Ô chờ trống lại nên bấm được tiếp.
    /// </summary>
    public List<int> PackHolding()
    {
        var packed = new List<int>();
        foreach (HoldingEntry h in Holding)
        {
            if (!h.HasFish) continue;
            Bag.Add(h.FishId);
            h.FishId = -1;
            packed.Add(h.Index);
        }

        return packed;
    }

    /// <summary>Con đầu tiên trong túi có bể đang mở nhận (filter như TryFindAutoCollect). Gọi sau khi ô chờ không còn con nào tự thu được.</summary>
    public bool TryFindBagCollect(out int bagIndex, out TankEntry tank, Predicate<TankEntry> filter = null)
    {
        for (int i = 0; i < Bag.Count; i++)
        {
            TankEntry t = FindTankFor(Bag[i], filter);
            if (t == null) continue;
            bagIndex = i;
            tank = t;
            return true;
        }

        bagIndex = -1;
        tank = null;
        return false;
    }

    public int TakeFromBag(int index)
    {
        int id = Bag[index];
        Bag.RemoveAt(index);
        return id;
    }

    /// <summary>Ô chờ đầu tiên (trái sang phải) đang có cá loài fishId, −1 nếu không có.</summary>
    public int FindHoldingFish(int fishId)
    {
        foreach (HoldingEntry h in Holding)
        {
            if (h.HasFish && h.FishId == fishId) return h.Index;
        }

        return -1;
    }

    /// <summary>
    /// AutoFill: tìm một con loài fishId trên bàn theo ưu tiên: bóng bấm được → bóng còn vỏ HP → bóng băng; trong mỗi hạng: cá thường →
    /// cá ẩn → hàng đợi spawner (kind = Spawner, index là vị trí trong SpawnerFishes) → lớp còn ẩn của MultilayerBubble, lớp gần lộ
    /// trước (kind = Layer, layer là vị trí trong Layers, index là vị trí trong lớp). Bóng đá và bóng chưa lên bàn không tính.
    /// </summary>
    public bool TryFindAutoFillSource(int fishId, out BubbleEntry bubble, out int index, out AutoFillSourceKind kind, out int layer)
    {
        layer = -1;
        for (int rank = 0; rank < 4; rank++)
        {
            for (int pass = 0; pass < 4; pass++)
            {
                foreach (BubbleEntry front in FrontBubbles)
                {
                    if (front.IsRock || AutoFillRank(front) != rank) continue;
                    if (pass < 2)
                    {
                        bool wantHidden = pass == 1;
                        for (int i = 0; i < front.Fishes.Count; i++)
                        {
                            FishEntry f = front.Fishes[i];
                            if (f.FishId != fishId || f.IsHidden != wantHidden) continue;
                            bubble = front;
                            index = i;
                            kind = AutoFillSourceKind.Visible;
                            return true;
                        }
                    }
                    else if (pass == 2)
                    {
                        for (int k = 0; k < front.SpawnerFishes.Count; k++)
                        {
                            if (front.SpawnerFishes[k].FishId != fishId) continue;
                            bubble = front;
                            index = k;
                            kind = AutoFillSourceKind.Spawner;
                            return true;
                        }
                    }
                    else
                    {
                        for (int l = 0; l < front.Layers.Count; l++)
                        {
                            List<FishEntry> fishes = front.Layers[l];
                            for (int k = 0; k < fishes.Count; k++)
                            {
                                if (fishes[k].FishId != fishId) continue;
                                bubble = front;
                                index = k;
                                layer = l;
                                kind = AutoFillSourceKind.Layer;
                                return true;
                            }
                        }
                    }
                }
            }
        }

        bubble = null;
        index = -1;
        kind = AutoFillSourceKind.Visible;
        return false;
    }

    /// <summary>Hạng ưu tiên AutoFill: 0 = bấm được, 1 = còn vỏ HP (chưa băng/khoá), 2 = còn băng (chưa khoá), 3 = khoá chìa / khoá đơn.</summary>
    private static int AutoFillRank(BubbleEntry entry)
    {
        if (entry.IsGated || entry.IsKey) return 3;
        if (entry.IsFrozen) return 2;
        if (entry.Hp > 0) return 1;
        return 0;
    }

    /// <summary>
    /// Ép dùng thử PackedUp (BoardManager.PrefillHoldingAsync): tìm một con cá để tự bấm lên ô chờ. Loài phải không bể nào đang mở nhận
    /// (nếu không tự thu kéo nó lên bể ngay). usedSpecies / usedBubbles khác null thì bỏ loài / bóng đã lấy (null = cho trùng).
    /// strict = chỉ cá thường trong bóng bấm được, không có spawner và còn ít nhất 2 con (không làm bóng nổ); false = mọi bóng bấm được,
    /// kể cả cá ẩn, bóng spawner, con cuối. Bóng đá, băng, vỏ HP không tính. Duyệt bóng theo thứ tự sinh, cá theo thứ tự ô.
    /// </summary>
    public bool TryFindHoldingPrefillSource(ICollection<int> usedSpecies, ICollection<BubbleEntry> usedBubbles, bool strict,
        out BubbleEntry bubble, out int index)
    {
        foreach (BubbleEntry front in FrontBubbles)
        {
            if (!front.IsTappable || front.Fishes.Count == 0) continue;
            if (strict && (front.HasSpawner || front.Fishes.Count < 2)) continue;
            if (usedBubbles != null && usedBubbles.Contains(front)) continue;
            for (int i = 0; i < front.Fishes.Count; i++)
            {
                FishEntry f = front.Fishes[i];
                if (strict && f.IsHidden) continue;
                if (FindTankFor(f.FishId) != null) continue;
                if (usedSpecies != null && usedSpecies.Contains(f.FishId)) continue;
                bubble = front;
                index = i;
                return true;
            }
        }

        bubble = null;
        index = -1;
        return false;
    }

    /// <summary>
    /// HiddenFish: mọi con còn lại trong bóng đều đang ẩn thì tất cả tự lộ hình thật (bóng một con ẩn là trường hợp riêng).
    /// Trả về true khi vừa lộ (view đổi model cho cả đàn). Gọi sau khi một con rời bóng (RemoveFishAt), trước khi spawn view
    /// của bóng và sau Shuffle.
    /// </summary>
    public bool TryAutoRevealAll(BubbleEntry entry)
    {
        if (entry == null || entry.IsEmpty) return false;
        foreach (FishEntry fish in entry.Fishes)
        {
            if (!fish.IsHidden) return false;
        }

        foreach (FishEntry fish in entry.Fishes) fish.IsHidden = false;
        return true;
    }

    /// <summary>IceBubble: bóng chạm đỉnh vùng chơi thì vỡ băng, cá bên trong bấm được. Trả về true khi vừa vỡ (view đổi material / config).</summary>
    public bool TryBreakIce(BubbleEntry entry)
    {
        if (entry == null || !entry.IsFrozen) return false;
        entry.IsFrozen = false;
        return true;
    }

    public void ReserveTank(TankEntry tank) => tank.Incoming++;

    public void CancelTankReservation(TankEntry tank) => tank.Incoming = Math.Max(0, tank.Incoming - 1);

    /// <summary>Cá tới bể. Trả về true khi bể vừa đủ cá.</summary>
    public bool ArriveAtTank(TankEntry tank)
    {
        tank.Incoming = Math.Max(0, tank.Incoming - 1);
        tank.Count++;
        return tank.IsComplete;
    }

    /// <summary>TANK-03: chốt bể, cộng tiến độ, lấy đơn kế (TANK-04), trừ HP vỏ, mở khoá đơn khớp loài. Trả về số cá đã thu.</summary>
    public int CompleteTank(TankEntry tank)
    {
        int collected = tank.Count;
        int completedFishId = tank.FishId;
        Collected += collected;
        tank.Count = 0;
        tank.Incoming = 0;
        tank.FishId = -1;
        AssignNextOrder(tank);
        DamageShells();
        UnlockOrderLocks(completedFishId);
        UnlockOrphanOrderLocks();
        return collected;
    }

    /// <summary>
    /// Đánh số cặp ổ khoá / chìa cho view chọn màu (BubbleEntry.KeyPairIndex): ổ khoá theo thứ tự trong data là cặp 0, 1, 2...; chìa mang
    /// keyId (= Index + 1 của ổ, cùng luật với UnlockKeyLocks) nhận số của ổ đó. Chìa không trỏ ổ nào (keyId trống, mở ổ duy nhất) nhận
    /// cặp 0. Chỉ tính lúc dựng từ data nên không đổi khi ổ mở hay khi Restore.
    /// </summary>
    private void AssignKeyPairs()
    {
        int pair = 0;
        foreach (BubbleEntry lockEntry in Bubbles)
        {
            if (!lockEntry.IsLock) continue;
            lockEntry.KeyPairIndex = pair;
            foreach (BubbleEntry key in Bubbles)
            {
                if (key.IsKey && key.KeyId == lockEntry.Index + 1) key.KeyPairIndex = pair;
            }

            pair++;
        }

        foreach (BubbleEntry key in Bubbles)
        {
            if (key.IsKey && key.KeyPairIndex < 0) key.KeyPairIndex = 0;
        }
    }

    /// <summary>Bóng chìa nổ: mở ổ khoá có SoftBubbleId (hoặc số thứ tự 1..n) = keyId của chìa. keyId trống thì mở nếu chỉ còn một ổ.</summary>
    private void UnlockKeyLocks(int lockId)
    {
        int matched = 0;
        foreach (BubbleEntry bubble in Bubbles)
        {
            if (!bubble.IsLock) continue;
            if (lockId > 0 && bubble.Index + 1 == lockId)
            {
                bubble.IsLock = false;
                matched++;
            }
        }

        if (matched > 0) return;

        BubbleEntry sole = null;
        int locked = 0;
        foreach (BubbleEntry bubble in Bubbles)
        {
            if (!bubble.IsLock || bubble.Popped) continue;
            sole = bubble;
            locked++;
        }

        if (locked == 1) sole.IsLock = false;
    }

    /// <summary>Hoàn thành bể loài fishId: mọi khoá đơn đúng loài đó được mở.</summary>
    private void UnlockOrderLocks(int fishId)
    {
        if (fishId < 0) return;
        foreach (BubbleEntry bubble in Bubbles)
        {
            if (bubble.IsOrderBubble && bubble.FishOrderId == fishId) bubble.IsOrderBubble = false;
        }
    }

    /// <summary>Hết đơn khớp trên bể đang mở và OrderQueue thì khoá đơn tự mở.</summary>
    private void UnlockOrphanOrderLocks()
    {
        foreach (BubbleEntry bubble in Bubbles)
        {
            if (bubble.IsOrderBubble && !HasSuitableOrder(bubble)) bubble.IsOrderBubble = false;
        }
    }

    /// <summary>Còn bể đang mở hoặc đơn trong hàng đợi khớp OrderFishId.</summary>
    private bool HasSuitableOrder(BubbleEntry entry)
    {
        if (entry.FishOrderId <= 0) return false;
        foreach (TankEntry tank in Tanks)
        {
            if (tank.HasOrder && tank.FishId == entry.FishOrderId) return true;
        }

        for (int i = 0; i < OrderQueue.Count; i++)
        {
            if (OrderQueue[i] == entry.FishOrderId) return true;
        }

        return false;
    }

    /// <summary>MultiHpBubble: một bể vừa hoàn thành, mọi bóng HP đang hiện trên bàn bị trừ 1 (bóng trong hàng đợi không tính). View đọc lại Hp sau đó.</summary>
    private void DamageShells()
    {
        foreach (BubbleEntry front in FrontBubbles)
        {
            if (front.Hp > 0) front.Hp--;
        }
    }

    /// <summary>Booster UnlockTank: mở thêm một bể (nhận đơn kế ngay). Null nếu đã đủ bể hoặc hết đơn.</summary>
    public TankEntry AddTank()
    {
        if (!CanAddTank) return null;
        var tank = new TankEntry { Index = Tanks.Count, Goal = TankGoal };
        Tanks.Add(tank);
        AssignNextOrder(tank);
        return tank;
    }

    public void ReserveHolding(int index) => Holding[index].Reserved = true;

    public void CancelHoldingReservation(int index) => Holding[index].Reserved = false;

    public void ArriveAtHolding(int index, int fishId)
    {
        Holding[index].Reserved = false;
        Holding[index].FishId = fishId;
    }

    public int TakeFromHolding(int index)
    {
        int id = Holding[index].FishId;
        Holding[index].FishId = -1;
        Holding[index].Reserved = false;
        return id;
    }

    /// <summary>
    /// HOLD-02 kiểu WordSort: cá đầu tiên trong ô chờ (trái sang phải) đã nằm yên (cá đang bay tới ô chờ mới chỉ Reserved, bỏ qua)
    /// và có bể đang cần (kể cả cá đang bay); false nếu không có. filter như FindTankFor (view: chỉ bể đã đậu).
    /// </summary>
    public bool TryFindAutoCollect(out int holdingIndex, out TankEntry tank, Predicate<TankEntry> filter = null)
    {
        foreach (HoldingEntry h in Holding)
        {
            if (!h.HasFish) continue;
            TankEntry t = FindTankFor(h.FishId, filter);
            if (t == null) continue;
            holdingIndex = h.Index;
            tank = t;
            return true;
        }

        holdingIndex = -1;
        tank = null;
        return false;
    }

    public bool AddHoldingSlot()
    {
        if (Holding.Count >= MaxHoldingSlots) return false;
        Holding.Add(new HoldingEntry { Index = Holding.Count });
        return true;
    }

    /// <summary>Lấy đơn kế đúng thứ tự hàng đợi (FIFO như FolderHandler của WordSort; cho phép trùng loài với bể đang hiện).</summary>
    private void AssignNextOrder(TankEntry tank)
    {
        tank.Count = 0;
        tank.Incoming = 0;
        if (OrderQueue.Count == 0)
        {
            tank.FishId = -1;
            return;
        }

        tank.FishId = OrderQueue[0];
        OrderQueue.RemoveAt(0);
    }

    /// <summary>TANK-05 / booster Swap: đơn hiện tại xuống cuối hàng đợi, đơn kế thay vào (cá đã có trong bể do view xử lý).</summary>
    public bool SwapOrder(TankEntry tank)
    {
        if (!tank.HasOrder || OrderQueue.Count == 0) return false;
        OrderQueue.Add(tank.FishId);
        tank.FishId = -1;
        tank.Count = 0;
        tank.Incoming = 0;
        AssignNextOrder(tank);
        return true;
    }

    // ---------- Booster ----------

    /// <summary>MOVE-02: cá ở bóng đang hiện khớp bể thiếu ít nhất; không có thì cá bất kỳ trong bóng ít cá nhất.</summary>
    public bool FindHint(out BubbleEntry bubble, out int fishIndex)
    {
        bubble = null;
        fishIndex = -1;
        if (!CanTap) return false;   // ô chờ đầy: không bấm được gì, chờ tự thu
        int bestMissing = int.MaxValue;
        foreach (BubbleEntry front in FrontBubbles)
        {
            if (!front.IsTappable) continue;   // bóng đá / bóng băng: không bấm được
            for (int i = 0; i < front.Fishes.Count; i++)
            {
                FishEntry fish = front.Fishes[i];
                if (fish.IsHidden) continue;   // cá ẩn: hint không được lộ loài
                TankEntry tank = FindTankFor(fish.FishId);
                if (tank == null || tank.Missing >= bestMissing) continue;
                bestMissing = tank.Missing;
                bubble = front;
                fishIndex = i;
            }
        }

        if (bubble != null) return true;
        if (HoldingFreeCount == 0) return false;
        int smallest = int.MaxValue;
        foreach (BubbleEntry front in FrontBubbles)
        {
            if (!front.IsTappable || front.FishCount == 0 || front.FishCount >= smallest) continue;
            if (front.IsFishSchool && !TryPlanSchool(front, 0, null)) continue;   // đàn không đủ chỗ thì không gợi ý
            smallest = front.FishCount;
            bubble = front;
            fishIndex = 0;
        }

        return bubble != null;
    }

    /// <summary>Có bể đang mang đơn loài này (kể cả bể đã đủ người nhận hay vừa thay đơn, chưa nhận được). FTUE L1: bấm loài này là "chờ", không phải bấm sai.</summary>
    public bool HasOrderFor(int fishId)
    {
        foreach (TankEntry tank in Tanks)
        {
            if (tank.HasOrder && tank.FishId == fishId) return true;
        }

        return false;
    }

    /// <summary>
    /// Idle hint / tutorial: con cá đang hiện (không ẩn, bóng bấm được, không phải đàn) khớp một bể đang nhận (tankFilter, ví dụ bể đã
    /// đậu), ưu tiên bể thiếu ít nhất. Không có con nào khớp thì false: không gợi ý cá đi xuống ô chờ.
    /// </summary>
    public bool FindMatchingHint(Predicate<TankEntry> tankFilter, out BubbleEntry bubble, out int fishIndex)
    {
        bubble = null;
        fishIndex = -1;
        int bestMissing = int.MaxValue;
        foreach (BubbleEntry front in FrontBubbles)
        {
            if (!front.IsTappable || front.IsFishSchool) continue;
            for (int i = 0; i < front.Fishes.Count; i++)
            {
                FishEntry fish = front.Fishes[i];
                if (fish.IsHidden) continue;
                TankEntry tank = FindTankFor(fish.FishId, tankFilter);
                if (tank == null || tank.Missing >= bestMissing) continue;
                bestMissing = tank.Missing;
                bubble = front;
                fishIndex = i;
            }
        }

        return bubble != null;
    }

    /// <summary>
    /// Booster Shuffle: xếp lại cá giữa các bóng đang hiện bấm được, giữ số cá mỗi bóng (bán kính không đổi). Shuffle chỉ để gỡ kẹt,
    /// không sắp sẵn cả bàn: với tối đa groupCount bể đang mở (thiếu ít ưu tiên, đồng hạng ngẫu nhiên), bể nào có đủ số con còn thiếu
    /// của loài đó trong các bóng tham gia (bể trống = TankGoal con) thì gom đúng số con đó vào MỘT bóng: bóng trên cùng (heightOf lớn
    /// nhất) trong các bóng còn chứa được cả cụm; cụm ưu tiên cá đang hiện, thiếu mới lấy cá ẩn. Bể không đủ cá trên bàn, hoặc không
    /// bóng nào chứa nổi cả cụm, thì bỏ qua (không gom cụm thiếu, không chia cụm ra nhiều bóng); không bể nào đạt thì trộn thuần ngẫu
    /// nhiên. Mọi cá còn lại trộn ngẫu nhiên, thứ tự ô trong bóng ngẫu nhiên. groupCount &lt;= 0 = trộn thuần ngẫu nhiên. heightOf (view:
    /// độ cao tâm bóng) null thì bóng đích chọn ngẫu nhiên trong các bóng vừa. Cờ ẩn đi theo con cá; bóng toàn cá ẩn thì lộ hết. Bóng
    /// đá / băng / vỏ HP / khoá / bóng đàn không tham gia (cá trong đó không tính là "đủ"), hàng đợi spawner và lớp ẩn giữ nguyên.
    /// Trả về false nếu chưa đủ 2 bóng. FishEntry giữ nguyên tham chiếu nên view tra được bóng đích của từng con.
    /// </summary>
    public bool ShuffleFrontBubbles(Random rng, int groupCount, Func<BubbleEntry, float> heightOf = null)
    {
        var fronts = new List<BubbleEntry>();
        foreach (BubbleEntry front in FrontBubbles)
        {
            if (front.IsTappable && front.FishCount > 0 && !front.IsFishSchool) fronts.Add(front);   // bóng đàn giữ nguyên cá
        }

        if (fronts.Count < 2) return false;

        // Cá theo loài: xáo trong loài để con nào vào cụm là ngẫu nhiên, rồi đưa cá đang hiện lên trước cá ẩn (giữ thứ tự đã xáo) để
        // cụm ưu tiên cá người chơi nhìn thấy được.
        var bySpecies = new Dictionary<int, List<FishEntry>>();
        foreach (BubbleEntry b in fronts)
        {
            foreach (FishEntry f in b.Fishes)
            {
                if (!bySpecies.TryGetValue(f.FishId, out List<FishEntry> list)) bySpecies[f.FishId] = list = new List<FishEntry>();
                list.Add(f);
            }
        }

        foreach (List<FishEntry> list in bySpecies.Values)
        {
            ShuffleList(list, rng);
            var hidden = new List<FishEntry>();
            foreach (FishEntry f in list)
            {
                if (f.IsHidden) hidden.Add(f);
            }

            if (hidden.Count == 0 || hidden.Count == list.Count) continue;
            list.RemoveAll(f => f.IsHidden);
            list.AddRange(hidden);
        }

        // Bóng nhận cá: sức chứa = số cá cũ. Độ cao để chọn bóng đích của cụm; không có heightOf thì ngẫu nhiên.
        var slots = new List<(BubbleEntry bubble, List<FishEntry> fish)>();
        foreach (BubbleEntry b in fronts) slots.Add((b, new List<FishEntry>(b.FishCount)));
        var capacity = new int[slots.Count];
        var heights = new double[slots.Count];
        for (int i = 0; i < slots.Count; i++)
        {
            capacity[i] = slots[i].bubble.FishCount;
            heights[i] = heightOf != null ? heightOf(slots[i].bubble) : rng.NextDouble();
        }

        // Bể ứng viên: bể đang mở còn thiếu cá; thiếu ít xét trước, đồng hạng ngẫu nhiên.
        var candidates = new List<(double key, TankEntry tank)>();
        if (groupCount > 0)
        {
            foreach (TankEntry tank in Tanks)
            {
                if (tank.HasOrder && tank.Missing > 0) candidates.Add((tank.Missing + rng.NextDouble() * 0.5, tank));
            }

            candidates.Sort((a, b) => a.key.CompareTo(b.key));
        }

        // Gom cụm: bể đủ cá trên bàn và có bóng chứa được cả cụm thì xếp vào bóng trên cùng trong các bóng đó; đủ groupCount cụm thì
        // bỏ qua các bể còn lại.
        var taken = new Dictionary<int, int>();   // loài → số con đã vào cụm (hai bể trùng loài không lấy trùng cá)
        var grouped = new HashSet<FishEntry>();
        int groups = 0;
        foreach ((double _, TankEntry tank) in candidates)
        {
            if (groups >= groupCount) break;
            if (!bySpecies.TryGetValue(tank.FishId, out List<FishEntry> pool)) continue;
            int need = tank.Missing;
            int offset = taken.GetValueOrDefault(tank.FishId);
            if (pool.Count - offset < need) continue;   // không đủ cá trên bàn cho bể này

            int pick = -1;
            for (int i = 0; i < slots.Count; i++)
            {
                if (capacity[i] - slots[i].fish.Count < need) continue;   // không chứa nổi cả cụm
                if (pick < 0 || heights[i] > heights[pick]) pick = i;
            }

            if (pick < 0) continue;
            for (int k = 0; k < need; k++)
            {
                FishEntry f = pool[offset + k];
                slots[pick].fish.Add(f);
                grouped.Add(f);
            }

            taken[tank.FishId] = offset + need;
            groups++;
        }

        // Cá ngoài cụm trộn ngẫu nhiên, lấp chỗ còn trống.
        var rest = new List<FishEntry>();
        foreach (List<FishEntry> list in bySpecies.Values)
        {
            foreach (FishEntry f in list)
            {
                if (!grouped.Contains(f)) rest.Add(f);
            }
        }

        ShuffleList(rest, rng);
        int next = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            while (slots[i].fish.Count < capacity[i] && next < rest.Count) slots[i].fish.Add(rest[next++]);
        }

        foreach ((BubbleEntry bubble, List<FishEntry> fish) in slots)
        {
            ShuffleList(fish, rng);
            bubble.Fishes.Clear();
            bubble.Fishes.AddRange(fish);
        }

        // Cờ ẩn đi theo con cá; bóng nào toàn cá ẩn thì lộ hết (view dựng lại model theo cờ sau khi cá bay tới).
        foreach (BubbleEntry b in fronts) TryAutoRevealAll(b);
        return true;
    }

    private static void ShuffleList<T>(List<T> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ---------- Snapshot (bộ giải LevelSolver quay lui) ----------

    public BoardSnapshot Snapshot()
    {
        var s = new BoardSnapshot
        {
            Collected = Collected,
            SpawnedCount = SpawnedCount,
            OrderQueue = OrderQueue.ToArray(),
            TankFishId = new int[Tanks.Count],
            TankCount = new int[Tanks.Count],
            TankIncoming = new int[Tanks.Count],
            HoldingFishId = new int[Holding.Count],
            HoldingReserved = new bool[Holding.Count],
            BubbleFish = new FishEntry[Bubbles.Count][],
            BubblePopped = new bool[Bubbles.Count],
            BubbleFrozen = new bool[Bubbles.Count],
            BubbleLocked = new bool[Bubbles.Count],
            BubbleOrder = new bool[Bubbles.Count],
            BubbleKey = new bool[Bubbles.Count],
            BubbleHp = new int[Bubbles.Count],
            BubbleSpawner = new FishEntry[Bubbles.Count][],
            BubbleLayers = new FishEntry[Bubbles.Count][][]
        };
        for (int i = 0; i < Tanks.Count; i++)
        {
            s.TankFishId[i] = Tanks[i].FishId;
            s.TankCount[i] = Tanks[i].Count;
            s.TankIncoming[i] = Tanks[i].Incoming;
        }

        for (int i = 0; i < Holding.Count; i++)
        {
            s.HoldingFishId[i] = Holding[i].FishId;
            s.HoldingReserved[i] = Holding[i].Reserved;
        }

        for (int i = 0; i < Bubbles.Count; i++)
        {
            var fishes = new FishEntry[Bubbles[i].Fishes.Count];
            for (int k = 0; k < fishes.Length; k++) fishes[k] = Bubbles[i].Fishes[k].Clone();
            s.BubbleFish[i] = fishes;
            s.BubblePopped[i] = Bubbles[i].Popped;
            s.BubbleFrozen[i] = Bubbles[i].IsFrozen;
            s.BubbleLocked[i] = Bubbles[i].IsLock;
            s.BubbleOrder[i] = Bubbles[i].IsOrderBubble;
            s.BubbleKey[i] = Bubbles[i].IsKey;
            s.BubbleHp[i] = Bubbles[i].Hp;
            var spawner = new FishEntry[Bubbles[i].SpawnerFishes.Count];
            for (int k = 0; k < spawner.Length; k++) spawner[k] = Bubbles[i].SpawnerFishes[k].Clone();
            s.BubbleSpawner[i] = spawner;
            var layers = new FishEntry[Bubbles[i].Layers.Count][];
            for (int l = 0; l < layers.Length; l++)
            {
                List<FishEntry> source = Bubbles[i].Layers[l];
                layers[l] = new FishEntry[source.Count];
                for (int k = 0; k < source.Count; k++) layers[l][k] = source[k].Clone();
            }

            s.BubbleLayers[i] = layers;
        }

        return s;
    }

    /// <summary>Khôi phục snapshot (bộ giải). Bể mở thêm sau snapshot vẫn giữ.</summary>
    public void Restore(BoardSnapshot s)
    {
        Collected = s.Collected;
        SpawnedCount = Math.Clamp(s.SpawnedCount, 0, Bubbles.Count);
        OrderQueue.Clear();
        OrderQueue.AddRange(s.OrderQueue);
        for (int i = 0; i < Tanks.Count && i < s.TankFishId.Length; i++)
        {
            Tanks[i].FishId = s.TankFishId[i];
            Tanks[i].Count = s.TankCount[i];
            Tanks[i].Incoming = s.TankIncoming[i];
        }

        while (Holding.Count < s.HoldingFishId.Length) Holding.Add(new HoldingEntry { Index = Holding.Count });
        for (int i = 0; i < Holding.Count; i++)
        {
            bool has = i < s.HoldingFishId.Length;
            Holding[i].FishId = has ? s.HoldingFishId[i] : -1;
            Holding[i].Reserved = has && s.HoldingReserved[i];
        }

        for (int i = 0; i < Bubbles.Count && i < s.BubbleFish.Length; i++)
        {
            Bubbles[i].Fishes.Clear();
            foreach (FishEntry f in s.BubbleFish[i]) Bubbles[i].Fishes.Add(f.Clone());
            Bubbles[i].Popped = s.BubblePopped[i];
            Bubbles[i].IsFrozen = s.BubbleFrozen != null && i < s.BubbleFrozen.Length && s.BubbleFrozen[i];
            Bubbles[i].IsLock = s.BubbleLocked != null && i < s.BubbleLocked.Length && s.BubbleLocked[i];
            Bubbles[i].IsOrderBubble = s.BubbleOrder != null && i < s.BubbleOrder.Length && s.BubbleOrder[i];
            Bubbles[i].IsKey = s.BubbleKey != null && i < s.BubbleKey.Length && s.BubbleKey[i];
            if (s.BubbleHp != null && i < s.BubbleHp.Length) Bubbles[i].Hp = s.BubbleHp[i];
            Bubbles[i].SpawnerFishes.Clear();
            if (s.BubbleSpawner != null && i < s.BubbleSpawner.Length && s.BubbleSpawner[i] != null)
            {
                foreach (FishEntry f in s.BubbleSpawner[i]) Bubbles[i].SpawnerFishes.Add(f.Clone());
            }

            Bubbles[i].Layers.Clear();
            if (s.BubbleLayers != null && i < s.BubbleLayers.Length && s.BubbleLayers[i] != null)
            {
                foreach (FishEntry[] source in s.BubbleLayers[i])
                {
                    var layer = new List<FishEntry>(source.Length);
                    foreach (FishEntry f in source) layer.Add(f.Clone());
                    Bubbles[i].Layers.Add(layer);
                }
            }
        }
    }
}

[Serializable]
public class BoardSnapshot
{
    public int Collected;
    public int SpawnedCount;
    public int[] OrderQueue;
    public int[] TankFishId;
    public int[] TankCount;
    public int[] TankIncoming;
    public int[] HoldingFishId;
    public bool[] HoldingReserved;
    public FishEntry[][] BubbleFish;
    public bool[] BubblePopped;
    public bool[] BubbleFrozen;
    public bool[] BubbleLocked;
    public bool[] BubbleOrder;
    public bool[] BubbleKey;
    public int[] BubbleHp;
    public FishEntry[][] BubbleSpawner;
    public FishEntry[][][] BubbleLayers;
}
