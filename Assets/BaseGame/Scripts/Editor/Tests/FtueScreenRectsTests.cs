using Core.Tutorial;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// Bounds world / RectTransform → pixel màn hình cho chỗ đặt khung chữ (spec bản sửa mục 3).
public class FtueScreenRectsTests
{
    private GameObject camGo, canvasGo;
    private RenderTexture target;

    [TearDown]
    public void TearDown()
    {
        if (camGo != null) Object.DestroyImmediate(camGo);
        if (canvasGo != null) Object.DestroyImmediate(canvasGo);
        if (target != null)
        {
            target.Release();
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void FromBounds_OrthographicCamera_PixelRect()
    {
        camGo = EditorUtility.CreateGameObjectWithHideFlags("cam", HideFlags.HideAndDontSave, typeof(Camera));
        var cam = camGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 10f;                  // cao 20 đơn vị
        target = new RenderTexture(1000, 2000, 0);   // 100 px mỗi đơn vị
        cam.targetTexture = target;
        camGo.transform.position = new Vector3(0f, 0f, -10f);

        Rect r = ScreenRects.FromBounds(new Bounds(new Vector3(1f, 2f, 0f), new Vector3(2f, 2f, 2f)), cam);
        Assert.AreEqual(500f, r.xMin, 0.5f);
        Assert.AreEqual(700f, r.xMax, 0.5f);
        Assert.AreEqual(1100f, r.yMin, 0.5f);
        Assert.AreEqual(1300f, r.yMax, 0.5f);
    }

    [Test]
    public void FromRectTransform_OverlayCanvas_UsesWorldCorners()
    {
        canvasGo = EditorUtility.CreateGameObjectWithHideFlags("canvas", HideFlags.HideAndDontSave, typeof(Canvas));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        GameObject part = EditorUtility.CreateGameObjectWithHideFlags("part", HideFlags.HideAndDontSave, typeof(RectTransform));
        part.transform.SetParent(canvasGo.transform, false);
        var rt = (RectTransform)part.transform;
        rt.sizeDelta = new Vector2(100f, 50f);
        rt.position = new Vector3(300f, 400f, 0f);

        Rect r = ScreenRects.FromRectTransform(rt);
        Assert.AreEqual(250f, r.xMin, 0.5f);
        Assert.AreEqual(375f, r.yMin, 0.5f);
        Assert.AreEqual(100f, r.width, 0.5f);
        Assert.AreEqual(50f, r.height, 0.5f);
    }
}
