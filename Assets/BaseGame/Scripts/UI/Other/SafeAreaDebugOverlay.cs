using UnityEngine;

/// <summary>
/// Vẽ Screen.safeArea (khung xanh) và từng Screen.cutouts (tô đỏ) lên màn hình bằng OnGUI để kiểm tra SafeAreaZone trên Device Simulator
/// hoặc thiết bị thật. Chỉ dùng khi debug: thêm vào bất kỳ object nào trong scene rồi gỡ ra.
/// </summary>
public class SafeAreaDebugOverlay : MonoBehaviour
{
    [field: SerializeField, Tooltip("Hiện dòng chữ tóm tắt safeArea / cutouts ở góc dưới-trái vùng an toàn")]
    private bool ShowLabel { get; set; } = true;

    [field: SerializeField, Tooltip("Vẽ khung các SafeAreaZone đang hoạt động (màu vàng)")]
    private bool ShowZones { get; set; } = true;

    private static Texture2D white;
    private static GUIStyle labelStyle;
    private readonly Vector3[] corners = new Vector3[4];

    private void OnGUI()
    {
        if (white == null)
        {
            white = new Texture2D(1, 1);
            white.SetPixel(0, 0, Color.white);
            white.Apply();
        }
        labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 28, normal = { textColor = Color.white } };

        SafeAreaInfo.Poll();
        float h = SafeAreaInfo.ScreenSize.y;
        Color old = GUI.color;

        foreach (Rect c in SafeAreaInfo.Cutouts)
        {
            GUI.color = new Color(1f, 0f, 0f, 0.5f);
            GUI.DrawTexture(ToGui(c, h), white);
        }

        DrawFrame(ToGui(SafeAreaInfo.SafeArea, h), new Color(0f, 1f, 0f, 0.9f), 4f);

        if (ShowZones)
        {
            foreach (SafeAreaZone zone in FindObjectsByType<SafeAreaZone>(FindObjectsSortMode.None))
            {
                RectTransform rt = (RectTransform)zone.transform;
                rt.GetWorldCorners(corners);
                Canvas canvas = rt.GetComponentInParent<Canvas>();
                Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
                Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
                DrawFrame(ToGui(Rect.MinMaxRect(a.x, a.y, b.x, b.y), h), new Color(1f, 0.9f, 0f, 0.9f), 3f);
            }
        }

        if (ShowLabel)
        {
            GUI.color = Color.white;
            Rect s = SafeAreaInfo.SafeArea;
            string text = $"screen {SafeAreaInfo.ScreenSize.x}x{SafeAreaInfo.ScreenSize.y}  safe x{s.xMin:0}-{s.xMax:0} y{s.yMin:0}-{s.yMax:0}  cutouts {SafeAreaInfo.Cutouts.Length}";
            foreach (Rect c in SafeAreaInfo.Cutouts) text += $"\n  cutout x{c.xMin:0}-{c.xMax:0} y{c.yMin:0}-{c.yMax:0} ({SafeAreaInfo.ClassifyEdge(c)})";
            GUI.Label(new Rect(s.xMin + 8f, h - s.yMin - 120f, s.width - 16f, 120f), text, labelStyle);
        }

        GUI.color = old;
    }

    /// <summary>Đổi rect gốc dưới-trái (Screen) sang gốc trên-trái (GUI).</summary>
    private static Rect ToGui(Rect r, float screenHeight) => new(r.xMin, screenHeight - r.yMax, r.width, r.height);

    private static void DrawFrame(Rect r, Color color, float thickness)
    {
        GUI.color = color;
        GUI.DrawTexture(new Rect(r.xMin, r.yMin, r.width, thickness), white);
        GUI.DrawTexture(new Rect(r.xMin, r.yMax - thickness, r.width, thickness), white);
        GUI.DrawTexture(new Rect(r.xMin, r.yMin, thickness, r.height), white);
        GUI.DrawTexture(new Rect(r.xMax - thickness, r.yMin, thickness, r.height), white);
    }
}
