using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// Câu cứu miễn phí làm sáng cả túi PackedUp (spec bản sửa mục 2.2): túi lên layer TutMesh khi làm tối, rồi trả đúng layer gốc của
/// từng renderer (kể cả số đếm). Object test tạo HideAndDontSave để không làm bẩn scene đang mở.
public class FtueHoldingBagTutTests
{
    private GameObject root;

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
    }

    [Test]
    public void SetTut_MovesRenderersToTutMesh_ThenRestoresTheirOwnLayers()
    {
        root = EditorUtility.CreateGameObjectWithHideFlags("bag", HideFlags.HideAndDontSave, typeof(HoldingBagView));
        GameObject body = EditorUtility.CreateGameObjectWithHideFlags("body", HideFlags.HideAndDontSave, typeof(MeshFilter), typeof(MeshRenderer));
        GameObject count = EditorUtility.CreateGameObjectWithHideFlags("count", HideFlags.HideAndDontSave, typeof(MeshFilter), typeof(MeshRenderer));
        body.transform.SetParent(root.transform, false);
        count.transform.SetParent(root.transform, false);
        body.layer = 0;
        count.layer = 5;
        var bag = root.GetComponent<HoldingBagView>();
        int tut = LayerMask.NameToLayer("TutMesh");

        bag.SetTut(true);
        Assert.AreEqual(tut, body.layer);
        Assert.AreEqual(tut, count.layer);

        bag.SetTut(false);
        Assert.AreEqual(0, body.layer);
        Assert.AreEqual(5, count.layer);
    }
}
