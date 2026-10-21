using Core.Tutorial;
using NUnit.Framework;

/// TutData v2: DoneSteps + migrate từ save v1 chỉ có IsPassFirstTut (spec mục 6).
public class FtueTutDataTests
{
    [Test]
    public void Migrate_PassedOldTutorial_MarksL1Done()
    {
        var data = new TutData { IsPassFirstTut = true, DoneSteps = null };
        data.Migrate();
        CollectionAssert.AreEqual(new[] { TutorialIds.L1Match }, data.DoneSteps);
        Assert.AreEqual(2, data.Version);
    }

    [Test]
    public void Migrate_NewPlayer_AddsNothing()
    {
        var data = new TutData();
        data.SetDefault();
        data.Migrate();
        Assert.IsEmpty(data.DoneSteps);
    }

    [Test]
    public void Migrate_Twice_NoDuplicate()
    {
        var data = new TutData { IsPassFirstTut = true };
        data.Migrate();
        data.Migrate();
        Assert.AreEqual(1, data.DoneSteps.Count);
    }

    [Test]
    public void V1Json_WithoutDoneSteps_MigratesAfterLoad()
    {
        TutData data = DataSerializer.FromJson<TutData>("{\"Version\":1,\"IsPassFirstTut\":true}");
        Assert.AreEqual(1, data.Version);
        data.Migrate();
        Assert.IsTrue(data.IsDone(TutorialIds.L1Match));
    }

    [Test]
    public void MarkDone_L1_SetsIsPassFirstTut()
    {
        var data = new TutData();
        data.SetDefault();
        Assert.IsTrue(data.MarkDone(TutorialIds.L1Match));
        Assert.IsTrue(data.IsPassFirstTut);
        Assert.IsFalse(data.MarkDone(TutorialIds.L1Match));
    }

    [Test]
    public void MarkDone_OtherStep_LeavesIsPassFirstTut()
    {
        var data = new TutData();
        data.SetDefault();
        data.MarkDone(TutorialIds.L2Holding);
        Assert.IsFalse(data.IsPassFirstTut);
        Assert.IsTrue(data.IsDone(TutorialIds.L2Holding));
    }
}
