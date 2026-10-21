using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chỉ để test. Kéo một GameObject có component này vào scene thì mọi BezierPath được Add sẽ vẽ Gizmos trong Scene view:
/// đường cong, điểm đi qua và mũi tên hướng tại từng điểm. Không có trong scene thì Add/Remove không làm gì, code gọi không cần kiểm tra.
/// Gọi BezierPathGizmos.Add(path) khi tạo đường, BezierPathGizmos.Remove(path) khi dùng xong. Chạy cả ở edit mode nên
/// tool editor cũng Add được.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class BezierPathGizmos : MonoBehaviour
{
    public static BezierPathGizmos Instance { get; private set; }

    /// <summary>Số đường đang được vẽ, 0 khi không có instance.</summary>
    public static int DrawCount => Instance != null ? Instance.entries.Count : 0;

    [SerializeField, Min(2), Tooltip("Số đoạn thẳng vẽ cho mỗi đoạn Bezier")]
    private int SamplesPerSegment = 24;

    [SerializeField, Min(0f), Tooltip("Bán kính chấm tại điểm đi qua, 0 = không vẽ")]
    private float PointRadius = 0.05f;

    [SerializeField, Min(0f), Tooltip("Độ dài mũi tên hướng tại điểm, 0 = không vẽ")]
    private float ArrowLength = 0.4f;

    [SerializeField, Tooltip("Vẽ mũi tên tiếp tuyến dọc đường (mỗi đoạn một mũi tên ở giữa) để xem hướng bơi")]
    private bool ShowMidTangents = true;

    [SerializeField, Tooltip("Màu lần lượt cho các đường được Add không kèm màu")]
    private Color[] Palette =
    {
        new(0.2f, 0.9f, 1f), new(1f, 0.6f, 0.2f), new(0.6f, 1f, 0.3f), new(1f, 0.4f, 0.8f), new(1f, 0.95f, 0.3f)
    };

    private struct Entry
    {
        public BezierPath Path;
        public Color Color;
    }

    private readonly List<Entry> entries = new();
    private int colorIndex;

    /// <summary>Đăng ký vẽ một đường. Gọi lại với cùng path chỉ đổi màu. Không có instance trong scene thì bỏ qua.</summary>
    public static void Add(BezierPath path, Color? color = null)
    {
        if (Instance == null || path == null) return;
        Instance.AddInternal(path, color);
    }

    /// <summary>Thôi vẽ một đường. Không có instance hoặc chưa Add thì bỏ qua.</summary>
    public static void Remove(BezierPath path)
    {
        if (Instance == null || path == null) return;
        Instance.RemoveInternal(path);
    }

    /// <summary>Thôi vẽ tất cả.</summary>
    public static void Clear()
    {
        if (Instance == null) return;
        Instance.entries.Clear();
    }

    private void OnEnable()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("BezierPathGizmos: đã có instance khác trong scene, instance này sẽ được dùng thay.", this);
        }

        Instance = this;
    }

    private void OnDisable()
    {
        entries.Clear();
        if (Instance != this) return;

        // Còn instance khác đang bật (ví dụ vừa tạo tạm một cái rồi hủy) thì trả quyền vẽ về cho nó.
        Instance = null;
        foreach (BezierPathGizmos other in FindObjectsByType<BezierPathGizmos>(FindObjectsSortMode.None))
        {
            if (other == this || !other.isActiveAndEnabled) continue;
            Instance = other;
            break;
        }
    }

    private void AddInternal(BezierPath path, Color? color)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (!ReferenceEquals(entries[i].Path, path)) continue;
            if (color.HasValue) entries[i] = new Entry { Path = path, Color = color.Value };
            return;
        }

        Color c = color ?? (Palette != null && Palette.Length > 0 ? Palette[colorIndex++ % Palette.Length] : Color.cyan);
        entries.Add(new Entry { Path = path, Color = c });
    }

    private void RemoveInternal(BezierPath path)
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(entries[i].Path, path)) entries.RemoveAt(i);
        }
    }

    private void OnDrawGizmos()
    {
        foreach (Entry entry in entries)
        {
            DrawPath(entry.Path, entry.Color);
        }
    }

    private void DrawPath(BezierPath path, Color color)
    {
        if (path == null || path.Count == 0) return;

        Gizmos.color = color;
        if (path.Count >= 2)
        {
            int samples = Mathf.Max(2, path.SegmentCount * SamplesPerSegment);
            Vector3 prev = path.GetPoint(0f);
            for (int k = 1; k <= samples; k++)
            {
                Vector3 cur = path.GetPoint((float)k / samples);
                Gizmos.DrawLine(prev, cur);
                prev = cur;
            }

            if (ShowMidTangents && ArrowLength > 0f)
            {
                Gizmos.color = Color.Lerp(color, Color.white, 0.6f);
                for (int i = 0; i < path.SegmentCount; i++)
                {
                    float t = (i + 0.5f) / path.SegmentCount;
                    DrawArrow(path.GetPoint(t), path.GetTangent(t) * (ArrowLength * 0.6f));
                }
            }
        }

        IReadOnlyList<Vector3> points = path.Points;
        IReadOnlyList<Vector3> forwards = path.Forwards;
        for (int i = 0; i < points.Count; i++)
        {
            Gizmos.color = color;
            if (PointRadius > 0f) Gizmos.DrawSphere(points[i], PointRadius);
            if (ArrowLength > 0f)
            {
                Gizmos.color = Color.Lerp(color, Color.white, 0.3f);
                DrawArrow(points[i], forwards[i] * ArrowLength);
            }
        }
    }

    private static void DrawArrow(Vector3 origin, Vector3 vector)
    {
        if (vector.sqrMagnitude < 1e-10f) return;
        Vector3 tip = origin + vector;
        Gizmos.DrawLine(origin, tip);

        Vector3 dir = vector.normalized;
        Vector3 side = Vector3.Cross(dir, Vector3.up);
        if (side.sqrMagnitude < 1e-6f) side = Vector3.Cross(dir, Vector3.right);
        side.Normalize();
        float head = vector.magnitude * 0.25f;
        Gizmos.DrawLine(tip, tip - dir * head + side * (head * 0.5f));
        Gizmos.DrawLine(tip, tip - dir * head - side * (head * 0.5f));
    }
}
