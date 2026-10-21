using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// Vị trí cụm booster theo banner (NoAdsSwitchPosition). Dựng object HideAndDontSave nên không làm bẩn scene.
public class BannerAdsLayoutTests
{
    private const float ActiveY = 20f;
    private const float InactiveY = -100f;

    private GameObject bar;
    private Transform root;
    private NoAdsSwitchPosition switcher;

    [SetUp]
    public void SetUp()
    {
        bar = EditorUtility.CreateGameObjectWithHideFlags("BannerAdsLayoutTest", HideFlags.HideAndDontSave);
        root = CreateChild("Root", 0f);
        Transform active = CreateChild("BannerActivePosition", ActiveY);
        Transform inactive = CreateChild("BannerInactivePosition", InactiveY);

        switcher = bar.AddComponent<NoAdsSwitchPosition>();
        SerializedObject so = new SerializedObject(switcher);
        so.FindProperty("<Root>k__BackingField").objectReferenceValue = root;
        so.FindProperty("<BannerActivePosition>k__BackingField").objectReferenceValue = active;
        so.FindProperty("<BannerInactivePosition>k__BackingField").objectReferenceValue = inactive;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    [TearDown]
    public void TearDown()
    {
        if (bar != null) Object.DestroyImmediate(bar);
    }

    [TestCase(true, ActiveY)]
    [TestCase(false, InactiveY)]
    public void Apply_MovesRootToMatchingMarker(bool hasBanner, float expectedY)
    {
        switcher.Apply(hasBanner);
        Assert.AreEqual(expectedY, root.position.y, 0.0001f);
    }

    // Level tutorial đầu không chạy ScreenInGame.LoadLevel và không có banner: cụm booster phải ở chỗ không banner.
    [Test]
    public void Start_WithoutLevelDecision_UsesNoBannerPosition()
    {
        InvokeStart();
        Assert.AreEqual(InactiveY, root.position.y, 0.0001f);
    }

    // LoadLevel có thể chạy trước Start (object bật muộn): Start không được đè quyết định của level.
    [Test]
    public void Start_AfterLevelDecision_KeepsIt()
    {
        switcher.Apply(true);
        InvokeStart();
        Assert.AreEqual(ActiveY, root.position.y, 0.0001f);
    }

    private Transform CreateChild(string name, float y)
    {
        GameObject child = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave);
        child.transform.SetParent(bar.transform, false);
        child.transform.localPosition = new Vector3(0f, y, 0f);
        return child.transform;
    }

    private void InvokeStart()
    {
        MethodInfo start = typeof(NoAdsSwitchPosition).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(start, "NoAdsSwitchPosition.Start không còn");
        start.Invoke(switcher, null);
    }
}
