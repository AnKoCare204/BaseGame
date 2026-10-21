#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Giảm lưới cho mesh collider (chỉ editor): hàn các đỉnh trùng vị trí rồi gộp cạnh theo sai số quadric (Garland-Heckbert) tới khi
/// còn targetTriangles tam giác. Chỉ giữ vị trí + tam giác vì collider không cần UV / normal / skin. Mép hở và mép gập của tấm mỏng
/// (vây, đuôi) được thêm mặt phẳng ràng buộc vuông góc với tấm để silhouette không co lại khi gộp cạnh.
/// </summary>
public static class ColliderMeshSimplifier
{
    private const double WeldRelative = 1e-5;   // ô hàn đỉnh = tỉ lệ này * đường chéo bounds
    private const double BorderWeight = 8.0;    // trọng số mặt phẳng ràng buộc mép (nhân bình phương độ dài cạnh)
    private const double SupportDot = -0.8;     // hai tam giác chung cạnh có dot normal dưới mức này = tấm gập đôi, coi như mép
    private const double FlipDot = 0.2;         // gộp cạnh làm normal tam giác lệch quá mức này thì bỏ
    private const double TieBreak = 1e-4;       // ưu tiên cạnh ngắn khi sai số bằng nhau (vùng phẳng)

    /// <summary>Thống kê lưới nguồn sau khi hàn đỉnh, để log / kiểm tra model.</summary>
    public struct Report
    {
        public int WeldedVertices;
        public int WeldedTriangles;
        public int OpenEdges;       // cạnh chỉ có 1 tam giác (mép hở của tấm một lớp)
        public int FoldedEdges;     // cạnh có 2 tam giác quay lưng vào nhau (mép tấm hai lớp / vây mỏng khép kín)
        public int BackFaces;       // tam giác mặt sau thêm vào khi doubleSided (không tính trong targetTriangles)
    }

    /// <summary>
    /// doubleSided: sau khi giảm lưới, tam giác nào chưa có bản ngược chiều thì thêm một bản (dùng chung đỉnh). Raycast vào
    /// MeshCollider bỏ qua mặt sau khi Physics.queriesHitBackfaces tắt, nên tấm một lớp (vây) nhìn từ phía sau sẽ không bấm trúng.
    /// </summary>
    public static Report Simplify(IReadOnlyList<Vector3> sourceVertices, IReadOnlyList<int> sourceTriangles, int targetTriangles,
        bool doubleSided, out Vector3[] vertices, out int[] triangles)
    {
        var solver = new Solver(sourceVertices, sourceTriangles);
        solver.Run(Mathf.Max(4, targetTriangles));
        solver.Export(doubleSided, out vertices, out triangles);
        return solver.Report;
    }

    /// <summary>Xoay bộ ba cho đỉnh nhỏ nhất lên đầu, giữ chiều: hai tam giác cùng đỉnh cùng chiều cho cùng một khoá.</summary>
    private static (int, int, int) Canonical(int a, int b, int c)
    {
        while (a > b || a > c) (a, b, c) = (b, c, a);
        return (a, b, c);
    }

    private struct Quadric
    {
        private double a2, ab, ac, ad, b2, bc, bd, c2, cd, d2;

        public static Quadric FromPlane(double3 n, double d, double weight)
        {
            return new Quadric
            {
                a2 = n.x * n.x * weight, ab = n.x * n.y * weight, ac = n.x * n.z * weight, ad = n.x * d * weight,
                b2 = n.y * n.y * weight, bc = n.y * n.z * weight, bd = n.y * d * weight,
                c2 = n.z * n.z * weight, cd = n.z * d * weight,
                d2 = d * d * weight
            };
        }

        public void Add(in Quadric o)
        {
            a2 += o.a2; ab += o.ab; ac += o.ac; ad += o.ad;
            b2 += o.b2; bc += o.bc; bd += o.bd;
            c2 += o.c2; cd += o.cd;
            d2 += o.d2;
        }

