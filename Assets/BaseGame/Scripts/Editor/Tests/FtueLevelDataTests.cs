using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.GamePlay;
using Core.Tutorial;
using NUnit.Framework;

/// Data L1-L3 của FTUE (spec bản sửa mục 6, 3 order mở): file đã ship khớp FtueLevelSeeds, qua Validate, và đủ / không quá áp lực ô chờ.
public class FtueLevelDataTests
{
    private static LevelData Load(int level) =>
        DataSerializer.Deserialize<LevelData>(File.ReadAllText($"Assets/BaseGame/TextAssets/LevelDataConverted/LevelData_{level}.json"));

    private static int[][] FishOf(LevelData d) => d.SoftBubbleDataArray.Select(b => b.FishDataArray.Select(f => f.FishId).ToArray()).ToArray();

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void ShippedFile_MatchesSeeds(int level)
    {
        LevelData shipped = Load(level);
        LevelData expected = FtueLevelSeeds.Build(level);
        CollectionAssert.AreEqual(expected.OrderQueue, shipped.OrderQueue);
        int[][] a = FishOf(expected), b = FishOf(shipped);
        Assert.AreEqual(a.Length, b.Length);
        for (int i = 0; i < a.Length; i++) CollectionAssert.AreEqual(a[i], b[i], $"bóng {i}");
        Assert.AreEqual(LevelDifficulty.Normal, shipped.Difficulty);
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void Seeds_PassValidate_AndFitOnScreen(int level)
    {
        LevelData d = FtueLevelSeeds.Build(level);
        var errors = new List<string>();
        Assert.IsTrue(d.Validate(errors), string.Join("; ", errors));
        Assert.LessOrEqual(d.BubbleCount, 12);
        Assert.AreNotEqual(d.OrderQueue[0], d.OrderQueue[1]);
        Assert.IsFalse(d.SoftBubbleDataArray.Any(b => b.isIce || b.isRock || b.isSpawner || b.isLock || b.isKey || b.isBubbleHp || b.isMultiLayer || b.isBundleBubble || b.isOrderBubble));
        Assert.IsFalse(d.SoftBubbleDataArray.SelectMany(b => b.FishDataArray).Any(f => f.isHidden));
    }

    [TestCase(1, 3, 6)]
    [TestCase(2, 4, 9)]
    [TestCase(3, 5, 15)]
    public void Seeds_SpeciesAndTanks(int level, int species, int tanks)
    {
        LevelData d = FtueLevelSeeds.Build(level);
        Assert.AreEqual(species, d.OrderQueue.Distinct().Count());
        Assert.AreEqual(tanks, d.OrderQueue.Length);
    }

    [Test]
    public void L2_HoldingSpecies_FourthOrder_AndOnFirstBubble()
    {
        LevelData d = FtueLevelSeeds.Build(2);
        int open = FtueLevelRules.OpenOrderSlots(2, 2);
        Assert.IsFalse(d.OrderQueue.Take(open).Contains(FtueLevelSeeds.L2HoldingSpecies));
        Assert.AreEqual(FtueLevelSeeds.L2HoldingSpecies, d.OrderQueue[open]);
        Assert.IsTrue(d.SoftBubbleDataArray[0].FishDataArray.Any(f => f.FishId == FtueLevelSeeds.L2HoldingSpecies));
    }

    [Test]
    public void L1_WithoutHolding_AlwaysWins()
    {
        LevelData d = FtueLevelSeeds.Build(1);
        for (int seed = 0; seed < 200; seed++)
        {
            bool stuck = Simulate(d, seed, 0.0, false).stuck;
            Assert.IsFalse(stuck, $"seed {seed}");
        }
    }

    [Test]
    public void L3_RandomPlayer_FeelsHoldingPressure()
    {
        LevelData d = FtueLevelSeeds.Build(3);
        int runs = 300, hit = 0;
        for (int seed = 0; seed < runs; seed++)
        {
            if (Simulate(d, seed, 0.5, true).maxHeld >= 3) hit++;
        }

        Assert.GreaterOrEqual(hit / (double)runs, 0.6);
    }

    [TestCase(0.7, 0.10)]
    [TestCase(0.9, 0.02)]
    public void L3_CarefulPlayer_RarelyStuck(double bias, double maxStuck)
    {
        LevelData d = FtueLevelSeeds.Build(3);
        int runs = 300, stuck = 0;
        for (int seed = 0; seed < runs; seed++)
        {
            if (Simulate(d, seed, bias, true).stuck) stuck++;
        }

        Assert.LessOrEqual(stuck / (double)runs, maxStuck);
    }

    [TestCase(2)]
    [TestCase(3)]
    public void HumanBot_MeetsTargetWinRate(int level)
    {
        LevelData d = FtueLevelSeeds.Build(level);
        float win = LevelHumanBot.EstimateWinRate(d, LevelHumanBot.PlayerMistakeRate, 300,
            tankSlots: FtueLevelRules.OpenOrderSlots(level, LevelBands.DefaultTankSlots));
        Assert.GreaterOrEqual(win, LevelHumanBot.PlayerTargetWinRate(level, LevelDifficulty.Normal) - 0.05f, $"L{level} win {win:P0}");
    }

    /// <summary>
    /// Người chơi ngẫu nhiên trên BoardModel (số order mở theo FtueLevelRules: 3 ở L1-L3; 5 ô chờ, bay tức thì, tự thu): mỗi lượt chọn cá khớp bể với xác suất bias, không thì
    /// chọn bất kỳ cá bấm được. holdingEnabled = false: chỉ bấm cá khớp bể (luật L1). Trả về (kẹt, số ô chờ dùng nhiều nhất).
    /// </summary>
    private static (bool stuck, int maxHeld) Simulate(LevelData data, int seed, double bias, bool holdingEnabled)
    {
        var m = new BoardModel(data, FtueLevelRules.OpenOrderSlots(data.Level, 2), 4, 5, 7, 15);
        var rng = new Random(seed);
        int maxHeld = 0;
        for (int step = 0; step < 1000; step++)
        {
            while (m.TryFindAutoCollect(out int h, out TankEntry collectTank))
            {
                m.TakeFromHolding(h);
                m.ReserveTank(collectTank);
                if (m.ArriveAtTank(collectTank)) m.CompleteTank(collectTank);
            }

            if (m.IsWin) return (false, maxHeld);
            if (m.HoldingFreeCount == 0) return (true, maxHeld);   // ô chờ đầy thì game khóa bấm: kẹt

            var all = new List<(BubbleEntry b, int i)>();
            var match = new List<(BubbleEntry b, int i)>();
            foreach (BubbleEntry b in m.FrontBubbles)
            {
                if (!b.IsTappable) continue;
                for (int i = 0; i < b.Fishes.Count; i++)
                {
                    all.Add((b, i));
                    if (m.FindTankFor(b.Fishes[i].FishId) != null) match.Add((b, i));
                }
            }

            if (!holdingEnabled) all = match;
            if (all.Count == 0) return (true, maxHeld);
            (BubbleEntry pb, int pi) = match.Count > 0 && rng.NextDouble() < bias ? match[rng.Next(match.Count)] : all[rng.Next(all.Count)];
            FishEntry fish = m.RemoveFishAt(pb, pi);
            TankEntry tank = m.FindTankFor(fish.FishId);
            if (tank != null)
            {
                m.ReserveTank(tank);
                if (m.ArriveAtTank(tank)) m.CompleteTank(tank);
            }
            else
            {
                int hi = m.FindFreeHoldingIndex();
                m.ReserveHolding(hi);
                m.ArriveAtHolding(hi, fish.FishId);
            }

            maxHeld = Math.Max(maxHeld, m.HoldingFishCount);
        }

        return (true, maxHeld);
    }
}
