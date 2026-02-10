using System;
using System.Collections.Generic;

namespace SDK
{
    [Serializable]
    public class AdScheduleUnitID
    {
        
#if UNITY_ANDROID
        public List<string> AndroidID = new List<string>();
#elif UNITY_IOS
        public List<string> IosID = new List<string>();
#endif

        private int currentID;

        public void ChangeID()
        {
            currentID++;
            if (currentID >= CurrentPlatformID.Count)
            {
                currentID = 0;
            }
        }

        public void Refresh()
        {
            currentID = 0;
        }

        public string ID => CurrentPlatformID.Count == 0 ? "" : CurrentPlatformID[currentID];

        public List<string> CurrentPlatformID
        {
            get
            {
#if UNITY_ANDROID
                return AndroidID;
#elif UNITY_IOS
                return IosID;
#else
                return null;
#endif
            }
            set
            {
#if UNITY_ANDROID
                AndroidID = value;
#elif UNITY_IOS
                IosID = value;
#endif
            }
        }

        public bool IsActive()
        {
            return CurrentPlatformID.Count > 0;
        }
    }
}