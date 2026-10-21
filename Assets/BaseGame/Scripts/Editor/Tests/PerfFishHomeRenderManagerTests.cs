using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// Camera cá Home: group tắt khi chưa StartRender, bật/tắt theo Start/StopRender, RenderTexture được tạo/thả theo; manager rỗng không ném lỗi.
public class PerfFishHomeRenderManagerTests
{
    private GameObject root;
    private RenderTexture rt;

    [SetUp]
    public void SetUp() { root = new GameObject("PerfHome"); rt = new RenderTexture(64, 64, 0); }

    [TearDown]
    public void TearDown() { Object.DestroyImmediate(root); rt.Release(); Object.DestroyImmediate(rt); }

    private FishHomeRenderManager Build(out GameObject group)
    {
        Camera camera = new GameObject("Camera").AddComponent<Camera>();
        camera.transform.SetParent(root.transform, false);
        camera.orthographic = true;
        camera.orthographicSize = 7f;
        group = new GameObject("RendererGroup");
        group.transform.SetParent(root.transform, false);
        FishHomeRenderManager manager = root.AddComponent<FishHomeRenderManager>();
        SerializedObject so = new SerializedObject(manager);
        so.FindProperty("<RendererGroup>k__BackingField").objectReferenceValue = group;
        so.FindProperty("<RenderCamera>k__BackingField").objectReferenceValue = camera;
        so.FindProperty("<RenderTexture>k__BackingField").objectReferenceValue = rt;
        so.FindProperty("<Swimmers>k__BackingField").arraySize = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
        return manager;
    }

    [Test]
    public void StartRender_EnablesGroupAndCreatesTexture()
    {
        FishHomeRenderManager manager = Build(out GameObject group);
        group.SetActive(false);
        manager.StartRender();
        Assert.IsTrue(group.activeSelf);
        Assert.IsTrue(rt.IsCreated());
    }

    [Test]
    public void StopRender_DisablesGroupAndReleasesTexture()
    {
        FishHomeRenderManager manager = Build(out GameObject group);
        manager.StartRender();
        Assert.IsTrue(rt.IsCreated(), "StartRender phải tạo RT trước, nếu không test thả RT vô nghĩa");
        manager.StopRender();
        Assert.IsFalse(group.activeSelf);
        Assert.IsFalse(rt.IsCreated());
    }

    [Test]
    public void StopRender_OnUnassignedManager_DoesNotThrow()
    {
        FishHomeRenderManager manager = root.AddComponent<FishHomeRenderManager>();
        Assert.DoesNotThrow(() => manager.StopRender());
        Assert.DoesNotThrow(() => manager.StartRender());
    }
}
