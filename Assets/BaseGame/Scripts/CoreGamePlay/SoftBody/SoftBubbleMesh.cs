using System;
using System.IO;
using System.Text;
using Sirenix.OdinInspector;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Lưới bong bóng lưu sẵn thành asset (bán kính 1): lưới mô phỏng (particle) và lưới hiển thị mịn hơn, đúng cấu trúc mà
/// SoftBubbleWorld cần (cạnh duy nhất, bảng Parents để nội suy cung tròn), kèm Mesh render là sub-asset cùng file để xem trước
/// trong Editor. Bấm "Dựng lại mesh" để sinh, gán vào SoftBubbleConfig.MeshAsset; SoftBubble chỉ đọc lại lúc Awake, không sinh
/// icosphere ở runtime nữa. "Xuất OBJ" ghi file .obj mở được trong Blender (2 object: lưới hiển thị và lưới mô phỏng).
/// </summary>
[CreateAssetMenu(menuName = "FishSort/Soft Bubble Mesh", fileName = "SoftBubble")]
public class SoftBubbleMesh : ScriptableObject
{
    [Header("Sinh lưới (Editor)")]
    [Tooltip("Lưới mô phỏng (particle): 1 = 42 đỉnh, 2 = 162 đỉnh, 3 = 642 đỉnh. Tốn chi phí vật lý. Đổi xong bấm 'Dựng lại mesh', bóng đang chạy cần spawn lại")]
    [Range(1, 3)] public int Subdivisions = 2;

    [Tooltip("Lưới hiển thị, chia nhỏ từ lưới mô phỏng bằng nội suy cung tròn nên rìa tròn mượt mà không tốn thêm vật lý. " +
             "3 = 642 đỉnh, 4 = 2562 đỉnh. Nhỏ hơn Subdivisions thì dùng bằng Subdivisions")]
    [Range(1, 4)] public int VisualSubdivisions = 4;

    [Tooltip("Đường dẫn .obj khi bấm Xuất OBJ")]
    public string ObjExportPath = "Assets/BaseGame/Graphics/Models/SoftBubble.obj";

    [SerializeField, ReadOnly, Tooltip("Mesh render (sub-asset cùng file) dựng từ lưới hiển thị, chỉ để xem trước trong Editor và xuất OBJ")]
    private Mesh renderMesh;

    [SerializeField, HideInInspector] private Icosphere.Data sim;
    [SerializeField, HideInInspector] private Icosphere.Data visual;

    /// <summary>Mesh render dựng từ lưới hiển thị (bán kính 1), null khi chưa dựng.</summary>
    public Mesh RenderMesh => renderMesh;

    /// <summary>Lưới mô phỏng: mỗi đỉnh là một particle bề mặt.</summary>
    public Icosphere.Data Sim => sim;

    /// <summary>Lưới hiển thị: N đỉnh đầu trùng N particle của Sim, phần sau là trung điểm với Parents index nhỏ hơn.</summary>
    public Icosphere.Data Visual => visual;

    [ShowInInspector, ReadOnly, Tooltip("Số particle bề mặt (đỉnh lưới mô phỏng) đã dựng")]
    public int ParticleCount => sim?.Vertices?.Length ?? 0;

    [ShowInInspector, ReadOnly, Tooltip("Số đỉnh lưới hiển thị đã dựng")]
    public int VisualVertexCount => visual?.Vertices?.Length ?? 0;

    /// <summary>Đã dựng và đủ dữ liệu cho SoftBubbleWorld: lưới hiển thị chứa lưới mô phỏng ở N đỉnh đầu và có bảng Parents.</summary>
    public bool IsValid =>
        sim != null && visual != null
        && sim.Vertices != null && sim.Vertices.Length > 0
        && sim.Triangles != null && sim.Triangles.Length > 0
        && sim.Edges != null && sim.Edges.Length > 0
        && visual.Vertices != null && visual.Vertices.Length >= sim.Vertices.Length
        && visual.Triangles != null && visual.Triangles.Length > 0
        && visual.Parents != null && visual.Parents.Length == visual.Vertices.Length
        && visual.Uvs != null && visual.Uvs.Length == visual.Vertices.Length;

