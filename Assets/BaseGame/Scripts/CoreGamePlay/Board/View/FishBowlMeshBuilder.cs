using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Dựng mesh bể cá kiểu bình tròn (thân phình, đáy hơi dẹt, cổ thu vào, gờ miệng bo tròn, có thành trong) bằng cách xoay
/// một profile 2D (r, y) quanh trục Y. Sửa thông số trong Inspector là mesh dựng lại ngay (LiveRebuild).
/// Mesh có 3 submesh để gán 3 vật liệu: 0 = lòng trong, 1 = thân ngoài, 2 = gờ miệng (lòng trong vẽ trước, thân đè lên,
/// gờ đè lên cùng nên đúng cả khi không có depth buffer). Gốc mesh ở tâm đáy, trục Y hướng lên.
/// Nút "Lưu mesh thành asset" ghi mesh ra .asset để prefab tham chiếu, "Xuất OBJ" ghi file .obj mở được trong Blender.
/// </summary>
public class FishBowlMeshBuilder : MonoBehaviour
{
    public const int SubMeshInner = 0;
    public const int SubMeshBody = 1;
    public const int SubMeshRim = 2;

    [field: SerializeField, Tooltip("MeshFilter nhận mesh (gán sẵn, cùng GameObject)")]
    public MeshFilter MeshFilter { get; private set; }

    [field: SerializeField, Tooltip("Dựng lại mesh ngay mỗi khi sửa thông số trong Inspector")]
    public bool LiveRebuild { get; private set; } = true;

    [field: SerializeField, Header("Thân bể"), Min(0.01f), Tooltip("Bán kính chỗ phình to nhất")]
    public float Radius { get; private set; } = 1f;

    [field: SerializeField, Min(0.01f), Tooltip("Độ cao chỗ phình to nhất tính từ đáy")]
    public float BellyHeight { get; private set; } = 0.86f;

    [field: SerializeField, Range(1.2f, 6f), Tooltip("Số mũ siêu elip phần dưới: 2 = tròn như cầu, càng lớn đáy càng phẳng")]
    public float BottomFlatness { get; private set; } = 2.3f;

    [field: SerializeField, Min(0.01f), Tooltip("Độ cao vai: từ chỗ phình to nhất lên tới cổ (chân gờ miệng)")]
    public float ShoulderHeight { get; private set; } = 0.72f;

    [field: SerializeField, Min(0.01f), Tooltip("Bán kính cổ (chân gờ miệng), phải nhỏ hơn Radius")]
    public float NeckRadius { get; private set; } = 0.53f;

    [field: SerializeField, Header("Gờ miệng"), Min(0.01f), Tooltip("Bán kính ngoài của gờ miệng (thường lớn hơn cổ một chút)")]
    public float RimOuterRadius { get; private set; } = 0.58f;

    [field: SerializeField, Min(0.01f), Tooltip("Chiều cao gờ miệng")]
    public float RimHeight { get; private set; } = 0.16f;

    [field: SerializeField, Min(0.001f), Tooltip("Bán kính bo tròn các cạnh của gờ miệng")]
    public float RimFillet { get; private set; } = 0.05f;

    [field: SerializeField, Min(0.005f), Tooltip("Độ dày thành bể (lòng trong = thân ngoài thu vào bấy nhiêu; bán kính trong của miệng = NeckRadius - độ dày)")]
    public float WallThickness { get; private set; } = 0.08f;

    [field: SerializeField, Header("Độ mịn"), Range(8, 128), Tooltip("Số cạnh quanh trục")]
    public int RadialSegments { get; private set; } = 48;

    [field: SerializeField, Range(4, 64), Tooltip("Số đoạn cho cung đáy (từ tâm đáy lên chỗ phình)")]
    public int BodySamples { get; private set; } = 14;

    [field: SerializeField, Range(3, 64), Tooltip("Số đoạn cho cung vai (từ chỗ phình lên cổ)")]
    public int ShoulderSamples { get; private set; } = 10;

