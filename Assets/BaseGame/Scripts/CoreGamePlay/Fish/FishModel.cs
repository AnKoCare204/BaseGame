using System;
using System.Collections.Generic;
using System.IO;
using Sirenix.OdinInspector;
using TW.Utility.CustomComponent;
using UnityEngine;

/// Cách vỏ Fish xoay model: Heading = đầu (+Z) theo hướng bơi như cá; FaceCameraHeadFirst = luôn quay mặt về camera, khi di chuyển đỉnh đầu (+Y)
/// xoay về phía vận tốc trong mặt phẳng màn hình tối đa FaceCameraLean độ (180 = bạch tuộc, sứa; 0 = sao biển đứng thẳng) — xem HeadFirstOrienter;
/// HeadUpAlongPath = khi bơi đỉnh đầu (+Y) theo tiếp tuyến đường bơi 3D, mặt xoay về camera nhiều nhất có thể, đứng yên thì xoay như vỏ (hải cẩu) —
/// xem HeadUpPathOrienter;
/// Tumble = đồ vật lắc lư đứng thẳng: quanh trục Y đung đưa qua lại, mặt trước (+Z) lệch tối đa TumbleFacing độ khỏi hướng về camera để mặt lưng
/// không quay ra trước (TumbleFacing ≥ 180 = xoay tự do mọi góc), tốc độ tăng giảm mượt ngẫu nhiên, không quay theo hướng đi; quanh trục X, Z
/// của chính nó chỉ nghiêng tối đa TumbleTilt độ để không lộ ánh sáng vẽ sẵn trên model — xem TumbleOrienter.
public enum FishOrientation { Heading, FaceCameraHeadFirst, HeadUpAlongPath, Tumble }

public class FishModel : ABetterMonoBehaviour
{
    public FishProceduralAnimator Animator;
    public FishOrientation Orientation = FishOrientation.Heading;
    public float FaceCameraLean = 180f;   // FaceCameraHeadFirst: độ nghiêng tối đa của đỉnh đầu về phía đang đi (180 = xoay trọn vòng, 0 = luôn đứng thẳng)
    public float TumbleSpeed = 40f;       // Tumble: độ/giây tối đa của mỗi kênh xoay quanh Y (tốc độ thường gặp khoảng 0.4 lần)
    public float TumbleChange = 0.15f;    // Tumble: Hz cơ sở của việc xoay và nghiêng tăng giảm (lớn = đổi nhanh chậm dồn dập hơn)
    public float TumbleTilt = 20f;        // Tumble: độ nghiêng tối đa quanh mỗi trục X, Z của model; 0 = luôn đứng thẳng
    public float TumbleFacing = 75f;      // Tumble: độ lệch tối đa của mặt trước (+Z) khỏi hướng về camera quanh trục Y; ≥ 180 = xoay tự do mọi góc
    public MeshCollider MeshCollider;
    public Vector3 Center;
    public Vector3 Size;
    public Vector3 IconRotation = new Vector3(18f, 134f, 0f);
    /// <summary>
    /// Hộp bao thân cá trong local space của model: bounds của mesh collider (GenerateSimpleColliderWithMesh). Model chưa có
    /// collider thì dùng Center / Size nhập tay.
    /// </summary>
    public Bounds LocalBounds => MeshCollider != null && MeshCollider.sharedMesh != null
        ? MeshCollider.sharedMesh.bounds
        : new Bounds(Center, Size);

    private void Reset()
    {
        Animator = GetComponent<FishProceduralAnimator>();
    }
#if UNITY_EDITOR
    private const string ColliderMeshFolder = "Assets/BaseGame/Graphics/Meshes/FishCollider";
    private const int MinSourceTriangles = 32;   // mesh phụ nhỏ hơn mức này (bubbles_mesh của cá ẩn / spawner) không tính vào collider

