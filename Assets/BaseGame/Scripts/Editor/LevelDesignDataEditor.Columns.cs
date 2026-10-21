using System;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Hai cột trái: palette cá (kéo ra thả vào ô) và OrderQueue.
/// Xem <see cref="LevelDesignDataEditor"/> cho danh sách các file partial khác.
/// </summary>
public partial class LevelDesignDataEditor
{
    private Vector2 paletteScroll;
    private Vector2 orderScroll;

    /// <summary>
    /// ID hiện trong "List item cá", lấy từ FullFishModelConfigList. Bỏ ID âm: đó là model vật cản dùng chung
    /// (cá ẩn, spawner, key), không phải loài cá.
    /// </summary>
    private int[] paletteIds;

    private static int[] LoadPaletteIds()
    {
        FishModelGlobalConfig config = FishModelGlobalConfig.Instance;
        if (config == null) return Array.Empty<int>();

        var full = config.GetFullFishModelConfigList();
        var ids = new System.Collections.Generic.List<int>(full.Count);
        foreach (FishModelConfig entry in full)
        {
            if (entry is { ID: >= 0 }) ids.Add(entry.ID);
        }

        return ids.ToArray();
    }

    private void DrawFishPalette(DesignConfig design, float width, float wrapHeight)
    {
        paletteIds ??= LoadPaletteIds();
        int[] fishIds = paletteIds;

        EditorGUILayout.BeginVertical(ColumnWidth(PaletteColumn, width));
        EditorGUILayout.BeginVertical(SirenixGUIStyles.BoxContainer);
        PanelTitle("List item cá", "kéo vào ô cá");
        paletteScroll = EditorGUILayout.BeginScrollView(paletteScroll,
            GUILayout.Height(Mathf.Max(80f, wrapHeight - TitleBlockHeight)));

        for (int i = 0; i < fishIds.Length; i += WrapFishPerRow)
        {
            EditorGUILayout.BeginHorizontal();
            for (int c = 0; c < WrapFishPerRow && i + c < fishIds.Length; c++)
            {
                int fishId = fishIds[i + c];
                Rect rect = GUILayoutUtility.GetRect(WrapFishSlot, WrapFishSlot, GUILayout.Width(WrapFishSlot),
                    GUILayout.Height(WrapFishSlot));
                DrawFishIcon(rect, fishId, design, false);
                HandleFishDragSource(rect, fishId);
                if (c + 1 < WrapFishPerRow) GUILayout.Space(WrapSlotGap);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            if (i + WrapFishPerRow < fishIds.Length) GUILayout.Space(WrapSlotGap);
        }

        if (fishIds.Length == 0)
        {
            GUILayout.Label("FishModelGlobalConfig trống", SirenixGUIStyles.CenteredGreyMiniLabel, GUILayout.ExpandWidth(true));
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// Đơn xếp cố định WrapFishPerRow ô mỗi hàng. Kéo cạnh phải wrap chỉ đổi bề rộng khung, không đổi số ô / size ô.
    /// Đầu mỗi hàng có số thứ tự của ô đầu hàng. Ô cuối cùng luôn là ô "+" để thả thêm đơn.
    ///
    /// Kéo một ô đơn thả lên ô đơn khác thì hai ô đổi chỗ (thứ tự đơn là thứ tự bể nhận, nên đổi chỗ không làm lệch
    /// tổng số cá). Kéo cá từ palette / từ ô cá trong bóng vào ô đơn thì chỉ ghi đè loài của ô đích.
    /// </summary>
    private void DrawOrderQueue(LevelDesignData data, DesignConfig design, float width, float wrapHeight)
    {
        int[] orders = data.levelData.OrderQueue ?? Array.Empty<int>();

        EditorGUILayout.BeginVertical(ColumnWidth(OrderColumn, width));
        EditorGUILayout.BeginVertical(SirenixGUIStyles.BoxContainer);
        PanelTitle("OrderQueue", $"{orders.Length} đơn × {GamePlayGlobalConfig.TankGoal}");
        orderScroll = EditorGUILayout.BeginScrollView(orderScroll,
            GUILayout.Height(Mathf.Max(80f, wrapHeight - TitleBlockHeight)));

        int cells = orders.Length + 1;

        for (int i = 0; i < cells; i += WrapFishPerRow)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(NumberLabel(i + 1), SirenixGUIStyles.LeftAlignedGreyMiniLabel,
                GUILayout.Width(WrapRowLabelWidth));

            for (int c = 0; c < WrapFishPerRow && i + c < cells; c++)
            {
                int index = i + c;
                Rect rect = GUILayoutUtility.GetRect(WrapFishSlot, WrapFishSlot, GUILayout.Width(WrapFishSlot),
                    GUILayout.Height(WrapFishSlot));

                if (index >= orders.Length)
                {
                    DrawEmptySlot(rect, design, "+");

                    // Kéo một đơn đang có ra ô "+" là dời nó xuống cuối hàng, không thêm đơn mới: thêm nữa là nhân đôi
                    // loài đó và lệch ngay phép cân "số cá = số đơn × TankGoal". Cá từ palette thì thêm đơn như cũ.
                    if (TryGetFishDrop(rect, out FishDrag added))
                    {
                        int end = orders.Length;
                        pending = added.FromOrder
                            ? () => InsertOrderAt(data, added.OrderIndex, end)
                            : () => AddOrder(data, added.FishId);
                    }
                    if (c + 1 < WrapFishPerRow) GUILayout.Space(WrapSlotGap);
                    continue;
                }

                DrawFishIcon(rect, orders[index], design, false, lossSpot: IsLossOrder(data, index));
                HandleOrderContextMenu(data, rect, orders[index]);   // chuột phải: thay loài ở cả level

                // Nút xoá to hơn và có nền tối: ở 14px chữ × bị nuốt mất, nhìn như ô trống.
                var close = new Rect(rect.xMax - OverlaySize - 2f, rect.y + 2f, OverlaySize, OverlaySize);
                DrawRoundedRect(close, OverlayBackdrop, Color.clear, 0f);
                bool remove = GUI.Button(close, "×", SirenixGUIStyles.MiniButton);

                // Nút × ăn chuột trước, phần còn lại của ô mới là chỗ bắt đầu kéo con cá đi.
                if (!close.Contains(Event.current.mousePosition))
                {
                    HandleFishDragSource(rect, orders[index], orderIndex: index);
                }

                DrawOrderInsertLine(rect, index);

                // Đọc vị trí chuột trước TryGetFishDrop: hàm đó Use() event nên sau nó mousePosition không còn tin được.
                int slot = InsertSlotAt(rect, Event.current.mousePosition, index);
                if (TryGetFishDrop(rect, out FishDrag dropped))
                {
                    int target = index;

                    // Ô đơn kéo sang ô đơn: mép trái / phải = chèn vào khe đó, giữa ô = đổi chỗ như cũ.
                    // Kéo từ palette hay từ ô cá trong bóng vào thì vẫn ghi đè như cũ.
                    if (!dropped.FromOrder) pending = () => SetOrder(data, target, dropped.FishId);
                    else if (slot >= 0) pending = () => InsertOrderAt(data, dropped.OrderIndex, slot);
                    else pending = () => SwapOrder(data, dropped.OrderIndex, target);
                }

                if (remove) pending = () => RemoveOrder(data, index);
                if (c + 1 < WrapFishPerRow) GUILayout.Space(WrapSlotGap);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            if (i + WrapFishPerRow < cells) GUILayout.Space(WrapSlotGap);
        }

        if (orders.Length == 0)
        {
            // GUILayout.Label chứ không phải LabelField: LabelField chỉ chừa đúng một dòng nên chuỗi nhiều dòng bị cắt.
            GUILayout.Label("Chưa có đơn nào - kéo cá từ palette vào ô +", SirenixGUIStyles.MultiLineCenteredLabel);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndVertical();
    }
}
