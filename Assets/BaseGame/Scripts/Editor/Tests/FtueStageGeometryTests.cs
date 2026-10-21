using Core.Tutorial;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// Vị trí đặt khung chữ dưới bể: prefab bể không gán TankView.Body nên phải lấy bounds các renderer đang bật (bỏ renderer tắt và hạt).
/// Object test tạo HideAndDontSave để không làm bẩn scene đang mở.
public class FtueStageGeometryTests
{
    private static GameObject Make(string name, Transform parent, Vector3 position, params System.Type[] components)
    {
        GameObject go = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave, components);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = position;
        return go;
    }

    private static GameObject Cube(Transform parent, Vector3 position)
    {
        GameObject go = Make("cube", parent, position, typeof(MeshFilter), typeof(MeshRenderer));
        go.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        return go;
    }

    [Test]
    public void RendererBounds_EncapsulatesEnabledRenderers_IgnoresDisabledAndParticles()
    {
        GameObject root = Make("root", null, Vector3.zero);
        try
        {
            Cube(root.transform, new Vector3(0f, 0f, 0f));
            Cube(root.transform, new Vector3(0f, -3f, 0f));
            Cube(root.transform, new Vector3(0f, -10f, 0f)).GetComponent<MeshRenderer>().enabled = false;
            Make("ps", root.transform, new Vector3(0f, -20f, 0f), typeof(ParticleSystem));

            Assert.IsTrue(TutorialStage.TryGetRendererBounds(root.transform, out Bounds bounds));
            Assert.AreEqual(-3.5f, bounds.min.y, 1e-3f);
            Assert.AreEqual(0.5f, bounds.max.y, 1e-3f);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void RendererBounds_NoRenderer_False()
    {
        GameObject root = Make("root", null, Vector3.zero);
        try
        {
            Assert.IsFalse(TutorialStage.TryGetRendererBounds(root.transform, out _));
            Assert.IsFalse(TutorialStage.TryGetRendererBounds(null, out _));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
