using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// Test FishModelPriority.Apply: cá ưu tiên của bản cheat vào đúng các loài đầu của level, đổi chỗ chứ không nhân đôi model,
/// không đụng entry ID âm (object của asset FishModelGlobalConfig).
/// Chạy qua menu Tools/FishSort/Run Cheat Fish Tests hoặc cửa sổ Test Runner (EditMode).
public class CheatFishModelPriorityTests
{
    private readonly List<GameObject> created = new List<GameObject>();
    private FishModel a, b, c, d, e, x, hidden;

    [SetUp]
    public void SetUp()
    {
        a = Make("Fish_A");
        b = Make("Fish_B");
        c = Make("Fish_C");
        d = Make("Fish_D");
        e = Make("Fish_E");
        x = Make("Prop_X");
        hidden = Make("Hidden");
    }

    [TearDown]
    public void TearDown()
    {
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

    private static List<FishModelConfig> Table(params (int id, FishModel model)[] entries)
        => entries.Select(t => new FishModelConfig { ID = t.id, FishModel = t.model }).ToList();

    private static FishModel ModelOf(List<FishModelConfig> table, int id) => table.First(t => t.ID == id).FishModel;

    private static string Describe(List<FishModelConfig> table)
        => string.Join(", ", table.Select(t => $"{t.ID}:{(t.FishModel != null ? t.FishModel.name : "null")}"));

    [Test]
    public void Apply_EmptyPreferred_LeavesTableUnchanged()
    {
        List<FishModelConfig> table = Table((0, a), (1, b), (2, c));

        FishModelPriority.Apply(table, new[] { 0, 1, 2 }, new FishModel[0]);

        Assert.AreEqual("0:Fish_A, 1:Fish_B, 2:Fish_C", Describe(table));
    }

    [Test]
    public void Apply_GivesPreferredToSpeciesInFirstAppearanceOrder()
    {
        List<FishModelConfig> table = Table((0, a), (1, b), (2, c), (3, d));

        FishModelPriority.Apply(table, new[] { 2, 0, 2, 1 }, new[] { d, b });

        Assert.AreSame(d, ModelOf(table, 2));
        Assert.AreSame(b, ModelOf(table, 0));
    }

    [Test]
    public void Apply_SwapsDisplacedModelIntoTheFreedEntry()
    {
        List<FishModelConfig> table = Table((0, a), (1, b), (2, c), (3, d));

        FishModelPriority.Apply(table, new[] { 2, 0, 2, 1 }, new[] { d, b });

        Assert.AreEqual("0:Fish_B, 1:Fish_A, 2:Fish_D, 3:Fish_C", Describe(table));
    }

    [Test]
    public void Apply_KeepsEveryModelExactlyOnce()
    {
        List<FishModelConfig> table = Table((0, a), (1, b), (2, c), (3, d), (4, e));

        FishModelPriority.Apply(table, new[] { 4, 3, 4 }, new[] { a, b });

        CollectionAssert.AreEquivalent(new[] { a, b, c, d, e }, table.Select(t => t.FishModel).ToArray());
    }

    [Test]
    public void Apply_MorePreferredThanSpecies_UsesOnlyTheFirstOnes()
    {
        List<FishModelConfig> table = Table((0, a), (1, b), (2, c), (3, d));

        FishModelPriority.Apply(table, new[] { 0, 1 }, new[] { c, d, a });

        Assert.AreEqual("0:Fish_C, 1:Fish_D, 2:Fish_A, 3:Fish_B", Describe(table));
    }

    [Test]
    public void Apply_PreferredOutsideTable_ReplacesTheSpeciesModel()
    {
        List<FishModelConfig> table = Table((0, a), (1, b));

        FishModelPriority.Apply(table, new[] { 0, 1 }, new[] { x });

        Assert.AreEqual("0:Prop_X, 1:Fish_B", Describe(table));
    }

    [Test]
    public void Apply_PreferredAlreadyOnItsSpecies_LeavesTableUnchanged()
    {
        List<FishModelConfig> table = Table((0, a), (1, b), (2, c));

        FishModelPriority.Apply(table, new[] { 0, 1 }, new[] { a, b });

        Assert.AreEqual("0:Fish_A, 1:Fish_B, 2:Fish_C", Describe(table));
    }

    [Test]
    public void Apply_NegativeIdEntry_IsNeverModified()
    {
        List<FishModelConfig> table = Table((-1, hidden), (0, a), (1, b));
        FishModelConfig negative = table[0];

        FishModelPriority.Apply(table, new[] { -1, 0 }, new[] { b });

        Assert.AreSame(negative, table[0]);
        Assert.AreSame(hidden, negative.FishModel);
        Assert.AreEqual("-1:Hidden, 0:Fish_B, 1:Fish_A", Describe(table));
    }

    [Test]
    public void Apply_DoesNotTakeModelFromNegativeIdEntry()
    {
        List<FishModelConfig> table = Table((-1, b), (0, a), (1, c));

        FishModelPriority.Apply(table, new[] { 0 }, new[] { b });

        Assert.AreEqual("-1:Fish_B, 0:Fish_B, 1:Fish_C", Describe(table));
    }

    [Test]
    public void Apply_SpeciesWithoutEntry_IsSkippedAndNextSpeciesGetsThePreferred()
    {
        List<FishModelConfig> table = Table((0, a), (1, b), (2, c));

        FishModelPriority.Apply(table, new[] { 7, 1 }, new[] { c, a });

        Assert.AreEqual("0:Fish_A, 1:Fish_C, 2:Fish_B", Describe(table));
    }

    [Test]
    public void Apply_DuplicateAndNullPreferred_AreCountedOnce()
    {
        List<FishModelConfig> table = Table((0, a), (1, b), (2, c));

        FishModelPriority.Apply(table, new[] { 0, 1 }, new[] { null, c, c, b });

        Assert.AreEqual("0:Fish_C, 1:Fish_B, 2:Fish_A", Describe(table));
    }

    [Test]
    public void ApplyShuffled_MorePreferredThanSpecies_EveryPreferredGetsPickedAcrossLevels()
    {
        var picked = new HashSet<FishModel>();
        for (int seed = 0; seed < 100; seed++)
        {
            List<FishModelConfig> table = Table((0, a), (1, b), (2, c), (3, d));

            FishModelPriority.ApplyShuffled(table, new[] { 0 }, new[] { b, c, d }, new System.Random(seed));

            FishModel model = ModelOf(table, 0);
            Assert.That(new[] { b, c, d }, Has.Member(model), $"seed {seed}: {Describe(table)}");
            picked.Add(model);
        }

        CollectionAssert.AreEquivalent(new[] { b, c, d }, picked);
    }

    [Test]
    public void ApplyShuffled_LeavesTheCallersListInOrder()
    {
        List<FishModelConfig> table = Table((0, a), (1, b), (2, c));
        var preferred = new List<FishModel> { c, b, a };

        FishModelPriority.ApplyShuffled(table, new[] { 0, 1, 2 }, preferred, new System.Random(3));

        CollectionAssert.AreEqual(new[] { c, b, a }, preferred);
        CollectionAssert.AreEquivalent(new[] { a, b, c }, table.Select(t => t.FishModel).ToArray());
    }

    [Test]
    public void Apply_NullArguments_DoNothing()
    {
        List<FishModelConfig> table = Table((0, a), (1, b));

        FishModelPriority.Apply(null, new[] { 0 }, new[] { b });
        FishModelPriority.Apply(table, null, new[] { b });
        FishModelPriority.Apply(table, new[] { 0 }, null);

        Assert.AreEqual("0:Fish_A, 1:Fish_B", Describe(table));
    }
}
