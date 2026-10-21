using System;
using System.Collections.Generic;
using Core;
using CoreData;
using Cysharp.Threading.Tasks;
using DataModule;
using Manager;
using Newtonsoft.Json;
using TW.Reactive.CustomComponent;
using UnityEngine;

/// <summary>
/// Ported from Mahjong. Holds profile/server/event progress data.
/// Fields that already exist in AntFood (UserData/ResourceData) are exposed as
/// non-serialized proxy properties so there is a single source of truth.
/// </summary>
[System.Serializable]
[DataGenerate(7)]
public class PlayerData : IDataModule
{
    public static PlayerData Instance => InGameDataManager.Instance.InGameData.PlayerData;
    public string Key => nameof(PlayerData);
    public int TargetVersion => 1;
    [field: SerializeField] public int Version { get; set; } = 1;
    public Reactive<int> CurrentLevel = new(1);
    public Reactive<int> HomeIndex = new(0);
    public string InstallDate => UserData.Instance.FirstSessionDay;
    public string LastPlayedDate => UserData.Instance.LastSessionDay;
    public int IapCount => UserData.Instance.IAPCount;
    public int AdsRewardCount => UserData.Instance.AdsRewardCount;
    public int AdsInterCount => UserData.Instance.AdsInterstitialCount;
    public int TotalCoinEarn => ResourceData.Instance.totalCoinsEarned;
    public int TotalCoinSpend => ResourceData.Instance.totalCoinsSpent;
    public int BoosterEarn => ResourceData.Instance.totalBoostersEarned;
    public int BoosterSpend => ResourceData.Instance.totalBoostersSpent;
    public int BoosterUsed => ResourceData.Instance.totalBoostersSpent;

    /// <summary>Name effect ai cũng có, không bao giờ bị khoá. Đeo mặc định.</summary>
    public const int DefaultEffectNameId = 0;

    // ===== Own serialized fields =====
    [field: SerializeField] public string CountryCode = "";

    [field: SerializeField] public Reactive<string> UserName { get; set; } = new();
    [field: SerializeField] public Reactive<int> AvatarIndex { get; set; } = new(0);
    [field: SerializeField] public Reactive<int> AvatarFrameIndex { get; set; } = new(0);
    [field: SerializeField] public Reactive<int> BadgeIndex { get; set; } = new(-1);
    [field: SerializeField] public Reactive<int> EffectNameID { get; set; } = new(PlayerData.DefaultEffectNameId);
    [field: SerializeField] public int WinStreak { get; set; } = 0;
    [field: SerializeField] public int FirstTryCount { get; set; } = 0;
    [field: SerializeField] public int SweetMealChampion { get; set; } = 0;
    [field: SerializeField] public int StarBlazeClaimed { get; set; } = 0;
    [field: SerializeField] public int CollectionsDone { get; set; } = 0;
    [field: SerializeField] public int CompleteTreetopMission { get; set; } = 0;
    [field: SerializeField] public List<int> LevelPlayed { get; set; } = new List<int>();

    public void SetDefault()
    {
        CurrentLevel.Value = 1;
        HomeIndex.Value = 0;
        UserName.Value = GenerateDefaultUserName();
        WinStreak = 0;
        SweetMealChampion = 0;
        StarBlazeClaimed = 0;
        AvatarIndex.Value = 0;
        AvatarFrameIndex.Value = 0;
        BadgeIndex.Value = -1;
        EffectNameID.Value = DefaultEffectNameId;
        CompleteTreetopMission = 0;
        CollectionsDone = 0;
        LevelPlayed.Clear();
    }

    public void Migrate()
    {

    }

    public void SavePlayerData()
    {
        this.SaveData();
    }

    public string ToJson()
    {
        var settings = new JsonSerializerSettings
        {
            // Formatting = Formatting.Indented,
            // ContractResolver = new ReactiveValueContractResolver(),
            // Converters = new JsonConverter[]
            // {
            //     new ReactiveValueConverter()
            // }
        };

        return JsonConvert.SerializeObject(this, settings);
    }

