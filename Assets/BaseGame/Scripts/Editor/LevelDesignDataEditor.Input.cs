using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Chuột và bàn phím: vùng chọn (bóng / ô cá), kéo thả cá lẫn bóng, phím tắt vật cản.
/// Xem <see cref="LevelDesignDataEditor"/> cho danh sách các file partial khác.
/// </summary>
public partial class LevelDesignDataEditor
{
    private const string FishDragKey = "FishSort.LevelDesign.Fish";
    private const string BubbleDragKey = "FishSort.LevelDesign.Bubble";

    /// <summary>
    /// Chỉ danh sách cá trong một bóng, dùng chung cho vẽ / chọn / kéo thả:
    /// &gt;= 0 là chỉ số lớp (0 = FishDataArray = lớp đầu, 1 = layers[0], 2 = layers[1], ...), -1 là fishOnSpawner.
    /// Gói ba thứ vào một số thay vì cờ bool để ô cá lớp 2 và ô cá lớp 1 không lẫn danh tính khi chọn / kéo.
    /// </summary>
    private const int FishListSpawner = -1;

    private const int FishListMain = 0;

    private int selectedBubbleIndex = -1;
    private int selectedFishIndex = -1;
    private int selectedFishList = FishListMain;

    /// <summary>Trong pass này đã bấm trúng một ô cá chưa; để click ô cá không bị click bóng xoá mất vùng chọn.</summary>
    private bool fishClicked;

    private sealed class FishDrag
    {
        public int FishId;
        public int BubbleIndex = -1;
        public int FishIndex = -1;
        public int ListId = FishListMain;

        /// <summary>Ô đơn thứ mấy trong OrderQueue đã nhả con cá này ra; -1 = không phải kéo từ OrderQueue.</summary>
        public int OrderIndex = -1;

        public bool FromSlot => BubbleIndex >= 0 && FishIndex >= 0;

        /// <summary>Kéo từ một ô đơn của OrderQueue. Chỉ đường thả trong chính OrderQueue mới quan tâm cờ này.</summary>
        public bool FromOrder => OrderIndex >= 0;
    }

    // ---------------------------------------------------------------- vùng chọn

    /// <summary>Click chọn bóng. Không dùng hover: đưa chuột lên không còn coi như đang chọn.</summary>
    private void HandleBubbleSelect(Rect box, int index)
    {
        if (splitterDrag >= 0) return;
        Event e = Event.current;
        if (e.type != EventType.MouseDown || e.button != 0) return;
        if (!box.Contains(e.mousePosition)) return;

        selectedBubbleIndex = index;
        if (!fishClicked)
        {
            selectedFishIndex = -1;
            selectedFishList = FishListMain;
        }

        Repaint();
    }

    private void HandleFishSelect(Rect cell, int bubbleIndex, int fishIndex, int listId)
    {
        if (splitterDrag >= 0) return;
        Event e = Event.current;
        if (e.type != EventType.MouseDown || e.button != 0) return;
        if (!cell.Contains(e.mousePosition)) return;

        fishClicked = true;
        bool same = IsSelectedFish(bubbleIndex, fishIndex, listId);
        selectedBubbleIndex = bubbleIndex;
        selectedFishIndex = fishIndex;
        selectedFishList = listId;
        if (!same) Repaint();
    }

    private bool IsSelectedFish(int bubbleIndex, int fishIndex, int listId)
    {
        return selectedBubbleIndex == bubbleIndex && selectedFishIndex == fishIndex && selectedFishList == listId;
    }

    private void ClampSelectedFish(LevelDesignData data)
    {
        SoftBubbleData bubble = GetSelectedBubble(data);
        if (bubble == null || selectedFishIndex < 0)
        {
            selectedFishIndex = -1;
            selectedFishList = FishListMain;
            return;
        }

        // Danh sách đang chọn có thể biến mất: tắt spawner, hoặc xoá bớt lớp làm lớp đang chọn không còn.
        bool listGone = selectedFishList == FishListSpawner
            ? !bubble.isSpawner
            : selectedFishList >= bubble.LayerCount;
        if (listGone)
        {
            selectedFishIndex = -1;
            selectedFishList = FishListMain;
            return;
        }

        FishData[] fish = GetFishArray(bubble, selectedFishList);
        if (selectedFishIndex >= fish.Length) selectedFishIndex = -1;
    }

    private SoftBubbleData GetSelectedBubble(LevelDesignData data)
    {
        SoftBubbleData[] bubbles = data.levelData?.SoftBubbleDataArray;
        if (bubbles == null || selectedBubbleIndex < 0 || selectedBubbleIndex >= bubbles.Length) return null;
        return bubbles[selectedBubbleIndex];
    }

