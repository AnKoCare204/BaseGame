using UnityEditor;
using UnityEngine;

/// <summary>
/// Cửa sổ chính của tool vẽ level (Tools/FishSort/Level Design), để khỏi phải chọn asset LevelDesignData rồi vẽ trong
/// Inspector mỗi lần dùng.
///
/// Cũng như <see cref="LevelRandomWindow"/>, cửa sổ không chép logic: nó dựng một LevelDesignDataEditor cho asset rồi
/// gọi DrawToolGUI, nên mọi thao tác vẫn đi qua Mutate / Undo / SetDirty như cũ. AttachToWindow đổi đích Repaint của
/// instance đó về cửa sổ này - Editor.Repaint mặc định chỉ vẽ lại Inspector.
/// </summary>
public class LevelDesignWindow : EditorWindow
{
    private const string DataPath = "Assets/Resources/GlobalConfig/LevelDesignData.asset";

    /// <summary>Đường dẫn Resources cho lượt tìm dự phòng, khi asset bị dời khỏi DataPath.</summary>
    private const string ResourcePath = "GlobalConfig/LevelDesignData";

    private LevelDesignData data;
    private LevelDesignDataEditor editor;

    [MenuItem("Tools/FishSort/Level Design")]
    public static void Open()
    {
        LevelDesignWindow window = GetWindow<LevelDesignWindow>("Level Design");
        window.minSize = new Vector2(900f, 480f);
        window.Show();
    }

    /// <summary>Asset LevelDesignData của project; null khi không tìm thấy ở cả hai chỗ.</summary>
    public static LevelDesignData FindData()
    {
        LevelDesignData found = AssetDatabase.LoadAssetAtPath<LevelDesignData>(DataPath);
        return found != null ? found : Resources.Load<LevelDesignData>(ResourcePath);
    }

    private void OnEnable()
    {
        // Hover ô cá dựa vào MouseMove; EditorWindow mặc định không nhận event này.
        wantsMouseMove = true;
        Undo.undoRedoPerformed += Repaint;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= Repaint;

        // Editor.CreateEditor ra một ScriptableObject thật, không tự dọn theo cửa sổ. Huỷ ở lượt sau chứ không huỷ
        // ngay: bấm X thì OnDisable chạy giữa lúc Unity đang tháo cửa sổ (RequestCloseSentByNativeCode), huỷ editor
        // lúc đó là Editor.OnDisableINTERNAL ném NullReferenceException.
        LevelDesignDataEditor closing = editor;
        editor = null;
        if (closing != null) EditorApplication.delayCall += () => { if (closing != null) DestroyImmediate(closing); };
    }

    private void OnGUI()
    {
        Bind();
        if (data == null || editor == null)
        {
            EditorGUILayout.HelpBox($"Không thấy LevelDesignData ở {DataPath}", MessageType.Error);
            return;
        }

        editor.DrawToolGUI();
    }

    /// <summary>Nạp asset và instance editor. Chạy mỗi OnGUI vì domain reload xoá sạch cả hai.</summary>
    private void Bind()
    {
        if (data == null) data = FindData();
        if (data == null) return;
        if (editor != null && editor.target == data) return;

        if (editor != null) DestroyImmediate(editor);
        editor = (LevelDesignDataEditor)Editor.CreateEditor(data, typeof(LevelDesignDataEditor));
        editor.AttachToWindow(Repaint);
    }
}