    public IDataModule FromJson(string json)
    {
        var settings = new JsonSerializerSettings
        {
            // ObjectCreationHandling = ObjectCreationHandling.Replace,
            // ContractResolver = new ReactiveValueContractResolver(),
            // Converters = new JsonConverter[]
            // {
            //     new ReactiveValueConverter()
            // }
        };

        return JsonConvert.DeserializeObject<PlayerData>(json, settings);
    }

    public void SetPlayerData(PlayerData playerData)
    {
        this.CurrentLevel.Value = playerData.CurrentLevel.Value;
    //    this.CountryCode = playerData.CountryCode;
    //    this.PlayerIdServer = playerData.PlayerIdServer;

        this.SaveData();
    }

    public void AddLevelPlayed(int playerCurrentLevel)
    {
        LevelPlayed.Add(playerCurrentLevel);
        if (LevelPlayed.Count > 20)
        {
            LevelPlayed.RemoveAt(0);
        }
        this.SaveData();
    }
    public int GetLastLevelPlayed()
    {
        if (LevelPlayed.Count == 0)
        {
            return 0;
        }
        return LevelPlayed[^1];
    }

    private const string UserNameRandomChars = "0123456789abcdefghijklmnopqrstuvwxyz";
    private const int UserNameRandomSuffixLength = 5;
    private const int MaxUserNameLength = 12;
    private const int MinUserNameLength = 2;
    private const char FirstAllowedUserNameChar = ' ';
    private const char LastAllowedUserNameChar = '~';

    private static string GenerateDefaultUserName()
    {
        var suffix = new char[UserNameRandomSuffixLength];
        for (int i = 0; i < UserNameRandomSuffixLength; i++)
        {
            System.Random random = new System.Random();
            suffix[i] = UserNameRandomChars[random.Next(0, UserNameRandomChars.Length)];
        }

        return $"Player_{new string(suffix)}";
    }

    /// <summary>
    /// Tên chỉ cho phép ký tự ASCII in được: chữ Latin a-z/A-Z, chữ số, khoảng trắng và các ký hiệu
    /// !@#$%^&amp;*(){}[]... Ký tự có dấu (é, ậ), CJK, emoji... đều bị chặn vì font/leaderboard không hiển thị được.
    /// </summary>
    public static bool IsValidUserName(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
            return false;

        foreach (char c in userName)
        {
            if (c < FirstAllowedUserNameChar || c > LastAllowedUserNameChar)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Save cũ có thể chứa tên đã lưu từ trước khi có luật ký tự (emoji, tiếng Việt có dấu, CJK...).
    /// Gọi lúc load data: tên không hợp lệ sẽ bị thay bằng tên default. Trả về true nếu có đổi.
    /// </summary>
    public bool EnsureValidUserName()
    {
        if (IsValidUserName(UserName?.Value))
            return false;

        UserName.Value = GenerateDefaultUserName();
        //UploadUserNameWhenReadyAsync().Forget();
        return true;
    }

    public bool TryUpdateUserNameFromPlatform(string platformUserName)
    {
        string current = UserName?.Value;
        // Người chơi đã tự đặt một tên hợp lệ thì không đụng tới.
        if (!string.IsNullOrEmpty(current) && !current.StartsWith("Player_") && IsValidUserName(current))
            return false;

        string newName = platformUserName == null ? string.Empty : platformUserName.Trim();
        if (newName.Length > MaxUserNameLength)
            newName = newName.Substring(0, MaxUserNameLength);

        // Tên từ Game Center/Play Games có thể rỗng, quá ngắn hoặc chứa dấu/CJK/emoji.
        if (newName.Length < MinUserNameLength || !IsValidUserName(newName))
        {
            // Tên hiện tại còn dùng được thì giữ, không thì rơi về tên default.
            if (IsValidUserName(current))
                return false;

            newName = GenerateDefaultUserName();
        }

        UserName.Value = newName;
        SavePlayerData();
        //UploadUserDataToServer();
        return true;
    }

    public bool IsFirstTryLevel()
    {
        if (LevelPlayed.Count < 2)
        {
            return true;
        }
        return LevelPlayed[^2] != LevelPlayed[^1];
    }
}
