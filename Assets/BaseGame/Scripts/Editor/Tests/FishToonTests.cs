using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// Test shader cá FishSort/Fish Toon Outline (branch fish-toon-outline):
/// - FishSmoothNormals: hướng đẩy viền = trung bình các normal khác nhau tại cùng vị trí, để viền không hở ở cạnh cứng / đường seam UV;
/// - mesh trong Graphics/Models/Fish Rig sau import mang hướng đó trong kênh tangent (SkinnedMeshRenderer skin cả tangent);
/// - material cá / prop dùng shader mới, và shader được preload trong FishSortShaderVariants.
/// Chạy qua menu Tools/FishSort/Run Fish Toon Tests hoặc cửa sổ Test Runner (EditMode).
public class FishToonTests
{
    private const string ShaderName = "FishSort/Fish Toon Outline";
    private const string FishRigFolder = "Assets/BaseGame/Graphics/Models/Fish Rig";
    private const string FishMaterialFolder = "Assets/BaseGame/Graphics/Models/Fish Material";
    private const string VariantsPath = "Assets/BaseGame/Graphics/Shaders/FishSortShaderVariants.shadervariants";
    private const float Eps = 1e-4f;

    private static readonly Vector3 Diagonal = new Vector3(1f, 1f, 1f).normalized;

    private static void AssertDirection(Vector3 expected, Vector3 actual, string label)
    {
        Assert.Less(Vector3.Distance(expected, actual), Eps, $"{label}: mong đợi {expected:F4}, nhận {actual:F4}");
    }

    [Test]
    public void SmoothNormals_CubeCorner_PointsDiagonally()
    {
        Vector3[] vertices = { Vector3.one, Vector3.one, Vector3.one };
        Vector3[] normals = { Vector3.right, Vector3.up, Vector3.forward };

        Vector3[] result = FishSmoothNormals.Compute(vertices, normals);

        for (int i = 0; i < 3; i++) AssertDirection(Diagonal, result[i], "đỉnh " + i);
    }

    [Test]
    public void SmoothNormals_UvSeamDuplicates_DoNotBiasTheAverage()
    {
        // Đỉnh tách vì seam UV mang cùng normal: chỉ tính một lần, nếu không hướng lệch về phía +X.
        Vector3[] vertices = { Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero };
        Vector3[] normals = { Vector3.right, Vector3.right, Vector3.up, Vector3.forward };

        Vector3[] result = FishSmoothNormals.Compute(vertices, normals);

        for (int i = 0; i < 4; i++) AssertDirection(Diagonal, result[i], "đỉnh " + i);
    }

    [Test]
    public void SmoothNormals_SmoothVertex_KeepsItsOwnNormal()
    {
        Vector3 n = new Vector3(0.3f, -0.5f, 0.8f).normalized;
        Vector3[] vertices = { Vector3.zero, Vector3.zero, new Vector3(5f, 0f, 0f) };
        Vector3[] normals = { n, n, Vector3.down };

        Vector3[] result = FishSmoothNormals.Compute(vertices, normals);

        AssertDirection(n, result[0], "đỉnh 0");
        AssertDirection(n, result[1], "đỉnh 1");
        AssertDirection(Vector3.down, result[2], "đỉnh lẻ");
    }

    [Test]
    public void SmoothNormals_OppositeNormals_FallBackToOwnNormal()
    {
        // Hai mặt trùng vị trí, ngược hướng (tấm 2 mặt): tổng = 0, giữ normal riêng thay vì ra vector 0.
        Vector3[] vertices = { Vector3.zero, Vector3.zero };
        Vector3[] normals = { Vector3.forward, Vector3.back };

        Vector3[] result = FishSmoothNormals.Compute(vertices, normals);

        AssertDirection(Vector3.forward, result[0], "mặt trước");
        AssertDirection(Vector3.back, result[1], "mặt sau");
    }

    [Test]
    public void SmoothNormals_NearlyCoincidentPositions_AreWelded()
    {
        Vector3[] vertices = { new Vector3(1f, 2f, 3f), new Vector3(1f + 2e-6f, 2f, 3f - 2e-6f) };
        Vector3[] normals = { Vector3.right, Vector3.up };

        Vector3[] result = FishSmoothNormals.Compute(vertices, normals);

        Vector3 expected = new Vector3(1f, 1f, 0f).normalized;
        AssertDirection(expected, result[0], "đỉnh 0");
        AssertDirection(expected, result[1], "đỉnh 1");
    }

    [Test]
    public void FishRigMeshes_TangentsHoldSmoothedNormals()
    {
        var failures = new List<string>();
        int meshCount = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { FishRigFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is not Mesh mesh) continue;
                meshCount++;
                Vector3[] expected = FishSmoothNormals.Compute(mesh.vertices, mesh.normals);
                Vector4[] tangents = mesh.tangents;
                if (tangents.Length != expected.Length)
                {
                    failures.Add($"{path}/{mesh.name}: {tangents.Length} tangent / {expected.Length} đỉnh");
                    continue;
                }

                int bad = 0;
                for (int i = 0; i < expected.Length; i++)
                {
                    if (Vector3.Distance(expected[i], tangents[i]) > 1e-3f) bad++;
                }
                if (bad > 0) failures.Add($"{path}/{mesh.name}: {bad}/{expected.Length} đỉnh sai hướng viền");
            }
        }

        Assert.Greater(meshCount, 30, "Không tìm thấy mesh cá trong " + FishRigFolder);
        Assert.IsEmpty(failures, string.Join("\n", failures));
    }

    [Test]
    public void FishMaterialFolder_AllMaterialsUseToonShader()
    {
        var wrong = new List<string>();
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { FishMaterialFolder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material.shader.name != ShaderName) wrong.Add(path + " -> " + material.shader.name);
        }

        Assert.AreEqual(64, guids.Length, "Số material trong " + FishMaterialFolder);   // 37 + 27 cá / prop mới 2026-10-02
        Assert.IsEmpty(wrong, string.Join("\n", wrong));
    }

    [Test]
    public void FishAndPropPrefabs_RenderWithToonShader()
    {
        var wrong = new List<string>();
        int rendererCount = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/BaseGame/Prefabs/Fish", "Assets/BaseGame/Prefabs/Prop" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                rendererCount++;
                foreach (Material material in renderer.sharedMaterials)
                {
                    string shader = material == null ? "null" : material.shader.name;
                    if (shader != ShaderName) wrong.Add($"{path}/{renderer.name} -> {shader}");
                }
            }
        }

        Assert.Greater(rendererCount, 36, "Số renderer cá / prop");
        Assert.IsEmpty(wrong, string.Join("\n", wrong));
    }

    [Test]
    public void ShaderVariants_PreloadBothToonPasses()
    {
        Shader shader = Shader.Find(ShaderName);
        Assert.IsNotNull(shader, "Chưa có shader " + ShaderName);
        var collection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(VariantsPath);
        Assert.IsNotNull(collection, VariantsPath);

        // Pass Body tag SRPDefaultUnlit, pass Outline tag Universal2D (Renderer2D vẽ SRPDefaultUnlit trước).
        Assert.IsTrue(collection.Contains(new ShaderVariantCollection.ShaderVariant(shader, PassType.ScriptableRenderPipelineDefaultUnlit)), "Thiếu pass SRPDefaultUnlit");
        Assert.IsTrue(collection.Contains(new ShaderVariantCollection.ShaderVariant(shader, PassType.ScriptableRenderPipeline)), "Thiếu pass Universal2D");
    }
}
