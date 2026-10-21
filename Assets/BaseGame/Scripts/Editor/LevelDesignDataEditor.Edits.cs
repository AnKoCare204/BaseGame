using System;
using System.Collections.Generic;
using UnityEditor;
using CoreData;
using CoreGame.GameGlobalConfig;
using UnityEngine;

/// <summary>
/// Mọi thao tác sửa data (bóng, cá, đơn) và đọc / ghi file level. Tất cả đều đi qua Mutate để có Undo và SetDirty.
/// Xem <see cref="LevelDesignDataEditor"/> cho danh sách các file partial khác.
/// </summary>
public partial class LevelDesignDataEditor
{
    private void Mutate(LevelDesignData data, string label, Action change)
    {
        Undo.RecordObject(data, label);
        change();
        EditorUtility.SetDirty(data);
        ClearLossSpots();   // chỗ thua đo trên data cũ, sửa rồi là không còn đúng - bấm Đo lại
        RefreshCheckIfActive(data);
    }

    // ---------------------------------------------------------------- vật cản

    private void SetObstacle(LevelDesignData data, SoftBubbleData bubble, BubbleObstacle obstacle, bool value)
    {
        if (bubble == null) return;
        if (obstacle == BubbleObstacle.Spawner && value && bubble.isRock) return;

        Mutate(data, "Đổi vật cản bóng", () =>
        {
            DesignConfig.SetFlag(bubble, obstacle, value);
            if (obstacle == BubbleObstacle.Rock && value) EvacuateFishFromRock(data, bubble);
        });
        if (obstacle == BubbleObstacle.Rock && value)
        {
            selectedFishIndex = -1;
            selectedFishList = FishListMain;
        }
    }

    /// <summary>
    /// Bóng đá không chứa cá và không được là spawner: rải cá trong bóng lẫn cá trên spawner sang bóng khác
    /// còn chỗ (dưới Cá/bóng nhiều nhất). Hết chỗ thì tạo bóng mới rồi nhét tiếp, rồi tắt isSpawner.
    /// </summary>
    private void EvacuateFishFromRock(LevelDesignData data, SoftBubbleData rock)
    {
        var moved = new List<FishData>();
        if (rock.FishDataArray != null) moved.AddRange(rock.FishDataArray);
        if (rock.fishOnSpawner != null) moved.AddRange(rock.fishOnSpawner);
        rock.FishDataArray = Array.Empty<FishData>();
        rock.fishOnSpawner = Array.Empty<FishData>();
        rock.isSpawner = false;
        EnforceMinRadius(rock);
        if (moved.Count == 0) return;

        var bubbles = new List<SoftBubbleData>(data.levelData.SoftBubbleDataArray ?? Array.Empty<SoftBubbleData>());
        int maxFish = Mathf.Max(1, randomMaxFishPerBubble);
        int insertAt = bubbles.IndexOf(rock) + 1;
        if (insertAt < 1) insertAt = bubbles.Count;

        var candidates = new List<SoftBubbleData>();
        foreach (FishData fish in moved)
        {
            if (fish == null) continue;

            candidates.Clear();
            foreach (SoftBubbleData bubble in bubbles)
            {
                if (bubble == null || bubble.isRock || bubble.FishCount >= maxFish) continue;
                candidates.Add(bubble);
            }

            SoftBubbleData dest;
            if (candidates.Count == 0)
            {
                dest = new SoftBubbleData();
                bubbles.Insert(insertAt, dest);
                insertAt++;
            }
            else
            {
                dest = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            }

            int oldCount = dest.FishCount;
            var list = new List<FishData>(dest.FishDataArray ?? Array.Empty<FishData>()) { fish };
            dest.FishDataArray = list.ToArray();
            ResizeForFishCount(dest, oldCount);
        }

        data.levelData.EditorSetBubbles(bubbles.ToArray());
        data.NormalizeBubbleIds();
    }

    // ---------------------------------------------------------------- bóng

    private void AddBubble(LevelDesignData data)
    {
        var bubbles = new List<SoftBubbleData>(data.levelData.SoftBubbleDataArray ?? Array.Empty<SoftBubbleData>())
        {
            new SoftBubbleData()
        };
        SetBubbles(data, bubbles, "Thêm bóng");
    }