        public double Evaluate(double3 p)
        {
            return a2 * p.x * p.x + 2.0 * ab * p.x * p.y + 2.0 * ac * p.x * p.z + 2.0 * ad * p.x
                   + b2 * p.y * p.y + 2.0 * bc * p.y * p.z + 2.0 * bd * p.y
                   + c2 * p.z * p.z + 2.0 * cd * p.z
                   + d2;
        }

        /// <summary>Điểm có sai số nhỏ nhất. False khi ma trận gần suy biến (vùng phẳng / nếp thẳng): bên gọi tự chọn điểm.</summary>
        public bool TryMinimum(out double3 p)
        {
            var m = new double3x3(a2, ab, ac, ab, b2, bc, ac, bc, c2);
            double det = math.determinant(m);
            double trace = a2 + b2 + c2;
            if (math.abs(det) <= 1e-6 * trace * trace * trace)
            {
                p = default;
                return false;
            }

            p = math.mul(math.inverse(m), -new double3(ad, bd, cd));
            return true;
        }
    }

    private struct Entry
    {
        public double Cost;
        public int A, B, StampA, StampB;
        public double3 Target;
    }

    private sealed class Solver
    {
        public Report Report;

        private double3[] pos;
        private readonly List<int> tris = new();    // 3 chỉ số / tam giác
        private bool[] triAlive;
        private bool[] vertAlive;
        private int[] stamp;                        // tăng mỗi lần đỉnh đổi; entry trong heap lệch stamp là entry cũ
        private Quadric[] quadrics;
        private List<int>[] vertTris;
        private int liveTriangles;

        private readonly List<Entry> heap = new();
        private readonly HashSet<int> neighborSet = new();
        private readonly List<int> thirds = new();
        private readonly List<int> neighborList = new();

        public Solver(IReadOnlyList<Vector3> sourceVertices, IReadOnlyList<int> sourceTriangles)
        {
            Weld(sourceVertices, sourceTriangles);
            BuildQuadrics();
        }

        // ---------- Dựng dữ liệu ----------

        private void Weld(IReadOnlyList<Vector3> src, IReadOnlyList<int> srcTris)
        {
            var welded = new List<double3>();
            int[] remap = new int[src.Count];
            if (src.Count > 0)
            {
                var bounds = new Bounds(src[0], Vector3.zero);
                for (int i = 1; i < src.Count; i++) bounds.Encapsulate(src[i]);
                double cell = math.max(bounds.size.magnitude * WeldRelative, 1e-9);

                var map = new Dictionary<Vector3Int, int>();
                for (int i = 0; i < src.Count; i++)
                {
                    Vector3 v = src[i];
                    var key = new Vector3Int((int)math.round(v.x / cell), (int)math.round(v.y / cell), (int)math.round(v.z / cell));
                    if (!map.TryGetValue(key, out int index))
                    {
                        index = welded.Count;
                        welded.Add(new double3(v.x, v.y, v.z));
                        map.Add(key, index);
                    }

                    remap[i] = index;
                }
            }

            pos = welded.ToArray();

            // Bỏ tam giác suy biến và tam giác trùng hẳn (cùng 3 đỉnh, cùng chiều). Tam giác ngược chiều (mặt sau của tấm hai lớp) giữ lại.
            var seen = new HashSet<(int, int, int)>();
            for (int t = 0; t + 2 < srcTris.Count; t += 3)
            {
                int a = remap[srcTris[t]], b = remap[srcTris[t + 1]], c = remap[srcTris[t + 2]];
                if (a == b || b == c || a == c) continue;
                if (!seen.Add(Canonical(a, b, c))) continue;
                tris.Add(a);
                tris.Add(b);
                tris.Add(c);
            }

            int vertexCount = pos.Length;
            liveTriangles = tris.Count / 3;
            triAlive = new bool[liveTriangles];
            vertAlive = new bool[vertexCount];
            stamp = new int[vertexCount];
            quadrics = new Quadric[vertexCount];
            vertTris = new List<int>[vertexCount];
            for (int t = 0; t < liveTriangles; t++)
            {
                triAlive[t] = true;
                for (int k = 0; k < 3; k++)
                {
                    int v = tris[t * 3 + k];
                    vertAlive[v] = true;
                    (vertTris[v] ??= new List<int>()).Add(t);
                }
            }

            Report.WeldedVertices = vertexCount;
            Report.WeldedTriangles = liveTriangles;
        }