    private FishData GetSelectedFish(LevelDesignData data)
    {
        if (selectedFishIndex < 0) return null;
        SoftBubbleData bubble = GetSelectedBubble(data);
        if (bubble == null) return null;
        FishData[] fish = GetFishArray(bubble, selectedFishList);
        if (selectedFishIndex >= fish.Length) return null;
        return fish[selectedFishIndex];
    }

    /// <summary>Bỏ chọn bóng / ô cá sau khi mảng bóng bị dựng lại, kẻo con trỏ trỏ vào chỗ không còn như cũ.</summary>
    private void ResetSelection()
    {
        selectedBubbleIndex = -1;
        selectedFishIndex = -1;
        selectedFishList = FishListMain;
    }

    /// <summary>Esc: đang chọn ô cá thì bỏ chọn cá; chỉ đang chọn bóng thì bỏ chọn bóng.</summary>
    private bool ClearSelection()
    {
        if (selectedFishIndex >= 0)
        {
            selectedFishIndex = -1;
            selectedFishList = FishListMain;
            Repaint();
            return true;
        }

        if (selectedBubbleIndex < 0) return false;
        selectedBubbleIndex = -1;
        selectedFishList = FishListMain;
        Repaint();
        return true;
    }

    // ---------------------------------------------------------------- kéo thả

    /// <summary>
    /// Bắt đầu kéo một con cá. bubbleIndex / fishIndex chỉ có khi kéo từ ô cá trong bóng, orderIndex chỉ có khi kéo từ
    /// ô đơn của OrderQueue; kéo từ palette thì không có gì cả nên bên nhận biết là thả ra con cá mới.
    /// </summary>
    private void HandleFishDragSource(Rect rect, int fishId, int bubbleIndex = -1, int fishIndex = -1,
        int listId = FishListMain, int orderIndex = -1)
    {
        if (splitterDrag >= 0) return;
        Event e = Event.current;
        if (e.type != EventType.MouseDrag || !rect.Contains(e.mousePosition)) return;

        DragAndDrop.PrepareStartDrag();
        DragAndDrop.objectReferences = Array.Empty<UnityEngine.Object>();
        DragAndDrop.SetGenericData(FishDragKey, new FishDrag
        {
            FishId = fishId,
            BubbleIndex = bubbleIndex,
            FishIndex = fishIndex,
            ListId = listId,
            OrderIndex = orderIndex
        });
        DragAndDrop.StartDrag($"Cá {fishId}");
        e.Use();
    }

    /// <summary>True đúng một lần, ở event DragPerform, khi có cá được thả vào rect.</summary>
    private static bool TryGetFishDrop(Rect rect, out FishDrag drag)
    {
        drag = null;
        Event e = Event.current;
        if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return false;
        if (!rect.Contains(e.mousePosition)) return false;
        if (DragAndDrop.GetGenericData(FishDragKey) is not FishDrag dragged) return false;

        // Kéo từ ô cá hoặc ô đơn là dời chỗ; chỉ kéo từ palette mới là nhân ra con cá mới.
        DragAndDrop.visualMode = dragged.FromSlot || dragged.FromOrder
            ? DragAndDropVisualMode.Move
            : DragAndDropVisualMode.Copy;
        if (e.type == EventType.DragUpdated)
        {
            e.Use();
            return false;
        }

        DragAndDrop.AcceptDrag();
        e.Use();
        drag = dragged;
        return true;
    }

    /// <summary>Bề dày vạch chèn vẽ ở khe giữa hai khung bóng.</summary>
    private const float InsertLineWidth = 3f;

    private static readonly Color InsertLineColor = new Color(1f, 0.79f, 0.2f, 1f);

    /// <summary>
    /// Khe sẽ chèn vào khi thả chuột ở vị trí này trên khung bóng index, hoặc -1 khi chuột đang ở khoảng giữa khung
    /// (nghĩa là đổi chỗ chứ không chèn). Khe đếm theo mảng gốc: 0 = trước bóng đầu, n = sau bóng cuối.
    /// Dải mép rộng 30% bề ngang khung mỗi bên, tối đa 24px, để giữa khung vẫn còn chỗ rộng cho thao tác đổi chỗ.
    /// </summary>
    private static int InsertSlotAt(Rect box, Vector2 mouse, int index)
    {
        float band = Mathf.Min(24f, box.width * 0.3f);
        if (mouse.x <= box.xMin + band) return index;
        if (mouse.x >= box.xMax - band) return index + 1;
        return -1;
    }