    [field: SerializeField, Range(1, 16), Tooltip("Số đoạn cho mỗi góc bo của gờ miệng")]
    public int FilletSamples { get; private set; } = 4;

    [field: SerializeField, Header("Lưu / xuất (Editor)"), Tooltip("Đường dẫn .asset khi bấm Lưu mesh")]
    public string MeshAssetPath { get; private set; } = "Assets/BaseGame/Graphics/Meshes/FishBowl.asset";

    [field: SerializeField, Tooltip("Đường dẫn .obj khi bấm Xuất OBJ")]
    public string ObjExportPath { get; private set; } = "Assets/BaseGame/Graphics/Models/FishBowl.obj";

    /// <summary>Chiều cao toàn bộ bể (đáy → mép trên gờ miệng).</summary>
    public float TotalHeight => BellyHeight + ShoulderHeight + RimHeight;

    [Button("Dựng lại mesh")]
    public void Rebuild()
    {
        if (MeshFilter == null)
        {
            Debug.LogWarning("FishBowlMeshBuilder: chưa gán MeshFilter", this);
            return;
        }

        Mesh mesh = MeshFilter.sharedMesh;
        if (mesh == null)
        {
            mesh = new Mesh { name = "FishBowl" };
            MeshFilter.sharedMesh = mesh;
        }

        FillMesh(mesh);
    }

    /// <summary>Tạo mesh mới từ thông số hiện tại (người gọi tự quản lý vòng đời mesh).</summary>
    public Mesh BuildMesh()
    {
        Mesh mesh = new Mesh { name = "FishBowl" };
        FillMesh(mesh);
        return mesh;
    }

    /// <summary>Ghi đè dữ liệu mesh (đỉnh, pháp tuyến, uv, 3 submesh) từ thông số hiện tại.</summary>
    public void FillMesh(Mesh mesh)
    {
        List<Vector2> points = new();
        List<bool> hard = new();
        List<int> segmentGroup = new();
        BuildProfile(points, hard, segmentGroup);

        int ringCount = points.Count;
        int columns = RadialSegments + 1; // cột cuối trùng vị trí cột đầu để khép uv
        List<Vector3> vertices = new(ringCount * 2 * columns);
        List<Vector3> normals = new(ringCount * 2 * columns);
        List<Vector2> uvs = new(ringCount * 2 * columns);

        // Tọa độ v = độ dài profile tích lũy.
        float[] arcLength = new float[ringCount];
        for (int i = 1; i < ringCount; i++)
        {
            arcLength[i] = arcLength[i - 1] + Vector2.Distance(points[i - 1], points[i]);
        }

        float totalLength = Mathf.Max(arcLength[ringCount - 1], 1e-5f);

        // Mỗi điểm profile sinh một vòng đỉnh; điểm "cứng" sinh hai vòng (pháp tuyến hai phía khác nhau).
        // ringStart[i] = chỉ số vòng dùng cho đoạn (i-1 → i), ringEnd[i] = vòng dùng cho đoạn (i → i+1).
        int[] ringBefore = new int[ringCount];
        int[] ringAfter = new int[ringCount];
        int ringIndex = 0;
        for (int i = 0; i < ringCount; i++)
        {
            Vector2 tangentPrev = i > 0 ? (points[i] - points[i - 1]).normalized : Vector2.zero;
            Vector2 tangentNext = i < ringCount - 1 ? (points[i + 1] - points[i]).normalized : Vector2.zero;
            bool split = hard[i] && i > 0 && i < ringCount - 1;
            if (split)
            {
                ringBefore[i] = ringIndex++;
                AppendRing(vertices, normals, uvs, points[i], ProfileNormal(tangentPrev), arcLength[i] / totalLength, columns);
                ringAfter[i] = ringIndex++;
                AppendRing(vertices, normals, uvs, points[i], ProfileNormal(tangentNext), arcLength[i] / totalLength, columns);
            }
            else
            {
                Vector2 tangent = tangentPrev + tangentNext;
                if (tangent.sqrMagnitude < 1e-10f)
                {
                    tangent = tangentPrev.sqrMagnitude > 0 ? tangentPrev : tangentNext;
                }

                ringBefore[i] = ringAfter[i] = ringIndex++;
                AppendRing(vertices, normals, uvs, points[i], ProfileNormal(tangent.normalized), arcLength[i] / totalLength, columns);
            }
        }

        List<int>[] triangles = { new(), new(), new() };
        for (int i = 0; i < ringCount - 1; i++)
        {
            int ringA = ringAfter[i] * columns;
            int ringB = ringBefore[i + 1] * columns;
            bool poleA = points[i].x < 1e-6f;
            bool poleB = points[i + 1].x < 1e-6f;
            List<int> target = triangles[Mathf.Clamp(segmentGroup[i], 0, 2)];
            for (int j = 0; j < RadialSegments; j++)
            {
                int a0 = ringA + j, a1 = ringA + j + 1;
                int b0 = ringB + j, b1 = ringB + j + 1;
                if (!poleA)
                {
                    AddTriangle(target, vertices, normals, a0, a1, b0);
                }

                if (!poleB)
                {
                    AddTriangle(target, vertices, normals, a1, b1, b0);
                }
            }
        }

        mesh.Clear();
        mesh.indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 3;
        mesh.SetTriangles(triangles[SubMeshInner], SubMeshInner);
        mesh.SetTriangles(triangles[SubMeshBody], SubMeshBody);
        mesh.SetTriangles(triangles[SubMeshRim], SubMeshRim);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
    }

