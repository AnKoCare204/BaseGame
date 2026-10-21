using System;
using UnityEngine;

// Sfx tách theo từng cụm mục đích. Mỗi enum có mảng SfxConfig<TKey>[] riêng trong AudioManager.prefab,
// không dùng chung AudioKey như trước (AudioKey chỉ còn music + UI core: click, win, lose, complete, coin).
// Muốn đổi clip của một cụm: sửa mảng tương ứng trong AudioManager.prefab, không đụng cụm khác.

/// <summary>Một entry key → clip cho một cụm sfx. Không có clip = im lặng.</summary>
[Serializable]
public class SfxConfig<TKey> where TKey : struct, Enum
{
    [field: SerializeField] public TKey Key { get; private set; }
    [field: SerializeField] public AudioClip[] AudioClip { get; private set; }
}
