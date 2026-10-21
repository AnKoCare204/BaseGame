using System;
using System.Globalization;
using Game.Helper;
using R3;
using R3.Triggers;
using TMPro;
using UnityEngine;

public class FillHeartTimeBar : MonoBehaviour
{
    public CanvasGroup canvasGroup;
    public TextMeshProUGUI timeTxt;

    private void Start()
    {
        this.UpdateAsObservable().Subscribe(OnFillHeartTimeChanged).AddTo(this);
    }

    private void OnFillHeartTimeChanged(Unit _)
    {
        bool isFullLife = PlayerResourceManager.Instance.HasFullLife;
        canvasGroup.alpha = isFullLife ? 0 : 1;
        if (isFullLife) return;
        
        bool isInfiniteLife = PlayerResourceManager.Instance.HasInfiniteLife;
        canvasGroup.alpha = isInfiniteLife ? 0 : 1;
        if (isInfiniteLife) return;
        
        timeTxt.text = PlayerResourceManager.Instance.TimeToNextLife.TimeSpanToStringUI();
    }
}
