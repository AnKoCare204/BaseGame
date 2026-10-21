using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Sinh icosphere bán kính 1 với đỉnh được hàn (welded), kèm danh sách cạnh duy nhất.
/// Kết quả thường được bake sẵn vào SoftBubbleMesh (asset), runtime chỉ dùng Get khi thiếu asset.
/// </summary>
public static class Icosphere
{
    /// <summary>Lưới soft body: đỉnh, tam giác, cạnh duy nhất, uv và bảng cha để nội suy. Serialize được để lưu vào asset.</summary>
    [Serializable]
    public sealed class Data
    {
        public float3[] Vertices;
        public int3[] Triangles;
        public int2[] Edges;
        public Vector2[] Uvs;

        /// <summary>
        /// Hai đỉnh cha của mỗi đỉnh (đỉnh là trung điểm cạnh cha khi chia nhỏ), (-1,-1) với 12 đỉnh gốc.
        /// Đỉnh được đánh số theo mức chia: mức thấp hơn luôn đứng trước, nên cha luôn có index nhỏ hơn con.
        /// Nhờ vậy lưới mức cao có thể nội suy từ lưới mức thấp (lưới hiển thị mịn hơn lưới mô phỏng).
        /// </summary>
        public int2[] Parents;

        /// <summary>Độ dài cạnh trung bình của icosphere bán kính 1.</summary>
        public float MeanEdgeLength;
    }

    public const int MaxSubdivisions = 4;

    private static readonly Dictionary<int, Data> Cache = new();

    /// <summary>Số đỉnh của icosphere ở mức chia cho trước: 10 * 4^mức + 2.</summary>
    public static int VertexCount(int subdivisions)
    {
        subdivisions = Mathf.Clamp(subdivisions, 0, MaxSubdivisions);
        return 10 * (1 << (2 * subdivisions)) + 2;
    }

    /// <summary>subdivisions: 0 = 12 đỉnh, 1 = 42, 2 = 162, 3 = 642, 4 = 2562. N đỉnh đầu của mức k+1 trùng đúng N đỉnh của mức k.</summary>
    public static Data Get(int subdivisions)
    {
        subdivisions = Mathf.Clamp(subdivisions, 0, MaxSubdivisions);
        if (Cache.TryGetValue(subdivisions, out Data data)) return data;
        data = Build(subdivisions);
        Cache[subdivisions] = data;
        return data;
    }

    /// <summary>Sinh bản mới không qua cache, dùng khi cần lưu vào asset (SoftBubbleMesh) để không dính tới bản dùng chung.</summary>
    public static Data Create(int subdivisions)
    {
        return Build(Mathf.Clamp(subdivisions, 0, MaxSubdivisions));
    }

    private static Data Build(int subdivisions)
    {
        float t = (1f + math.sqrt(5f)) * 0.5f;
        var verts = new List<float3>
        {
            new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0),
            new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t),
            new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1)
        };
        for (int i = 0; i < verts.Count; i++) verts[i] = math.normalize(verts[i]);
        var parents = new List<int2>(verts.Count);
        for (int i = 0; i < verts.Count; i++) parents.Add(new int2(-1, -1));

        var tris = new List<int3>
        {
            new(0, 11, 5), new(0, 5, 1), new(0, 1, 7), new(0, 7, 10), new(0, 10, 11),
            new(1, 5, 9), new(5, 11, 4), new(11, 10, 2), new(10, 7, 6), new(7, 1, 8),
            new(3, 9, 4), new(3, 4, 2), new(3, 2, 6), new(3, 6, 8), new(3, 8, 9),
            new(4, 9, 5), new(2, 4, 11), new(6, 2, 10), new(8, 6, 7), new(9, 8, 1)
        };

        var midCache = new Dictionary<long, int>();
        for (int s = 0; s < subdivisions; s++)
        {
            var next = new List<int3>(tris.Count * 4);
            foreach (int3 tri in tris)
            {
                int a = Midpoint(verts, parents, midCache, tri.x, tri.y);
                int b = Midpoint(verts, parents, midCache, tri.y, tri.z);
                int c = Midpoint(verts, parents, midCache, tri.z, tri.x);
                next.Add(new int3(tri.x, a, c));
                next.Add(new int3(tri.y, b, a));
                next.Add(new int3(tri.z, c, b));
                next.Add(new int3(a, b, c));
            }

            tris = next;
        }

        var edgeSet = new HashSet<long>();
        var edges = new List<int2>(tris.Count * 3 / 2);
        foreach (int3 tri in tris)
        {
            AddEdge(edgeSet, edges, tri.x, tri.y);
            AddEdge(edgeSet, edges, tri.y, tri.z);
            AddEdge(edgeSet, edges, tri.z, tri.x);
        }

        var uvs = new Vector2[verts.Count];
        for (int i = 0; i < verts.Count; i++)
        {
            float3 v = verts[i];
            float u = 0.5f + math.atan2(v.z, v.x) / (2f * math.PI);
            float w = 0.5f - math.asin(math.clamp(v.y, -1f, 1f)) / math.PI;
            uvs[i] = new Vector2(u, w);
        }

        float edgeSum = 0f;
        foreach (int2 e in edges) edgeSum += math.distance(verts[e.x], verts[e.y]);

        return new Data
        {
            Vertices = verts.ToArray(),
            Triangles = tris.ToArray(),
            Edges = edges.ToArray(),
            Uvs = uvs,
            Parents = parents.ToArray(),
            MeanEdgeLength = edges.Count > 0 ? edgeSum / edges.Count : 1f
        };
    }

    private static int Midpoint(List<float3> verts, List<int2> parents, Dictionary<long, int> cache, int a, int b)
    {
        long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        if (cache.TryGetValue(key, out int idx)) return idx;
        idx = verts.Count;
        verts.Add(math.normalize((verts[a] + verts[b]) * 0.5f));
        parents.Add(new int2(a, b));
        cache[key] = idx;
        return idx;
    }

    private static void AddEdge(HashSet<long> set, List<int2> edges, int a, int b)
    {
        int lo = math.min(a, b);
        int hi = math.max(a, b);
        long key = ((long)lo << 32) | (uint)hi;
        if (set.Add(key)) edges.Add(new int2(lo, hi));
    }
}
