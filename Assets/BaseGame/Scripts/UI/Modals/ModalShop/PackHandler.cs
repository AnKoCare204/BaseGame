using System;
using System.Linq;
using Core.GamePlay;
using UnityEngine;

public class PackHandler : MonoBehaviour
{
    [field: SerializeField] private PackConditionGroup[] ConditionGroups { get; set; }
    private void Start()
    {
        CheckConditions();
        GameGlobalEvent.OnPurchaseIapSuccess += CheckConditions;
    }

    private void OnDestroy()
    {
        GameGlobalEvent.OnPurchaseIapSuccess -= CheckConditions;
    }
    private void CheckConditions()
    {
        if (!ConditionGroups.Any(packConditionGroup => packConditionGroup.IsMet())) return;
        gameObject.SetActive(false);
        GameGlobalEvent.ForceRebuildCanvas?.Invoke();
    }
}
[Serializable]
public class PackConditionGroup
{
    [field: SerializeReference] public IPackHandlerCondition[] PackHandlerConditions { get; set; }
    
    public bool IsMet()
    {
        return PackHandlerConditions.All(packHandlerCondition => packHandlerCondition.IsConditionMet());
    }
}