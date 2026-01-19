using System;
using Helper;
using Sirenix.OdinInspector;
using TW.Reactive.CustomComponent;
using UnityEngine;

[Serializable]
public class GameResource
{
    [field: SerializeField] public ResourceType Type { get;  set; }
    [field: ResourceSpecificTypeEditor("Type")]
    [field: SerializeField] public int SpecificType { get;  set; }
    [field: SerializeField] public ReactiveValue<float> Value { get;  set; }
    public int IntValue => Mathf.RoundToInt(Value);
    /// <summary>
    /// Default constructor for JSON deserialization
    /// </summary>
    public GameResource()
    {
        Type = ResourceType.None;
        SpecificType = 0;
        Value = new ReactiveValue<float>(0);
    }

    public GameResource(ResourceType type, int specificType, float value)
    {
        InitializeResource(type, specificType, value);
    }
    public GameResource(Enum specificType, float value)
        : this(specificType.GetResourceType(), (int)(object)specificType, value)
    {

    }

    private void InitializeResource(ResourceType type, int specificType, float value)
    {
        if (value < 0)
            throw new ArgumentException("Resource value cannot be negative", nameof(value));
                
        Type = type;
        SpecificType = specificType;
        Value = new ReactiveValue<float>(value);
    }
}