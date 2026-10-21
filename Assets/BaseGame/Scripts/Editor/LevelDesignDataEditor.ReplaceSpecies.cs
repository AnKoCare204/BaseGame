using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Chuột phải vào ô đơn ở cột OrderQueue -> "Thay loài": chọn loài mới, tool thay loài cũ bằng loài mới ở MỌI chỗ
/// trong level - hàng đơn, cá trong bóng (mọi lớp), hàng chờ spawner, khoá đơn (fishOrderId). Nhờ vậy số cá mỗi loài
/// vẫn khớp số đơn x 3, đổi hình cá cả level chỉ một thao tác.
///
/// Loài mới đã có sẵn trong level thì ĐỔI CHỖ hai loài chứ không thay thẳng: thay thẳng là gộp hai loài làm một, level
/// mất một loài và hai đơn đầu có thể trùng loài (LevelData.Validate báo lỗi).
/// </summary>
public partial class LevelDesignDataEditor
{
    /// <summary>Gọi ở ô đơn: chuột phải mở menu chọn loài thay cho loài của đơn này.</summary>
    private void HandleOrderContextMenu(LevelDesignData data, Rect cell, int fishId)
    {
        Event e = Event.current;
        if (e.type != EventType.ContextClick || !cell.Contains(e.mousePosition)) return;

        var present = new HashSet<int>(data.levelData.OrderQueue ?? Array.Empty<int>());
        var menu = new GenericMenu();
        menu.AddDisabledItem(new GUIContent($"Thay loài {fishId} ({SpeciesName(fishId)}) ở cả level bằng:"));
        menu.AddSeparator(string.Empty);
        foreach (int id in fishIds)
        {
            if (id == fishId) continue;
            bool swap = present.Contains(id);
            string label = $"{(swap ? "Đổi chỗ với" : "Thay bằng")}/{id} · {SpeciesName(id)}{(swap ? "  (đang có trong level)" : "")}";
            int target = id;
            menu.AddItem(new GUIContent(label), false, () =>
            {
                pending = () => ReplaceSpecies(data, fishId, target);
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();   // tool có thể nằm ở Inspector hoặc cửa sổ riêng
            });
        }

        menu.ShowAsContext();
        e.Use();
    }

    private static string SpeciesName(int id)
    {
        FishModel model = ToolFishCatalog.GetModel(id);
        return model != null ? Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(model)) : "?";
    }

    /// <summary>Thay loài from bằng to ở mọi chỗ; to đã có trong level thì đổi chỗ hai loài.</summary>
    private void ReplaceSpecies(LevelDesignData data, int from, int to)
    {
        if (from == to || data.levelData == null) return;

        var present = new HashSet<int>(data.levelData.OrderQueue ?? Array.Empty<int>());
        bool swap = present.Contains(to);
        int Map(int id) => id == from ? to : swap && id == to ? from : id;

        int changed = 0;
        Mutate(data, swap ? "Đổi chỗ hai loài cá" : "Thay loài cá", () =>
        {
            int[] orders = (int[])(data.levelData.OrderQueue ?? Array.Empty<int>()).Clone();
            for (int i = 0; i < orders.Length; i++) orders[i] = Map(orders[i]);
            data.levelData.EditorSetOrderQueue(orders);

            void Remap(FishData[] fish)
            {
                if (fish == null) return;
                foreach (FishData f in fish)
                {
                    if (f == null) continue;
                    int mapped = Map(f.FishId);
                    if (mapped == f.FishId) continue;
                    f.FishId = mapped;
                    changed++;
                }
            }

            foreach (SoftBubbleData bubble in data.levelData.SoftBubbleDataArray ?? Array.Empty<SoftBubbleData>())
            {
                if (bubble == null) continue;
                Remap(bubble.FishDataArray);
                Remap(bubble.fishOnSpawner);
                if (bubble.layers != null)
                {
                    foreach (BubbleLayerData layer in bubble.layers) Remap(layer?.FishDataArray);
                }

                if (bubble.isOrderBubble) bubble.fishOrderId = Map(bubble.fishOrderId);
            }
        });

        RefreshHighlightFishId();
        SetStatus(true, swap
            ? $"Đã đổi chỗ loài {from} ({SpeciesName(from)}) và loài {to} ({SpeciesName(to)}) - {changed} con cá"
            : $"Đã thay loài {from} ({SpeciesName(from)}) bằng loài {to} ({SpeciesName(to)}) - {changed} con cá");
    }
}