    private void InsertBubble(LevelDesignData data, int index)
    {
        var bubbles = new List<SoftBubbleData>(data.levelData.SoftBubbleDataArray ?? Array.Empty<SoftBubbleData>());
        if (index < 0 || index > bubbles.Count) return;
        bubbles.Insert(index, new SoftBubbleData());
        SetBubbles(data, bubbles, "Chèn bóng");
    }

    private void RemoveBubble(LevelDesignData data, int index)
    {
        var bubbles = new List<SoftBubbleData>(data.levelData.SoftBubbleDataArray ?? Array.Empty<SoftBubbleData>());
        if (index < 0 || index >= bubbles.Count) return;
        bubbles.RemoveAt(index);
        SetBubbles(data, bubbles, "Xoá bóng");
    }

    /// <summary>
    /// Chèn bóng from vào khe slot (đếm theo mảng gốc: 0 = trước bóng đầu, n = sau bóng cuối). Thứ tự các bóng còn
    /// lại giữ nguyên, chỉ dồn chỗ - khác SwapBubbles là đổi chỗ hai bóng.
    /// Ví dụ kéo bóng 7 thả vào khe trước bóng 4 thì 7 thành 4, còn 4/5/6 thành 5/6/7.
    /// </summary>
    private void InsertBubbleAt(LevelDesignData data, int from, int slot)
    {
        var bubbles = new List<SoftBubbleData>(data.levelData.SoftBubbleDataArray ?? Array.Empty<SoftBubbleData>());
        if (from < 0 || from >= bubbles.Count) return;

        slot = Mathf.Clamp(slot, 0, bubbles.Count);

        // Bỏ bóng ra trước rồi mới chèn, nên khe nằm sau nó bị lùi một chỗ.
        int target = slot > from ? slot - 1 : slot;
        if (target == from) return;

        SoftBubbleData moved = bubbles[from];
        bubbles.RemoveAt(from);
        bubbles.Insert(target, moved);
        SetBubbles(data, bubbles, "Chèn bóng vào vị trí khác");

        // Giữ vệt chọn trên đúng con bóng vừa kéo, không phải trên số thứ tự cũ.
        selectedBubbleIndex = target;
    }

    private void SwapBubbles(LevelDesignData data, int from, int to)
    {
        var bubbles = new List<SoftBubbleData>(data.levelData.SoftBubbleDataArray ?? Array.Empty<SoftBubbleData>());
        if (from < 0 || to < 0 || from >= bubbles.Count || to >= bubbles.Count) return;
        (bubbles[from], bubbles[to]) = (bubbles[to], bubbles[from]);
        SetBubbles(data, bubbles, "Đổi chỗ bóng");
    }

    private void SetBubbles(LevelDesignData data, List<SoftBubbleData> bubbles, string label)
    {
        Mutate(data, label, () =>
        {
            data.levelData.EditorSetBubbles(bubbles.ToArray());
            data.NormalizeBubbleIds();
        });
    }

    // ---------------------------------------------------------------- cá

    private static FishData[] GetFishArray(SoftBubbleData bubble, int listId)
    {
        if (bubble == null) return Array.Empty<FishData>();
        if (listId == FishListSpawner) return bubble.fishOnSpawner ?? Array.Empty<FishData>();
        return bubble.GetLayerFish(listId);
    }

    private static void SetFishArray(SoftBubbleData bubble, int listId, FishData[] fish)
    {
        if (bubble == null) return;
        fish ??= Array.Empty<FishData>();

        if (listId == FishListSpawner)
        {
            bubble.fishOnSpawner = fish;
            return;
        }

        if (listId <= FishListMain)
        {
            bubble.FishDataArray = fish;
            return;
        }

        int k = listId - 1;
        if (bubble.layers == null || k >= bubble.layers.Length) return;
        bubble.layers[k] ??= new BubbleLayerData { index = k };
        bubble.layers[k].FishDataArray = fish;
    }

