using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Ô cắm bể (TankSlot.prefab): vị trí cố định trong prefab Level, có thể khóa (booster UnlockTank mở).
/// Bể (TankView = thân bể + cá đậu) rơi vào ô khi model có đơn và mang cá bay đi khi đủ cá.
/// Logic đơn nằm trong BoardModel.TankEntry; ô chỉ trình diễn và giữ TapCollider để chọn bể cho Magnet / Swap.
/// </summary>
public class TankSlotView : MonoBehaviour
{
    private static readonly Dictionary<Collider, TankSlotView> ByCollider = new();
    
    public static bool TryGetByCollider(Collider collider, out TankSlotView slot)
    {
        if (collider != null && ByCollider.TryGetValue(collider, out slot) && slot != null) return true;
        slot = null;
        return false;
    }
    
    [field: SerializeField] public int Index { get; private set; }
    [field: SerializeField] public Collider TapCollider { get; private set; }
    [field: SerializeField] public TankView TankPrefab { get; private set; }
    [field: SerializeField] public Transform TankAnchor { get; private set; }
    [field: SerializeField] public GameObject LockObject { get; private set; }
    [field: SerializeField] public FeelAnimation HideAnimation {get; private set;}
    public TankView Tank { get; private set; }
    public int FishId => Tank != null ? Tank.FishId : -1;
    public int Goal => Tank != null ? Tank.Goal : 0;
    public bool IsLocked { get; private set; }
    public bool IsReady => Tank != null && !Tank.IsMoving;
    public IReadOnlyList<Fish> Fishes => Tank != null ? Tank.Fishes : Array.Empty<Fish>();

    private void OnEnable()
    {
        if (TapCollider != null) ByCollider[TapCollider] = this;
    }

    private void OnDisable()
    {
        if (TapCollider != null && ByCollider.TryGetValue(TapCollider, out TankSlotView owner) && owner == this) ByCollider.Remove(TapCollider);
    }

    public void Setup(int index)
    {
        Index = index;
        Clear();
        SetLocked(false);
    }
    public void SetLocked(bool locked)
    {
        IsLocked = locked;
        if (locked) Clear();
        if (LockObject != null) LockObject.SetActive(locked);
    }
    public async UniTask SetOrderAsync(int fishId, int goal, CancellationToken ct)
    {
        if (IsLocked) return;
        if (Tank != null && fishId >= 0 && Tank.FishId == fishId && Tank.Goal == goal) return;
        if (Tank != null) await ReleaseTankAsync(ct);
        if (fishId < 0) return;
        if (TankPrefab == null)
        {
            Debug.LogError("TankSlotView: chưa gán TankPrefab (Tank.prefab).", this);
            return;
        }

        Transform anchor = TankAnchor != null ? TankAnchor : transform;
        TankView tank = Instantiate(TankPrefab, anchor);
        tank.name = $"Tank_{fishId}";
        tank.transform.localRotation = Quaternion.identity;
        tank.transform.localScale = Vector3.one;
        tank.Initialize(fishId, goal);
        Tank = tank;
        await tank.DropInAsync(ct);
    }

    private async UniTask ReleaseTankAsync(CancellationToken ct)
    {
        TankView tank = Tank;
        Tank = null;
        if (tank != null) await tank.ReleaseAsync(ct);
    }

    public Transform PrepareArrival(Fish fish) => Tank != null ? Tank.PrepareArrival(fish) : transform;
    
    public void Park(Fish fish, Transform anchor)
    {
        if (Tank != null) Tank.Park(fish, anchor);
    }

    public List<Fish> TakeAll() => Tank != null ? Tank.TakeAll() : new List<Fish>();
    public UniTask PlayComplete(CancellationToken ct) => ReleaseTankAsync(ct);
    public void Clear()
    {
        if (Tank == null) return;
        Destroy(Tank.gameObject);
        Tank = null;
    }

    public void Hide()
    {
        HideAnimation.Play();
    }
}

