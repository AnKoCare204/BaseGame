using TW.Reactive.CustomComponent;
using System;
using R3;
using UnityEngine;

namespace CoreData
{
    /// <summary>
    /// Represents a game resource with type, value and expiration information
    /// </summary>
    [Serializable]
    public class GameResource
    {
        [field: SerializeField] public ResourceType Type { get; private set; }
        [field: SerializeField] public Reactive<int> Value { get; private set; }

        /// <summary>Alias for Mahjong-ported code (Mahjong GameResource uses Amount).</summary>
        public Reactive<int> Amount => Value;
        public GameResource()
        {
        }

        public GameResource(ResourceType type, int value)
        {
            InitializeResource(type, value);
        }

        private void InitializeResource(ResourceType type, int value)
        {
            Type = type;
            Value = new Reactive<int>(value);
        }
    }
}