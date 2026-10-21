using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Dựng canvas thử cho test lớp BG trong một scene tạm cộng thêm, để không làm bẩn GamePlayScene đang mở.
/// Root là canvas World Space (rect = rootSize, coi như màn hình); Layer là lớp BG (rect = layerSize) đặt giữa root.
public sealed class GameplayBgTestRig
{
    public RectTransform Root { get; private set; }
    public RectTransform Layer { get; private set; }

    private Scene scene;
    private Scene previousActive;

    public static GameplayBgTestRig Create(Vector2 rootSize, Vector2 layerSize)
    {
        var rig = new GameplayBgTestRig();
        rig.previousActive = SceneManager.GetActiveScene();
        rig.scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(rig.scene);

        var rootGo = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
        rootGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        rig.Root = (RectTransform)rootGo.transform;
        rig.Root.sizeDelta = rootSize;

        var layerGo = new GameObject("Layer", typeof(RectTransform));
        rig.Layer = (RectTransform)layerGo.transform;
        rig.Layer.SetParent(rig.Root, false);
        rig.Layer.anchorMin = rig.Layer.anchorMax = rig.Layer.pivot = new Vector2(0.5f, 0.5f);
        rig.Layer.sizeDelta = layerSize;
        return rig;
    }

    public void Dispose()
    {
        if (previousActive.IsValid()) SceneManager.SetActiveScene(previousActive);
        if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
    }
}
