using System;
using System.Collections.Generic;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Lưới khung bóng ở giữa: mỗi khung là một SoftBubbleData với thanh size và các ô cá bên trong.
/// Xem <see cref="LevelDesignDataEditor"/> cho danh sách các file partial khác.
/// </summary>
public partial class LevelDesignDataEditor
{
    private const int SpawnerFishPerRow = 5;

    /// <summary>Khoảng cách ngang giữa hai cột bóng.</summary>
    private const float BubbleColumnGap = 10f;

    /// <summary>Khoảng cách dọc giữa tiêu đề và ô cá trong một khung bóng.</summary>
    private const float BubbleSectionGap = 6f;

    private const float BubbleSizeMin = 1f;
    private const float BubbleSizeMax = 5f;

    /// <summary>Bước nhảy của hai nút - / + cạnh slider size, khớp với mức làm tròn 0.1 của bán kính.</summary>
    private const float SizeStep = 0.1f;

    private const float SizeStepButtonWidth = 20f;

    /// <summary>Khoảng cách giữa hai ô cá, cả ngang lẫn dọc.</summary>
    private const float FishSlotGap = 4f;

    private Vector2 gridScroll;

    /// <summary>Lớp đang xem của từng bóng, khoá theo index bóng. Trạng thái tool, không phải data.</summary>
    private readonly Dictionary<int, int> layerPage = new Dictionary<int, int>();

