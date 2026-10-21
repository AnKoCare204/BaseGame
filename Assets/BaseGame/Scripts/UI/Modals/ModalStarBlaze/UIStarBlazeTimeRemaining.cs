using System;
using R3;
using R3.Triggers;
using TMPro;
using TW.Utility.Extension;
using UnityEngine;

public class UIStarBlazeTimeRemaining : MonoBehaviour
{
    [field: SerializeField] private TextMeshProUGUI TextTime {get; set;}

    private void Start()
    {
        this.UpdateAsObservable().Subscribe(OnTimeRemainingChanged).AddTo(this);
    }
    private void OnTimeRemainingChanged(Unit _)
    {
        TimeSpan timeRemaining = StarBlazeManager.Instance.StarBlazeTimeRemaining;
        TextTime.text = timeRemaining.TimeSpanToStringUI();
    }
}