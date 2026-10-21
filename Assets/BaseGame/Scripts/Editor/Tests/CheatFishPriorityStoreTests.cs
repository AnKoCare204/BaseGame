using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// Test CheatFishPriority: đọc / ghi list tên prefab cá ưu tiên và tra ra FishModel.
/// Test Toggle / Clear ghi thật vào PlayerPrefs nên SetUp lưu giá trị cũ, TearDown trả lại.
/// Chạy qua menu Tools/FishSort/Run Cheat Fish Tests hoặc cửa sổ Test Runner (EditMode).
public class CheatFishPriorityStoreTests
{
    private readonly List<GameObject> created = new List<GameObject>();
    private bool hadSaved;
    private string saved;

    [SetUp]
    public void SetUp()
    {
        hadSaved = PlayerPrefs.HasKey(CheatFishPriority.PrefsKey);
        saved = PlayerPrefs.GetString(CheatFishPriority.PrefsKey, "");
    }

    [TearDown]
    public void TearDown()
    {
        if (hadSaved) PlayerPrefs.SetString(CheatFishPriority.PrefsKey, saved);
        else PlayerPrefs.DeleteKey(CheatFishPriority.PrefsKey);
        PlayerPrefs.Save();

        foreach (GameObject go in created)
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        created.Clear();
    }

    private FishModel Make(string name)
    {
        var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
        created.Add(go);
        return go.AddComponent<FishModel>();
    }

    [Test]
    public void Parse_TrimsAndDropsEmptyAndDuplicateNames()
    {
        List<string> names = CheatFishPriority.Parse(" Fish_1|Fish_2||Fish_1| Prop_4 ");

        CollectionAssert.AreEqual(new[] { "Fish_1", "Fish_2", "Prop_4" }, names);
    }

    [Test]
    public void Parse_NullOrEmpty_ReturnsEmptyList()
    {
        CollectionAssert.IsEmpty(CheatFishPriority.Parse(null));
        CollectionAssert.IsEmpty(CheatFishPriority.Parse(""));
    }

    [Test]
    public void Format_ThenParse_GivesTheSameNames()
    {
        var names = new List<string> { "Fish_24", "Prop_4", "Fish_1" };

        string raw = CheatFishPriority.Format(names);

        Assert.AreEqual("Fish_24|Prop_4|Fish_1", raw);
        CollectionAssert.AreEqual(names, CheatFishPriority.Parse(raw));
    }

    [Test]
    public void Resolve_KeepsNameOrderAndSkipsUnknownNames()
    {
        FishModel fish1 = Make("Fish_1");
        FishModel fish2 = Make("Fish_2");
        FishModel prop4 = Make("Prop_4");

        List<FishModel> models = CheatFishPriority.Resolve(new[] { "Prop_4", "Nope", "Fish_1" }, new[] { fish1, fish2, null, prop4 });

        CollectionAssert.AreEqual(new[] { prop4, fish1 }, models);
    }

    [Test]
    public void Resolve_NullArguments_ReturnEmptyList()
    {
        CollectionAssert.IsEmpty(CheatFishPriority.Resolve(null, new[] { Make("Fish_1") }));
        CollectionAssert.IsEmpty(CheatFishPriority.Resolve(new[] { "Fish_1" }, null));
    }

    [Test]
    public void Toggle_AddsThenRemoves_AndClearEmptiesTheList()
    {
        CheatFishPriority.Clear();
        Assert.IsFalse(CheatFishPriority.Contains("Fish_3"));

        CheatFishPriority.Toggle("Fish_3");
        CheatFishPriority.Toggle("Fish_5");
        Assert.IsTrue(CheatFishPriority.Contains("Fish_3"));
        CollectionAssert.AreEqual(new[] { "Fish_3", "Fish_5" }, CheatFishPriority.Names);

        CheatFishPriority.Toggle("Fish_3");
        Assert.IsFalse(CheatFishPriority.Contains("Fish_3"));
        CollectionAssert.AreEqual(new[] { "Fish_5" }, CheatFishPriority.Names);

        CheatFishPriority.Clear();
        CollectionAssert.IsEmpty(CheatFishPriority.Names);
    }

    [Test]
    public void Names_ReadsWhatIsStoredInPlayerPrefs()
    {
        PlayerPrefs.SetString(CheatFishPriority.PrefsKey, "Fish_9|Prop_2");

        CollectionAssert.AreEqual(new[] { "Fish_9", "Prop_2" }, CheatFishPriority.Names.ToArray());
    }
}
