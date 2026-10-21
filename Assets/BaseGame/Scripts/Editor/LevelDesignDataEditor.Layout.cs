using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Bề rộng bốn cột và thanh kéo giữa chúng: nạp / ghi EditorPrefs, bắt chuột kéo, vẽ vạch dọc.
/// Xem <see cref="LevelDesignDataEditor"/> cho danh sách các file partial khác.
/// </summary>
public partial class LevelDesignDataEditor
{
    private const int PaletteColumn = 0;
    private const int OrderColumn = 1;
    private const int SideColumn = 2;

    private const int WrapFishPerRow = 3;

    /// <summary>
    /// Cạnh ô cá ở hai cột List item cá / OrderQueue. Bề rộng mặc định lẫn bề rộng tối thiểu của hai cột đều suy ra từ
    /// số này, nên muốn hai cột gọn hay to hơn thì chỉnh đúng một chỗ này. Ô trong khung bóng đi theo
    /// DesignConfig.FishSlotSize, không dính gì tới đây.
    /// </summary>
    private const float WrapFishSlot = 48f;

    /// <summary>Khoảng cách giữa hai ô cá ở List item cá / OrderQueue, cả ngang lẫn dọc.</summary>
    private const float WrapSlotGap = 8f;
    private const float WrapRowLabelWidth = 20f;
    private const float WrapPadding = 36f;

    private const float DefaultPaletteWidth = WrapFishPerRow * (WrapFishSlot + WrapSlotGap) + WrapPadding;
    private const float DefaultOrderWidth = WrapFishPerRow * (WrapFishSlot + WrapSlotGap) + WrapRowLabelWidth + WrapPadding;
    private const float MinPaletteWidth = DefaultPaletteWidth;
    private const float MinOrderWidth = DefaultOrderWidth;
    private const float DefaultSideWidth = 256f;
    private const float MaxWrapWidth = 900f;
    private const float SplitterWidth = 12f;
    private const float SplitterHitPadLeft = 4f;
    private const float SplitterHitPadRight = 10f;

    // Nhét luôn bề rộng mặc định vào tên khoá: đổi WrapFishSlot là khoá đổi theo, EditorPrefs cũ tự hết hiệu lực.
    // Không thì số cũ vẫn nằm đó và mở lên tưởng là code chưa đổi gì.
    private static readonly string WidthPrefKey =
        $"FishSort.LevelDesign.Col{(int)DefaultPaletteWidth}x{(int)DefaultOrderWidth}.";

    private static readonly int WrapSplitterHint = "FishSort.LevelDesign.WrapSplitter".GetHashCode();

    [System.NonSerialized] private Rect[] splitterHitRects = new Rect[3];

    /// <summary>Bề rộng wrap kéo tay được, nhớ trong EditorPrefs. 0 = chưa nạp.</summary>
    private float paletteWidth;

    private float orderWidth;
    private float sideWidth;
    private int splitterDrag = -1;
    private float splitterLastX;

    /// <summary>
    /// Mỗi cột cần đúng 4 GUILayoutOption và GUILayout.Width cấp phát cái mới mỗi lần gọi, mà cột nào cũng dựng lại ở
    /// mọi pass GUILayout. Giữ lại theo cột, chỉ dựng lại khi bề rộng đổi - tức là lúc đang kéo thanh chia cột.
    /// </summary>
    [System.NonSerialized] private float[] columnWidthKeys = { -1f, -1f, -1f };

    [System.NonSerialized] private GUILayoutOption[][] columnWidthOptions = new GUILayoutOption[3][];

    /// <summary>
    /// Dựng lại các mảng trạng thái nếu bị null. Instance editor do LevelDesignWindow giữ sống qua domain reload, và
    /// sau reload các field này có thể về null dù có initializer - HandleWrapWidthInput từng ném NullReference vì thế.
    /// </summary>
    private void EnsureLayoutState()
    {
        if (splitterHitRects == null || splitterHitRects.Length != 3) splitterHitRects = new Rect[3];
        if (columnWidthKeys == null || columnWidthKeys.Length != 3) columnWidthKeys = new[] { -1f, -1f, -1f };
        if (columnWidthOptions == null || columnWidthOptions.Length != 3) columnWidthOptions = new GUILayoutOption[3][];
    }

    private static float MinSideWidth() => 160f;

    private static float ClampWrap(float value, float min, float max) => Mathf.Clamp(value, min, max);

    private void LoadWrapSizes()
    {
        paletteWidth = ClampWrap(EditorPrefs.GetFloat(WidthPrefKey + "palette", DefaultPaletteWidth), MinPaletteWidth,
            MaxWrapWidth);
        orderWidth = ClampWrap(EditorPrefs.GetFloat(WidthPrefKey + "order", DefaultOrderWidth), MinOrderWidth, MaxWrapWidth);
        sideWidth = ClampWrap(EditorPrefs.GetFloat(WidthPrefKey + "side", DefaultSideWidth), MinSideWidth(), MaxWrapWidth);
    }

    private void ClampLoadedWrapSizes()
    {
        paletteWidth = ClampWrap(paletteWidth, MinPaletteWidth, MaxWrapWidth);
        orderWidth = ClampWrap(orderWidth, MinOrderWidth, MaxWrapWidth);
        sideWidth = ClampWrap(sideWidth, MinSideWidth(), MaxWrapWidth);
    }