    /// <summary>
    /// Profile (r, y) đi từ tâm đáy ngoài → lên thân ngoài → cổ → gờ miệng (mặt dưới, thành ngoài, mặt trên, bo tròn)
    /// → thành trong đi xuống → đáy trong → tâm đáy trong. hard[i] = tách pháp tuyến tại điểm i (cạnh sắc).
    /// segmentGroup[i] = submesh của đoạn (i → i+1).
    /// </summary>
    private void BuildProfile(List<Vector2> points, List<bool> hard, List<int> segmentGroup)
    {
        float radius = Radius;
        float thickness = Mathf.Min(WallThickness, radius * 0.45f);
        float neckRadius = Mathf.Clamp(NeckRadius, thickness + 0.01f, radius * 0.98f);
        float rimInner = neckRadius - thickness;
        float rimOuter = Mathf.Max(RimOuterRadius, neckRadius);
        float rimHeight = Mathf.Max(RimHeight, 0.01f);
        float fillet = Mathf.Min(RimFillet, Mathf.Min(rimHeight * 0.5f, (rimOuter - rimInner) * 0.5f));
        float belly = BellyHeight;
        float neckY = belly + ShoulderHeight;
        float topY = neckY + rimHeight;

        int group = SubMeshBody;

        void Add(Vector2 p, bool isHard = false)
        {
            if (points.Count > 0)
            {
                if (Vector2.Distance(points[^1], p) < 1e-5f)
                {
                    hard[^1] |= isHard;
                    return;
                }

                segmentGroup.Add(group);
            }

            points.Add(p);
            hard.Add(isHard);
        }

        void Arc(Vector2 center, float r, float fromDeg, float toDeg)
        {
            if (r < 1e-4f)
            {
                return;
            }

            for (int i = 1; i <= FilletSamples; i++)
            {
                float angle = Mathf.Lerp(fromDeg, toDeg, (float)i / FilletSamples) * Mathf.Deg2Rad;
                Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r);
            }
        }

        // 1. Thân ngoài: cung đáy (siêu elip) từ tâm đáy lên chỗ phình, rồi cung vai (elip) lên cổ.
        group = SubMeshBody;
        foreach (Vector2 p in LowerBody(radius, 0f, belly, false))
        {
            Add(p);
        }

        foreach (Vector2 p in Shoulder(radius, belly, neckRadius, neckY, false))
        {
            Add(p);
        }

