using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// Test EditMode cho giới hạn bề ngang vùng bóng (GamePlayRectManager.MaxWorldWidth): dựng camera ortho nhìn theo +Z và một canvas
/// world space chứa rect tường 20 x 10 (tâm x = 4) nằm ngay trên mặt phẳng bóng z = 0, nên chiếu xuống được đúng x -6..14, y -5..5.
/// Field gán qua SerializedObject (Awake của Singleton không chạy trong edit mode). Object dựng ẩn, không lưu vào scene đang mở.
/// Chạy qua menu Tools/FishSort/Run GamePlayRect Tests.
public class GamePlayRectManagerTests
{
    private GameObject root;
    private RenderTexture target;
    private GamePlayRectManager manager;

    [SetUp]
    public void SetUp()
    {
        root = Create("TestGamePlayRect", null);

        // Render vào texture riêng để phép chiếu không phụ thuộc cỡ Game view.
        Camera camera = Create("Camera", root.transform).AddComponent<Camera>();
        camera.transform.localPosition = new Vector3(0f, 0f, -10f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        target = new RenderTexture(64, 64, 0) { hideFlags = HideFlags.HideAndDontSave };
        camera.targetTexture = target;

        Canvas canvas = Create("Canvas", root.transform, typeof(RectTransform)).AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera;

        RectTransform wall = (RectTransform)Create("Wall", canvas.transform, typeof(RectTransform)).transform;
        wall.anchorMin = wall.anchorMax = new Vector2(0.5f, 0.5f);
        wall.sizeDelta = new Vector2(20f, 10f);
        wall.anchoredPosition = new Vector2(4f, 0f);

        manager = root.AddComponent<GamePlayRectManager>();
        SerializedObject so = new SerializedObject(manager);
        so.FindProperty("<GamePlayRectWall>k__BackingField").objectReferenceValue = wall;
        so.FindProperty("<GamePlayCanvas>k__BackingField").objectReferenceValue = canvas;
        so.FindProperty("<WorldCamera>k__BackingField").objectReferenceValue = camera;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(target);
    }

    private static GameObject Create(string name, Transform parent, params System.Type[] components)
    {
        GameObject go = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave, components);
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    private void SetMaxWorldWidth(float value)
    {
        SerializedObject so = new SerializedObject(manager);
        SerializedProperty property = so.FindProperty("<MaxWorldWidth>k__BackingField");
        Assert.IsNotNull(property, "GamePlayRectManager chưa có field MaxWorldWidth");
        property.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private Rect ComputeWorldRect()
    {
        Assert.IsTrue(manager.TryComputeWorldRect(out Rect rect), "không chiếu được rect tường xuống mặt phẳng bóng");
        return rect;
    }

    private static void AssertRect(Rect rect, float xMin, float xMax, float yMin, float yMax)
    {
        const float eps = 1e-3f;
        Assert.AreEqual(xMin, rect.xMin, eps, "xMin");
        Assert.AreEqual(xMax, rect.xMax, eps, "xMax");
        Assert.AreEqual(yMin, rect.yMin, eps, "yMin");
        Assert.AreEqual(yMax, rect.yMax, eps, "yMax");
    }

    [Test]
    public void TryComputeWorldRect_MaxZero_KeepsFullWall()
    {
        SetMaxWorldWidth(0f);
        AssertRect(ComputeWorldRect(), -6f, 14f, -5f, 5f);
    }

    [Test]
    public void TryComputeWorldRect_WallNarrowerThanMax_KeepsFullWall()
    {
        SetMaxWorldWidth(30f);
        AssertRect(ComputeWorldRect(), -6f, 14f, -5f, 5f);
    }

    [Test]
    public void TryComputeWorldRect_WallWiderThanMax_CapsAroundWallCenter()
    {
        // Tâm tường x = 4, giới hạn 15 m: 4 ± 7.5; trục Y giữ nguyên.
        SetMaxWorldWidth(15f);
        AssertRect(ComputeWorldRect(), -3.5f, 11.5f, -5f, 5f);
    }
}
