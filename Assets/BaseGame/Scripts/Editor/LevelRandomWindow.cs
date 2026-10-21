using UnityEditor;
using UnityEngine;

/// <summary>
/// Cửa sổ nổi cho khối "Function random level". Trước đây khối này nằm cuối panel phải của Inspector, mà Inspector
/// còn phải chứa 4 cột (palette / OrderQueue / lưới bóng / panel phải) nên lúc nào cũng chật.
///
/// Cửa sổ không chép lại logic: nó dựng một LevelDesignDataEditor riêng cho asset LevelDesignData rồi gọi
/// LevelDesignDataEditor.DrawRandomWindowGUI. Nhờ vậy mọi thao tác sửa data vẫn đi qua đúng Mutate / Undo / SetDirty
/// như cũ, và không có bản sao thứ hai của khâu sinh seed để mà lệch nhau về sau.
///
/// Lưu ý instance editor ở đây KHÁC instance của Inspector: ô cá / bóng đang chọn là của riêng mỗi bên. Các ô nhập
/// số thì static (xem LevelDesignDataEditor.RandomPanel) nên hai bên vẫn thấy chung.
/// </summary>
public class LevelRandomWindow : EditorWindow
{
    private LevelDesignData data;
    private Editor editor;
    private Vector2 scroll;

    [MenuItem("Tools/FishSort/Random Level")]
    public static void Open()
    {
        LevelRandomWindow window = GetWindow<LevelRandomWindow>("Random Level");
        window.minSize = new Vector2(580f, 460f);
        window.Show();
    }

    private void OnDisable()
    {
        // Editor.CreateEditor ra một ScriptableObject thật, không tự dọn theo cửa sổ. Huỷ ở lượt sau chứ không huỷ
        // ngay: bấm X thì OnDisable chạy giữa lúc Unity đang tháo cửa sổ (RequestCloseSentByNativeCode), huỷ editor
        // lúc đó là Editor.OnDisableINTERNAL ném NullReferenceException.
        Editor closing = editor;
        editor = null;
        if (closing != null) EditorApplication.delayCall += () => { if (closing != null) DestroyImmediate(closing); };
    }

    private void OnGUI()
    {
        Bind();
        if (data == null)
        {
            EditorGUILayout.HelpBox("Không thấy asset LevelDesignData", MessageType.Error);
            return;
        }

        EditorGUILayout.Space(2f);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Level {data.levelData?.Level ?? 0}", EditorStyles.boldLabel);
        if (GUILayout.Button("Chọn asset", GUILayout.Width(88f))) Selection.activeObject = data;
        EditorGUILayout.EndHorizontal();

        // Số đơn / số bóng đổi sau mỗi lần sinh, mà cửa sổ này không phải chỗ sửa tay nên chỉ hiện để đối chiếu.
        EditorGUILayout.LabelField($"{data.levelData?.SoftBubbleDataArray?.Length ?? 0} bóng · " +
                                   $"{data.TotalFishInBubbles} cá · {data.levelData?.OrderQueue?.Length ?? 0} đơn · " +
                                   $"{ToolLevelStats.SpeciesCount(data)} loài",
            EditorStyles.miniLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        ((LevelDesignDataEditor)editor).DrawRandomWindowGUI();
        EditorGUILayout.EndScrollView();
    }

    /// <summary>Nạp asset và instance editor. Chạy mỗi OnGUI vì domain reload xoá sạch cả hai.</summary>
    private void Bind()
    {
        if (data == null) data = LevelDesignWindow.FindData();
        if (data == null) return;

        if (editor == null || editor.target != data)
        {
            if (editor != null) DestroyImmediate(editor);
            editor = Editor.CreateEditor(data);
        }
    }
}
