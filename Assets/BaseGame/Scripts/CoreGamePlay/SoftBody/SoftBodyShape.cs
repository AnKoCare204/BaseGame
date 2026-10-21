using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Collider giải tích cho soft body. Không dùng PhysX để va chạm, nhưng có thể lấy hình từ
/// BoxCollider, SphereCollider, CapsuleCollider gán sẵn (AttachedCollider) để dựng level bằng collider quen thuộc.
/// Bật Inverted để biến shape thành thùng chứa (giữ bóng bên trong).
/// </summary>
[DisallowMultipleComponent]
public class SoftBodyShape : MonoBehaviour
{
    [SerializeField, Tooltip("Lấy hình từ AttachedCollider (BoxCollider, SphereCollider hoặc CapsuleCollider gán sẵn), bỏ qua Type và Size")]
    private bool UseAttachedCollider = true;

    [SerializeField, Tooltip("Collider gán sẵn để lấy hình khi UseAttachedCollider bật (thường là collider cùng GameObject)")]
    private Collider AttachedCollider;

    [SerializeField] private SoftShapeType Type = SoftShapeType.Box;

    [SerializeField, Tooltip("Đảo trong/ngoài: dùng làm thùng chứa, giữ bóng bên trong")]
    private bool Inverted;

    [SerializeField, Tooltip("Box: kích thước đầy đủ. Sphere: x = bán kính. Capsule: x = bán kính, y = chiều cao tổng. Plane: bỏ qua, pháp tuyến = trục Y")]
    private Vector3 Size = Vector3.one;

    [SerializeField, Range(0f, 1f)] private float Friction = 0.3f;

    [SerializeField, Tooltip("Lấy friction từ PhysicsMaterial của collider (dynamicFriction) nếu có")]
    private bool FrictionFromMaterial = true;

    private void OnEnable()
    {
        SoftBubbleWorld world = SoftBubbleWorld.Get();
        if (world != null) world.Register(this);
    }

    private void OnDisable()
    {
        if (SoftBubbleWorld.Instance != null) SoftBubbleWorld.Instance.Unregister(this);
    }

    public static bool IsSupported(Collider collider)
    {
        return collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider;
    }