    /// <summary>
    /// Giữ BubbleLayerData.index bằng đúng vị trí trong mảng. Runtime xếp lớp theo index tăng dần (BoardModel), nên
    /// đồng bộ như vậy thì thứ tự lộ lớp khớp hẳn thứ tự thấy trong tool, và luật "không hai lớp trùng index" của
    /// LevelData.Validate không bao giờ bị phạm.
    /// </summary>
    private static void NormalizeLayerIndices(SoftBubbleData bubble)
    {
        if (bubble?.layers == null) return;
        for (int k = 0; k < bubble.layers.Length; k++)
        {
            bubble.layers[k] ??= new BubbleLayerData();
            bubble.layers[k].index = k;
        }
    }

    /// <summary>
    /// Luật của bóng nhiều lớp: lớp sau không được nhiều cá hơn lớp trước. Trả về false khi thêm một con nữa vào
    /// danh sách này là phạm luật. Lớp 1 và cá trên spawner không bị chặn.
    /// </summary>
    private static bool CanAddToList(SoftBubbleData bubble, int listId)
    {
        if (bubble == null || listId <= FishListMain) return true;
        return bubble.LayerFishCount(listId) < bubble.LayerFishCount(listId - 1);
    }

    private void ReportLayerFull(SoftBubbleData bubble, int listId)
    {
        SetStatus(false, $"Lớp {listId + 1} không được nhiều cá hơn lớp {listId} " +
                         $"({bubble.LayerFishCount(listId - 1)} con)");
    }

    private void AddBubbleLayer(LevelDesignData data, SoftBubbleData bubble)
    {
        Mutate(data, "Thêm lớp bóng", () =>
        {
            var layers = new List<BubbleLayerData>(bubble.layers ?? Array.Empty<BubbleLayerData>())
            {
                new BubbleLayerData()
            };
            bubble.layers = layers.ToArray();
            bubble.isMultiLayer = true;
            NormalizeLayerIndices(bubble);
        });
    }

    private void RemoveBubbleLayer(LevelDesignData data, SoftBubbleData bubble)
    {
        Mutate(data, "Xoá lớp bóng", () =>
        {
            var layers = new List<BubbleLayerData>(bubble.layers ?? Array.Empty<BubbleLayerData>());
            if (layers.Count == 0) return;
            layers.RemoveAt(layers.Count - 1);
            bubble.layers = layers.ToArray();
            if (layers.Count == 0) bubble.isMultiLayer = false;
            NormalizeLayerIndices(bubble);
        });
        selectedFishList = FishListMain;
    }

    /// <summary>
    /// Ô cá ← ô cá: đổi chỗ cả FishData (kể cả isHidden); thả vào ô "+" thì chuyển cá sang đích.
    /// Palette / OrderQueue ← chỉ ghi FishId lên ô đích, nguồn không đổi.
    /// </summary>
    private void DropFishOnSlot(LevelDesignData data, FishDrag drag, int destBubbleIndex, int destFishIndex,
        int destList = FishListMain)
    {
        if (drag == null) return;

        int fishId = drag.FishId;
        int fromBubble = drag.BubbleIndex;
        int fromFish = drag.FishIndex;
        bool fromSlot = drag.FromSlot;
        int fromList = drag.ListId;

        Mutate(data, fromSlot ? "Đổi chỗ cá" : "Đổi cá", () =>
        {
            SoftBubbleData[] bubbles = data.levelData.SoftBubbleDataArray;
            if (bubbles == null || destBubbleIndex < 0 || destBubbleIndex >= bubbles.Length) return;
            SoftBubbleData dest = bubbles[destBubbleIndex];
            if (dest == null) return;
            if (dest.isRock) return;

            if (fromSlot)
            {
                SwapOrMoveFish(bubbles, fromBubble, fromFish, fromList, destBubbleIndex, destFishIndex, destList);
                return;
            }

            FishData[] destFish = GetFishArray(dest, destList);
            if (destFishIndex >= 0 && destFishIndex < destFish.Length)
            {
                destFish[destFishIndex].FishId = fishId;
                return;
            }

            if (!CanAddToList(dest, destList))
            {
                ReportLayerFull(dest, destList);
                return;
            }

            int oldCount = dest.FishCount;
            var list = new List<FishData>(destFish) { new FishData { FishId = fishId } };
            SetFishArray(dest, destList, list.ToArray());
            if (destList == FishListMain) ResizeForFishCount(dest, oldCount);
        });
    }

