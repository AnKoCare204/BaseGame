using Sirenix.OdinInspector;
using Sirenix.Utilities;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;
using Spine.Unity;
#if UNITY_EDITOR
using UnityEditor;
#endif


[CreateAssetMenu(fileName = "ProfileGlobalConfig", menuName = "GlobalConfigs/ProfileGlobalConfig")]
[GlobalConfig("Assets/Resources/GlobalConfig/")]
public class ProfileGlobalConfig : GlobalConfig<ProfileGlobalConfig>
{
    // Use for main user avatar and frame
    [Title("Main User Avatar and Frame")]
    public List<ProfileDataDetail> UserAvatarDatas;
    public List<ProfileDataDetail> UserFrameDatas;
    public List<ProfileDataDetail> UserBadgeDatas;
    public List<ProfileDataDetail> UserNameDatas;

    // Use for reward
    [Title("Reward")]
    public List<FrameProfileConfig> frameRewards;
    public List<AvatarProfileConfig> avatarRewards;
    public List<BadgeProfileConfig> badgeRewards;
    public List<NameProfileConfig> nameRewards;

    // Use for edit profile
    [Title("Edit Profile")]
    public List<AvatarProfileConfig> avatarEditProfile;
    public List<FrameProfileConfig> frameEditProfile;
    public List<BadgeProfileConfig> badgeEditProfile;
    public List<NameProfileConfig> nameEditProfile;

    [Title("Hiden Id")]
    public List<int> hidenAvatarIds;
    public List<int> hidenFrameIds;
    public List<int> hidenBadgeIds;
    public List<int> hidenNameIds;

    public Sprite GetFrameRewardSprite(int frameIndex)
    {
        return frameRewards.Find(x => x.frameIndex == frameIndex).spriteFrame;
    }

    public Sprite GetAvatarRewardSprite(int avatarIndex)
    {
        return avatarRewards.Find(x => x.avatarIndex == avatarIndex).spriteAvatar;
    }

    public Sprite GetBadgeRewardSprite(int badgeIndex)
    {
        return badgeRewards.Find(x => x.badgeIndex == badgeIndex).spriteBadge;
    }

    /// <summary>
    /// Thiếu entry thì trả null thay vì ném NullReferenceException — danh sách
    /// name effect còn đang thêm dần, không phải id nào cũng đã có art thưởng.
    /// </summary>
    public Sprite GetNameRewardSprite(int nameIndex)
    {
        return nameRewards.Find(x => x.nameIndex == nameIndex)?.spriteName;
    }

    public Sprite GetAvatarEditProfileSprite(int avatarIndex)
    {
        return avatarEditProfile.Find(x => x.avatarIndex == avatarIndex).spriteAvatar;
    }

    public Sprite GetFrameEditProfileSprite(int frameIndex)
    {
        return frameEditProfile.Find(x => x.frameIndex == frameIndex).spriteFrame;
    }

    public Sprite GetBadgeEditProfileSprite(int badgeIndex)
    {
        return badgeEditProfile.Find(x => x.badgeIndex == badgeIndex).spriteBadge;
    }

    public Sprite GetNameEditProfileSprite(int nameIndex)
    {
        return nameEditProfile.Find(x => x.nameIndex == nameIndex)?.spriteName;
    }

    public float GetSizeRectTransformFrameEdit(int frameIndex)
    {
        return frameEditProfile.Find(x => x.frameIndex == frameIndex).sizeRectTransformFrame;
    }

    public float GetSizeRectTransformAvatarEdit(int avatarIndex)
    {
        return avatarEditProfile.Find(x => x.avatarIndex == avatarIndex).sizeRectTransformAvatar;
    }

    public float GetSizeRectTransformBadgeEdit(int badgeIndex)
    {
        return badgeEditProfile.Find(x => x.badgeIndex == badgeIndex).sizeRectTransformBadge;
    }

    public float GetSizeRectTransformNameEdit(int nameIndex)
    {
        return nameEditProfile.Find(x => x.nameIndex == nameIndex)?.sizeRectTransformName ?? 0f;
    }

    public float GetSizeRectTransformFrameReward(int frameIndex)
    {
        return frameRewards.Find(x => x.frameIndex == frameIndex).sizeRectTransformFrame;
    }

    public float GetSizeRectTransformAvatarReward(int avatarIndex)
    {
        return avatarRewards.Find(x => x.avatarIndex == avatarIndex).sizeRectTransformAvatar;
    }

    public float GetSizeRectTransformBadgeReward(int badgeIndex)
    {
        return badgeRewards.Find(x => x.badgeIndex == badgeIndex).sizeRectTransformBadge;
    }

    public float GetSizeRectTransformNameReward(int nameIndex)
    {
        return nameRewards.Find(x => x.nameIndex == nameIndex)?.sizeRectTransformName ?? 0f;
    }