        private void BuildQuadrics()
        {
            int triangleCount = tris.Count / 3;
            var normals = new double3[triangleCount];
            var valid = new bool[triangleCount];
            var edgeTris = new Dictionary<long, List<int>>();

            for (int t = 0; t < triangleCount; t++)
            {
                int a = tris[t * 3], b = tris[t * 3 + 1], c = tris[t * 3 + 2];
                double3 cross = math.cross(pos[b] - pos[a], pos[c] - pos[a]);
                double length = math.length(cross);
                if (length > 1e-20)
                {
                    normals[t] = cross / length;
                    valid[t] = true;
                    Quadric q = Quadric.FromPlane(normals[t], -math.dot(normals[t], pos[a]), length * 0.5);   // trọng số = diện tích
                    quadrics[a].Add(q);
                    quadrics[b].Add(q);
                    quadrics[c].Add(q);
                }

                AddEdgeTriangle(edgeTris, a, b, t);
                AddEdgeTriangle(edgeTris, b, c, t);
                AddEdgeTriangle(edgeTris, c, a, t);
            }

            // Mép: tam giác không có tam giác nào khác cùng cạnh "đỡ" nó (cạnh hở, hoặc chỉ có mặt sau quay lưng lại) thì thêm mặt phẳng
            // chứa cạnh và vuông góc với tam giác. Dịch đỉnh dọc mép vẫn rẻ, kéo mép vào trong tấm thì đắt.
            foreach (KeyValuePair<long, List<int>> pair in edgeTris)
            {
                int i = (int)(pair.Key >> 32), j = (int)(pair.Key & 0xFFFFFFFF);
                List<int> list = pair.Value;
                if (list.Count == 1) Report.OpenEdges++;
                bool folded = false;
                foreach (int t in list)
                {
                    if (!valid[t]) continue;
                    bool supported = false;
                    foreach (int u in list)
                    {
                        if (u == t || !valid[u] || math.dot(normals[t], normals[u]) <= SupportDot) continue;
                        supported = true;
                        break;
                    }

                    if (supported) continue;
                    if (list.Count > 1) folded = true;
                    double3 edge = pos[j] - pos[i];
                    double3 side = math.cross(edge, normals[t]);
                    double sideLength = math.length(side);
                    if (sideLength <= 1e-20) continue;
                    side /= sideLength;
                    Quadric q = Quadric.FromPlane(side, -math.dot(side, pos[i]), BorderWeight * math.lengthsq(edge));
                    quadrics[i].Add(q);
                    quadrics[j].Add(q);
                }

                if (folded) Report.FoldedEdges++;
            }
        }

        private static void AddEdgeTriangle(Dictionary<long, List<int>> edgeTris, int a, int b, int t)
        {
            long key = EdgeKey(a, b);
            if (!edgeTris.TryGetValue(key, out List<int> list))
            {
                list = new List<int>(2);
                edgeTris.Add(key, list);
            }

            list.Add(t);
        }

