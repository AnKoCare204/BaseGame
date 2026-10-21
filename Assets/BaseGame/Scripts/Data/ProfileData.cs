using DataModule;
using TW.Reactive.CustomComponent;
using UnityEngine;
using System.Collections.Generic;
using System;
using CoreNotification;
using System.Linq;
using UI.ModalProfile;
using Sirenix.OdinInspector;
using Manager;

[System.Serializable]
[DataGenerate(8)]
public class ProfileData : IDataModule
{
    public static ProfileData Instance => InGameDataManager.Instance.InGameData.ProfileData;
    public string Key => nameof(ProfileData);
    public int TargetVersion => 1;
    [field: SerializeField] public int Version { get; set; } = 1;

    [field: SerializeField] public List<FrameData> FrameDatas { get; set; } = new();
    [field: SerializeField] public List<AvatarData> AvatarDatas { get; set; } = new();
    [field: SerializeField] public List<BadgeData> BadgeDatas { get; set; } = new();
    [field: SerializeField] public List<EffectNameData> EffectNameDatas { get; set; } = new();

    [field: SerializeField] public Reactive<int> IsShowFrameNotification { get; set; } = new(-1);
    [field: SerializeField] public Reactive<int> IsShowAvatarNotification { get; set; } = new(-1);
    [field: SerializeField] public Reactive<int> IsShowBadgeNotification { get; set; } = new(-1);
    [field: SerializeField] public Reactive<int> IsShowNameNotification { get; set; } = new(-1);

    [field: SerializeField] public bool IsEditProfile { get; set; } = false;

    public void SetDefault()
    {

    }

    public void Migrate()
    {

    }

    public string ToJson()
    {
        return DataSerializer.ToJson(this);
    }

    public IDataModule FromJson(string json)
    {
        return DataSerializer.FromJson<ProfileData>(json);
    }

    public void SetProfileData(ProfileData profileData)
    {
        this.SaveData();
    }

    public void OnChangeStatusNotification<T>(T notification, int index) where T : IProfileNotificationCondition
    {
        switch (notification)
        {
            case FrameNotificationCondition:
                IsShowFrameNotification.Value = index;
                break;
            case AvatarNotificationCondition:
                IsShowAvatarNotification.Value = index;
                break;
            case BadgeNotificationCondition:
                IsShowBadgeNotification.Value = index;
                break;
            case NameNotificationCondition:
                IsShowNameNotification.Value = index;
                break;
            default:
                break;
        }
        this.SaveData();
    }

    public List<int> SortFrameData()
    {
        // Sắp xếp frame data theo yêu cầu:
        // 1. Frame unlock = true được ưu tiên trước, sắp xếp theo priority cao -> thấp
        // 2. Frame unlock = false xếp sau, sắp xếp theo frameIndex
        var sortedFrames = FrameDatas
            .OrderBy(frame => frame.IsUnlocked ? 0 : 1) // Unlock = true lên trước (0), unlock = false xuống sau (1)
            .ThenByDescending(frame => frame.IsUnlocked ? frame.priorityLevel : int.MinValue) // Nếu unlock = true, sort theo priority cao -> thấp
            .ThenBy(frame => frame.IsUnlocked ? int.MaxValue : frame.FrameIndex) // Nếu unlock = false, sort theo frameIndex
            .Select(frame => frame.FrameIndex)
            .ToList();

        return sortedFrames;
    }

    public List<int> SortAvatarData()
    {
        var sortedAvatars = AvatarDatas
            .OrderBy(avatar => avatar.IsUnlocked ? 0 : 1) // Unlock = true lên trước (0), unlock = false xuống sau (1)
            .ThenByDescending(avatar => avatar.IsUnlocked ? avatar.priorityLevel : int.MinValue) // Nếu unlock = true, sort theo priority cao -> thấp
            .ThenBy(avatar => avatar.IsUnlocked ? int.MaxValue : avatar.AvatarIndex) // Nếu unlock = false, sort theo AvatarIndex
            .Select(avatar => avatar.AvatarIndex)
            .ToList();

        return sortedAvatars;
    }

    public List<int> SortBadgeData()
    {
        var sortedBadges = BadgeDatas
            .OrderBy(badge => badge.IsUnlocked ? 0 : 1) // Unlock = true lên trước (0), unlock = false xuống sau (1)
            .ThenByDescending(badge => badge.IsUnlocked ? badge.priorityLevel : int.MinValue) // Nếu unlock = true, sort theo priority cao -> thấp
            .ThenBy(badge => badge.IsUnlocked ? int.MaxValue : badge.BadgeIndex) // Nếu unlock = false, sort theo BadgeIndex
            .Select(badge => badge.BadgeIndex)
            .ToList();

        return sortedBadges;
    }