    /// <summary>
    /// Đặt hình bằng code (bỏ qua collider gắn kèm). Box: size đầy đủ. Sphere: x = bán kính. Capsule: x = bán kính, y = chiều cao tổng.
    /// World tự phát hiện thay đổi ở step kế tiếp và đánh thức bóng đang ngủ.
    /// </summary>
    public void Configure(SoftShapeType type, Vector3 size, bool inverted, float friction)
    {
        UseAttachedCollider = false;
        FrictionFromMaterial = false;
        Type = type;
        Size = size;
        Inverted = inverted;
        Friction = Mathf.Clamp01(friction);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Công cụ editor (nút trên SoftBubbleWorld): bọc mọi collider hỗ trợ trong scene (không phải trigger, đúng layer, chưa có
    /// SoftBodyShape) thành SoftBodyShape với AttachedCollider gán sẵn. Chỉ chạy lúc dựng scene, không chạy runtime.
    /// Trả về số shape được thêm.
    /// </summary>
    public static int WrapSceneColliders(LayerMask layers, bool includeInactive = false)
    {
        int added = 0;
        Collider[] colliders = FindObjectsByType<Collider>(
            includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        foreach (Collider collider in colliders)
        {
            if (collider.isTrigger || !IsSupported(collider)) continue;
            if ((layers.value & (1 << collider.gameObject.layer)) == 0) continue;
            if (collider.GetComponent<SoftBodyShape>() != null) continue;
            if (collider.GetComponent<SoftBubble>() != null) continue;

            SoftBodyShape shape = UnityEditor.Undo.AddComponent<SoftBodyShape>(collider.gameObject);
            shape.UseAttachedCollider = true;
            shape.AttachedCollider = collider;
            UnityEditor.EditorUtility.SetDirty(shape);
            added++;
        }

        return added;
    }
#endif

    public SoftShapeData ToData()
    {
        if (UseAttachedCollider && AttachedCollider != null && TryFromCollider(AttachedCollider, out SoftShapeData fromCollider))
        {
            return fromCollider;
        }

        Vector3 ls = transform.lossyScale;
        var data = new SoftShapeData
        {
            Type = (int)Type,
            Inverted = Inverted ? 1 : 0,
            Position = transform.position,
            Rotation = transform.rotation,
            Friction = Friction
        };

        switch (Type)
        {
            case SoftShapeType.Box:
                data.Size = new float3(
                    math.abs(Size.x * ls.x),
                    math.abs(Size.y * ls.y),
                    math.abs(Size.z * ls.z)) * 0.5f;
                break;
            case SoftShapeType.Sphere:
            {
                float r = Size.x * math.max(math.abs(ls.x), math.max(math.abs(ls.y), math.abs(ls.z)));
                data.Size = new float3(r, 0f, 0f);
                break;
            }
            case SoftShapeType.Capsule:
            {
                float r = Size.x * math.max(math.abs(ls.x), math.abs(ls.z));
                float half = math.max(0f, Size.y * math.abs(ls.y) * 0.5f - r);
                data.Size = new float3(r, half, 0f);
                break;
            }
            default:
                data.Size = float3.zero;
                break;
        }

        return data;
    }

    private bool TryFromCollider(Collider collider, out SoftShapeData data)
    {
        data = default;
        Transform t = collider.transform;
        Vector3 ls = t.lossyScale;
        float friction = Friction;
        if (FrictionFromMaterial && collider.sharedMaterial != null)
        {
            friction = Mathf.Clamp01(collider.sharedMaterial.dynamicFriction);
        }

        int inverted = Inverted ? 1 : 0;
        switch (collider)
        {
            case BoxCollider box:
                data = new SoftShapeData
                {
                    Type = (int)SoftShapeType.Box,
                    Inverted = inverted,
                    Position = t.TransformPoint(box.center),
                    Rotation = t.rotation,
                    Size = new float3(
                        math.abs(box.size.x * ls.x),
                        math.abs(box.size.y * ls.y),
                        math.abs(box.size.z * ls.z)) * 0.5f,
                    Friction = friction
                };
                return true;

            case SphereCollider sphere:
            {
                float r = sphere.radius * math.max(math.abs(ls.x), math.max(math.abs(ls.y), math.abs(ls.z)));
                data = new SoftShapeData
                {
                    Type = (int)SoftShapeType.Sphere,
                    Inverted = inverted,
                    Position = t.TransformPoint(sphere.center),
                    Rotation = t.rotation,
                    Size = new float3(r, 0f, 0f),
                    Friction = friction
                };
                return true;
            }

            case CapsuleCollider capsule:
            {
                Vector3 axis;
                float radiusScale;
                float heightScale;
                switch (capsule.direction)
                {
                    case 0:
                        axis = Vector3.right;
                        radiusScale = math.max(math.abs(ls.y), math.abs(ls.z));
                        heightScale = math.abs(ls.x);
                        break;
                    case 2:
                        axis = Vector3.forward;
                        radiusScale = math.max(math.abs(ls.x), math.abs(ls.y));
                        heightScale = math.abs(ls.z);
                        break;
                    default:
                        axis = Vector3.up;
                        radiusScale = math.max(math.abs(ls.x), math.abs(ls.z));
                        heightScale = math.abs(ls.y);
                        break;
                }

                float r = capsule.radius * radiusScale;
                float half = Mathf.Max(0f, capsule.height * heightScale * 0.5f - r);
                data = new SoftShapeData
                {
                    Type = (int)SoftShapeType.Capsule,
                    Inverted = inverted,
                    Position = t.TransformPoint(capsule.center),
                    Rotation = t.rotation * Quaternion.FromToRotation(Vector3.up, axis),
                    Size = new float3(r, half, 0f),
                    Friction = friction
                };
                return true;
            }

            default:
                return false;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Inverted ? new Color(0.2f, 0.9f, 1f, 0.8f) : new Color(0.3f, 1f, 0.3f, 0.8f);
        SoftShapeData d = ToData();
        Gizmos.matrix = Matrix4x4.TRS(d.Position, d.Rotation, Vector3.one);
        switch ((SoftShapeType)d.Type)
        {
            case SoftShapeType.Box:
                Gizmos.DrawWireCube(Vector3.zero, (Vector3)(d.Size * 2f));
                break;
            case SoftShapeType.Sphere:
                Gizmos.DrawWireSphere(Vector3.zero, d.Size.x);
                break;
            case SoftShapeType.Capsule:
            {
                Vector3 top = new Vector3(0f, d.Size.y, 0f);
                Gizmos.DrawWireSphere(top, d.Size.x);
                Gizmos.DrawWireSphere(-top, d.Size.x);
                Gizmos.DrawLine(top + Vector3.right * d.Size.x, -top + Vector3.right * d.Size.x);
                Gizmos.DrawLine(top - Vector3.right * d.Size.x, -top - Vector3.right * d.Size.x);
                Gizmos.DrawLine(top + Vector3.forward * d.Size.x, -top + Vector3.forward * d.Size.x);
                Gizmos.DrawLine(top - Vector3.forward * d.Size.x, -top - Vector3.forward * d.Size.x);
                break;
            }
            default:
            {
                const float half = 3f;
                Gizmos.DrawLine(new Vector3(-half, 0f, -half), new Vector3(half, 0f, -half));
                Gizmos.DrawLine(new Vector3(half, 0f, -half), new Vector3(half, 0f, half));
                Gizmos.DrawLine(new Vector3(half, 0f, half), new Vector3(-half, 0f, half));
                Gizmos.DrawLine(new Vector3(-half, 0f, half), new Vector3(-half, 0f, -half));
                Gizmos.DrawLine(Vector3.zero, Vector3.up * (Inverted ? -0.5f : 0.5f));
                break;
            }
        }

        Gizmos.matrix = Matrix4x4.identity;
    }
}
