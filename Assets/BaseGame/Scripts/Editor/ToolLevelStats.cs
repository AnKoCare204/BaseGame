using System.Collections.Generic;

/// <summary>Số đo nhanh của level đang mở cho panel tool: số loài cá, số match.</summary>
public static class ToolLevelStats
{
    /// <summary>Số loài cá khác nhau trong level: mọi lớp trong bóng + hàng chờ spawner, bỏ bóng đá.</summary>
    public static int SpeciesCount(LevelDesignData data)
    {
        SoftBubbleData[] bubbles = data != null ? data.levelData?.SoftBubbleDataArray : null;
        if (bubbles == null) return 0;

        var species = new HashSet<int>();
        foreach (SoftBubbleData bubble in bubbles)
        {
            if (bubble == null || bubble.isRock) continue;
            for (int layer = 0; layer < bubble.LayerCount; layer++)
            {
                foreach (FishData fish in bubble.GetLayerFish(layer))
                {
                    if (fish != null) species.Add(fish.FishId);
                }
            }

            if (!bubble.isSpawner || bubble.fishOnSpawner == null) continue;
            foreach (FishData fish in bubble.fishOnSpawner)
            {
                if (fish != null) species.Add(fish.FishId);
            }
        }

        return species.Count;
    }
}
