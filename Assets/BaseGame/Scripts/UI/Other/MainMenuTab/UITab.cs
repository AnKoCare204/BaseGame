using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TW.Utility.CustomComponent;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UITab : ACachedMonoBehaviour, IUITabLifecycleEvent
{
    private static List<ScrollRect> ScrollRects { get; set; }  = new List<ScrollRect>();
    public static void RegisterScrollRect(ScrollRect scrollRect)
    {
        if(ScrollRects.Contains(scrollRect)) return;
        ScrollRects.Add(scrollRect);
    }
    public static void UnregisterScrollRect(ScrollRect scrollRect)
    {
        if(!ScrollRects.Contains(scrollRect)) return;
        ScrollRects.Remove(scrollRect);
    }
    private static void SetScrollRectsInteractable(bool interactable)
    {
        foreach (ScrollRect scrollRect in ScrollRects)
        {
            scrollRect.enabled = interactable;
        }
    }
    
    
    [field: SerializeField] public int TabIndex {get; private set;}
    [field: SerializeField] public MonoBehaviour MainView {get; private set;}
    [field: SerializeField] public CanvasGroup CanvasGroup {get; private set;}


    public void SetLocalPosition(Vector3 position)
    {
        Transform.localPosition = position;
    }
    public UITab SetVisible(bool visible)
    {
        // CanvasGroup.alpha = visible ? 1 : 0;
        return this;
    }
    public UITab SetInteractable(bool interactable)
    {
        CanvasGroup.interactable = interactable;
        CanvasGroup.blocksRaycasts = interactable;
        SetScrollRectsInteractable(interactable);
        return this;
    }
    public void ReleaseUI()
    {
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        if (EventSystem.current.currentSelectedGameObject)
        {
            ExecuteEvents.Execute(EventSystem.current.currentSelectedGameObject, eventData, ExecuteEvents.pointerUpHandler);
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    public UniTask Initialize(Memory<object> args)
    {
        if (MainView is IUITabLifecycleEvent lifecycleEvent)
        {
            lifecycleEvent.Initialize(args).Forget();
        }
        return UniTask.CompletedTask;
    }

    public void OnTabEnter(Memory<object> args)
    {
        if (MainView is IUITabLifecycleEvent lifecycleEvent)
        {
            lifecycleEvent.OnTabEnter(args);
        }
    }

    public void OnTabExit(Memory<object> args)
    {
        if (MainView is IUITabLifecycleEvent lifecycleEvent)
        {
            lifecycleEvent.OnTabExit(args);
        }
    }
}