    private static void SwapOrMoveFish(SoftBubbleData[] bubbles, int fromBubble, int fromFish, int fromList,
        int toBubble, int toFish, int toList)
    {
        if (fromBubble < 0 || toBubble < 0 || fromBubble >= bubbles.Length || toBubble >= bubbles.Length) return;
        if (fromBubble == toBubble && fromList == toList && fromFish == toFish) return;

        SoftBubbleData source = bubbles[fromBubble];
        SoftBubbleData dest = bubbles[toBubble];
        if (source == null || dest == null) return;

        bool sameList = fromBubble == toBubble && fromList == toList;
        var sourceFish = new List<FishData>(GetFishArray(source, fromList));
        var destFish = sameList ? sourceFish : new List<FishData>(GetFishArray(dest, toList));

        if (fromFish < 0 || fromFish >= sourceFish.Count) return;

        // Số cá trước khi đổi, để ResizeForFishCount biết bóng nào vừa tăng / giảm cá.
        int sourceOldCount = source.FishCount;
        int destOldCount = dest.FishCount;

        if (toFish >= 0 && toFish < destFish.Count)
        {
            (sourceFish[fromFish], destFish[toFish]) = (destFish[toFish], sourceFish[fromFish]);
        }
        else
        {
            // Chuyển sang cuối danh sách đích: chỉ đổi số lượng khi hai danh sách khác nhau nên mới phải xét luật lớp.
            if (!sameList && !CanAddToList(dest, toList)) return;
            FishData moved = sourceFish[fromFish];
            sourceFish.RemoveAt(fromFish);
            destFish.Add(moved);
        }

        SetFishArray(source, fromList, sourceFish.ToArray());
        if (!sameList) SetFishArray(dest, toList, destFish.ToArray());
        if (fromList == FishListMain) ResizeForFishCount(source, sourceOldCount);
        if (toList == FishListMain && !sameList) ResizeForFishCount(dest, destOldCount);
    }

    private void RemoveFish(LevelDesignData data, SoftBubbleData bubble, int index, int listId = FishListMain)
    {
        Mutate(data, listId == FishListSpawner ? "Xoá cá spawner" : "Xoá cá", () =>
        {
            var fish = new List<FishData>(GetFishArray(bubble, listId));
            if (index < 0 || index >= fish.Count) return;
            int oldCount = bubble.FishCount;
            fish.RemoveAt(index);
            SetFishArray(bubble, listId, fish.ToArray());
            if (listId == FishListMain) ResizeForFishCount(bubble, oldCount);
        });
    }

    // ---------------------------------------------------------------- đơn

    private void AddOrder(LevelDesignData data, int fishId)
    {
        Mutate(data, "Thêm đơn", () =>
        {
            var orders = new List<int>(data.levelData.OrderQueue ?? Array.Empty<int>()) { fishId };
            data.levelData.EditorSetOrderQueue(orders.ToArray());
        });
    }

    private void SetOrder(LevelDesignData data, int index, int fishId)
    {
        Mutate(data, "Đổi đơn", () =>
        {
            var orders = new List<int>(data.levelData.OrderQueue ?? Array.Empty<int>());
            if (index < 0 || index >= orders.Count) return;
            orders[index] = fishId;
            data.levelData.EditorSetOrderQueue(orders.ToArray());
        });
    }

    /// <summary>
    /// Đổi chỗ hai đơn trong OrderQueue. Chỉ xáo thứ tự nên tổng số cá và số đơn mỗi loài không đổi - seed vẫn cân
    /// sau thao tác này, khác hẳn SetOrder (ghi đè loài, làm lệch phép cân).
    /// </summary>
    private void SwapOrder(LevelDesignData data, int from, int to)
    {
        if (from == to) return;

        Mutate(data, "Đổi chỗ đơn", () =>
        {
            int[] orders = data.levelData.OrderQueue;
            if (orders == null || from < 0 || to < 0 || from >= orders.Length || to >= orders.Length) return;

            var swapped = (int[])orders.Clone();
            (swapped[from], swapped[to]) = (swapped[to], swapped[from]);
            data.levelData.EditorSetOrderQueue(swapped);
        });
    }