        private static long EdgeKey(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        // ---------- Gộp cạnh ----------

        public void Run(int targetTriangles)
        {
            // Entry bị loại vì lật mặt / sai topo không được đẩy lại cho tới khi đầu mút đổi; heap cạn mà chưa đủ thì dựng lại heap
            // từ mọi cạnh còn sống, tới khi một lượt không gộp thêm được cạnh nào.
            while (liveTriangles > targetTriangles)
            {
                BuildHeap();
                int collapsed = 0;
                while (liveTriangles > targetTriangles && heap.Count > 0)
                {
                    Entry e = Pop();
                    if (!vertAlive[e.A] || !vertAlive[e.B] || stamp[e.A] != e.StampA || stamp[e.B] != e.StampB) continue;
                    if (!CanCollapse(e.A, e.B, e.Target)) continue;
                    Collapse(e.A, e.B, e.Target);
                    collapsed++;
                }

                if (collapsed == 0) break;
            }
        }

        private void BuildHeap()
        {
            heap.Clear();
            var pushed = new HashSet<long>();
            for (int t = 0; t < triAlive.Length; t++)
            {
                if (!triAlive[t]) continue;
                for (int k = 0; k < 3; k++)
                {
                    int a = tris[t * 3 + k], b = tris[t * 3 + (k + 1) % 3];
                    if (pushed.Add(EdgeKey(a, b))) Push(a, b);
                }
            }
        }

        private void Push(int a, int b)
        {
            Quadric q = quadrics[a];
            q.Add(quadrics[b]);
            double3 pa = pos[a], pb = pos[b], mid = (pa + pb) * 0.5;
            double lengthSq = math.distancesq(pa, pb);

            double cost;
            // Điểm tối ưu chạy xa khỏi cạnh (ma trận gần suy biến) thì không tin, chọn trong 2 đầu mút + trung điểm.
            if (q.TryMinimum(out double3 target) && math.distancesq(target, mid) <= 2.25 * lengthSq)
            {
                cost = q.Evaluate(target);
            }
            else
            {
                double costA = q.Evaluate(pa), costB = q.Evaluate(pb), costMid = q.Evaluate(mid);
                target = mid;
                cost = costMid;
                if (costA < cost) { target = pa; cost = costA; }
                if (costB < cost) { target = pb; cost = costB; }
            }

            var entry = new Entry
            {
                Cost = math.max(cost, 0.0) + TieBreak * lengthSq * lengthSq,
                A = a, B = b, StampA = stamp[a], StampB = stamp[b], Target = target
            };

            heap.Add(entry);
            int i = heap.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (heap[parent].Cost <= heap[i].Cost) break;
                (heap[parent], heap[i]) = (heap[i], heap[parent]);
                i = parent;
            }
        }

        private Entry Pop()
        {
            Entry top = heap[0];
            int last = heap.Count - 1;
            heap[0] = heap[last];
            heap.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int left = i * 2 + 1, right = left + 1, smallest = i;
                if (left < last && heap[left].Cost < heap[smallest].Cost) smallest = left;
                if (right < last && heap[right].Cost < heap[smallest].Cost) smallest = right;
                if (smallest == i) break;
                (heap[smallest], heap[i]) = (heap[i], heap[smallest]);
                i = smallest;
            }

            return top;
        }

        private bool CanCollapse(int a, int b, double3 target)
        {
            // Điều kiện link: đỉnh kề chung của a và b chỉ được là đỉnh thứ ba của các tam giác chứa cạnh ab; nhiều hơn thì gộp sẽ
            // ép hai mảng lưới dính vào nhau (tam giác trùng, lưới non-manifold).
            neighborSet.Clear();
            thirds.Clear();
            foreach (int t in vertTris[b])
            {
                if (!triAlive[t]) continue;
                bool hasA = Contains(t, a);
                for (int k = 0; k < 3; k++)
                {
                    int v = tris[t * 3 + k];
                    if (v == b) continue;
                    neighborSet.Add(v);
                    if (hasA && v != a && !thirds.Contains(v)) thirds.Add(v);
                }
            }

            foreach (int t in vertTris[a])
            {
                if (!triAlive[t]) continue;
                for (int k = 0; k < 3; k++)
                {
                    int v = tris[t * 3 + k];
                    if (v == a || v == b) continue;
                    if (neighborSet.Contains(v) && !thirds.Contains(v)) return false;
                }
            }

            return !Flips(a, b, target) && !Flips(b, a, target);
        }

