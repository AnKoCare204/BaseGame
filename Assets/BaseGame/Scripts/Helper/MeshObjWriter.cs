using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>
/// Ghi một hoặc nhiều Mesh ra chuỗi OBJ (mở được trong Blender). X đảo dấu và đảo chiều mặt để Unity nhập lại ra đúng mesh
/// (Unity tay trái, OBJ tay phải). Mỗi mesh là một object "o", mỗi submesh là một group "g" kèm "usemtl" cùng tên.
/// </summary>
public sealed class MeshObjWriter
{
    private readonly StringBuilder sb = new();
    private int vertexOffset;

    public MeshObjWriter(string comment)
    {
        if (!string.IsNullOrEmpty(comment)) sb.Append("# ").Append(comment).Append('\n');
    }

    /// <summary>Thêm mesh thành một object. groupNames: tên group cho từng submesh, thiếu thì dùng "Sub{i}".</summary>
    public void AddMesh(Mesh mesh, string objectName, string[] groupNames = null)
    {
        CultureInfo ci = CultureInfo.InvariantCulture;
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Vector2[] uvs = mesh.uv;
        bool hasUv = uvs.Length == vertices.Length;
        bool hasNormal = normals.Length == vertices.Length;

        sb.Append("o ").Append(objectName).Append('\n');
        foreach (Vector3 v in vertices)
        {
            sb.Append("v ").Append((-v.x).ToString("R", ci)).Append(' ').Append(v.y.ToString("R", ci)).Append(' ').Append(v.z.ToString("R", ci)).Append('\n');
        }

        if (hasUv)
        {
            foreach (Vector2 uv in uvs)
            {
                sb.Append("vt ").Append(uv.x.ToString("R", ci)).Append(' ').Append(uv.y.ToString("R", ci)).Append('\n');
            }
        }

        if (hasNormal)
        {
            foreach (Vector3 n in normals)
            {
                sb.Append("vn ").Append((-n.x).ToString("R", ci)).Append(' ').Append(n.y.ToString("R", ci)).Append(' ').Append(n.z.ToString("R", ci)).Append('\n');
            }
        }

        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            string name = groupNames != null && s < groupNames.Length ? groupNames[s] : "Sub" + s;
            sb.Append("g ").Append(name).Append('\n');
            sb.Append("usemtl ").Append(name).Append('\n');
            int[] tris = mesh.GetTriangles(s);
            for (int i = 0; i < tris.Length; i += 3)
            {
                // Đảo thứ tự đỉnh vì đã đảo dấu x. Index OBJ bắt đầu từ 1 và tính tiếp sau các object trước.
                int a = tris[i] + 1 + vertexOffset;
                int b = tris[i + 2] + 1 + vertexOffset;
                int c = tris[i + 1] + 1 + vertexOffset;
                sb.Append("f ");
                AppendIndex(a, hasUv, hasNormal).Append(' ');
                AppendIndex(b, hasUv, hasNormal).Append(' ');
                AppendIndex(c, hasUv, hasNormal).Append('\n');
            }
        }

        vertexOffset += vertices.Length;
    }

    private StringBuilder AppendIndex(int index, bool hasUv, bool hasNormal)
    {
        sb.Append(index);
        if (hasUv || hasNormal)
        {
            sb.Append('/');
            if (hasUv) sb.Append(index);
        }

        if (hasNormal) sb.Append('/').Append(index);
        return sb;
    }

    public override string ToString() => sb.ToString();
}
