using System;
using System.Linq;
using UnityEngine;

public class ConfirmLoseEventPriority : MonoBehaviour
{
    [field: SerializeReference] private ConfirmLoseEventPriorityData[] EventPriorityData {get; set;}

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
public class ConfirmLoseEventPriorityData
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
// [Serializable]
// public class SkyJumpConfirmLoseEventPriorityData : ConfirmLoseEventPriorityData
// {
//     public override int GetPoint()
//     {
//         if (!SkyJumpManager.Instance.IsEventUnlock) return 0;
//         if (!SkyJumpManager.Instance.IsInitialize) return 0;
//         // Same rule as the lose screen: only a stage still in progress is worth warning about. Note
//         // this reads differently depending on how ModalConfirmLose was reached - the normal path runs
//         // GameManager.ShowEventOnFailed first, which has already resolved the stage and left the player
//         // NotJoin, so it scores 0; the Ice/Cage paths push the modal directly without resolving, so the
//         // player is still Join and it scores. That matches "user thua -> 0".
//         if (SkyJumpManager.Instance.CurPlayerState.Value != PlayerJumpState.Join) return 0;
//         return 40;
//     }
// }
[Serializable]
public class StarBlazeConfirmLoseEventPriorityData : ConfirmLoseEventPriorityData
{
    public override int GetPoint()
    {
        if (!StarBlazeManager.Instance.IsEventUnlock) return 0;
        if (!StarBlazeManager.Instance.IsInitialize) return 0;
        if (!StarBlazeManager.Instance.IsStartEvent) return 0;
        if (StarBlazeManager.Instance.IsCompleted()) return 0;
        if (StarBlazeData.Instance.LastWinStreak == 0) return 0;
        return 10;
    }
}

[Serializable]
public class BoatRaceConfirmLoseEventPriorityData : ConfirmLoseEventPriorityData
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