    private void DrawBubbleGrid(LevelDesignData data, DesignConfig design, float viewHeight)
    {
        SoftBubbleData[] bubbles = data.levelData.SoftBubbleDataArray ?? Array.Empty<SoftBubbleData>();
        float boxWidth = BubbleBoxWidth(design);
        int perRow = Mathf.Clamp(design.BubblesPerRow, 1, DesignConfig.MaxBubblesPerRow);
        int totalCells = bubbles.Length + 1;

        // Băng chuyền rộng bằng cả dòng, và số ô cá một hàng tính theo bấy nhiêu px.
        float conveyorWidth = perRow * (boxWidth + BubbleColumnGap) - BubbleColumnGap;
        int conveyorFishPerRow = Mathf.Max(1,
            Mathf.FloorToInt((conveyorWidth - 16f) / (design.FishSlotSize + FishSlotGap)));

        EditorGUILayout.BeginVertical();
        GUILayout.Label($"Bóng ({bubbles.Length})", SirenixGUIStyles.BoldLabelCentered, GUILayout.ExpandWidth(true));
        GUILayout.Label("thả vào giữa khung = đổi chỗ · thả vào mép = chèn", SirenixGUIStyles.CenteredGreyMiniLabel,
            GUILayout.ExpandWidth(true));
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Label("ID cá", SirenixGUIStyles.CenteredGreyMiniLabel, GUILayout.Width(36f));

        // Parse đúng lúc gõ chứ không parse lại ở từng ô cá: Trim + TryParse cho vài trăm ô mỗi pass là rác thuần tuý.
        EditorGUI.BeginChangeCheck();
        highlightFishIdText = EditorGUILayout.TextField(highlightFishIdText, GUILayout.Width(64f), GUILayout.MaxWidth(64f));
        if (EditorGUI.EndChangeCheck()) RefreshHighlightFishId();

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        gridScroll = EditorGUILayout.BeginScrollView(gridScroll, GUILayout.Height(viewHeight - TitleBlockHeight));
        // Xếp kiểu dòng chảy chứ không tính index = row * perRow + col nữa: băng chuyền chiếm trọn một dòng nên số ô
        // mỗi dòng không còn cố định. Luồng này chỉ phụ thuộc cờ isConveyor trong data - thứ không đổi giữa pass
        // Layout và Repaint - nên số lần Begin/End vẫn khớp nhau.
        bool rowOpen = false;
        int col = 0;
        for (int index = 0; index < totalCells; index++)
        {
            SoftBubbleData bubble = index < bubbles.Length ? bubbles[index] : null;
            bool fullRow = bubble != null && bubble.isConveyor;

            // Đang dở dòng mà gặp băng chuyền thì đóng dòng đó lại, băng chuyền luôn mở dòng mới của riêng nó.
            if (fullRow && rowOpen)
            {
                EndBubbleRow();
                rowOpen = false;
            }

            if (!rowOpen)
            {
                EditorGUILayout.BeginHorizontal();
                rowOpen = true;
                col = 0;
            }

            EditorGUILayout.BeginVertical(GUILayout.Width(fullRow ? conveyorWidth : boxWidth));
            if (bubble != null) DrawBubble(data, design, bubbles, index, fullRow ? conveyorFishPerRow : 0);
            else if (GUILayout.Button("+ Thêm bóng", GUILayout.Height(30f))) pending = () => AddBubble(data);
            EditorGUILayout.EndVertical();
            GUILayout.Space(BubbleColumnGap);
            col++;

            if (!fullRow && col < perRow) continue;
            EndBubbleRow();
            rowOpen = false;
        }

        if (rowOpen) EndBubbleRow();

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private static void EndBubbleRow()
    {
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private static float BubbleBoxWidth(DesignConfig design)
    {
        return design.FishPerRow * (design.FishSlotSize + 4f) + 16f;
    }

    /// <summary>
    /// Một khung bóng. Khung cao đúng theo nội dung của chính nó - số cá của nó, có hàng ô đơn hay không - chứ không
    /// chừa chỗ theo bóng to nhất. Lưới xếp theo hàng, mỗi hàng là một BeginHorizontal, nên các khung trong cùng hàng
    /// tự khớp cạnh trên; cao thấp khác nhau chỉ làm cạnh dưới lệch, không làm lệch hàng.
    /// </summary>
    /// <param name="fishPerRow">0 = theo DesignConfig.FishPerRow; băng chuyền truyền số ô vừa hết bề rộng một dòng.</param>
    private void DrawBubble(LevelDesignData data, DesignConfig design, SoftBubbleData[] bubbles, int index,
        int fishPerRow = 0)
    {
        SoftBubbleData bubble = bubbles[index];
        if (bubble == null) return;

        Rect box = EditorGUILayout.BeginVertical(SirenixGUIStyles.BoxContainer);
        bool selected = index == selectedBubbleIndex;
        bool error = IsCheckErrorBubble(index);
        if (Event.current.type == EventType.Repaint)
        {
            Color fill = design.GetBubbleColor(bubble);
            Color border = selected ? design.HoverColor : SirenixGUIStyles.BorderColor;
            float borderWidth = selected ? 2f : 1f;
            if (error)
            {
                fill = Color.Lerp(fill, CheckErrorFill, 0.75f);
                fill.a = Mathf.Max(fill.a, CheckErrorFill.a);
                border = CheckError;
                borderWidth = 2f;
            }
            else if (IsLossBubble(data, index))
            {
                // Bóng giữ cá bể cần mà người chơi không lấy ra được lúc kẹt (xem LossSpots): chỉ viền cam, giữ màu
                // vật cản bên trong để còn nhìn ra là băng / spawner / HP.
                border = LossSpot;
                borderWidth = 2f;
            }

            DrawRoundedRect(box, fill, border, borderWidth);
        }

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(BubbleIdLabel(bubble.softBubbleId), SirenixGUIStyles.BoldLabel);

        // Cá spawner chỉ bày ra ở panel phải khi chọn bóng, nên ghi sẵn số con trong hàng đợi ngay trên khung.
        if (bubble.isSpawner && !bubble.isRock)
        {
            int queued = bubble.fishOnSpawner?.Length ?? 0;
            TempContent.text = $"S+{queued}";
            TempContent.tooltip = queued > 0
                ? $"Spawner: {queued} cá chờ, mỗi con cá hiện rời bóng thì một con thay vào. Chọn bóng để xem / sửa."
                : "Spawner chưa có cá nào trong hàng đợi";
            GUILayout.Label(TempContent, queued > 0 ? SirenixGUIStyles.LeftAlignedGreyMiniLabel : SirenixGUIStyles.LeftAlignedGreyLabel);
        }

        GUILayout.FlexibleSpace();
        int layer = DrawLayerPager(bubble, index);
        GUILayout.FlexibleSpace();
        GUILayout.Label(IndexLabel(index + 1), SirenixGUIStyles.RightAlignedGreyMiniLabel, GUILayout.Width(30f));
        if (GUILayout.Button("×", SirenixGUIStyles.MiniButton, GUILayout.Width(20f))) pending = () => RemoveBubble(data, index);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(BubbleSectionGap);

        // Băng chuyền không phải bóng nên không có bán kính; thanh Size chỉ vẽ cho bóng thường.
        if (!bubble.isConveyor)
        {
            DrawBubbleSizeSlider(data, bubble);
            GUILayout.Space(BubbleSectionGap);
        }

        // Ô cá của đơn nằm giữa khung, phía trên phần ô cá thường; bóng không phải bóng đơn thì không tốn hàng nào.
        if (bubble.isOrderBubble)
        {
            DrawOrderFishSlot(data, design, bubble);
            GUILayout.Space(BubbleSectionGap);
        }

        if (bubble.isRock) DrawRockContent(design);
        else DrawFishSlots(data, design, bubble, index, 0, layer, perRow: fishPerRow, showAddSlot: false);

        EditorGUILayout.EndVertical();

        HandleBubbleSelect(box, index);
        HandleBubbleDragAndSwap(data, box, index);
        HandleFishDropOnBubble(data, bubble, box, index, layer);
        GUILayout.Space(4f);
    }

    /// <summary>
    /// Bubble bundle: trong game ấn một con là cả bó cá được chọn, nên trong tool click một ô cũng sáng cả bó của
    /// đúng lớp đang xem. Nhìn là biết ngay cờ đang bật mà không phải mở panel Obstacle ra đọc.
    /// </summary>
    private bool IsBundleSelected(SoftBubbleData bubble, int bubbleIndex, int listId)
    {
        return bubble.isBundleBubble && selectedBubbleIndex == bubbleIndex && selectedFishIndex >= 0 &&
               selectedFishList == listId;
    }

    /// <summary>
    /// Thanh chuyển lớp nằm giữa dải tiêu đề: [◀] 2/3 [▶]. Trả về lớp đang xem, phần ô cá bên dưới vẽ đúng lớp đó,
    /// nên mọi thao tác kéo thả / xoá cá đều nhắm vào lớp đang hiển thị. Bóng một lớp thì không vẽ gì.
    ///
    /// Lớp đang xem là trạng thái của tool, không ghi vào data. Bấm nút chỉ hẹn đổi qua pending rồi Repaint chứ không
    /// đổi ngay: đổi giữa pass sẽ khiến số hàng ô cá vẽ ra khác số hàng pass Layout vừa tính, GUILayout sẽ kêu lệch.
    /// </summary>
    private int DrawLayerPager(SoftBubbleData bubble, int bubbleIndex)
    {
        int count = bubble.LayerCount;
        if (count <= 1)
        {
            layerPage.Remove(bubbleIndex);
            return FishListMain;
        }

        int page = Mathf.Clamp(layerPage.GetValueOrDefault(bubbleIndex), 0, count - 1);

        bool wasEnabled = GUI.enabled;
        GUI.enabled = page > 0;
        if (GUILayout.Button("◀", SirenixGUIStyles.MiniButton, GUILayout.Width(20f)))
        {
            int next = page - 1;
            pending = () => layerPage[bubbleIndex] = next;
        }

        GUI.enabled = wasEnabled;
        TempContent.text = $"{page + 1}/{count}";
        TempContent.tooltip = page == 0
            ? $"Lớp 1 - lớp đang hiện, {bubble.LayerFishCount(0)} cá"
            : $"Lớp {page + 1} - lộ ra khi lớp {page} hết cá; tối đa {bubble.LayerFishCount(page - 1)} cá";
        GUILayout.Label(TempContent, SirenixGUIStyles.BoldLabelCentered, GUILayout.Width(34f));

        GUI.enabled = page < count - 1;
        if (GUILayout.Button("▶", SirenixGUIStyles.MiniButton, GUILayout.Width(20f)))
        {
            int next = page + 1;
            pending = () => layerPage[bubbleIndex] = next;
        }

        GUI.enabled = wasEnabled;
        layerPage[bubbleIndex] = page;
        return page;
    }

    /// <summary>
    /// Thả cá vào chỗ trống trong khung bóng thì thêm vào cuối, thay cho ô "+" đã bỏ. Các ô cá và ô đơn vẽ trước và
    /// tự Use() event khi nhận thả, nên khi chạy tới đây event chỉ còn sống nếu con trỏ đang ở ngoài mọi ô - đúng chỗ
    /// cần hiểu là "thêm mới". Thả trúng một con cá sẵn có thì vẫn là thay con đó chứ không thêm.
    /// </summary>
    private void HandleFishDropOnBubble(LevelDesignData data, SoftBubbleData bubble, Rect box, int index, int layer)
    {
        if (bubble.isRock) return;
        if (!TryGetFishDrop(box, out FishDrag drop)) return;

        // Index ngoài mảng: DropFishOnSlot và SwapOrMoveFish đều hiểu là nối vào cuối danh sách.
        pending = () => DropFishOnSlot(data, drop, index, int.MaxValue, layer);
    }

    /// <summary>
    /// Ô cá của đơn (fishOrderId), căn giữa khung bóng. Kéo cá từ palette hay từ ô khác vào để đặt, giống ô cá thường;
    /// gõ số thì dùng field "Fish order id" ở panel Obstacle bên phải.
    /// </summary>
    private void DrawOrderFishSlot(LevelDesignData data, DesignConfig design, SoftBubbleData bubble)
    {
        float slot = design.FishSlotSize;
        Rect row = GUILayoutUtility.GetRect(slot, slot, GUILayout.Height(slot), GUILayout.ExpandWidth(true));
        var cell = new Rect(row.center.x - slot * 0.5f, row.y, slot, slot);
        DrawFishIcon(cell, bubble.fishOrderId, design, false);
        if (Event.current.type == EventType.Repaint)
        {
            GUI.Label(new Rect(row.x + 2f, row.y, cell.x - row.x - 4f, 14f), "Order",
                SirenixGUIStyles.LeftAlignedWhiteMiniLabel);
        }

        HandleFishDragSource(cell, bubble.fishOrderId);
        if (!TryGetFishDrop(cell, out FishDrag drop)) return;

        SoftBubbleData target = bubble;
        int fishId = drop.FishId;
        pending = () => Mutate(data, "Đổi cá của đơn", () => target.fishOrderId = fishId);
    }

    private void DrawBubbleSizeSlider(LevelDesignData data, SoftBubbleData bubble)
    {
        // Chặn dưới của slider là sàn theo số cá, nên không kéo xuống dưới mức bóng đủ chỗ cho đàn bên trong.
        float floor = Mathf.Max(BubbleSizeMin, MinRadiusForFishCount(bubble.FishCount));
        float shown = Mathf.Max(floor, EditorBubbleRadius(bubble));
        float max = Mathf.Max(BubbleSizeMax, shown);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Size", GUILayout.Width(32f));
        EditorGUI.BeginChangeCheck();
        float next = GUILayout.HorizontalSlider(shown, floor, max);
        next = Mathf.Round(Mathf.Max(floor, next) * 10f) / 10f;
        if (EditorGUI.EndChangeCheck())
        {
            SoftBubbleData target = bubble;
            float chosen = next;
            pending = () => Mutate(data, "Đổi size bóng",
                () => target.Radius = Mathf.Max(chosen, MinRadiusForFishCount(target.FishCount)));
        }

        GUILayout.Label(RadiusLabel(shown), SirenixGUIStyles.RightAlignedGreyMiniLabel, GUILayout.Width(28f));

        // Hai nút bấm từng bước SizeStep cho lúc cần đúng một con số, kéo slider khó dừng chuẩn ở 0.1.
        GUI.enabled = shown > floor + 0.001f;
        if (GUILayout.Button("-", SirenixGUIStyles.MiniButton, GUILayout.Width(SizeStepButtonWidth)))
        {
            StepBubbleSize(data, bubble, -SizeStep);
        }

        GUI.enabled = shown < max - 0.001f;
        if (GUILayout.Button("+", SirenixGUIStyles.MiniButton, GUILayout.Width(SizeStepButtonWidth)))
        {
            StepBubbleSize(data, bubble, SizeStep);
        }

        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// Cộng / trừ bán kính bóng một bước. Làm tròn 0.1 rồi kẹp trong [sàn theo số cá, trần slider] - bóng đang to hơn
    /// trần (chỉnh tay từ trước) thì trần lấy luôn size đó, nên bấm "+" không kéo tụt bóng xuống.
    /// </summary>
    private void StepBubbleSize(LevelDesignData data, SoftBubbleData bubble, float step)
    {
        SoftBubbleData target = bubble;
        pending = () => Mutate(data, "Đổi size bóng", () =>
        {
            float floor = Mathf.Max(BubbleSizeMin, MinRadiusForFishCount(target.FishCount));
            float current = Mathf.Max(floor, EditorBubbleRadius(target));
            float max = Mathf.Max(BubbleSizeMax, current);
            target.Radius = Mathf.Clamp(Mathf.Round((current + step) * 10f) / 10f, floor, max);
        });
    }

    /// <summary>Bóng đá: vẽ icon RockIcon từ DesignConfig cho dễ nhận; thiếu icon thì vẫn ghi chữ.</summary>
    private static void DrawRockContent(DesignConfig design)
    {
        Texture2D icon = design != null ? design.RockIcon : null;
        float size = design != null
            ? Mathf.Clamp(design.FishSlotSize * 2f + design.FishSlotExtraHeight, 80f, 140f)
            : 96f;
        Rect row = GUILayoutUtility.GetRect(size, size, GUILayout.Height(size), GUILayout.ExpandWidth(true));
        if (Event.current.type != EventType.Repaint) return;

        if (icon != null)
        {
            float x = row.x + (row.width - size) * 0.5f;
            GUI.DrawTexture(new Rect(x, row.y, size, size), icon, ScaleMode.ScaleToFit, true);
            return;
        }

        GUI.Label(row, "Đá — không chứa cá", SirenixGUIStyles.CenteredGreyMiniLabel);
    }

    /// <summary>
    /// Sàn bán kính theo số cá, lấy thẳng GamePlayGlobalConfig.BubbleRadiusFloors - cùng bảng mà
    /// RandomBubbleRadius dùng lúc sinh seed, nên tool và lúc spawn trong game không lệch nhau nữa.
    /// </summary>
    private static float MinRadiusForFishCount(int fishCount)
    {
        GamePlayGlobalConfig config = GamePlayGlobalConfig.Instance;
        return config != null ? config.MinBubbleRadius(fishCount) : BubbleSizeMin;
    }

    /// <summary>Bán kính để vẽ: seed ghi nhỏ hơn sàn của số cá hiện có thì hiện theo sàn.</summary>
    private static float EditorBubbleRadius(SoftBubbleData bubble)
    {
        if (bubble == null) return BubbleSizeMin;
        return Mathf.Max(bubble.Radius, MinRadiusForFishCount(bubble.FishCount));
    }

    /// <summary>
    /// Cập nhật bán kính sau khi số cá trong bóng đổi từ oldFishCount sang số hiện tại. Gọi sau mọi thao tác thêm /
    /// xoá / kéo cá giữa các bóng / rải cá khỏi bóng đá.
    ///
    /// Dải "tự động" của n cá là dải lúc sinh seed: [sàn n cá, sàn n+1 cá] (xem GamePlayGlobalConfig.RandomBubbleRadius).
    /// - Bán kính đang nằm trong dải của số cá cũ: coi là size tự động, dời sang dải của số cá mới và giữ vị trí tương
    ///   đối (đang ở giữa dải cũ thì ra giữa dải mới), làm tròn 0.1 như lúc sinh.
    /// - Bán kính to hơn trần dải cũ: coi là người dựng level cố ý chỉnh to để tạo độ khó, giữ nguyên - chỉ nâng lên
    ///   nếu thấp hơn sàn mới.
    /// </summary>
    private static void ResizeForFishCount(SoftBubbleData bubble, int oldFishCount)
    {
        if (bubble == null) return;

        int newFishCount = bubble.FishCount;
        if (newFishCount == oldFishCount)
        {
            EnforceMinRadius(bubble);
            return;
        }

        float oldMin = MinRadiusForFishCount(oldFishCount);
        float oldMax = Mathf.Max(oldMin, MinRadiusForFishCount(oldFishCount + 1));
        float newMin = MinRadiusForFishCount(newFishCount);
        float newMax = Mathf.Max(newMin, MinRadiusForFishCount(newFishCount + 1));

        // Lệch nhỏ cho sai số float của số đã làm tròn 0.1: 2.3 lưu ra 2.2999 vẫn tính là nằm trong dải.
        if (bubble.Radius > oldMax + 0.01f)
        {
            EnforceMinRadius(bubble);
            return;
        }

        float t = oldMax > oldMin ? Mathf.Clamp01((bubble.Radius - oldMin) / (oldMax - oldMin)) : 0f;
        float resized = Mathf.Round(Mathf.Lerp(newMin, newMax, t) * 10f) / 10f;
        bubble.Radius = Mathf.Max(newMin, resized);
    }

    /// <summary>
    /// Kẹp bán kính lên sàn của số cá đang có, không thu nhỏ bóng đang to hơn sàn. Dùng cho nút "Tự chỉnh size mọi
    /// bóng" và cho lượt đổi số cá mà bóng đang được chỉnh to cố ý.
    /// </summary>
    private static void EnforceMinRadius(SoftBubbleData bubble)
    {
        if (bubble == null) return;

        float floor = MinRadiusForFishCount(bubble.FishCount);
        if (bubble.Radius < floor) bubble.Radius = floor;
    }

    private void ApplyAutoSizeToAllBubbles(LevelDesignData data)
    {
        Mutate(data, "Tự chỉnh size mọi bóng", () =>
        {
            SoftBubbleData[] bubbles = data.levelData?.SoftBubbleDataArray;
            if (bubbles == null) return;
            foreach (SoftBubbleData bubble in bubbles) EnforceMinRadius(bubble);
        });
    }

    /// <summary>Các ô cá của bóng; mỗi ô có thêm checkbox isHidden của chính con cá đó.</summary>
    private void DrawFishSlots(LevelDesignData data, DesignConfig design, SoftBubbleData bubble, int bubbleIndex,
        int slotCount, int listId = FishListMain, float slot = -1f, int perRow = 0, bool showAddSlot = true)
    {
        FishData[] fish = GetFishArray(bubble, listId);

        // Trong khung bóng không còn ô "+": thả cá vào chỗ trống nào trong khung cũng tự thêm (HandleFishDropOnBubble).
        // Hàng fishOnSpawner ở panel phải vẫn giữ ô "+" vì quanh nó không có khung nào để thả.
        int cells = fish.Length + (showAddSlot ? 1 : 0);
        if (slot <= 0f) slot = design.FishSlotSize;
        if (perRow < 1) perRow = Mathf.Max(1, design.FishPerRow);

        // slotCount <= 0 = bóng tự co theo số cá của chính nó, chỉ làm tròn lên cho đủ hàng cuối. Luôn giữ ít nhất
        // một hàng để bóng rỗng vẫn có chỗ trống mà thả cá vào. Chỗ gọi truyền số cụ thể thì tôn trọng số đó.
        if (slotCount <= 0) slotCount = Mathf.Max(perRow, Mathf.CeilToInt(cells / (float)perRow) * perRow);
        float extra = Mathf.Max(0f, design.FishSlotExtraHeight);
        float height = slot + extra;

        for (int i = 0; i < slotCount; i += perRow)
        {
            EditorGUILayout.BeginHorizontal();
            for (int c = 0; c < perRow && i + c < slotCount; c++)
            {
                int k = i + c;

                // Cả ô xin đúng một rect rồi tự chia bằng toạ độ, không lồng group con: nút và toggle mang margin
                // riêng của style nên để GUILayout tự xếp thì ô có cá và ô trống lệch nhau vài px, cộng dồn 7 hàng là
                // các cột không còn thẳng hàng.
                Rect cell = GUILayoutUtility.GetRect(slot, height, GUILayout.Width(slot), GUILayout.Height(height));

                if (k < fish.Length)
                {
                    FishData entry = fish[k];
                    int fishIndex = k;
                    bool selected = IsSelectedFish(bubbleIndex, fishIndex, listId) ||
                                    IsBundleSelected(bubble, bubbleIndex, listId);
                    bool error = IsCheckErrorFish(bubbleIndex, fishIndex, listId);
                    HandleFishSelect(cell, bubbleIndex, fishIndex, listId);
                    DrawFishIcon(cell, entry.FishId, design, entry.isHidden, selected, false, error,
                        IsLossFish(data, bubbleIndex, entry.FishId));

                    // Checkbox isHidden và nút xoá nằm đè lên hai góc trên của icon, có nền tối phía sau cho nổi.
                    var hiddenRect = new Rect(cell.x + 2f, cell.y + 2f, OverlaySize, OverlaySize);
                    var closeRect = new Rect(cell.xMax - OverlaySize - 2f, cell.y + 2f, OverlaySize, OverlaySize);
                    DrawRoundedRect(hiddenRect, OverlayBackdrop, Color.clear, 0f);

                    TempContent.text = string.Empty;
                    TempContent.tooltip = "Ẩn con cá này (FishData.isHidden)";
                    bool hidden = EditorGUI.ToggleLeft(hiddenRect, TempContent, entry.isHidden, EditorStyles.miniLabel);
                    if (hidden != entry.isHidden) pending = () => Mutate(data, "Đổi cá ẩn", () => entry.isHidden = hidden);

                    bool remove = GUI.Button(closeRect, "×", SirenixGUIStyles.MiniButton);

                    // Hai nút đè lên icon nên phải trừ ra, không thì bấm vào chúng lại thành kéo con cá đi.
                    Vector2 mouse = Event.current.mousePosition;
                    if (!hiddenRect.Contains(mouse) && !closeRect.Contains(mouse))
                        HandleFishDragSource(cell, entry.FishId, bubbleIndex, fishIndex, listId);
                    if (TryGetFishDrop(cell, out FishDrag drop))
                        pending = () => DropFishOnSlot(data, drop, bubbleIndex, fishIndex, listId);
                    if (remove) pending = () => RemoveFish(data, bubble, fishIndex, listId);
                }
                else if (k < cells)
                {
                    DrawEmptySlot(cell, design, "+");
                    if (TryGetFishDrop(cell, out FishDrag added))
                        pending = () => DropFishOnSlot(data, added, bubbleIndex, k, listId);
                }

                // k >= cells: ô giữ chỗ, đã chiếm đủ chiều cao ở trên rồi nên không vẽ gì thêm.
                if (c + 1 < perRow) GUILayout.Space(FishSlotGap);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            if (i + perRow < slotCount) GUILayout.Space(FishSlotGap);
        }
    }

    private void DrawSpawnerFish(LevelDesignData data, DesignConfig design, SoftBubbleData bubble)
    {
        GUILayout.Label("fishOnSpawner", SirenixGUIStyles.CenteredGreyMiniLabel, GUILayout.ExpandWidth(true));
        int count = (bubble.fishOnSpawner?.Length ?? 0) + 1;
        DrawFishSlots(data, design, bubble, selectedBubbleIndex, count, FishListSpawner, WrapFishSlot, SpawnerFishPerRow);
    }
}