    /// <summary>
    /// Dựng mesh collider đơn giản cho model: bake mọi renderer ở tư thế nghỉ về không gian local của FishModel (FBX mỗi con một
    /// scale / offset khác nhau), giảm lưới còn targetTriangles (ColliderMeshSimplifier), lưu thành asset
    /// {ColliderMeshFolder}/{tên prefab}_Collider.asset rồi gán vào MeshCollider (non-convex, không trigger) trên chính object này.
    /// targetTriangles tính cho một mặt; mỗi tam giác còn được thêm một mặt sau dùng chung đỉnh nên asset có tối đa gấp đôi.
    /// Bấm lại thì ghi đè đúng asset cũ (giữ GUID). Bản B là prefab variant nên kế thừa từ prefab gốc, không bấm trên variant.
    /// </summary>
    [Button]
    public void GenerateSimpleColliderWithMesh(int targetTriangles = 200)
    {
        if (Application.isPlaying)
        {
            Debug.LogError("FishModel: chỉ tạo mesh collider ngoài Play mode (cần tư thế nghỉ của model).", this);
            return;
        }

        GameObject source = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
        if (source != null && source.GetComponent<FishModel>() != null)
        {
            Debug.LogError($"FishModel '{name}' là variant / instance của prefab '{source.name}': MeshCollider kế thừa từ prefab gốc, hãy bấm nút trên '{source.name}'.", source);
            return;
        }

        var sourceVertices = new List<Vector3>();
        var sourceTriangles = new List<int>();
        BakeSourceMesh(sourceVertices, sourceTriangles);
        if (sourceTriangles.Count == 0)
        {
            Debug.LogError($"FishModel '{name}': không tìm thấy SkinnedMeshRenderer / MeshFilter nào có mesh để dựng collider.", this);
            return;
        }

        // Hai mặt: vây là tấm một lớp, cá quay cả hai phía mà raycast bỏ qua mặt sau (Physics.queriesHitBackfaces tắt).
        ColliderMeshSimplifier.Report report = ColliderMeshSimplifier.Simplify(sourceVertices, sourceTriangles, targetTriangles, true,
            out Vector3[] vertices, out int[] triangles);

        string path = $"{ColliderMeshFolder}/{ResolvePrefabName()}_Collider.asset";
        Mesh asset = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool isNew = asset == null;
        if (isNew) asset = new Mesh { name = Path.GetFileNameWithoutExtension(path) };
        asset.Clear();
        asset.SetVertices(vertices);
        asset.SetTriangles(triangles, 0);   // chỉ vị trí + tam giác: collider không dùng normal / UV
        asset.RecalculateBounds();
        if (isNew)
        {
            if (!Directory.Exists(ColliderMeshFolder))
            {
                Directory.CreateDirectory(ColliderMeshFolder);
                UnityEditor.AssetDatabase.Refresh();
            }

            UnityEditor.AssetDatabase.CreateAsset(asset, path);
        }
        else
        {
            UnityEditor.EditorUtility.SetDirty(asset);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(asset);
        }

        if (!TryGetComponent(out MeshCollider meshCollider)) meshCollider = UnityEditor.Undo.AddComponent<MeshCollider>(gameObject);
        UnityEditor.Undo.RecordObject(meshCollider, "Generate fish collider mesh");
        meshCollider.isTrigger = false;   // non-convex không làm trigger được; tắt trước khi bỏ convex để Unity không báo lỗi
        meshCollider.convex = false;
        meshCollider.sharedMesh = null;   // asset ghi đè tại chỗ: gán lại để collider cook lại
        meshCollider.sharedMesh = asset;
        UnityEditor.EditorUtility.SetDirty(meshCollider);

        UnityEditor.Undo.RecordObject(this, "Generate fish collider mesh");
        MeshCollider = meshCollider;
        UnityEditor.EditorUtility.SetDirty(this);
        if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)) UnityEditor.PrefabUtility.SavePrefabAsset(transform.root.gameObject);

        Debug.Log($"FishModel '{name}': {sourceTriangles.Count / 3} → {triangles.Length / 3 - report.BackFaces} tam giác + {report.BackFaces} mặt sau "
                  + $"({vertices.Length} đỉnh); lưới nguồn sau khi hàn {report.WeldedTriangles} tam giác / {report.WeldedVertices} đỉnh, "
                  + $"mép hở {report.OpenEdges}, mép gập {report.FoldedEdges}. Đã lưu {path}", asset);
    }

    /// <summary>Gộp mesh của mọi renderer đang bật (skinned: bake ở tư thế hiện tại của xương) về không gian local của object này.</summary>
    private void BakeSourceMesh(List<Vector3> vertices, List<int> triangles)
    {
        Matrix4x4 worldToRoot = transform.worldToLocalMatrix;
        var buffer = new List<Vector3>();
        var indices = new List<int>();
        var baked = new Mesh();

        // includeInactive = true vì object trong prefab asset luôn có activeInHierarchy = false; tự lọc theo activeSelf / enabled.
        foreach (Renderer source in GetComponentsInChildren<Renderer>(true))
        {
            if (!source.enabled || !source.gameObject.activeSelf) continue;
            Mesh mesh = null;
            if (source is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
            {
                skinned.BakeMesh(baked, true);   // useScale: đỉnh ở local của renderer kể cả scale, nhân localToWorld ra world
                mesh = baked;
            }
            else if (source is MeshRenderer && source.TryGetComponent(out MeshFilter filter))
            {
                mesh = filter.sharedMesh;
            }

            if (mesh == null) continue;
            int triangleCount = 0;
            for (int sub = 0; sub < mesh.subMeshCount; sub++) triangleCount += (int)mesh.GetIndexCount(sub) / 3;
            if (triangleCount < MinSourceTriangles) continue;

            Matrix4x4 toRoot = worldToRoot * source.transform.localToWorldMatrix;
            bool mirrored = toRoot.determinant < 0f;   // scale âm đảo chiều mặt
            int offset = vertices.Count;
            mesh.GetVertices(buffer);
            foreach (Vector3 v in buffer) vertices.Add(toRoot.MultiplyPoint3x4(v));
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                if (mesh.GetTopology(sub) != MeshTopology.Triangles) continue;
                mesh.GetTriangles(indices, sub);
                for (int i = 0; i + 2 < indices.Count; i += 3)
                {
                    triangles.Add(offset + indices[i]);
                    triangles.Add(offset + indices[mirrored ? i + 2 : i + 1]);
                    triangles.Add(offset + indices[mirrored ? i + 1 : i + 2]);
                }
            }
        }

        DestroyImmediate(baked);
    }

    /// <summary>Tên file prefab chứa model (đang mở trong Prefab Mode hoặc chọn thẳng asset); không thuộc prefab nào thì lấy tên object.</summary>
    private string ResolvePrefabName()
    {
        UnityEditor.SceneManagement.PrefabStage stage = UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject);
        string path = stage != null ? stage.assetPath : UnityEditor.AssetDatabase.GetAssetPath(gameObject);
        return string.IsNullOrEmpty(path) ? name : Path.GetFileNameWithoutExtension(path);
    }
#endif
}