        /// <summary>Dời moved tới target có làm tam giác nào quanh nó (trừ các tam giác chứa removed, sẽ bị xoá) lật mặt / suy biến không.</summary>
        private bool Flips(int moved, int removed, double3 target)
        {
            foreach (int t in vertTris[moved])
            {
                if (!triAlive[t] || Contains(t, removed)) continue;
                int a = tris[t * 3], b = tris[t * 3 + 1], c = tris[t * 3 + 2];
                double3 before = math.cross(pos[b] - pos[a], pos[c] - pos[a]);
                double3 pa = a == moved ? target : pos[a], pb = b == moved ? target : pos[b], pc = c == moved ? target : pos[c];
                double3 after = math.cross(pb - pa, pc - pa);
                double beforeLength = math.length(before), afterLength = math.length(after);
                if (beforeLength <= 1e-20) continue;
                if (afterLength <= 1e-20 || math.dot(before, after) < FlipDot * beforeLength * afterLength) return true;
            }

            return false;
        }

        private void Collapse(int a, int b, double3 target)
        {
            pos[a] = target;
            quadrics[a].Add(quadrics[b]);

            List<int> trisA = vertTris[a];
            foreach (int t in vertTris[b])
            {
                if (!triAlive[t]) continue;
                if (Contains(t, a))
                {
                    triAlive[t] = false;
                    liveTriangles--;
                    continue;
                }

                for (int k = 0; k < 3; k++)
                {
                    if (tris[t * 3 + k] == b) tris[t * 3 + k] = a;
                }

                trisA.Add(t);
            }

            trisA.RemoveAll(t => !triAlive[t]);
            vertTris[b].Clear();
            vertAlive[b] = false;
            stamp[a]++;

            neighborList.Clear();
            foreach (int t in trisA)
            {
                for (int k = 0; k < 3; k++)
                {
                    int v = tris[t * 3 + k];
                    if (v != a && !neighborList.Contains(v)) neighborList.Add(v);
                }
            }

            foreach (int v in neighborList) Push(a, v);
        }

        private bool Contains(int t, int v) => tris[t * 3] == v || tris[t * 3 + 1] == v || tris[t * 3 + 2] == v;

        // ---------- Xuất ----------

        public void Export(bool doubleSided, out Vector3[] vertices, out int[] triangles)
        {
            int[] remap = new int[pos.Length];
            for (int i = 0; i < remap.Length; i++) remap[i] = -1;

            var outVertices = new List<Vector3>();
            var outTriangles = new List<int>(liveTriangles * 3);
            for (int t = 0; t < triAlive.Length; t++)
            {
                if (!triAlive[t]) continue;
                for (int k = 0; k < 3; k++)
                {
                    int v = tris[t * 3 + k];
                    if (remap[v] < 0)
                    {
                        remap[v] = outVertices.Count;
                        outVertices.Add(new Vector3((float)pos[v].x, (float)pos[v].y, (float)pos[v].z));
                    }

                    outTriangles.Add(remap[v]);
                }
            }

            if (doubleSided)
            {
                var oriented = new HashSet<(int, int, int)>();
                for (int i = 0; i < outTriangles.Count; i += 3) oriented.Add(Canonical(outTriangles[i], outTriangles[i + 1], outTriangles[i + 2]));
                int frontCount = outTriangles.Count;
                for (int i = 0; i < frontCount; i += 3)
                {
                    int a = outTriangles[i], b = outTriangles[i + 1], c = outTriangles[i + 2];
                    if (oriented.Contains(Canonical(a, c, b))) continue;   // đã có mặt sau (tấm hai lớp của model gốc)
                    outTriangles.Add(a);
                    outTriangles.Add(c);
                    outTriangles.Add(b);
                    Report.BackFaces++;
                }
            }

            vertices = outVertices.ToArray();
            triangles = outTriangles.ToArray();
        }
    }
}
#endif
