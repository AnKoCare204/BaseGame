using UnityEngine;

/// <summary>
/// Vùng chữ nhật world (mặt phẳng XY, tâm tại transform) dùng để căn camera. Level.LevelRectQueue là phần phía trên của gameplay:
/// GamePlayRectManager.FitCameraToQueue dời camera để vùng này nằm lọt trong GamePlayRectQueue (UI), căn giữa cả hai trục.
/// </summary>
public class LevelRect : MonoBehaviour
{
    [field: SerializeField, Tooltip("Kích thước vùng (m), tâm tại transform. Nhân với lossyScale khi tính WorldRect")]
    public Vector2 Size { get; private set; } = new(13f, 5.5f);

    /// <summary>Vùng trong world (XY), tâm tại transform.position, kích thước Size nhân lossyScale.</summary>
    public Rect WorldRect
    {
        get
        {
            Vector3 scale = transform.lossyScale;
            Vector2 size = new(Mathf.Abs(Size.x * scale.x), Mathf.Abs(Size.y * scale.y));
            Vector3 center = transform.position;
            return new Rect(center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y);
        }
    }

    private void OnDrawGizmos()
    {
        Rect r = WorldRect;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(new Vector3(r.center.x, r.center.y, transform.position.z), new Vector3(r.width, r.height, 0f));
    }
}