        hard[^1] = true; // cổ gặp mặt dưới gờ: cạnh sắc

        // 2. Gờ miệng: mặt dưới → bo góc → thành ngoài → bo góc → mặt trên → bo góc vào trong.
        group = SubMeshRim;
        Add(new Vector2(rimOuter - fillet, neckY), fillet < 1e-4f);
        Arc(new Vector2(rimOuter - fillet, neckY + fillet), fillet, -90f, 0f);
        Add(new Vector2(rimOuter, topY - fillet), fillet < 1e-4f);
        Arc(new Vector2(rimOuter - fillet, topY - fillet), fillet, 0f, 90f);
        Add(new Vector2(rimInner + fillet, topY), fillet < 1e-4f);
        Arc(new Vector2(rimInner + fillet, topY - fillet), fillet, 90f, 180f);

        // 3. Lòng trong: thành trong của miệng đi xuống cổ, rồi vai trong và đáy trong (thu vào một độ dày thành).
        group = SubMeshInner;
        Add(new Vector2(rimInner, neckY), true);
        foreach (Vector2 p in Shoulder(radius - thickness, belly, rimInner, neckY, true))
        {
            Add(p);
        }

        foreach (Vector2 p in LowerBody(radius - thickness, thickness, belly, true))
        {
            Add(p);
        }
    }

    /// <summary>Cung đáy siêu elip từ (0, floorY) tới (radius, bellyY); reverse = đi ngược từ chỗ phình xuống tâm đáy.</summary>
    private IEnumerable<Vector2> LowerBody(float radius, float floorY, float bellyY, bool reverse)
    {
        float exponent = 2f / Mathf.Max(BottomFlatness, 1.01f);
        float height = Mathf.Max(bellyY - floorY, 0.01f);
        int samples = BodySamples;
        for (int k = 0; k <= samples; k++)
        {
            int i = reverse ? samples - k : k;
            float angle = Mathf.Lerp(-90f, 0f, (float)i / samples) * Mathf.Deg2Rad;
            float r = radius * Mathf.Pow(Mathf.Abs(Mathf.Cos(angle)), exponent);
            float y = floorY + height * (1f - Mathf.Pow(Mathf.Abs(Mathf.Sin(angle)), exponent));
            if (r < 1e-6f)
            {
                r = 0f; // điểm cực: cos(±90°) trong float không ra đúng 0
            }

            yield return new Vector2(r, y);
        }
    }

    /// <summary>Cung vai elip từ (radius, bellyY) thu dần vào (neckRadius, neckY); reverse = đi từ cổ xuống chỗ phình.</summary>
    private IEnumerable<Vector2> Shoulder(float radius, float bellyY, float neckRadius, float neckY, bool reverse)
    {
        float ratio = Mathf.Clamp(neckRadius / Mathf.Max(radius, 1e-4f), 0f, 0.999f);
        float endAngle = Mathf.Acos(ratio);
        float semiAxisY = (neckY - bellyY) / Mathf.Max(Mathf.Sin(endAngle), 1e-4f);
        int samples = ShoulderSamples;
        for (int k = 0; k <= samples; k++)
        {
            int i = reverse ? samples - k : k;
            float angle = endAngle * i / samples;
            yield return new Vector2(radius * Mathf.Cos(angle), bellyY + semiAxisY * Mathf.Sin(angle));
        }
    }

    /// <summary>Pháp tuyến 2D hướng ra ngoài của profile khi đi theo tiếp tuyến t (đi lên thân ngoài → pháp tuyến hướng +r).</summary>
    private static Vector2 ProfileNormal(Vector2 tangent)
    {
        Vector2 n = new(tangent.y, -tangent.x);
        return n.sqrMagnitude > 1e-12f ? n.normalized : Vector2.up;
    }

    private void AppendRing(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, Vector2 point, Vector2 normal2D, float v, int columns)
    {
        for (int j = 0; j < columns; j++)
        {
            float u = (float)j / (columns - 1);
            float angle = u * Mathf.PI * 2f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            vertices.Add(new Vector3(point.x * cos, point.y, point.x * sin));
            normals.Add(new Vector3(normal2D.x * cos, normal2D.y, normal2D.x * sin).normalized);
            uvs.Add(new Vector2(u, v));
        }
    }

    /// <summary>Thêm tam giác, đảo chiều nếu mặt trước không cùng hướng với pháp tuyến đỉnh.</summary>
    private static void AddTriangle(List<int> target, List<Vector3> vertices, List<Vector3> normals, int i0, int i1, int i2)
    {
        Vector3 faceNormal = Vector3.Cross(vertices[i1] - vertices[i0], vertices[i2] - vertices[i0]);
        if (faceNormal.sqrMagnitude < 1e-14f)
        {
            return;
        }

        Vector3 vertexNormal = normals[i0] + normals[i1] + normals[i2];
        if (Vector3.Dot(faceNormal, vertexNormal) < 0f)
        {
            (i1, i2) = (i2, i1);
        }

        target.Add(i0);
        target.Add(i1);
        target.Add(i2);
    }