    /// <summary>Vạch chèn cho ô đơn trong OrderQueue, cùng kiểu với vạch chèn bóng: chỉ hiện khi đang kéo một ô đơn khác.</summary>
    private static void DrawOrderInsertLine(Rect cell, int index)
    {
        Event e = Event.current;
        if (e.type != EventType.Repaint || !cell.Contains(e.mousePosition)) return;
        if (DragAndDrop.GetGenericData(FishDragKey) is not FishDrag { FromOrder: true } drag) return;
        if (drag.OrderIndex == index || InsertSlotAt(cell, e.mousePosition, index) < 0) return;

        bool before = e.mousePosition.x < cell.center.x;
        float x = before ? cell.xMin - InsertLineWidth : cell.xMax;
        EditorGUI.DrawRect(new Rect(x, cell.yMin, InsertLineWidth, cell.height), InsertLineColor);
    }

    private void HandleBubbleDragAndSwap(LevelDesignData data, Rect box, int index)
    {
        if (splitterDrag >= 0) return;
        Event e = Event.current;
        if (e.type == EventType.Layout || !box.Contains(e.mousePosition)) return;

        if (e.type == EventType.MouseDrag)
        {
            if (GUIUtility.hotControl != 0) return;
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = Array.Empty<UnityEngine.Object>();
            DragAndDrop.SetGenericData(BubbleDragKey, index);
            DragAndDrop.StartDrag($"Bóng #{index + 1}");
            e.Use();
            return;
        }

        // Pass vẽ: kéo tới mép khung thì gạch một vạch dọc ngay đó, cho thấy trước bóng sẽ chèn vào chỗ nào.
        if (e.type == EventType.Repaint)
        {
            if (DragAndDrop.GetGenericData(BubbleDragKey) is not int dragging || dragging == index) return;
            if (InsertSlotAt(box, e.mousePosition, index) < 0) return;

            bool before = e.mousePosition.x < box.center.x;
            float x = before ? box.xMin - InsertLineWidth : box.xMax;
            EditorGUI.DrawRect(new Rect(x, box.yMin, InsertLineWidth, box.height), InsertLineColor);
            return;
        }

        if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return;
        if (DragAndDrop.GetGenericData(BubbleDragKey) is not int from || from == index) return;

        DragAndDrop.visualMode = DragAndDropVisualMode.Move;
        if (e.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            int to = index;

            // Mép trái / phải = chèn vào khe đó; giữa khung = đổi chỗ hai bóng như cũ.
            int slot = InsertSlotAt(box, e.mousePosition, index);
            pending = slot >= 0 ? () => InsertBubbleAt(data, from, slot) : () => SwapBubbles(data, from, to);
        }

        e.Use();
    }

    // ---------------------------------------------------------------- phím tắt

    private void HandleBubbleHotkeys(LevelDesignData data, DesignConfig design)
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown) return;
        if (EditorGUIUtility.editingTextField) return;

        if (e.keyCode == KeyCode.Escape)
        {
            if (ClearSelection()) e.Use();
            return;
        }

        if (design.DeleteHotkey != KeyCode.None && e.keyCode == design.DeleteHotkey)
        {
            if (HandleDeleteSelection(data)) e.Use();
            return;
        }

        if (design.InsertBubbleHotkey != KeyCode.None && e.keyCode == design.InsertBubbleHotkey)
        {
            if (HandleInsertBubble(data)) e.Use();
            return;
        }

        SoftBubbleData bubble = GetSelectedBubble(data);
        if (bubble == null) return;
        if (!design.TryGetObstacleByHotkey(e.keyCode, out BubbleObstacle obstacle)) return;

        bool value = !DesignConfig.GetFlag(bubble, obstacle);
        if (obstacle == BubbleObstacle.Spawner && value && bubble.isRock) return;

        SoftBubbleData target = bubble;
        pending = () => SetObstacle(data, target, obstacle, value);
        e.Use();
    }

    /// <summary>Chèn bóng trống đúng chỗ bóng đang chọn; bóng cũ và các bóng sau dồn sang.</summary>
    private bool HandleInsertBubble(LevelDesignData data)
    {
        if (selectedBubbleIndex < 0 || GetSelectedBubble(data) == null) return false;

        int index = selectedBubbleIndex;
        selectedFishIndex = -1;
        selectedFishList = FishListMain;
        pending = () => InsertBubble(data, index);
        return true;
    }

    /// <summary>Delete xoá ô cá đang chọn; không có ô cá thì xoá bóng đang chọn.</summary>
    private bool HandleDeleteSelection(LevelDesignData data)
    {
        SoftBubbleData bubble = GetSelectedBubble(data);
        if (bubble == null) return false;

        if (GetSelectedFish(data) != null)
        {
            int fishIndex = selectedFishIndex;
            int listId = selectedFishList;
            pending = () => RemoveFish(data, bubble, fishIndex, listId);
            return true;
        }

        int bubbleIndex = selectedBubbleIndex;
        pending = () => RemoveBubble(data, bubbleIndex);
        return true;
    }
}
