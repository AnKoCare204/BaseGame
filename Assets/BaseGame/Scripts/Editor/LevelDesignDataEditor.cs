using System;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tool vẽ level, mở ở cửa sổ <see cref="LevelDesignWindow"/> (Tools/FishSort/Level Design); Inspector của asset chỉ còn
/// nút mở cửa sổ.
/// Bố cục theo bản design: palette cá + cột OrderQueue bên trái, lưới khung bóng ở giữa (trái qua phải,
/// mỗi hàng tối đa DesignConfig.BubblesPerRow khung), panel Level / Biến thể / Số lượng cá / Swap / Check Level / Save, Obstacle và
/// Function random level bên phải.
/// Click một bóng để chọn (viền vàng); click một ô cá để chọn con đó. Checkbox vật cản và field HP / chìa hiện ở panel
/// Obstacle theo bóng đang chọn; Fish id / Ẩn của ô cá đang chọn hiện ngay dưới đó.
///
/// Thao tác: kéo cạnh phải wrap List item cá / OrderQueue để đổi bề rộng khung (ô cá luôn 3×70, không đổi số/hàng);
/// kéo ô cá thả lên ô cá khác để đổi chỗ data; kéo từ palette / OrderQueue thả vào ô cá chỉ ghi đè ô đích; kéo khung
/// bóng thả vào giữa khung khác để đổi chỗ, thả vào mép khung (có vạch vàng hiện ra) để chèn
/// vào khe đó, các bóng sau dồn xuống một số; click bóng rồi bấm phím tắt khai báo trong DesignConfig để bật/tắt vật cản;
/// phím DeleteHotkey (DesignConfig) xoá ô cá đang chọn, hoặc xoá cả bóng nếu chưa chọn ô cá;
/// phím InsertBubbleHotkey chèn bóng trống vào vị trí bóng đang chọn; Esc bỏ chọn.
///
/// Mọi thay đổi data đều dồn vào biến pending rồi chạy sau khi vẽ xong, để không sửa mảng giữa lúc GUILayout đang chạy.
///
/// Lớp này dài nên chia thành các file partial, mỗi file một mảng việc:
/// - .Layout.cs: bề rộng cột, thanh kéo giữa các cột, tiêu đề panel.
/// - .Slots.cs: vẽ ô bo góc / icon cá / ô trống, theo dõi ô đang hover và cache chuỗi nhãn.
/// - .Columns.cs: hai cột trái (palette cá, OrderQueue).
/// - .BubbleGrid.cs: lưới khung bóng ở giữa.
/// - .SidePanel.cs: panel phải (level, biến thể, obstacle, ô cá đang chọn).
/// - .Check.cs: nút Check Level, log đỏ và tô bóng/cá sai.
/// - .RandomPanel.cs: ba nút sinh seed (order / bubble / shuffle).
/// - .RandomObstacles.cs / .RandomHidden.cs: rắc vật cản và cá ẩn lên bóng đang có.
/// - .Input.cs: vùng chọn, kéo thả, phím tắt.
/// - .Edits.cs: mọi thao tác sửa data + đọc / ghi file.
/// Icon cá nằm hẳn ở lớp riêng <see cref="FishIconLibrary"/>.
/// </summary>
[CustomEditor(typeof(LevelDesignData))]
public partial class LevelDesignDataEditor : OdinEditor
{
    /// <summary>Chiều cao hai dòng tiêu đề đứng yên phía trên mỗi cột, phần còn lại của cột dành cho vùng cuộn.</summary>
    private const float TitleBlockHeight = 42f;

    private const float MinViewHeight = 260f;

    /// <summary>
    /// Screen.height là chiều cao cả cửa sổ Inspector, nhưng Inspector của một asset còn vẽ chân trang riêng
    /// (Asset Labels + AssetBundle) đè lên đáy. Không chừa bấy nhiêu px thì panel chạy xuống dưới chân trang và
    /// thanh cuộn ngang - nằm đúng ở cạnh dưới vùng cuộn - bị che, nhìn như lưới không cuộn ngang được.
    /// Chân trang thu gọn lại thì số này dư chút, chỉ thừa vài px trống ở đáy chứ không hỏng gì.
    /// </summary>
    private const float InspectorFooterReserve = 104f;

    /// <summary>GUIContent dùng chung cho nhãn có tooltip: IMGUI đọc xong ngay trong lời gọi nên xài lại thoải mái.</summary>
    private static readonly GUIContent TempContent = new GUIContent();

    private FishIconLibrary icons;
    private int[] fishIds;

    /// <summary>Thao tác sửa data chờ chạy sau khi vẽ xong; xem phần mô tả ở đầu lớp.</summary>
    private Action pending;

    private string status = "";
    private MessageType statusType = MessageType.Info;
    private int findLevel;

