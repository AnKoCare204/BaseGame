using System.Collections.Generic;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dòng "Cá ẩn" trong khâu 4 của "Function random level": bật cờ FishData.isHidden cho N con cá ngẫu nhiên.
///
/// Luật chọn:
/// - Chỉ bóng có từ 2 cá trở lên, và mỗi bóng luôn chừa ít nhất 1 con không ẩn. Runtime (BoardModel.TryAutoRevealAll)
///   thấy bóng toàn cá ẩn là lộ hết ngay, nên ẩn trọn một bóng là ẩn vô ích.
/// - Bóng ít cá có xác suất được chọn cao hơn bóng nhiều cá: mỗi lượt bốc một con, trọng số của bóng = 1 / số cá.
///   Ví dụ bóng 2 cá nặng gấp đôi bóng 4 cá.
/// - Chỉ xét cá trong bóng, không đụng cá trên spawner.
/// Xem <see cref="LevelDesignDataEditor"/> cho danh sách các file partial khác.
/// </summary>
public partial class LevelDesignDataEditor
{
    private int randomHiddenCount = 2;

    private void DrawRandomHiddenRow(LevelDesignData data)
    {
        EditorGUILayout.BeginVertical(SirenixGUIStyles.BoxContainer);
        GUILayout.Label("Cá ẩn", SirenixGUIStyles.LeftAlignedGreyLabel);

        int capacity = CountHiddenCapacity(data.levelData.SoftBubbleDataArray, out int eligibleBubbles);
        randomHiddenCount = Mathf.Max(0, EditorGUILayout.IntField("Số cá ẩn", randomHiddenCount));

        GUILayout.Label(
            eligibleBubbles == 0
                ? "Chưa có bóng nào từ 2 cá trở lên"
                : $"{eligibleBubbles} bóng đủ điều kiện, ẩn thêm được tối đa {capacity} con",
            SirenixGUIStyles.CenteredGreyMiniLabel, GUILayout.ExpandWidth(true));
        GUILayout.Label("Bóng từ 2 cá, luôn chừa 1 con không ẩn. Bóng ít cá dễ được chọn hơn.",
            SirenixGUIStyles.MultiLineCenteredLabel);

        EditorGUILayout.BeginHorizontal();
        GUI.enabled = capacity > 0 && randomHiddenCount > 0;
        if (GUILayout.Button("Random cá ẩn", GUILayout.Height(22f))) pending = () => RandomHiddenFish(data);

        GUI.enabled = HasHiddenFish(data.levelData.SoftBubbleDataArray);
        if (GUILayout.Button("Bỏ ẩn hết", GUILayout.Height(22f))) pending = () => ClearHiddenFish(data);
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void RandomHiddenFish(LevelDesignData data)
    {
        SoftBubbleData[] bubbles = data.levelData.SoftBubbleDataArray;
        int want = randomHiddenCount;
        if (want <= 0)
        {
            SetStatus(false, "Số cá ẩn phải lớn hơn 0");
            return;
        }

        if (CountHiddenCapacity(bubbles, out _) == 0)
        {
            SetStatus(false, "Không còn bóng nào ẩn thêm được: cần bóng từ 2 cá và còn ít nhất 2 con chưa ẩn");
            return;
        }

        int applied = 0;
        Mutate(data, "Random cá ẩn", () =>
        {
            var candidates = new List<SoftBubbleData>();
            var visible = new List<FishData>();

            for (int picked = 0; picked < want; picked++)
            {
                // Trọng số tính lại sau mỗi con: bóng vừa bị ẩn tới mức chỉ còn 1 con lộ thì rơi khỏi danh sách.
                candidates.Clear();
                float totalWeight = 0f;
                foreach (SoftBubbleData bubble in bubbles)
                {
                    if (HiddenSlotsLeft(bubble) <= 0) continue;
                    candidates.Add(bubble);
                    totalWeight += HiddenWeight(bubble);
                }

                if (candidates.Count == 0) break;

                SoftBubbleData chosen = candidates[candidates.Count - 1];
                float roll = Random.value * totalWeight;
                foreach (SoftBubbleData bubble in candidates)
                {
                    roll -= HiddenWeight(bubble);
                    if (roll > 0f) continue;
                    chosen = bubble;
                    break;
                }

                visible.Clear();
                foreach (FishData fish in chosen.FishDataArray)
                {
                    if (fish != null && !fish.isHidden) visible.Add(fish);
                }

                visible[Random.Range(0, visible.Count)].isHidden = true;
                applied++;
            }
        });

        SetStatus(true, applied < want
            ? $"Đã ẩn {applied} con - hết chỗ ẩn (mỗi bóng phải chừa 1 con không ẩn) nên chưa đủ {want}"
            : $"Đã ẩn {applied} con cá");
    }

    private void ClearHiddenFish(LevelDesignData data)
    {
        SoftBubbleData[] bubbles = data.levelData.SoftBubbleDataArray;
        int cleared = 0;
        Mutate(data, "Bỏ ẩn hết cá", () =>
        {
            if (bubbles == null) return;
            foreach (SoftBubbleData bubble in bubbles)
            {
                if (bubble?.FishDataArray == null) continue;
                foreach (FishData fish in bubble.FishDataArray)
                {
                    if (fish == null || !fish.isHidden) continue;
                    fish.isHidden = false;
                    cleared++;
                }
            }
        });

        SetStatus(true, $"Đã bỏ ẩn {cleared} con cá");
    }

    /// <summary>Trọng số chọn bóng: tỉ lệ nghịch với số cá, nên bóng ít cá dễ được chọn hơn.</summary>
    private static float HiddenWeight(SoftBubbleData bubble) => 1f / Mathf.Max(1, bubble.FishCount);

    /// <summary>Số con còn ẩn thêm được trong bóng: số con đang lộ trừ 1 con bắt buộc chừa lại. Bóng dưới 2 cá = 0.</summary>
    private static int HiddenSlotsLeft(SoftBubbleData bubble)
    {
        if (bubble == null || bubble.isRock || bubble.FishCount < 2) return 0;

        int visible = 0;
        foreach (FishData fish in bubble.FishDataArray)
        {
            if (fish != null && !fish.isHidden) visible++;
        }

        return Mathf.Max(0, visible - 1);
    }

    private static int CountHiddenCapacity(SoftBubbleData[] bubbles, out int eligibleBubbles)
    {
        eligibleBubbles = 0;
        int capacity = 0;
        if (bubbles == null) return 0;

        foreach (SoftBubbleData bubble in bubbles)
        {
            if (bubble == null || bubble.isRock || bubble.FishCount < 2) continue;
            eligibleBubbles++;
            capacity += HiddenSlotsLeft(bubble);
        }

        return capacity;
    }

    private static bool HasHiddenFish(SoftBubbleData[] bubbles)
    {
        if (bubbles == null) return false;
        foreach (SoftBubbleData bubble in bubbles)
        {
            if (bubble?.FishDataArray == null) continue;
            foreach (FishData fish in bubble.FishDataArray)
            {
                if (fish != null && fish.isHidden) return true;
            }
        }

        return false;
    }
}
