using System;
using R3;
using UnityEngine;

public class UIDailyRewardNotification : MonoBehaviour
{
    private void Start()
    {
        DailyRewardManager.Instance.dailyRewardClaimCount.Subscribe(OnDayClaimedChanged).AddTo(this);
    }
    private void OnDayClaimedChanged(int dailyRewardClaimCount)
    {
        gameObject.SetActive(dailyRewardClaimCount < 5);
    }
}