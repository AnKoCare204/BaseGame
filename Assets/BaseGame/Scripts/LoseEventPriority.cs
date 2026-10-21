using System;
using System.Linq;
using UnityEngine;

public class LoseEventPriority : MonoBehaviour
{
    [field: SerializeReference] private LoseEventPriorityData[] EventPriorityData {get; set;}

    private void Start()
    {
        EventPriorityData.AsEnumerable()
            .Where(e => e.GetPoint() != 0)
            .OrderByDescending(x =>x.GetPoint())
            .FirstOrDefault()?.SetActive();
    }

    public bool IsAnyEventCanWarning()
    {
        foreach (var loseEventPriorityData in EventPriorityData)
        {
            if (loseEventPriorityData.GetPoint() != 0)
            {
                return true;
            }
        }
        return false;
    }
}
[Serializable]
public class LoseEventPriorityData
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
public class StarBlazeLoseEventPriorityData : LoseEventPriorityData
{
    public override int GetPoint()
    {
        if (!StarBlazeManager.Instance.IsEventUnlock) return 0;
        if (!StarBlazeManager.Instance.IsInitialize) return 0;
        if (!StarBlazeManager.Instance.IsStartEvent) return 0;
        if (StarBlazeData.Instance.CurrentWinStreak <= 0) return 0;
        if (StarBlazeManager.Instance.IsCompleted()) return 0;
        return 10;
    }
}

[Serializable]
public class TreeTopLoseEventPriorityData : LoseEventPriorityData
{
    public override int GetPoint()
    {
        if (!TreetopManager.Instance.IsEventUnlock) return 0;
        if (!TreetopManager.Instance.IsInitialize) return 0;
        if (TreetopManager.Instance.IsCompleted()) return 0;
        if(TreetopData.Instance.Streak <= 0) return 0;
        if(TreetopManager.Instance.IsInCheckpoint()) return 0;
        return 20;
    }
}

[Serializable]
public class BoatRaceLoseEventPriorityData : LoseEventPriorityData
{
    public override int GetPoint()
    {
        if (!ShipRaceManager.Instance.IsEventUnlock) return 0;
        if (!ShipRaceManager.Instance.IsInitialize) return 0;
        if (ShipRaceManager.Instance.IsRunExpired) return 0;
        // Warning only: there has to be a live race with points still on the board for losing this
        // level to cost anything. A decided race has nothing left to warn about.
        if (!ShipRaceManager.Instance.IsRunning) return 0;
        if (ShipRaceData.Instance.PlayerPoint == 0) return 0;
        return 30;
    }
}