    /// <summary>
    /// Get avatar data detail with index and return null if not found
    /// </summary>
    /// <param name="avatarIndex"></param>
    /// <returns></returns>
    public ProfileDataDetail GetAvatarDataDetailWithIndex(int avatarIndex)
    {
        // return null if not found
        return UserAvatarDatas.Find(x => x.Id == avatarIndex);
    }

    /// <summary>
    /// Get frame data detail with index and return null if not found
    /// </summary>
    /// <param name="frameIndex"></param>
    /// <returns></returns>
    public ProfileDataDetail GetFrameDataDetailWithIndex(int frameIndex)
    {
        // return null if not found
        return UserFrameDatas.Find(x => x.Id == frameIndex);
    }

    /// <summary>
    /// Get badge data detail with index and return null if not found
    /// </summary>
    /// <param name="badgeIndex"></param>
    /// <returns></returns>
    public ProfileDataDetail GetBadgeDataDetailWithIndex(int badgeIndex)
    {
        // return null if not found
        return UserBadgeDatas.Find(x => x.Id == badgeIndex);
    }

    /// <summary>
    /// Get name data detail with index and return null if not found
    /// </summary>
    /// <param name="nameIndex"></param>
    /// <returns></returns>
    public ProfileDataDetail GetNameDataDetailWithIndex(int nameIndex)
    {
        return UserNameDatas.Find(x => x.Id == nameIndex);
    }

    public List<FrameData> GetDefaultFrameData()
    {
        List<FrameData> frameDatas = new List<FrameData>();
        foreach (var item in UserFrameDatas)
        {
            frameDatas.Add(new FrameData { FrameIndex = item.Id, IsUnlocked = item.Id > 6 ? false : true, HowToCollect = item.HowToCollect, requireToUnlock = item.requireToUnlock, levelRequire = item.levelRequire, priorityLevel = item.priorityLevel });
        }
        return frameDatas;
    }
    public List<AvatarData> GetDefaultAvatarData()
    {
        List<AvatarData> avatarDatas = new List<AvatarData>();
        foreach (var item in UserAvatarDatas)
        {
            avatarDatas.Add(new AvatarData { AvatarIndex = item.Id, IsUnlocked = item.Id > 6 ? false : true, HowToCollect = item.HowToCollect, requireToUnlock = item.requireToUnlock, levelRequire = item.levelRequire, priorityLevel = item.priorityLevel });
        }
        return avatarDatas;
    }

    public List<BadgeData> GetDefaultBadgeData()
    {
        List<BadgeData> badgeDatas = new List<BadgeData>();
        foreach (var item in UserBadgeDatas)
        {
            badgeDatas.Add(new BadgeData { BadgeIndex = item.Id, IsUnlocked = item.Id >= 0 ? false : true, HowToCollect = item.HowToCollect, requireToUnlock = item.requireToUnlock, levelRequire = item.levelRequire, priorityLevel = item.priorityLevel });
        }
        return badgeDatas;
    }

    public List<EffectNameData> GetDefaultNameData()
    {
        List<EffectNameData> nameDatas = new List<EffectNameData>();
        foreach (var item in UserNameDatas)
        {
            nameDatas.Add(new EffectNameData { NameIndex = item.Id, IsUnlocked = item.Id == PlayerData.DefaultEffectNameId || item.requireToUnlock == RequireToUnlock.None, HowToCollect = item.HowToCollect, requireToUnlock = item.requireToUnlock, levelRequire = item.levelRequire, priorityLevel = item.priorityLevel });
        }
        return nameDatas;
    }

    public int GetRandomAvatarIndex()
    {
        return Random.Range(0, UserAvatarDatas.Count);
    }

    public int GetRandomFrameIndex()
    {
        return Random.Range(0, UserFrameDatas.Count);
    }

    public int GetRandomBadgeIndex()
    {
        return Random.Range(-1, 0);
    }

    public int GetRandomNameIndex()
    {
        return Random.Range(-1, 0);
    }
}

[System.Serializable]
public class ProfileDataDetail
{
    public int Id;
    [PreviewField] public Sprite Sprite;
    public RequireToUnlock requireToUnlock;
    [ShowIf("requireToUnlock", RequireToUnlock.Level)] public int levelRequire;
    public string HowToCollect;
    public int priorityLevel;
}

[System.Serializable]
public class VertexGradientType
{
    
}

[System.Serializable]
public enum RequireToUnlock
{
    None,
    Level,
}

[System.Serializable]
public class FrameProfileConfig
{
    public int frameIndex;
    public float sizeRectTransformFrame;
    public Sprite spriteFrame;
}

[System.Serializable]
public class AvatarProfileConfig
{
    public int avatarIndex;
    public float sizeRectTransformAvatar;
    public Sprite spriteAvatar;
}

[System.Serializable]
public class BadgeProfileConfig
{
    public int badgeIndex;
    public float sizeRectTransformBadge;
    public Sprite spriteBadge;
}

[System.Serializable]
public class NameProfileConfig
{
    public int nameIndex;
    public float sizeRectTransformName;
    public Sprite spriteName;
}
