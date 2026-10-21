using System;
using UnityEngine;

public class TankStar : MonoBehaviour
{
    [field: SerializeField] public GameObject ActiveGroup { get; set; }
    [field: SerializeField] public FeelAnimation FeelAnimation { get; set; }
    private bool IsActive { get; set; }

    private void Awake()
    {
        Refresh();
    }

    public void Refresh()
    {
        IsActive = false;
        ActiveGroup.SetActive(false);
    }
    public void SetActive(bool isActive)
    {
        if (IsActive == isActive) return;
        IsActive = isActive;
        FeelAnimation.Play();
    }
}