    /// <summary>
    /// Chèn đơn from vào khe slot (0 = trước đơn đầu, n = sau đơn cuối), các đơn còn lại giữ thứ tự và dồn chỗ - như
    /// InsertBubbleAt. Chỉ dời chỗ nên phép cân số cá / số đơn không đổi.
    /// </summary>
    private void InsertOrderAt(LevelDesignData data, int from, int slot)
    {
        Mutate(data, "Chèn đơn vào vị trí khác", () =>
        {
            var orders = new List<int>(data.levelData.OrderQueue ?? Array.Empty<int>());
            if (from < 0 || from >= orders.Count) return;

            slot = Mathf.Clamp(slot, 0, orders.Count);

            // Bỏ đơn ra trước rồi mới chèn, nên khe nằm sau nó bị lùi một chỗ.
            int target = slot > from ? slot - 1 : slot;
            if (target == from) return;

            int moved = orders[from];
            orders.RemoveAt(from);
            orders.Insert(target, moved);
            data.levelData.EditorSetOrderQueue(orders.ToArray());
        });
    }

    private void RemoveOrder(LevelDesignData data, int index)
    {
        Mutate(data, "Xoá đơn", () =>
        {
            var orders = new List<int>(data.levelData.OrderQueue ?? Array.Empty<int>());
            if (index < 0 || index >= orders.Count) return;
            orders.RemoveAt(index);
            data.levelData.EditorSetOrderQueue(orders.ToArray());
        });
    }

    // ---------------------------------------------------------------- file

    private void Load(LevelDesignData data, int level)
    {
        ClearCheckHighlights();
        SetStatus(data.LoadLevel(level, out string message), message);
        findLevel = Mathf.Max(1, data.levelData.Level);
    }

    /// <summary>
    /// Nạp biến thể LevelData_{level}_{variant}.json. Không có file là chuyện thường (level chưa có biến thể đó): data
    /// bên dưới đã dọn trống bàn để xếp biến thể mới, ở đây chỉ cảnh báo vàng; file có mà đọc hỏng mới báo đỏ.
    /// </summary>
    private void LoadVariant(LevelDesignData data, int level, int variant)
    {
        bool exists = data.HasVariant(level, variant);
        bool ok = data.LoadVariant(level, variant, out string message);
        if (ok || !exists)
        {
            // Có file thì nạp, không có thì bàn đã được dọn trống - cả hai đều đổi data nên tô Check cũ không còn đúng.
            ClearCheckHighlights();
            findLevel = Mathf.Max(1, data.levelData.Level);
            findVariant = Mathf.Max(1, data.openVariant);
        }

        SetStatus(ok, message, !ok && !exists ? MessageType.Warning : (MessageType?)null);
    }

    /// <summary>Ghi data đang mở thành biến thể của level đang mở. Chặn seed chưa hợp lệ y như Save.</summary>
    private void SaveVariant(LevelDesignData data, int variant)
    {
        List<string> errors = CollectDataErrors(data);
        if (errors.Count > 0)
        {
            SetStatus(false, $"CHƯA LƯU BIẾN THỂ - level chưa hợp lệ ({errors.Count} lỗi):\n" + string.Join("\n", errors));
            return;
        }

        bool ok = data.SaveVariant(variant, out string message);
        if (!ok)
        {
            SetStatus(false, message);
            return;
        }

        findVariant = Mathf.Max(1, data.openVariant);
        int level = data.levelData.Level;
        SetStatus(true, message + VariantNote(level), LevelSourceRule.IsApkOnly(level) ? MessageType.Warning : (MessageType?)null);
    }