#if UNITY_EDITOR
    private void Reset()
    {
        MeshFilter = GetComponent<MeshFilter>();
    }

    private void OnValidate()
    {
        if (!LiveRebuild || MeshFilter == null)
        {
            return;
        }

        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && MeshFilter != null)
            {
                Rebuild();
            }
        };
    }

    [Button("Lưu mesh thành asset")]
    public void SaveMeshAsset()
    {
        string path = MeshAssetPath;
        if (string.IsNullOrEmpty(path) || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError("FishBowlMeshBuilder: MeshAssetPath phải là đường dẫn .asset trong Assets/", this);
            return;
        }

        EnsureFolder(path);
        Mesh asset = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (asset == null)
        {
            asset = new Mesh { name = Path.GetFileNameWithoutExtension(path) };
            FillMesh(asset);
            UnityEditor.AssetDatabase.CreateAsset(asset, path);
        }
        else
        {
            FillMesh(asset);
            UnityEditor.EditorUtility.SetDirty(asset);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(asset);
        }

        if (MeshFilter != null)
        {
            UnityEditor.Undo.RecordObject(MeshFilter, "Assign FishBowl mesh");
            MeshFilter.sharedMesh = asset;
            UnityEditor.EditorUtility.SetDirty(MeshFilter);
        }

        Debug.Log($"FishBowlMeshBuilder: đã lưu mesh {asset.vertexCount} đỉnh / {asset.triangles.Length / 3} tam giác vào {path}", asset);
    }

    [Button("Xuất OBJ")]
    public void ExportObj()
    {
        string path = ObjExportPath;
        if (string.IsNullOrEmpty(path) || !path.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError("FishBowlMeshBuilder: ObjExportPath phải là đường dẫn .obj", this);
            return;
        }

        EnsureFolder(path);
        Mesh mesh = BuildMesh();
        // X đảo dấu + đảo chiều mặt để Unity nhập lại ra đúng mesh; 3 group Inner/Body/Rim theo submesh.
        var writer = new MeshObjWriter("FishSort fish bowl, generated by FishBowlMeshBuilder");
        writer.AddMesh(mesh, Path.GetFileNameWithoutExtension(path), new[] { "Inner", "Body", "Rim" });
        DestroyImmediate(mesh);
        File.WriteAllText(path, writer.ToString(), new UTF8Encoding(false));
        UnityEditor.AssetDatabase.ImportAsset(path);
        Debug.Log($"FishBowlMeshBuilder: đã xuất {path}", UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path));
    }

    private static void EnsureFolder(string assetPath)
    {
        string dir = Path.GetDirectoryName(assetPath);
        if (string.IsNullOrEmpty(dir) || Directory.Exists(dir))
        {
            return;
        }

        Directory.CreateDirectory(dir);
        UnityEditor.AssetDatabase.Refresh();
    }
#endif
}