    private void SaveWrapSizes()
    {
        EditorPrefs.SetFloat(WidthPrefKey + "palette", paletteWidth);
        EditorPrefs.SetFloat(WidthPrefKey + "order", orderWidth);
        EditorPrefs.SetFloat(WidthPrefKey + "side", sideWidth);
    }

    /// <summary>
    /// Tiêu đề hai dòng của một cột. Bắt buộc dùng GUILayout.Label chứ KHÔNG được dùng EditorGUILayout.LabelField:
    /// LabelField là property field không có ô nhập, nó tự đặt minWidth = EditorGUIUtility.labelWidth + fieldWidth,
    /// mà labelWidth trong Inspector mặc định bằng ~45% bề rộng cửa sổ (cửa sổ 1900px thì ra ~850px). Trong GUILayout
    /// minWidth thắng maxWidth, nên một dòng tiêu đề như vậy đủ sức phình cả cột lên ~900px và đè chết GUILayout.Width
    /// mình đặt, đẩy các cột còn lại ra ngoài màn.
    /// </summary>
    private static void PanelTitle(string title, string subtitle)
    {
        GUILayout.Label(title, SirenixGUIStyles.BoldLabelCentered, GUILayout.ExpandWidth(true));
        GUILayout.Label(subtitle, SirenixGUIStyles.CenteredGreyMiniLabel, GUILayout.ExpandWidth(true));
    }

    /// <summary>Khoá cứng bề rộng một cột: Width thôi chưa đủ, GUILayout vẫn nong theo nội dung nếu không chặn cả min/max.</summary>
    private GUILayoutOption[] ColumnWidth(int column, float width)
    {
        if (columnWidthOptions[column] == null || !Mathf.Approximately(columnWidthKeys[column], width))
        {
            columnWidthKeys[column] = width;
            columnWidthOptions[column] = new[]
            {
                GUILayout.Width(width),
                GUILayout.MinWidth(width),
                GUILayout.MaxWidth(width),
                GUILayout.ExpandWidth(false)
            };
        }

        return columnWidthOptions[column];
    }

    /// <summary>
    /// Xử lý kéo bề rộng wrap trước khi vẽ con. Scroll view / ô cá vẽ trước thanh kéo nên nếu bắt chuột lúc vẽ
    /// splitter thì event đã bị nuốt; hit rect lấy từ pass Repaint trước.
    /// </summary>
    private void HandleWrapWidthInput()
    {
        int id = GUIUtility.GetControlID(WrapSplitterHint, FocusType.Passive);
        Event e = Event.current;

        for (int i = 0; i < splitterHitRects.Length; i++)
        {
            if (splitterHitRects[i].width > 1f)
                EditorGUIUtility.AddCursorRect(splitterHitRects[i], MouseCursor.ResizeHorizontal);
        }

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            for (int i = 0; i < splitterHitRects.Length; i++)
            {
                if (!splitterHitRects[i].Contains(e.mousePosition)) continue;
                GUIUtility.hotControl = id;
                splitterDrag = i;
                splitterLastX = e.mousePosition.x;
                e.Use();
                break;
            }

            return;
        }

        if (GUIUtility.hotControl != id || splitterDrag < 0) return;

        switch (e.type)
        {
            case EventType.MouseDrag:
                ApplyWrapDelta(splitterDrag, e.mousePosition.x - splitterLastX);
                splitterLastX = e.mousePosition.x;
                e.Use();
                Repaint();
                break;

            case EventType.MouseUp:
                GUIUtility.hotControl = 0;
                splitterDrag = -1;
                SaveWrapSizes();
                e.Use();
                break;

            case EventType.KeyDown when e.keyCode == KeyCode.Escape:
                GUIUtility.hotControl = 0;
                splitterDrag = -1;
                e.Use();
                break;
        }
    }

    private void ApplyWrapDelta(int index, float deltaX)
    {
        switch (index)
        {
            case PaletteColumn:
                paletteWidth = ClampWrap(paletteWidth + deltaX, MinPaletteWidth, MaxWrapWidth);
                break;
            case OrderColumn:
                orderWidth = ClampWrap(orderWidth + deltaX, MinOrderWidth, MaxWrapWidth);
                break;
            case SideColumn:
                // Thanh nằm bên trái panel phải: kéo sang phải là panel hẹp lại.
                sideWidth = ClampWrap(sideWidth - deltaX, MinSideWidth(), MaxWrapWidth);
                break;
        }
    }

    /// <summary>Chừa chỗ vạch dọc giữa hai cột; hit rect lưu lúc Repaint cho HandleWrapWidthInput frame sau.</summary>
    private void DrawWidthSplitter(DesignConfig design, int index, float height)
    {
        Rect rect = GUILayoutUtility.GetRect(SplitterWidth, height,
            GUILayout.Width(SplitterWidth), GUILayout.Height(height), GUILayout.ExpandWidth(false));
        if (Event.current.type != EventType.Repaint) return;

        splitterHitRects[index] = new Rect(
            rect.x - SplitterHitPadLeft, rect.y,
            rect.width + SplitterHitPadLeft + SplitterHitPadRight, rect.height);

        // Vạch cũng đổi màu theo chuột nên phải nằm trong danh sách hover, không thì rê vào mà không vẽ lại.
        TrackHover(splitterHitRects[index]);
        bool active = splitterDrag == index || splitterHitRects[index].Contains(Event.current.mousePosition);
        SirenixEditorGUI.DrawSolidRect(new Rect(rect.center.x - 2f, rect.y + 2f, 4f, rect.height - 4f),
            active ? design.HoverColor : SirenixGUIStyles.BorderColor, false);
    }
}
