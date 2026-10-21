using System.Linq;
using Core.Manager;
using Core.Tutorial;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// Chữ FTUE (spec bản sửa mục 4): game không còn khái niệm "tank", ô hàng trên gọi là "order". Prefab phải mang đúng chữ mặc định.
public class FtueTextsTests
{
    private static string[] All(TutorialTexts t) =>
        typeof(TutorialTexts).GetFields().Where(f => f.FieldType == typeof(string)).Select(f => (string)f.GetValue(t)).ToArray();

    [Test]
    public void Defaults_NeverSayTank() => Assert.IsFalse(All(new TutorialTexts()).Any(s => s.ToLowerInvariant().Contains("tank")));

    [Test]
    public void Prefab_MatchesDefaults()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BaseGame/Prefabs/Manager/TutManager.prefab");
        TutorialTexts texts = prefab.GetComponent<TutorialManager>().Texts;
        CollectionAssert.AreEqual(All(new TutorialTexts()), All(texts));
    }

    [Test]
    public void FullRule_FormatsSlotCount() => Assert.AreEqual("Don't fill all 5 slots!", string.Format(new TutorialTexts().L2FullRule, 5));
}