    public List<int> SortNameData()
    {
        var sortedNames = EffectNameDatas
            .OrderBy(name => name.IsUnlocked ? 0 : 1) // Unlock = true lên trước (0), unlock = false xuống sau (1)
            .ThenByDescending(name => name.IsUnlocked ? name.priorityLevel : int.MinValue) // Nếu unlock = true, sort theo priority cao -> thấp
            .ThenBy(name => name.IsUnlocked ? int.MaxValue : name.NameIndex) // Nếu unlock = false, sort theo Id
            .Select(name => name.NameIndex)
            .ToList();

        return sortedNames;
    }

    public bool CheckUnlockProfileData(int index, TypeProfile typeProfile)
    {
        switch (typeProfile)
        {
            case TypeProfile.Avatar:
                return AvatarDatas.Find(x => x.AvatarIndex == index).IsUnlocked;
            case TypeProfile.Frame:
                return FrameDatas.Find(x => x.FrameIndex == index).IsUnlocked;
            case TypeProfile.Badge:
                return BadgeDatas.Find(x => x.BadgeIndex == index).IsUnlocked;
            case TypeProfile.Name:
                return EffectNameDatas.Find(x => x.NameIndex == index).IsUnlocked;
            default:
                break;
        }
        return false;
    }

    public string GetUserName()
    {
        return PlayerData.Instance.UserName.Value;
    }

    [Button]
    public void ClearProfile()
    {
        FrameDatas = ProfileGlobalConfig.Instance.GetDefaultFrameData();
        AvatarDatas = ProfileGlobalConfig.Instance.GetDefaultAvatarData();
        BadgeDatas = ProfileGlobalConfig.Instance.GetDefaultBadgeData();
        EffectNameDatas = ProfileGlobalConfig.Instance.GetDefaultNameData();
        this.SaveData();
    }

    [Button]
    public void SaveProfile()
    {
        this.SaveData();
    }

    public void CheckEnoughData()
    {
        if (FrameDatas.Count != ProfileGlobalConfig.Instance.GetDefaultFrameData().Count)
        {
            for (int i = 0; i < ProfileGlobalConfig.Instance.GetDefaultFrameData().Count; i++)
            {
                if (!FrameDatas.Any(x => x.FrameIndex == i))
                {

                    FrameDatas.Add(ProfileGlobalConfig.Instance.GetDefaultFrameData()[i]);
                }
            }
        }
        if (AvatarDatas.Count != ProfileGlobalConfig.Instance.GetDefaultAvatarData().Count)
        {
            for (int i = 0; i < ProfileGlobalConfig.Instance.GetDefaultAvatarData().Count; i++)
            {
                if (!AvatarDatas.Any(x => x.AvatarIndex == i))
                {
                    AvatarDatas.Add(ProfileGlobalConfig.Instance.GetDefaultAvatarData()[i]);
                }
            }
        }
        if(BadgeDatas.Count != ProfileGlobalConfig.Instance.GetDefaultBadgeData().Count)
        {
            for (int i = 0; i < ProfileGlobalConfig.Instance.GetDefaultBadgeData().Count; i++)
            {
                if (!BadgeDatas.Any(x => x.BadgeIndex == i))
                {
                    BadgeDatas.Add(ProfileGlobalConfig.Instance.GetDefaultBadgeData()[i]);
                }
            }
        }
        if(EffectNameDatas.Count != ProfileGlobalConfig.Instance.GetDefaultNameData().Count)
        {
            for (int i = 0; i < ProfileGlobalConfig.Instance.GetDefaultNameData().Count; i++)
            {
                if (!EffectNameDatas.Any(x => x.NameIndex == i))
                {
                    EffectNameDatas.Add(ProfileGlobalConfig.Instance.GetDefaultNameData()[i]);
                }
            }
        }

        this.SaveData();
    }

