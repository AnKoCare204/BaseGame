using System;
using System.Linq;
using UnityEngine;

public class EventPriority : MonoBehaviour
{
    [field: SerializeReference] private EventPriorityData[] EventPriorityData {get; set;}

    private void Start()
    {
        EventPriorityData.AsEnumerable()
            .Where(e => e.GetPoint() != 0)
            .OrderByDescending(x =>x.GetPoint())
            .FirstOrDefault()?.SetActive();
    }
}
[Serializable]
public class EventPriorityData
{
    [field: SerializeField] public GameObject EventObject {get; set;}
    public void SetActive()
    {
        EventObject.SetActive(true);
    }

    public virtual int GetPoint()
    {
        return 0;
    }
}

[Serializable]
public class StarBlazeEventPriorityData : EventPriorityData
{
    public override int GetPoint()
    {
        if (!StarBlazeManager.Instance.IsEventUnlock) return 0;
        if (!StarBlazeManager.Instance.IsInitialize) return 0;
        if (!StarBlazeManager.Instance.IsStartEvent) return 0;
        if (StarBlazeManager.Instance.IsCompleted()) return 0;
        return 10;
    }
}

[Serializable]
public class TreetopEventPriorityData : EventPriorityData
{
    public override int GetPoint()
    {
        if (!TreetopManager.Instance.IsEventUnlock) return 0;
        if (!TreetopManager.Instance.IsInitialize) return 0;
        if (TreetopManager.Instance.IsCompleted()) return 0;
        if (TreetopManager.Instance.IsNextClaimed()) return 9;
        return 20;
    }
}

[Serializable]
public class BoatRacePriorityData : EventPriorityData
{
    public override int GetPoint()
    {
        if (!ShipRaceManager.Instance.IsEventUnlock) return 0;
        if (!ShipRaceManager.Instance.IsInitialize) return 0;
        if (ShipRaceManager.Instance.IsRunExpired) return 0;
        // A live race always earns the slot. A decided one earns it exactly once - the win screen
        // that carries the finish - and after that the slot goes to another event.
        if (ShipRaceManager.Instance.IsRunning) return 30;
        return ShipRaceManager.Instance.NeedShowEndGameResult ? 30 : 0;
    }
}