    /// <summary>Thông số trong Inspector khác với lưới đã dựng (chưa bấm Dựng lại mesh).</summary>
    public bool IsStale =>
        ParticleCount != Icosphere.VertexCount(Subdivisions)
        || VisualVertexCount != Icosphere.VertexCount(Mathf.Max(VisualSubdivisions, Subdivisions));

#if UNITY_EDITOR
    [InfoBox("Thông số đã đổi hoặc chưa dựng, bấm 'Dựng lại mesh' để cập nhật asset.", InfoMessageType.Warning, nameof(IsStale))]
    [Button("Dựng lại mesh")]
    public void Rebuild()
    {
        string path = UnityEditor.AssetDatabase.GetAssetPath(this);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("SoftBubbleMesh: phải là asset trong project mới lưu được mesh.", this);
            return;
        }

        int simLevel = Mathf.Clamp(Subdivisions, 1, Icosphere.MaxSubdivisions);
        int visualLevel = Mathf.Clamp(Mathf.Max(VisualSubdivisions, simLevel), 1, Icosphere.MaxSubdivisions);
        sim = Icosphere.Create(simLevel);
        visual = Icosphere.Create(visualLevel);

        if (renderMesh == null)
        {
            renderMesh = new Mesh();
            UnityEditor.AssetDatabase.AddObjectToAsset(renderMesh, this);
        }

        renderMesh.name = name;
        FillMesh(renderMesh, visual);
        UnityEditor.EditorUtility.SetDirty(renderMesh);
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
        Debug.Log($"SoftBubbleMesh: đã dựng lưới mô phỏng {sim.Vertices.Length} đỉnh / {sim.Edges.Length} cạnh, " +
                  $"lưới hiển thị {visual.Vertices.Length} đỉnh / {visual.Triangles.Length} tam giác vào {path}", this);
    }

    [Button("Xuất OBJ")]
    public void ExportObj()
    {
        string path = ObjExportPath;
        if (string.IsNullOrEmpty(path) || !path.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError("SoftBubbleMesh: ObjExportPath phải là đường dẫn .obj", this);
            return;
        }

        if (!IsValid)
        {
            Debug.LogError("SoftBubbleMesh: chưa dựng lưới, bấm 'Dựng lại mesh' trước.", this);
            return;
        }

        string objectName = Path.GetFileNameWithoutExtension(path);
        var visualMesh = new Mesh();
        var simMesh = new Mesh();
        FillMesh(visualMesh, visual);
        FillMesh(simMesh, sim);
        var writer = new MeshObjWriter("FishSort soft bubble (radius 1), generated by SoftBubbleMesh");
        writer.AddMesh(visualMesh, objectName, new[] { "Visual" });
        writer.AddMesh(simMesh, objectName + "_Sim", new[] { "Sim" });
        DestroyImmediate(visualMesh);
        DestroyImmediate(simMesh);

        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            UnityEditor.AssetDatabase.Refresh();
        }

        File.WriteAllText(path, writer.ToString(), new UTF8Encoding(false));
        UnityEditor.AssetDatabase.ImportAsset(path);
        Debug.Log($"SoftBubbleMesh: đã xuất {path}", UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path));
    }

    /// <summary>Đổ đỉnh, uv, tam giác của một lưới (bán kính 1) vào Mesh, tính lại pháp tuyến và bounds.</summary>
    private static void FillMesh(Mesh mesh, Icosphere.Data data)
    {
        int vertexCount = data.Vertices.Length;
        var vertices = new Vector3[vertexCount];
        for (int i = 0; i < vertexCount; i++) vertices[i] = data.Vertices[i];

        var indices = new int[data.Triangles.Length * 3];
        for (int i = 0; i < data.Triangles.Length; i++)
        {
            int3 tri = data.Triangles[i];
            indices[i * 3] = tri.x;
            indices[i * 3 + 1] = tri.y;
            indices[i * 3 + 2] = tri.z;
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, data.Uvs);
        mesh.SetTriangles(indices, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
#endif
}