    /// <summary>
    /// Nhắc khi level đang lưu nằm ngoài LevelMaxInConfig: file vẫn ghi bình thường nhưng chưa được bake vào bản build /
    /// CDN, nên trong game chưa chơi được cho tới khi tăng LevelMaxInConfig. Từ commit "Levels load through the provider"
    /// (ee16a371) config không còn giữ danh sách level, nên lưu level xa không còn đệm level rỗng như trước - chỉ nhắc.
    /// </summary>
    private static string LevelOutsideBuildNote(int level)
    {
        LevelGlobalConfig config = LevelGlobalConfig.Instance;
        if (config == null || level <= config.LevelMaxInConfig) return string.Empty;
        return $"\nLưu ý: level {level} > LevelMaxInConfig ({config.LevelMaxInConfig}) - chưa vào bản build, " +
               "tăng LevelMaxInConfig trong LevelGlobalConfig khi muốn đưa level này vào game.";
    }

    /// <summary>
    /// Nhắc sau khi lưu biến thể: game không dùng biến thể của level 1-2 (FTUE); level ngoài LevelMaxInConfig chưa vào build;
    /// còn lại phải Setup (vào bản build) và Build Levels + upload (lên CDN) thì người chơi mới gặp.
    /// </summary>
    internal static string VariantNote(int level)
    {
        if (LevelSourceRule.IsApkOnly(level))
        {
            return $"\nCảnh báo: game không dùng biến thể của level 1-{LevelSourceRule.ApkOnlyMaxLevel} (tutorial FTUE).";
        }

        string outside = LevelOutsideBuildNote(level);
        if (outside.Length > 0) return outside;
        return "\nChạy Tools/FishSort/Level Addressables/Setup để biến thể vào bản build, Build Levels + upload để lên CDN.";
    }

    /// <summary>
    /// Ghi level xuống file, nhưng chỉ khi seed qua được tầng đếm (CollectDataErrors). Trước đây Save vẫn ghi rồi báo
    /// vàng, nên file hỏng lọt vào thư mục level lúc nào không hay - tới lúc chơi mới biết.
    /// </summary>
    private void Save(LevelDesignData data)
    {
        List<string> errors = CollectDataErrors(data);
        if (errors.Count > 0)
        {
            SetStatus(false, $"CHƯA LƯU - level chưa hợp lệ ({errors.Count} lỗi):\n" + string.Join("\n", errors));
            return;
        }

        // Đang mở biến thể mà bấm Save thì file GỐC LevelData_{n}.json bị ghi đè bằng data của biến thể - hỏi lại cho
        // chắc, vì đa số lúc đó người thiết kế chỉ muốn lưu biến thể.
        if (data.openVariant > 0)
        {
            int level = data.levelData.Level;
            int choice = EditorUtility.DisplayDialogComplex(
                $"Đang mở biến thể {data.openVariant} của level {level}",
                $"Save sẽ ghi đè level gốc LevelData_{level}.json bằng data của biến thể này.",
                "Ghi đè level gốc", "Huỷ", $"Lưu biến thể {data.openVariant}");
            if (choice == 1)
            {
                SetStatus(true, "Đã huỷ Save, chưa ghi file nào");
                return;
            }

            if (choice == 2)
            {
                SaveVariant(data, data.openVariant);
                return;
            }
        }

        bool ok = data.SaveLevel(out string message);
        SetStatus(ok, ok ? message + LevelOutsideBuildNote(data.levelData.Level) : message);
    }

    /// <summary>
    /// Swap cũng là một đường GHI FILE (level đang mở bị ghi sang chỗ level kia), nên chặn y như Save: seed chưa hợp lệ
    /// thì không đổi chỗ, kẻo file hỏng lọt sang số level khác rồi khó lần ra.
    /// Chỉ soi level đang mở; level kia đã nằm sẵn trên đĩa, Swap chỉ chép nguyên nó về.
    /// </summary>
    private void Swap(LevelDesignData data)
    {
        List<string> errors = CollectDataErrors(data);
        if (errors.Count > 0)
        {
            SetStatus(false, $"CHƯA SWAP - level đang mở chưa hợp lệ ({errors.Count} lỗi):\n" +
                             string.Join("\n", errors));
            return;
        }

        ClearCheckHighlights();
        SetStatus(data.SwapLevel(data.swapLevel, out string message), message);
        findLevel = Mathf.Max(1, data.levelData.Level);
    }
}
