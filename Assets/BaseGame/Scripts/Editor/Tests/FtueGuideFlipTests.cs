using Core.Tutorial;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// Khung chữ lật khi bị kẹp mép phải (spec bản sửa mục 3, "khung lật"): linh vật và bóng thoại đổi bên và soi gương để đuôi bóng thoại
/// vẫn chỉ vào linh vật, chữ trong bóng thoại soi gương ngược lại để vẫn đọc xuôi. Object test HideAndDontSave để không làm bẩn scene.
public class FtueGuideFlipTests
{
    private GameObject root;
    private Transform mascot, bubble, text;

    [SetUp]
    public void SetUp()
    {
        root = EditorUtility.CreateGameObjectWithHideFlags("guide", HideFlags.HideAndDontSave);
        mascot = EditorUtility.CreateGameObjectWithHideFlags("mascot", HideFlags.HideAndDontSave).transform;
        bubble = EditorUtility.CreateGameObjectWithHideFlags("bubble", HideFlags.HideAndDontSave).transform;
        text = EditorUtility.CreateGameObjectWithHideFlags("text", HideFlags.HideAndDontSave).transform;
        mascot.SetParent(root.transform, false);
        bubble.SetParent(root.transform, false);
        text.SetParent(bubble, false);
        mascot.localPosition = new Vector3(-330f, 0f, 0f);
        bubble.localPosition = new Vector3(150f, 20f, 0f);
        text.localPosition = new Vector3(25f, 3f, 0f);
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
    }

    [Test]
    public void Right_SwapsSides_AndMirrorsMascotAndBubble()
    {
        GuideFlip.Apply(mascot, bubble, text, -330f, 150f, true);

        Assert.AreEqual(330f, mascot.localPosition.x, 0.001f);
        Assert.AreEqual(-150f, bubble.localPosition.x, 0.001f);
        Assert.Less(mascot.localScale.x, 0f, "linh vật quay mặt về bóng thoại");
        Assert.Less(bubble.localScale.x, 0f, "đuôi bóng thoại chỉ về linh vật");
    }

    [Test]
    public void Right_KeepsTextReadable()
    {
        GuideFlip.Apply(mascot, bubble, text, -330f, 150f, true);

        Assert.Greater(text.lossyScale.x, 0f);
    }

    [Test]
    public void Left_RestoresLayout()
    {
        GuideFlip.Apply(mascot, bubble, text, -330f, 150f, true);
        GuideFlip.Apply(mascot, bubble, text, -330f, 150f, false);

        Assert.AreEqual(-330f, mascot.localPosition.x, 0.001f);
        Assert.AreEqual(150f, bubble.localPosition.x, 0.001f);
        Assert.Greater(mascot.localScale.x, 0f);
        Assert.Greater(bubble.localScale.x, 0f);
        Assert.Greater(text.localScale.x, 0f);
    }

    [Test]
    public void KeepsScaleMagnitude_AndOtherAxes()
    {
        mascot.localScale = new Vector3(0.8f, 0.8f, 1f);
        GuideFlip.Apply(mascot, bubble, text, -330f, 150f, true);

        Assert.AreEqual(-0.8f, mascot.localScale.x, 0.001f);
        Assert.AreEqual(0.8f, mascot.localScale.y, 0.001f);
        Assert.AreEqual(20f, bubble.localPosition.y, 0.001f);
    }

    [Test]
    public void NullText_IsAllowed() => Assert.DoesNotThrow(() => GuideFlip.Apply(mascot, bubble, null, -330f, 150f, true));
}
