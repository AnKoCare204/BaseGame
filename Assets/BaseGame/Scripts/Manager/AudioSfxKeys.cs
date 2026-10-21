using System;
using UnityEngine;

// Sfx tách theo từng cụm mục đích. Mỗi enum có mảng SfxConfig<TKey>[] riêng trong AudioManager.prefab,
// không dùng chung AudioKey như trước (AudioKey chỉ còn music + UI core: click, win, lose, complete, coin).
// Muốn đổi clip của một cụm: sửa mảng tương ứng trong AudioManager.prefab, không đụng cụm khác.

/// <summary>Event StarBlaze (streak + progress bar ở Home / EndGame).</summary>
public enum StarBlazeSfx
{
    ClaimAppear = 0,    // UIStreakClaimPoint: ngôi sao +point hiện ra
    StarAdd = 1,        // UIStreakClaimPoint: lửa bay / cộng point
    StarClaim = 2,      // UIStreakClaimPoint: kết thúc anim cộng point
    StreakIncrease = 3, // UIStreakBar bật lửa, UIStarBlazeProgress vượt stage
}

/// <summary>Một entry key → clip cho một cụm sfx. Không có clip = im lặng.</summary>
[Serializable]
public class SfxConfig<TKey> where TKey : struct, Enum
{
    [field: SerializeField] public TKey Key { get; private set; }
    [field: SerializeField] public AudioClip[] AudioClip { get; private set; }
}
