using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Hướng đẩy viền (inverted hull) của shader FishSort/Fish Toon Outline cho từng đỉnh mesh cá.
/// Mesh tách đỉnh ở cạnh cứng (Fish24, Fish41, Prop2/3/4/6/7...) và ở seam UV; nếu đẩy theo normal riêng của từng đỉnh thì vỏ viền
/// bị xé ra ở đó thành khe hở. Ở đây mọi đỉnh cùng vị trí nhận chung một hướng = trung bình các normal KHÁC NHAU tại vị trí đó
/// (đỉnh tách vì seam UV mang cùng normal chỉ tính một lần, để không kéo lệch hướng về phía mặt có nhiều seam).
public static class FishSmoothNormals
{
    private const float WeldGrid = 1e4f;           // gộp đỉnh lệch nhau dưới ~0.0001 đơn vị
    private const float SameNormalDot = 0.9999f;   // hai normal coi là một nếu lệch < ~0.8°
    private const float MinSum = 1e-3f;            // tổng gần 0 (tấm 2 mặt trùng nhau): giữ normal riêng

    public static Vector3[] Compute(Vector3[] vertices, Vector3[] normals)
    {
        var keys = new Vector3Int[vertices.Length];
        var distinct = new Dictionary<Vector3Int, List<Vector3>>();
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i];
            var key = new Vector3Int(Mathf.RoundToInt(v.x * WeldGrid), Mathf.RoundToInt(v.y * WeldGrid), Mathf.RoundToInt(v.z * WeldGrid));
            keys[i] = key;
            if (!distinct.TryGetValue(key, out List<Vector3> list))
            {
                list = new List<Vector3>(2);
                distinct.Add(key, list);
            }

            Vector3 n = normals[i].normalized;
            if (n == Vector3.zero) continue;
            bool seen = false;
            foreach (Vector3 other in list)
            {
                if (Vector3.Dot(other, n) > SameNormalDot)
                {
                    seen = true;
                    break;
                }
            }
            if (!seen) list.Add(n);
        }

        var sums = new Dictionary<Vector3Int, Vector3>(distinct.Count);
        foreach (KeyValuePair<Vector3Int, List<Vector3>> pair in distinct)
        {
            Vector3 sum = Vector3.zero;
            foreach (Vector3 n in pair.Value) sum += n;
            sums.Add(pair.Key, sum);
        }

        var result = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 sum = sums[keys[i]];
            result[i] = sum.magnitude > MinSum ? sum.normalized : normals[i].normalized;
        }
        return result;
    }

    /// Ghi hướng viền vào kênh tangent (w = 1). Shader cá không dùng normal map nên tangent rảnh, và SkinnedMeshRenderer skin cả
    /// tangent theo xương nên hướng viền đi theo dáng bơi. Hàm idempotent: chỉ đọc vertices / normals.
    public static void Bake(Mesh mesh)
    {
        if (mesh == null) return;
        Vector3[] directions = Compute(mesh.vertices, mesh.normals);
        var tangents = new Vector4[directions.Length];
        for (int i = 0; i < directions.Length; i++)
        {
            Vector3 d = directions[i];
            tangents[i] = new Vector4(d.x, d.y, d.z, 1f);
        }
        mesh.tangents = tangents;
    }
}

/// Bake hướng viền cho mọi model trong Graphics/Models/Fish Rig lúc import (file FBX không đổi).
/// Đổi GetVersion khi đổi thuật toán để Unity import lại các model.
public sealed class FishSmoothNormalPostprocessor : AssetPostprocessor
{
    public const string FolderPrefix = "Assets/BaseGame/Graphics/Models/Fish Rig/";

    public override uint GetVersion() => 1;

    private void OnPostprocessModel(GameObject root)
    {
        if (!assetPath.StartsWith(FolderPrefix)) return;
        foreach (SkinnedMeshRenderer skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) FishSmoothNormals.Bake(skinned.sharedMesh);
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true)) FishSmoothNormals.Bake(filter.sharedMesh);
    }
}
