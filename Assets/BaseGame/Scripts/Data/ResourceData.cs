using System;
using System.Collections.Generic;
using System.Globalization;
using DataModule;
using Manager;
using UnityEngine;

namespace CoreData
{
    [Serializable]
    [DataGenerate(2)]
    public class ResourceData : IDataModule
    {
        public static ResourceData Instance => InGameDataManager.Instance.InGameData.ResourceData;
        public string Key => nameof(ResourceData);
        public int TargetVersion => 1;
        [field: SerializeField] public int Version { get; set; } = 1;
        [field: SerializeField] public List<GameResource> Resources { get; set; }
        [field: SerializeField] public string LastAutoLifeAdd { get; set; }
        [field: SerializeField] public string InfinityLifeExpireTime { get; set; }
        [field: SerializeField] public int totalCoinsEarned { get; set; }
        [field: SerializeField] public int totalCoinsSpent { get; set; }
        [field: SerializeField] public int totalBoostersEarned { get; set; }
        [field: SerializeField] public int totalBoostersSpent { get; set; }
        [field: SerializeField] private bool shuffleBoosterMigrationApplied;
        [field: SerializeField] private bool startGoldRemoteConfigApplied;
        public List<ClaimGameResourceGroup> ClaimGameResourceGroupList = new();

        public ResourceData()
        {
            InitDefaultValues();
        }

        public void SetDefault()
        {
            InitDefaultValues();
            LastAutoLifeAdd = null;
            InfinityLifeExpireTime = null;
            shuffleBoosterMigrationApplied = false;
            startGoldRemoteConfigApplied = false;
        }

        private void InitDefaultValues()
        {
            Resources = new List<GameResource>()
            {
                new GameResource(ResourceType.Life, 5),
                new GameResource(ResourceType.Money, 1000),
                new GameResource(ResourceType.Hint, 0),
                new GameResource(ResourceType.Undo, 0),
                new GameResource(ResourceType.Shuffle, 0),
            };
            totalBoostersEarned = 0;
            totalBoostersSpent = 0;
            totalCoinsEarned = 0;
            totalCoinsSpent = 0;
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
            return DataSerializer.FromJson<ResourceData>(json);
        }

        public bool ApplyDataMigrations()
        {
            if (shuffleBoosterMigrationApplied)
            {
                return false;
            }

            shuffleBoosterMigrationApplied = true;
            return true;
        }

        public bool ApplyStartGoldRemoteConfig(int startGold, bool isNewUser)
        {
            if (startGoldRemoteConfigApplied)
            {
                return false;
            }

            startGoldRemoteConfigApplied = true;
            if (isNewUser)
            {
                SetResourceValue(ResourceType.Money, startGold);
            }

            return true;
        }

        public GameResource GetResource(ResourceType type)
        {
            for (int i = 0; i < Resources.Count; i++)
            {
                GameResource resource = Resources[i];
                if (resource.Type == type)
                {
                    return resource;
                }
            }

            GameResource newResource = new GameResource(type, 0);
            Resources.Add(newResource);
            return newResource;
        }

        public bool IsEnoughResourceValue(ResourceType type, int value)
        {
            GameResource resource = GetResource(type);
            return resource.Value.Value >= value;
        }

        public void SubResourceValue(ResourceType type, int value)
        {
            if (type == ResourceType.Money)
            {
                totalCoinsSpent += value;
            }
            else if (type.IsBooster())
            {
                totalBoostersSpent += value;
            }
            GameResource resource = GetResource(type);
            resource.Value.Value -= value;
        }

        public void AddResourceValue(ResourceType type, int value)
        {
            if (type == ResourceType.Money)
            {
                totalCoinsEarned += value;
            }
            else if (type.IsBooster())
            {
                totalBoostersEarned += value;
            }
            GameResource resource = GetResource(type);
            resource.Value.Value += value;
        }

        public void SetResourceValue(ResourceType type, int value)
        {
            GameResource resource = GetResource(type);
            resource.Value.Value = value;
        }

        /// <summary>Ported from Mahjong — dùng khi load cloud save (PlayerDataManager).</summary>
        public void SetResourceData(ResourceData resourceData)
        {
            this.Resources = resourceData.Resources;
            this.InfinityLifeExpireTime = resourceData.InfinityLifeExpireTime;
            this.LastAutoLifeAdd = resourceData.LastAutoLifeAdd;
            this.totalCoinsEarned = resourceData.totalCoinsEarned;
            this.totalCoinsSpent = resourceData.totalCoinsSpent;
            this.totalBoostersEarned = resourceData.totalBoostersEarned;
            this.totalBoostersSpent = resourceData.totalBoostersSpent;
            this.SaveData();
        }

        public void AddClaimGameResourceGroup(ClaimGameResourceGroup resourceGroup)
        {
            ClaimGameResourceGroupList.Add(resourceGroup);
            this.SaveData();
        }
        public void RemoveClaimGameResourceGroup(ClaimGameResourceGroup resourceGroup)
        {
            ClaimGameResourceGroupList.Remove(resourceGroup);
            this.SaveData();
        }
        public void ClearClaimGameResourceGroup()
        {
            ClaimGameResourceGroupList.Clear();
            this.SaveData();
        }
        public void CollectAllClaimGameResourceGroup()
        {
            for (int i = 0; i < ClaimGameResourceGroupList.Count; i++)
            {
                for (int j = 0; j < ClaimGameResourceGroupList[i].GameResourceList.Count; j++)
                {
                    AddResourceValue(ClaimGameResourceGroupList[i].GameResourceList[j].Type, ClaimGameResourceGroupList[i].GameResourceList[j].Amount);
                }
            }
            ClaimGameResourceGroupList.Clear();
            this.SaveData();
        }
    }

    [System.Serializable]
    public class ClaimGameResourceGroup
    {
        public ClaimGameResourceType ClaimGameResourceType = ClaimGameResourceType.Default;
        public string TitleName = "Congratulations!";
        public List<GameResource> GameResourceList = new List<GameResource>();
        public string Placement;
        public string ClaimType;
    }

    public enum ClaimGameResourceType
    {
        Default,
        GamePlay,
    }
}