    /// <summary>Số x gõ ở ô "Biến thể": LevelData_{n}_{x}.json. Khởi tạo theo biến thể đang mở trên asset.</summary>
    private int findVariant;
    /// <summary>
    /// Trạng thái đóng / mở của khối data thô, nhớ riêng cho Inspector và cho cửa sổ tool.
    /// Inspector mặc định MỞ: từ khi tool chuyển sang cửa sổ riêng, Inspector là chỗ sửa data bằng tay (Odin tự vẽ).
    /// Cửa sổ mặc định ĐÓNG: mở ra thì cả cây data chiếm hết phần trên, đẩy lưới bóng xuống mất hút.
    /// </summary>
    private const string ShowRawKey = "FishSort.LevelDesign.ShowRaw";

    // Nạp trong OnEnable chứ không gán ngay tại đây: Unity cấm gọi EditorPrefs trong field initializer của
    // ScriptableObject, nó ném exception giữa constructor và mọi field khai báo sau đó ở lại null.
    private bool showRawInspector = true;
    private bool showRawWindow;

    /// <summary>Đã dò style vật cản còn thiếu trong DesignConfig chưa; chỉ làm một lần mỗi lần mở Inspector.</summary>
    private bool stylesChecked;

    /// <summary>Toạ độ y nơi phần lưới bắt đầu, đo ở lần Repaint gần nhất; dùng để tính chiều cao còn lại của cửa sổ.</summary>
    private float contentTop;

    /// <summary>Cửa sổ đáy không có chân trang Asset Labels như Inspector, chỉ chừa vài px cho thanh cuộn ngang.</summary>
    private const float WindowFooterReserve = 8f;

    /// <summary>
    /// Repaint của cửa sổ <see cref="LevelDesignWindow"/> đang giữ instance này; null khi instance là của Inspector.
    /// Editor.Repaint chỉ vẽ lại các Inspector, nên instance nằm trong cửa sổ phải gọi Repaint của chính cửa sổ.
    /// </summary>
    private Action hostRepaint;

    private float footerReserve = InspectorFooterReserve;

    /// <summary>Gắn instance vào cửa sổ: từ đây mọi Repaint() trong tool đi về cửa sổ, và bỏ phần chừa chân trang.</summary>
    public void AttachToWindow(Action repaint)
    {
        hostRepaint = repaint;
        footerReserve = WindowFooterReserve;
    }

    /// <summary>Che Editor.Repaint để mọi lời gọi Repaint() trong các file partial tự đi đúng chỗ đang vẽ tool.</summary>
    private new void Repaint()
    {
        if (hostRepaint != null) hostRepaint();
        else base.Repaint();
    }

    /// <summary>
    /// Inspector giờ chỉ là lối tắt: tool đầy đủ nằm ở cửa sổ Tools/FishSort/Level Design. Vẽ cả tool trong Inspector
    /// nữa thì hai instance cùng giữ trạng thái chọn / trạng thái Check riêng, sửa bên này bên kia không biết.
    /// </summary>
    public override void OnInspectorGUI()
    {
        EditorGUILayout.Space(4f);
        if (GUILayout.Button("Mở Level Design Tool", GUILayout.Height(32f))) LevelDesignWindow.Open();
        DrawRawSection(true);
    }

    /// <summary>Vẽ toàn bộ tool. Chỉ LevelDesignWindow gọi hàm này.</summary>
    public void DrawToolGUI()
    {
        if (target is not LevelDesignData data) return;
        data.levelData ??= new LevelData();

        DesignConfig design = DesignConfig.Instance;
        if (design == null)
        {
            EditorGUILayout.HelpBox("Chưa có DesignConfig trong Assets/Resources/GlobalConfig", MessageType.Error);
            return;
        }

        // Thêm cờ mới vào SoftBubbleData thì asset DesignConfig cũ vẫn thiếu style của nó, chip sẽ không hiện ra.
        // Bù một lần cho mỗi lần mở Inspector để không phải bấm tay nút "Bù vật cản còn thiếu".
        if (!stylesChecked)
        {
            stylesChecked = true;
            if (design.ObstacleStyles == null || design.ObstacleStyles.Count < DesignConfig.AllObstacles.Length)
            {
                design.FillMissingStyles();
            }
        }

        EnsureLayoutState();
        fishIds ??= ToolFishCatalog.GetIds();   // 26 loài thường, ID đúng như game gán (xem ToolFishCatalog)
        if (findLevel <= 0) findLevel = Mathf.Max(1, data.levelData.Level);
        if (findVariant <= 0) findVariant = Mathf.Max(1, data.openVariant);
        if (paletteWidth <= 0f) LoadWrapSizes();
        ClampLoadedWrapSizes();
        int bubbleCount = data.levelData.SoftBubbleDataArray?.Length ?? 0;
        if (selectedBubbleIndex >= bubbleCount) selectedBubbleIndex = bubbleCount > 0 ? bubbleCount - 1 : -1;
        ClampSelectedFish(data);
        fishClicked = false;

        Event e = Event.current;
        // Bắt chuột trước khi scroll view / ô cá ăn event, dùng hit rect của frame Repaint trước.
        HandleWrapWidthInput();
        HandlePointerRepaint(e);

        DrawRawSection(false);
        if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, statusType);