    public void AddProfileDataWithLevel(int level)
    {
        CheckEnoughData();

        foreach (var frameData in FrameDatas)
        {
            if (frameData.requireToUnlock == RequireToUnlock.Level && frameData.levelRequire <= level && !frameData.IsUnlocked)
            {
                frameData.IsUnlocked = true;

                OnChangeStatusNotification(new FrameNotificationCondition(), frameData.FrameIndex);
            }
        }

        foreach (var avatarData in AvatarDatas)
        {
            if (avatarData.requireToUnlock == RequireToUnlock.Level && avatarData.levelRequire <= level && !avatarData.IsUnlocked)
            {
                avatarData.IsUnlocked = true;

                OnChangeStatusNotification(new AvatarNotificationCondition(), avatarData.AvatarIndex);
            }
        }

        foreach (var badgeData in BadgeDatas)
        {
            if (badgeData.requireToUnlock == RequireToUnlock.Level && badgeData.levelRequire <= level && !badgeData.IsUnlocked)
            {
                badgeData.IsUnlocked = true;

                OnChangeStatusNotification(new BadgeNotificationCondition(), badgeData.BadgeIndex);
            }
        }

        foreach (var nameData in EffectNameDatas)
        {
            if (nameData.requireToUnlock == RequireToUnlock.Level && nameData.levelRequire <= level && !nameData.IsUnlocked)
            {
                nameData.IsUnlocked = true;

                OnChangeStatusNotification(new NameNotificationCondition(), nameData.NameIndex);
            }
        }

        this.SaveData();
    }

    /// <summary>
    /// Unlock profile cosmetic từ reward. index = Value của GameResource (Badge/Frame/Avatar).
    /// </summary>
    public void UnlockFromReward(CoreData.ResourceType type, int index)
    {
        CheckEnoughData();
        switch (type)
        {
            case CoreData.ResourceType.Avatar:
            {
                var data = AvatarDatas.Find(x => x.AvatarIndex == index);
                if (data == null || data.IsUnlocked) return;
                data.IsUnlocked = true;
                OnChangeStatusNotification(new AvatarNotificationCondition(), index);
                break;
            }
            case CoreData.ResourceType.Frame:
            {
                var data = FrameDatas.Find(x => x.FrameIndex == index);
                if (data == null || data.IsUnlocked) return;
                data.IsUnlocked = true;
                OnChangeStatusNotification(new FrameNotificationCondition(), index);
                break;
            }
            case CoreData.ResourceType.Badge:
            {
                var data = BadgeDatas.Find(x => x.BadgeIndex == index);
                if (data == null || data.IsUnlocked) return;
                data.IsUnlocked = true;
                OnChangeStatusNotification(new BadgeNotificationCondition(), index);
                break;
            }
            case CoreData.ResourceType.NameEffect:
            {
                var data = EffectNameDatas.Find(x => x.NameIndex == index);
                if (data == null || data.IsUnlocked) return;
                data.IsUnlocked = true;
                OnChangeStatusNotification(new NameNotificationCondition(), index);
                break;
            }
        }
        this.SaveData();
    }
}

[Serializable]
public class FrameData
{
    [field: SerializeField] public int FrameIndex { get; set; } = 0;
    [field: SerializeField] public bool IsUnlocked { get; set; } = false;
    [field: SerializeField] public string HowToCollect { get; set; } = "";
    [field: SerializeField] public RequireToUnlock requireToUnlock { get; set; } = RequireToUnlock.None;
    [field: SerializeField] public int levelRequire { get; set; } = 0;
    [field: SerializeField] public int priorityLevel { get; set; } = 0;
}

[Serializable]
public class AvatarData
{
    [field: SerializeField] public int AvatarIndex { get; set; } = 0;
    [field: SerializeField] public bool IsUnlocked { get; set; } = false;
    [field: SerializeField] public string HowToCollect { get; set; } = "";
    [field: SerializeField] public RequireToUnlock requireToUnlock { get; set; } = RequireToUnlock.None;
    [field: SerializeField] public int levelRequire { get; set; } = 0;
    [field: SerializeField] public int priorityLevel { get; set; } = 0;
}

[Serializable]
public class BadgeData
{
    [field: SerializeField] public int BadgeIndex { get; set; } = 0;
    [field: SerializeField] public bool IsUnlocked { get; set; } = false;
    [field: SerializeField] public string HowToCollect { get; set; } = "";
    [field: SerializeField] public RequireToUnlock requireToUnlock { get; set; } = RequireToUnlock.None;
    [field: SerializeField] public int levelRequire { get; set; } = 0;
    [field: SerializeField] public int priorityLevel { get; set; } = 0;
}

[Serializable]
public class EffectNameData
{
    [field: SerializeField] public int NameIndex { get; set; } = 0;
    [field: SerializeField] public bool IsUnlocked { get; set; } = false;
    [field: SerializeField] public string HowToCollect { get; set; } = "";
    [field: SerializeField] public RequireToUnlock requireToUnlock { get; set; } = RequireToUnlock.None;
    [field: SerializeField] public int levelRequire { get; set; } = 0;
    [field: SerializeField] public int priorityLevel { get; set; } = 0;
}