using System;
using System.Collections.Generic;
using R3;
using Sirenix.OdinInspector;
using TW.Reactive.CustomComponent;
using TW.Utility.DesignPattern;
using UnityEngine;

namespace Manager
{
    public class PlayerResourceManager : Singleton<PlayerResourceManager>
    {
        private void Start()
        {

        }

        private void Update()
        {

        }

        #region Resource
        
        public ReactiveValue<float> GetResourceValue(CurrencyType type)
        {
            return ResourceDataSave.Instance.GetReactiveValue(type);
        }
        
        public bool IsEnoughResourceValue(CurrencyType type, float value)
        {
            return GetResourceValue(type).Value + 0.01f >= value;
        }
        public bool IsEnoughResourceValue(SpecialResourceType type, float value)
        {
            if (type == SpecialResourceType.AdsReward) return true;
            return ResourceDataSave.Instance.GetReactiveValue(type).Value + 0.01f >= value;
        }   
        
        public void SetResourceValue(CurrencyType type, float value)
        {
            ResourceDataSave.Instance.SetResourceValue(type, value);
            ResourceDataSave.Instance.SaveData();

        }
        
        public void AddResourceValue(CurrencyType type, float value)
        {
            ResourceDataSave.Instance.AddResourceValue(type, value);
            
            ResourceDataSave.Instance.SaveData();
        }
        public void SubResourceValue(CurrencyType type, float value)
        {
            ResourceDataSave.Instance.SubResourceValue(type, value);
        }
        public void SubResourceValue(SpecialResourceType type, float value)
        {
            ResourceDataSave.Instance.SubResourceValue(type, value);
        }
        
        public void AddResourceValue(List<GameResource> gameResources, string placement, string claimType)
        {
            foreach (GameResource gameResource in gameResources)
            {
                AddResourceValue(gameResource, placement, claimType);
            }
        }
        public void AddResourceValue(GameResource[] gameResources, string placement, string claimType)
        {
            foreach (GameResource gameResource in gameResources)
            {
                AddResourceValue(gameResource, placement, claimType);
            }
        }
        public void AddResourceValue(GameResource gameResource, string placement, string claimType)
        {
            switch (gameResource.Type)
            {
                case ResourceType.None:
                    break;
                case ResourceType.Currency:
                    ResourceDataSave.Instance.AddResourceValue((CurrencyType)gameResource.SpecificType, gameResource.Value);
                    break;
            }
        }

        public bool IsEnoughResource(GameResource cost)
        {
            switch (cost.Type)
            {
                case ResourceType.None:
                    return false;
                case ResourceType.Currency:
                    return IsEnoughResourceValue((CurrencyType)cost.SpecificType, cost.Value);
                default:
                    return false;
            }
        }

        public void ConsumeResource(GameResource resource, string placement, string spendType)
        {
            switch (resource.Type)
            {
                case ResourceType.None:
                    break;
                case ResourceType.Currency:
                    SubResourceValue((CurrencyType)resource.SpecificType, resource.Value);
                    break;
            }
        }
        #endregion


    }
}