        float minWidth = paletteWidth + orderWidth + sideWidth + BubbleBoxWidth(design) + 3f * SplitterWidth + 10f;
        if (EditorGUIUtility.currentViewWidth < minWidth)
        {
            EditorGUILayout.HelpBox(
                $"Cửa sổ đang rộng {(int)EditorGUIUtility.currentViewWidth}px, cần ít nhất {(int)minWidth}px mới đủ chỗ " +
                "cho 4 cột (palette / OrderQueue / bóng / panel phải). Kéo cửa sổ rộng ra, hoặc kéo hẹp các cột lại.",
                MessageType.Warning);
        }

        // Inspector nằm trong scroll view nên ExpandHeight không ăn; phải tự đo chỗ còn lại tới đáy cửa sổ.
        // Screen.height trong editor GUI = chiều cao view đang vẽ, anchor.y chỉ đúng ở Repaint nên giữ lại dùng cho pass sau.
        Rect anchor = GUILayoutUtility.GetRect(0f, 0f);
        if (e.type == EventType.Repaint && anchor.y > 0f) contentTop = anchor.y;
        float viewHeight = Mathf.Max(MinViewHeight, Screen.height - contentTop - footerReserve);

        EditorGUILayout.BeginHorizontal();
        {
            DrawFishPalette(design, paletteWidth, viewHeight);
            DrawWidthSplitter(design, PaletteColumn, viewHeight);

            DrawOrderQueue(data, design, orderWidth, viewHeight);
            DrawWidthSplitter(design, OrderColumn, viewHeight);

            DrawBubbleGrid(data, design, viewHeight);
            DrawWidthSplitter(design, SideColumn, viewHeight);

            DrawSidePanel(data, design, sideWidth, viewHeight);
        }
        EditorGUILayout.EndHorizontal();

        HandleBubbleHotkeys(data, design);

        if (pending != null)
        {
            Action action = pending;
            pending = null;
            action();
            // Cập nhật snapshot inspector sau Load/Save/sửa bóng, kẻo ApplyModifiedProperties
            // ghi đè levelData bằng YAML cũ (isIce trở về false).
            serializedObject.Update();
            Repaint();
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        showRawInspector = EditorPrefs.GetBool(ShowRawKey + ".Inspector", true);
        showRawWindow = EditorPrefs.GetBool(ShowRawKey + ".Window", false);
        icons = new FishIconLibrary(Repaint);
        RefreshHighlightFishId();
    }

    protected override void OnDisable()
    {
        icons?.Dispose();
        icons = null;
        base.OnDisable();
    }

    /// <summary>
    /// Quyết định khi nào phải vẽ lại vì chuột. Rê chuột trong lòng một ô thì hình không đổi tí nào, mà mỗi lần vẽ
    /// lại là chạy hết Layout + Repaint trên vài trăm ô - vẽ lại vô tội vạ chính là chỗ làm tool ì tay nhất.
    /// Kéo thả thì vẫn phải vẽ liên tục để ô dưới con trỏ sáng lên kịp.
    /// </summary>
    private void HandlePointerRepaint(Event e)
    {
        switch (e.type)
        {
            case EventType.Repaint:
                BeginHoverPass();
                break;

            case EventType.DragUpdated:
                Repaint();
                break;

            case EventType.MouseMove when HoverTargetChanged(e.mousePosition):
                Repaint();
                break;
        }
    }

    /// <param name="inInspector">true = đang vẽ trong Inspector của asset, false = trong cửa sổ tool.</param>
    private void DrawRawSection(bool inInspector)
    {
        bool show = inInspector ? showRawInspector : showRawWindow;

        EditorGUILayout.Space(4f);
        if (GUILayout.Button(show ? "▲ Ẩn data thô" : "▼ Hiện data thô (Odin mặc định)"))
        {
            show = !show;
            if (inInspector) showRawInspector = show;
            else showRawWindow = show;
            EditorPrefs.SetBool(ShowRawKey + (inInspector ? ".Inspector" : ".Window"), show);
            Repaint();
            GUIUtility.ExitGUI();
        }

        if (show) base.OnInspectorGUI();
    }

    private void SetStatus(bool ok, string message, MessageType? overrideType = null)
    {
        status = message;
        statusType = overrideType ?? (ok ? MessageType.Info : MessageType.Error);
    }
}
