using UnityEngine;
using UnityEngine.EventSystems;
using System;
using Cysharp.Threading.Tasks;

public class HoldToPreviewButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public Func<UniTask> OnHoldStart;
    public Func<UniTask> OnHoldEnd;

    private bool isHolding = false;

    public void OnPointerDown(PointerEventData eventData)
    {
        isHolding = true;
        OnHoldStart?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isHolding = false;
        OnHoldEnd?.Invoke();
    }

    // optional: nếu muốn an toàn khi drag ra ngoài
    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isHolding) return;

        isHolding = false;
        OnHoldEnd?.Invoke();
    }
}