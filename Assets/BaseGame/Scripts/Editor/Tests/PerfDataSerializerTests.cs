using System.Collections.Generic;
using NUnit.Framework;

/// DataSerializer: roundtrip JSON + mã hoá, và FromJson thay thế list (không nối) khi deserialize nhiều lần với settings dùng chung.
public class PerfDataSerializerTests
{
    private class Bag { public List<int> Items = new List<int> { 1, 2, 3 }; public string Name = "x"; }

    [Test]
    public void ToJson_FromJson_Roundtrip()
    {
        Bag bag = new Bag { Name = "fish", Items = new List<int> { 7, 8 } };
        string json = DataSerializer.ToJson(bag);
        Bag back = DataSerializer.FromJson<Bag>(json);
        Assert.AreEqual("fish", back.Name);
        CollectionAssert.AreEqual(new[] { 7, 8 }, back.Items);
    }

    [Test]
    public void Serialize_Deserialize_EncryptedRoundtrip()
    {
        Bag back = DataSerializer.Deserialize<Bag>(DataSerializer.Serialize(new Bag { Name = "enc" }));
        Assert.AreEqual("enc", back.Name);
    }

    [Test]
    public void FromJson_ReplacesListsOnRepeatedCalls()
    {
        string json = DataSerializer.ToJson(new Bag { Items = new List<int> { 9 } });
        Bag first = DataSerializer.FromJson<Bag>(json);
        Bag second = DataSerializer.FromJson<Bag>(json);
        Assert.AreEqual(1, first.Items.Count);
        Assert.AreEqual(1, second.Items.Count, "ObjectCreationHandling.Replace phải còn hiệu lực sau khi cache settings");
    }
}
