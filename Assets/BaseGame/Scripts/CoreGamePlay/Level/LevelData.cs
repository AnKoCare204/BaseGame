using System;
using System.Collections.Generic;
using Core.GamePlay;
using Core.GamePlay.GlobalEnum;
using Sirenix.OdinInspector;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Seed một level, tối giản: Level, Difficulty, hàng đợi đơn bể (OrderQueue) và danh sách bóng (SoftBubbleDataArray, thứ tự
/// trong mảng = thứ tự sinh; GamePlayGlobalConfig.VisibleBubbleCount bóng đầu hiện lúc bắt đầu, mỗi bóng nổ thì bóng kế sinh thêm).
/// Luật cố định (không nằm trong data): TankGoal = 3, số bể mở sẵn / khóa và số ô chờ lấy từ GamePlayGlobalConfig.
/// Ràng buộc: số cá mỗi loài = số đơn loài đó × TankGoal.
/// </summary>
[Serializable]
public class LevelData
{
    [field: SerializeField, Min(1)] public int Level { get; private set; } = 1;
    [field: SerializeField] public LevelDifficulty Difficulty { get; private set; } = LevelDifficulty.Normal;

    [field: SerializeField, Tooltip("Hàng đợi đơn theo FishId; DefaultTankSlots đơn đầu hiện ngay, bể xong thì lấy đơn kế")]
    public int[] OrderQueue { get; private set; } = Array.Empty<int>();

    [field: SerializeField, Tooltip("Bóng theo thứ tự sinh: VisibleBubbleCount bóng đầu hiện lúc bắt đầu, còn lại chờ trong hàng đợi")]
    public SoftBubbleData[] SoftBubbleDataArray { get; private set; } = Array.Empty<SoftBubbleData>();

    public int TotalFish => (OrderQueue?.Length ?? 0) * GamePlayGlobalConfig.TankGoal;
    public int BubbleCount => SoftBubbleDataArray?.Length ?? 0;

    public static LevelData Create(int level, LevelDifficulty difficulty, int[] orderQueue, SoftBubbleData[] bubbles)
    {
        return new LevelData
        {
            Level = level,
            Difficulty = difficulty,
            OrderQueue = orderQueue ?? Array.Empty<int>(),
            SoftBubbleDataArray = bubbles ?? Array.Empty<SoftBubbleData>()
        };
    }

    /// <summary>Kiểm tra ràng buộc seed. Trả về true khi hợp lệ, lỗi ghi vào errors.</summary>
    public bool Validate(List<string> errors)
    {
        errors ??= new List<string>();
        int before = errors.Count;
        int tankGoal = GamePlayGlobalConfig.TankGoal;
        if (OrderQueue == null || OrderQueue.Length == 0) errors.Add("OrderQueue rỗng");
        if (SoftBubbleDataArray == null || SoftBubbleDataArray.Length == 0) errors.Add("Không có bóng");

        var fishPerSpecies = new Dictionary<int, int>();
        var ordersPerSpecies = new Dictionary<int, int>();
        if (OrderQueue != null)
        {
            foreach (int id in OrderQueue)
            {
                if (id < 0) errors.Add($"OrderQueue có FishId âm ({id})");
                ordersPerSpecies[id] = ordersPerSpecies.GetValueOrDefault(id) + 1;
            }
        }

        if (SoftBubbleDataArray != null)
        {
            for (int i = 0; i < SoftBubbleDataArray.Length; i++)
            {
                SoftBubbleData b = SoftBubbleDataArray[i];
                if (b == null)
                {
                    errors.Add($"Bóng {i} null");
                    continue;
                }

                if (b.isRock)
                {
                    // Bóng đá chỉ chiếm chỗ, không nuôi cá; có cá trong data là lỗi vì runtime sẽ bỏ qua chúng.
                    if (b.FishDataArray != null && b.FishDataArray.Length > 0) errors.Add($"Bóng {i} là bóng đá nhưng có {b.FishDataArray.Length} cá");
                    if (b.isBubbleHp) errors.Add($"Bóng {i} là bóng đá nhưng bật HP (đá không có vỏ HP)");
                    if (b.isSpawner) errors.Add($"Bóng {i} là bóng đá nhưng bật spawner (đá không có cá)");
                    if (b.isMultiLayer) errors.Add($"Bóng {i} là bóng đá nhưng bật multiLayer (đá không có cá)");
                    if (b.isBundleBubble) errors.Add($"Bóng {i} là bóng đá nhưng bật bundle (đá không có cá)");
                    if (b.isConveyor) errors.Add($"Bóng {i} là bóng đá nhưng bật băng chuyền (đá không có cá)");
                    if (b.isLock) errors.Add($"Bóng {i} là bóng đá nhưng bật khoá chìa");
                    continue;
                }

                if (b.isBubbleHp && b.bubbleHp <= 0) errors.Add($"Bóng {i} bật HP nhưng bubbleHp = {b.bubbleHp}");
                if (b.isLock && b.isKey) errors.Add($"Bóng {i} vừa khoá chìa vừa chìa");

                if (b.isSpawner)
                {
                    // Spawner: hàng đợi cá trong con spawner, mỗi con cá hiện rời bóng thì một con trong hàng đợi thay vào; cần có cá hiện để kích.
                    if (b.fishOnSpawner == null || b.fishOnSpawner.Length == 0) errors.Add($"Bóng {i} bật spawner nhưng fishOnSpawner rỗng");
                    else if (b.FishDataArray == null || b.FishDataArray.Length == 0) errors.Add($"Bóng {i} có spawner nhưng không có cá hiện để kích spawner");
                    if (b.fishOnSpawner != null)
                    {
                        foreach (FishData f in b.fishOnSpawner)
                        {
                            if (f != null) fishPerSpecies[f.FishId] = fishPerSpecies.GetValueOrDefault(f.FishId) + 1;
                        }
                    }
                }

                if (!b.isMultiLayer && b.layers != null && b.layers.Length > 0)
                {
                    errors.Add($"Bóng {i} tắt multiLayer nhưng layers còn {b.layers.Length} lớp");
                }

                if (b.isMultiLayer)
                {
                    // MultilayerBubble: FishDataArray là lớp đầu, layers lộ dần theo index tăng dần khi lớp trước hết cá. Không dùng chung
                    // với spawner (runtime bỏ qua các lớp của bóng có spawner nên cá trong đó không được đếm).
                    if (b.isSpawner) errors.Add($"Bóng {i} bật cả multiLayer và spawner (không dùng chung, runtime bỏ qua các lớp)");
                    if (b.layers == null || b.layers.Length == 0) errors.Add($"Bóng {i} bật multiLayer nhưng layers rỗng");
                    else
                    {
                        var layerIndices = new HashSet<int>();

                        // Luật của tool vẽ level: lớp sau không được nhiều cá hơn lớp trước, tính từ FishDataArray.
                        int previousCount = b.FishCount;
                        for (int k = 0; k < b.layers.Length; k++)
                        {
                            BubbleLayerData layer = b.layers[k];
                            if (layer == null || layer.FishDataArray == null || layer.FishDataArray.Length == 0)
                            {
                                errors.Add($"Bóng {i} lớp {k} không có cá");
                                previousCount = 0;
                                continue;
                            }

                            if (!layerIndices.Add(layer.index)) errors.Add($"Bóng {i} có hai lớp trùng index {layer.index}");
                            if (layer.FishDataArray.Length > previousCount)
                            {
                                errors.Add($"Bóng {i} lớp {k + 1} có {layer.FishDataArray.Length} cá, " +
                                           $"nhiều hơn lớp trước ({previousCount})");
                            }

                            previousCount = layer.FishDataArray.Length;
                            if (b.isSpawner) continue;
                            foreach (FishData f in layer.FishDataArray)
                            {
                                if (f != null) fishPerSpecies[f.FishId] = fishPerSpecies.GetValueOrDefault(f.FishId) + 1;
                            }
                        }
                    }
                }

                if (b.isBundleBubble)
                {
                    // FishSchoolBubble: bấm một con là cả đàn bay. Không dùng chung với spawner (cá thay vào liên tục) và multiLayer
                    // (lớp kế lộ ra sau khi đàn đi); runtime gặp cả hai thì bỏ cờ bundle.
                    if (b.isSpawner) errors.Add($"Bóng {i} bật cả bundle (FishSchool) và spawner (không dùng chung)");
                    if (b.isMultiLayer) errors.Add($"Bóng {i} bật cả bundle (FishSchool) và multiLayer (không dùng chung)");
                }

                if (b.FishDataArray == null || b.FishDataArray.Length == 0)
                {
                    errors.Add($"Bóng {i} không có cá");
                    continue;
                }

                foreach (FishData f in b.FishDataArray)
                {
                    if (f != null) fishPerSpecies[f.FishId] = fishPerSpecies.GetValueOrDefault(f.FishId) + 1;
                }
            }
        }

        foreach (KeyValuePair<int, int> kv in ordersPerSpecies)
        {
            int need = kv.Value * tankGoal;
            int have = fishPerSpecies.GetValueOrDefault(kv.Key);
            if (have != need) errors.Add($"Loài {kv.Key}: có {have} cá, cần {need} ({kv.Value} đơn × {tankGoal})");
        }

        foreach (KeyValuePair<int, int> kv in fishPerSpecies)
        {
            if (!ordersPerSpecies.ContainsKey(kv.Key)) errors.Add($"Loài {kv.Key} có cá nhưng không có đơn");
        }

        if (OrderQueue != null && OrderQueue.Length > 0)
        {
            // Không 2 đơn cùng loài trong các bể mở sẵn.
            int tankSlots = LevelBands.DefaultTankSlots;
            var seen = new HashSet<int>();
            for (int i = 0; i < Mathf.Min(tankSlots, OrderQueue.Length); i++)
            {
                if (!seen.Add(OrderQueue[i])) errors.Add($"Đơn đầu trùng loài {OrderQueue[i]} trong {tankSlots} bể mở sẵn");
            }

            // Nước đầu: ít nhất 1 cá trong các bóng hiện lúc bắt đầu khớp một đơn đang hiện.
            bool firstMove = false;
            if (SoftBubbleDataArray != null)
            {
                int visible = Mathf.Min(LevelBands.VisibleBubbles, SoftBubbleDataArray.Length);
                for (int i = 0; i < visible && !firstMove; i++) firstMove = SoftBubbleDataArray[i] != null && SoftBubbleDataArray[i].Contains(seen);
            }

            if (!firstMove) errors.Add($"Không có cá nào trong {LevelBands.VisibleBubbles} bóng đầu khớp đơn đang hiện");
        }

        ValidateKeyLocks(errors);

        return errors.Count == before;
    }

    /// <summary>Mỗi ổ khoá chìa phải có đúng một chìa cầm keyId = softBubbleId; mỗi chìa phải trỏ ổ thật.</summary>
    private void ValidateKeyLocks(List<string> errors)
    {
        if (SoftBubbleDataArray == null) return;
        var lockIds = new HashSet<int>();
        var keyTargets = new Dictionary<int, int>();
        for (int i = 0; i < SoftBubbleDataArray.Length; i++)
        {
            SoftBubbleData b = SoftBubbleDataArray[i];
            if (b == null || b.isRock) continue;
            if (b.isLock)
            {
                if (b.softBubbleId <= 0) errors.Add($"Bóng khoá chìa {i}: softBubbleId chưa gán");
                else if (!lockIds.Add(b.softBubbleId)) errors.Add($"Bóng khoá chìa {i}: softBubbleId {b.softBubbleId} trùng");
            }

            if (!b.isKey) continue;
            if (b.keyId <= 0) errors.Add($"Bóng chìa {i}: chưa chọn ổ (keyId)");
            else keyTargets[b.keyId] = keyTargets.GetValueOrDefault(b.keyId) + 1;
        }

        foreach (int lockId in lockIds)
        {
            int keys = keyTargets.GetValueOrDefault(lockId);
            if (keys == 0) errors.Add($"Ổ khoá chìa id {lockId}: không có chìa");
            else if (keys > 1) errors.Add($"Ổ khoá chìa id {lockId}: có {keys} chìa");
        }

        foreach (KeyValuePair<int, int> kv in keyTargets)
        {
            if (!lockIds.Contains(kv.Key)) errors.Add($"Chìa trỏ keyId {kv.Key} nhưng không có ổ khoá đó");
        }
    }

    /// <summary>
    /// Bản cũ của nút Random: loài lấy mặc định 1..14 chứ không hỏi FishModelGlobalConfig.
    /// bubbleRadiusMin/Max giữ lại cho khớp chữ ký nút cũ nhưng không có tác dụng - cỡ bóng do
    /// GamePlayGlobalConfig.RandomBubbleRadius quyết định trong SoftBubbleData.CreateWithRandomRadius.
    /// </summary>
    [Button("Random LevelData")]
    public void RandomData(int orderCount = 6, int fishTypes = 3, int minFishPerBubble = 1, int maxFishPerBubble = 2, float bubbleRadiusMin = 0.9f, float bubbleRadiusMax = 1.5f)
    {
        var species = new List<int>();
        for (int i = 1; i <= 14; i++) species.Add(i);
        RandomData(species, orderCount, fishTypes, minFishPerBubble, maxFishPerBubble);
    }

    /// <summary>
    /// Sinh LevelData ngẫu nhiên nhất quán (đủ cá cho đơn, có nước đầu, không trùng loài ở bể mở sẵn), giữ Level và Difficulty.
    /// speciesPool = các loài được phép dùng (tool vẽ level truyền ID thật từ FishModelGlobalConfig);
    /// orderCount = số đơn (tổng cá = orderCount × TankGoal); fishTypes = số loài bốc ngẫu nhiên trong speciesPool;
    /// minFishPerBubble..maxFishPerBubble = số cá mỗi bóng.
    /// </summary>
    public void RandomData(IList<int> speciesPool, int orderCount = 6, int fishTypes = 3, int minFishPerBubble = 1,
        int maxFishPerBubble = 2)
    {
        if (!RandomOrderQueue(speciesPool, orderCount, fishTypes)) return;
        RandomBubbles(minFishPerBubble, maxFishPerBubble, true);
    }

    /// <summary>
    /// Chỉ sinh lại OrderQueue: chia đều orderCount đơn cho fishTypes loài bốc trong speciesPool rồi xáo sao cho các bể
    /// mở sẵn không có hai đơn cùng loài. Không đụng tới bóng nên số cá sẽ lệch với đơn cho tới khi sinh lại bóng.
    /// Trả về false khi speciesPool rỗng (chưa có loài nào để bốc).
    /// </summary>
    public bool RandomOrderQueue(IList<int> speciesPool, int orderCount = 6, int fishTypes = 3)
    {
        if (speciesPool == null || speciesPool.Count == 0) return false;

        int tankSlots = LevelBands.DefaultTankSlots;
        orderCount = Mathf.Max(1, orderCount);
        fishTypes = Mathf.Clamp(fishTypes, 1, speciesPool.Count);

        // Chọn loài.
        var species = new List<int>(speciesPool);
        Shuffle(species);
        species.RemoveRange(fishTypes, species.Count - fishTypes);

        // Đơn: chia đều loài, xáo, không trùng loài trong các bể mở sẵn.
        var orders = new List<int>();
        for (int i = 0; i < orderCount; i++) orders.Add(species[i % species.Count]);
        for (int attempt = 0; attempt < 50; attempt++)
        {
            Shuffle(orders);
            var seen = new HashSet<int>();
            bool ok = true;
            for (int i = 0; i < Mathf.Min(tankSlots, orders.Count); i++) ok &= seen.Add(orders[i]);
            if (ok) break;
        }

        OrderQueue = orders.ToArray();
        return true;
    }

    /// <summary>
    /// Sinh lại toàn bộ bóng từ OrderQueue đang có: mỗi đơn góp TankGoal con cá, gói vào các bóng có số cá ngẫu nhiên
    /// trong minFishPerBubble..maxFishPerBubble.
    /// shuffleFish = false (mặc định): kho cá giữ nguyên thứ tự đơn nên bóng đầu chứa cá của đơn đầu, người chơi cứ nổ
    /// lần lượt là xong - level dễ thở. shuffleFish = true: xáo kho cá rồi xáo luôn thứ tự bóng (vẫn giữ nước đầu),
    /// ra level khó hơn hẳn. Trả về false khi OrderQueue rỗng.
    /// </summary>
    public bool RandomBubbles(int minFishPerBubble = 1, int maxFishPerBubble = 2, bool shuffleFish = false,
        float largestShare = DefaultLargestBubbleShare)
    {
        if (OrderQueue == null || OrderQueue.Length == 0) return false;

        int tankGoal = GamePlayGlobalConfig.TankGoal;
        // Không chặn trần số cá mỗi bóng: kéo tay trong tool vẽ level nhét bao nhiêu con cũng được nên khâu random
        // theo đúng vậy. Lưu ý cỡ bóng vẫn lấy trong dải BubbleRadiusMin..Max, dải đó tuned cho bóng ít cá.
        maxFishPerBubble = Mathf.Max(1, maxFishPerBubble);
        minFishPerBubble = Mathf.Clamp(minFishPerBubble, 1, maxFishPerBubble);

        // Kho cá = mỗi đơn × TankGoal, gói vào bóng với cỡ ngẫu nhiên.
        var pool = new List<int>();
        foreach (int id in OrderQueue)
        {
            for (int k = 0; k < tankGoal; k++) pool.Add(id);
        }

        if (shuffleFish) Shuffle(pool);

        var bubbles = new List<SoftBubbleData>();
        int cursor = 0;
        while (cursor < pool.Count)
        {
            int size = NextBubbleSize(minFishPerBubble, maxFishPerBubble, largestShare, pool.Count - cursor);
            var ids = new int[size];
            for (int k = 0; k < size; k++) ids[k] = pool[cursor + k];
            cursor += size;
            bubbles.Add(SoftBubbleData.CreateWithRandomRadius(ids));
        }

        if (shuffleFish) ShuffleBubblesKeepingFirstMove(bubbles);

        SoftBubbleDataArray = bubbles.ToArray();
        return true;
    }

    /// <summary>
    /// Xáo cá giữa các bóng từ bóng thứ startIndex (0-based) trở đi: gom hết cá của khúc sau, xáo, rồi chia lại đúng
    /// số cá cũ của từng bóng. Các bóng phía trước không đụng tới, nhờ vậy giữ được đoạn mở đầu dễ và chỉ làm khó
    /// phần sau. Cá mang theo cả cờ isHidden; fishOnSpawner của bóng sinh không dính gì.
    /// Trả về số bóng đã xáo.
    /// </summary>
    public int ShuffleFishFromBubble(int startIndex)
    {
        if (SoftBubbleDataArray == null) return 0;
        startIndex = Mathf.Clamp(startIndex, 0, SoftBubbleDataArray.Length);

        var pool = new List<FishData>();
        for (int i = startIndex; i < SoftBubbleDataArray.Length; i++)
        {
            FishData[] fish = SoftBubbleDataArray[i]?.FishDataArray;
            if (fish == null) continue;
            foreach (FishData entry in fish)
            {
                if (entry != null) pool.Add(entry);
            }
        }

        if (pool.Count == 0) return 0;

        Shuffle(pool);
        int cursor = 0;
        int changed = 0;
        for (int i = startIndex; i < SoftBubbleDataArray.Length; i++)
        {
            SoftBubbleData bubble = SoftBubbleDataArray[i];
            if (bubble?.FishDataArray == null) continue;

            // Kẹp theo phần kho còn lại: bóng nào có ô null thì kho hụt so với tổng Length, bóng cuối nhận ít đi
            // chứ không được phép đọc lố.
            int count = Mathf.Min(bubble.FishDataArray.Length, pool.Count - cursor);
            var slots = new FishData[count];
            for (int k = 0; k < count; k++) slots[k] = pool[cursor + k];
            cursor += count;
            bubble.FishDataArray = slots;
            changed++;
        }

        return changed;
    }

    /// <summary>Tỉ lệ bóng nhiều cá nhất khi sinh: cỡ max chỉ chiếm chừng này, các cỡ còn lại chia đều phần kia.</summary>
    public const float DefaultLargestBubbleShare = 0.25f;

    /// <summary>
    /// Số cá cho bóng kế tiếp. Cỡ max chỉ ra với xác suất largestShare (mặc định 25%), các cỡ min..max-1 chia đều
    /// phần còn lại - bốc đều tay thì bóng to chiếm 1/(max-min+1), nhiều quá nên ra ít bóng.
    ///
    /// remaining = số cá còn trong kho. Không để lượt sau còn dư ít hơn min: dồn được vào bóng này (không vượt max)
    /// thì dồn, không thì cắt bớt để chừa đúng min cho bóng cuối. Nhờ vậy không còn bóng lẻ 1 cá khi min = 2.
    /// </summary>
    private static int NextBubbleSize(int min, int max, float largestShare, int remaining)
    {
        int size = max > min
            ? (Random.value < Mathf.Clamp01(largestShare) ? max : Random.Range(min, max))
            : min;

        if (size >= remaining) return remaining;

        int left = remaining - size;
        if (left >= min) return size;

        return size + left <= max ? size + left : remaining - min;
    }

    /// <summary>Xáo thứ tự bóng nhưng vẫn bảo đảm nước đầu: một trong các bóng hiện lúc bắt đầu có cá khớp đơn đang hiện.</summary>
    private void ShuffleBubblesKeepingFirstMove(List<SoftBubbleData> bubbles)
    {
        Shuffle(bubbles);

        var visible = new HashSet<int>();
        for (int i = 0; i < Mathf.Min(LevelBands.DefaultTankSlots, OrderQueue.Length); i++) visible.Add(OrderQueue[i]);

        int frontCount = Mathf.Min(LevelBands.VisibleBubbles, bubbles.Count);
        for (int i = 0; i < frontCount; i++)
        {
            if (bubbles[i].Contains(visible)) return;
        }

        for (int i = frontCount; i < bubbles.Count; i++)
        {
            if (!bubbles[i].Contains(visible)) continue;
            (bubbles[0], bubbles[i]) = (bubbles[i], bubbles[0]);
            return;
        }
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
    
    public HashSet<ObstacleFeatureType> GetPresentObstacleFeatures()
    {
        HashSet<ObstacleFeatureType> present = new HashSet<ObstacleFeatureType>();
        if (SoftBubbleDataArray == null) return present;

        for (int i = 0; i < SoftBubbleDataArray.Length; i++)
        {
            SoftBubbleData bubble = SoftBubbleDataArray[i];
            if (bubble == null) continue;

            if (bubble.isIce) present.Add(ObstacleFeatureType.Ice);
            if (bubble.isSpawner) present.Add(ObstacleFeatureType.Spawner);
            if (bubble.isLock || bubble.isKey) present.Add(ObstacleFeatureType.LockKey);
            if (bubble.isBubbleHp) present.Add(ObstacleFeatureType.BubbleHp);
            if (bubble.isRock) present.Add(ObstacleFeatureType.Rock);
            if (bubble.isOrderBubble) present.Add(ObstacleFeatureType.BubbleOrder);
            if (bubble.isMultiLayer) present.Add(ObstacleFeatureType.MultiLayer);
            if (bubble.isBundleBubble) present.Add(ObstacleFeatureType.FishSchool);
            // HiddenFish là cờ của từng con cá (FishData.isHidden), không phải cờ của bóng: BubbleObstacle.Hidden đã bỏ
            // khỏi DesignConfig nên SoftBubbleData không còn field isHidden nào để xét.
            if (ContainsHiddenFish(bubble.FishDataArray) || ContainsHiddenFish(bubble.fishOnSpawner))
                present.Add(ObstacleFeatureType.HiddenFish);
            if (bubble.isMultiLayer && bubble.layers != null)
            {
                foreach (BubbleLayerData layer in bubble.layers)
                {
                    if (layer != null && ContainsHiddenFish(layer.FishDataArray)) present.Add(ObstacleFeatureType.HiddenFish);
                }
            }
        }

        return present;
    }

    private static bool ContainsHiddenFish(FishData[] fishes)
    {
        if (fishes == null) return false;
        for (int i = 0; i < fishes.Length; i++)
        {
            if (fishes[i] != null && fishes[i].isHidden) return true;
        }

        return false;
    }

#if UNITY_EDITOR
    // Các setter dưới đây chỉ để tool vẽ level (LevelDesignData) sửa seed; runtime vẫn chỉ đọc qua Create hoặc file đã giải mã.
    public void EditorSetLevel(int level) => Level = Mathf.Max(1, level);

    public void EditorSetDifficulty(LevelDifficulty difficulty) => Difficulty = difficulty;

    public void EditorSetOrderQueue(int[] orders) => OrderQueue = orders ?? Array.Empty<int>();

    public void EditorSetBubbles(SoftBubbleData[] bubbles) => SoftBubbleDataArray = bubbles ?? Array.Empty<SoftBubbleData>();
#endif
}

/// <summary>Một bóng trong seed: cá bên trong và cỡ bóng (0 = ngẫu nhiên lúc spawn theo GamePlayGlobalConfig).</summary>
[Serializable]
public class SoftBubbleData
{
    public int softBubbleId;
    public FishData[] FishDataArray = Array.Empty<FishData>();

    public bool isIce; // bong bang
    
    public bool isSpawner; // spawner
    [ShowIf("@isSpawner==true")]
    public FishData[] fishOnSpawner = Array.Empty<FishData>();

    public bool isMultiLayer; // bong nhieu lop: FishDataArray la lop dau, layers la cac lop sau (lo dan theo index tang dan); khong dung chung voi spawner
    [ShowIf("@isMultiLayer==true")]
    public BubbleLayerData[] layers = Array.Empty<BubbleLayerData>();
    
    public bool isLock; // khoa
    public bool isKey; // chia---chia co keyId la softBubbleId cua khoa 
    [ShowIf("@isKey==true")]
    public int keyId;

    public bool isBubbleHp; // bong co hp
    [ShowIf("@isBubbleHp==true")]
    public int bubbleHp;
    
    public bool isRock; // bong da :)

    //order bubble
    public bool isOrderBubble;
    [ShowIf("@isOrderBubble==true")]
    public int fishOrderId;

    // bundle bubble (runtime: FishSchoolBubble, BubbleEntry.IsFishSchool): an vao mot con ca nao trong bong thi ca dan bay cung luc,
    // moi con vao be nhan no hoac o cho; khong du cho cho ca dan thi khong bam duoc. Khong co tham so kem, chi mot co.
    // Khong dung chung voi spawner / multiLayer (Validate bao loi, runtime bo co).
    public bool isBundleBubble;

    // bang chuyen: ca ben trong van nhu bong thuong, chi khac cach trinh bay.
    // Trong tool ve level, bong nay ngat dong va chiem tron mot dong, ca xep thanh mot hang ngang.
    public bool isConveyor;

    [Tooltip("0 = ngẫu nhiên trong GamePlayGlobalConfig.BubbleRadiusMin..Max lúc spawn; > 0 = cố định")] public float Radius;

    /// <summary>Số cá của lớp đang hiện (lớp đầu). Các lớp còn ẩn nằm ở layers, xem TotalFishCount.</summary>
    public int FishCount => FishDataArray?.Length ?? 0;

    /// <summary>Tổng số lớp, tính cả lớp đầu nằm ngay ở FishDataArray.</summary>
    public int LayerCount => 1 + (layers?.Length ?? 0);

    /// <summary>Cá của lớp thứ index (0 = lớp đầu = lớp đang hiện). Index ngoài phạm vi trả về mảng rỗng.</summary>
    public FishData[] GetLayerFish(int index)
    {
        if (index <= 0) return FishDataArray ?? Array.Empty<FishData>();
        int k = index - 1;
        if (layers == null || k >= layers.Length || layers[k] == null) return Array.Empty<FishData>();
        return layers[k].FishDataArray ?? Array.Empty<FishData>();
    }

    public int LayerFishCount(int index) => GetLayerFish(index).Length;

    /// <summary>Tổng cá trong bóng, cộng cả các lớp chưa lộ ra.</summary>
    public int TotalFishCount
    {
        get
        {
            int total = FishCount;
            if (layers == null) return total;
            foreach (BubbleLayerData layer in layers)
            {
                if (layer != null) total += layer.FishCount;
            }

            return total;
        }
    }

    /// <summary>
    /// Bóng mới với bán kính bốc theo số cá (GamePlayGlobalConfig.RandomBubbleRadius). Dùng cho khâu sinh bóng của
    /// tool vẽ level, nên bóng sinh ra không bao giờ chật so với số cá bên trong.
    /// </summary>
    public static SoftBubbleData CreateWithRandomRadius(int[] fishIds)
    {
        float radius = GamePlayGlobalConfig.Instance.RandomBubbleRadius(fishIds.Length);
        SoftBubbleData data = new SoftBubbleData { Radius = radius, FishDataArray = new FishData[fishIds.Length] };
        for (int i = 0; i < fishIds.Length; i++) data.FishDataArray[i] = new FishData { FishId = fishIds[i] };
        return data;
    }

    public bool Contains(HashSet<int> fishIds)
    {
        if (FishDataArray == null) return false;
        foreach (FishData f in FishDataArray)
        {
            if (f != null && fishIds.Contains(f.FishId)) return true;
        }

        return false;
    }
}

/// <summary>
/// Một lớp cá còn ẩn của MultilayerBubble: lộ ra khi lớp trước hết cá, thứ tự lộ theo index tăng dần (lớp đầu tiên là
/// SoftBubbleData.FishDataArray, không nằm trong layers). Bóng giữ nguyên cỡ của lớp đầu.
/// </summary>
[Serializable]
public class BubbleLayerData
{
    public int index;
    public FishData[] FishDataArray = Array.Empty<FishData>();

    public int FishCount => FishDataArray?.Length ?? 0;
}

[Serializable]
public class FishData
{
    public int FishId;
    public bool isHidden;
}
