using System;
using CoreData;
using Cysharp.Threading.Tasks;
using UnityEngine;

[Serializable]
public class InGameData
{
    [field: SerializeField] public ResourceDataSave ResourceDataSave { get; set; } = new();
    [field: SerializeField] public UserProfileDataSave UserProfileDataSave { get; set; } = new();
    [field: SerializeField] public SettingDataSave SettingDataSave { get; set; } = new();

    public void LoadData()
    {
        UserProfileDataSave = DataSerializer.LoadDataFromPrefs<UserProfileDataSave>();
        ResourceDataSave = DataSerializer.LoadDataFromPrefs<ResourceDataSave>();
        SettingDataSave = DataSerializer.LoadDataFromPrefs<SettingDataSave>();
    }

    public void SaveAllData()
    {
        UserProfileDataSave.SaveData();
        ResourceDataSave.SaveData();
        SettingDataSave.SaveData();
    }
    public void LoadDataFromFile()
    {
        UserProfileDataSave = DataSerializer.LoadDataLocal<UserProfileDataSave>();
        ResourceDataSave = DataSerializer.LoadDataLocal<ResourceDataSave>();
        SettingDataSave = DataSerializer.LoadDataLocal<SettingDataSave>();
    }

    // public async UniTask ForceSaveData()
    // {
    //     if (UserProfileDataSave.IsDirty)
    //     {
    //         UserProfileDataSave.IsDirty = false;
    //         await DataSerializer.SaveLocal<UserProfileDataSave>(UserProfileDataSave);
    //     }
    //     if (ResourceDataSave.IsDirty)
    //     {
    //         ResourceDataSave.IsDirty = false;
    //         await DataSerializer.SaveLocal<ResourceDataSave>(ResourceDataSave);
    //     }
    //     if (SettingDataSave.IsDirty)
    //     {
    //         SettingDataSave.IsDirty = false;
    //         await DataSerializer.SaveLocal<SettingDataSave>(SettingDataSave);
    //     }
    //     if (QuestData.IsDirty)
    //     {
    //         QuestData.IsDirty = false;
    //         await DataSerializer.SaveLocal<QuestDataSave>(QuestData);
    //     }
    //     if (SkewerHuntDataSave.IsDirty)
    //     {
    //         SkewerHuntDataSave.IsDirty = false;
    //         await DataSerializer.SaveLocal<SkewerHuntDataSave>(SkewerHuntDataSave);
    //     }
    //     if (JungleTreasureDataSave.IsDirty)
    //     {
    //         JungleTreasureDataSave.IsDirty = false;
    //         await DataSerializer.SaveLocal<JungleTreasureDataSave>(JungleTreasureDataSave);
    //     }
    //     if (ProfileDataSave.IsDirty)
    //     {
    //         ProfileDataSave.IsDirty = false;
    //         await DataSerializer.SaveLocal<ProfileDataSave>(ProfileDataSave);
    //     }
    //     if (LavaQuestDataSave.IsDirty)
    //     {
    //         LavaQuestDataSave.IsDirty = false;
    //         await DataSerializer.SaveLocal<LavaQuestDataSave>(LavaQuestDataSave);
    //     }
    //     if (MailBoxDataSave.IsDirty)
    //     {
    //         MailBoxDataSave.IsDirty = false;
    //         await DataSerializer.SaveLocal<MailBoxDataSave>(MailBoxDataSave);
    //     }
    //     if (DailyRewardDataSave.IsDirty)
    //     {
    //         DailyRewardDataSave.IsDirty = false;
    //         await DataSerializer.SaveLocal<DailyRewardDataSave>(DailyRewardDataSave);
    //     }
    //     if (DelayResourceDataSave.IsDirty)
    //     {
    //         DelayResourceDataSave.IsDirty = false;
    //         await DataSerializer.SaveLocal<DelayResourceDataSave>(DelayResourceDataSave);
    //     }
    //     if (GamePersistentDataSave.IsDirty)
    //     {
    //         GamePersistentDataSave.IsDirty = false;
    //         await DataSerializer.SaveLocal<GamePersistentDataSave>(GamePersistentDataSave);
    //     }
    //     if (HotPotDataSave.IsDirty)
    //     {
    //         HotPotDataSave.IsDirty = false;
    //         await DataSerializer.SaveLocal<HotPotDataSave>(HotPotDataSave);
    //     }
    //     if (BattlePassDataSave.IsDirty)
    //     {
    //         BattlePassDataSave.IsDirty = false;
    //         await DataSerializer.SaveLocal<BattlePassDataSave>(BattlePassDataSave);
    //     }
    //     if (MoonCakeDataSave.IsDirty)
    //     {
    //         MoonCakeDataSave.IsDirty = false;
    //         await DataSerializer.SaveLocal<MoonCakeDataSave>(MoonCakeDataSave);
    //     }
    //     if (CardCollectionDataSave.IsDirty)
    //     {
    //         CardCollectionDataSave.IsDirty = false;
    //         await DataSerializer.SaveLocal<CardCollectionDataSave>(CardCollectionDataSave);
    //     }
    //     if (FoodieLabDataSave.IsDirty)
    //     {
    //         FoodieLabDataSave.IsDirty = false;
    //         await DataSerializer.SaveLocal<FoodieLabDataSave>(FoodieLabDataSave);
    //     }
    //     if (JetpackRaceDataSave.IsDirty)
    //     {
    //         JetpackRaceDataSave.IsDirty = false;
    //         await DataSerializer.SaveLocal<JetpackRaceDataSave>(JetpackRaceDataSave);
    //     }
    // }
    // public void SetDirtyAll()
    // {
    //     UserProfileDataSave.Instance.SaveData();
    //     ResourceDataSave.Instance.SaveData();
    //     SettingDataSave.Instance.SaveData();
    //     QuestDataSave.Instance.SaveData();
    //     SkewerHuntDataSave.Instance.SaveData();
    //     JungleTreasureDataSave.Instance.SaveData();
    //     ProfileDataSave.Instance.SaveData();
    //     LavaQuestDataSave.Instance.SaveData();
    //     MailBoxDataSave.Instance.SaveData();
    //     DailyRewardDataSave.Instance.SaveData();
    //     DelayResourceDataSave.Instance.SaveData();
    //     GamePersistentDataSave.Instance.SaveData();
    //     HotPotDataSave.Instance.SaveData();
    //     BattlePassDataSave.Instance.SaveData();
    //     MoonCakeDataSave.Instance.SaveData();
    //     CardCollectionDataSave.Instance.SaveData();
    //     FoodieLabDataSave.Instance.SaveData();
    //     JetpackRaceDataSave.Instance.SaveData();
    // }
}