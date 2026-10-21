using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using LitMotion;
using TW.UGUI.Core.Screens;
using TW.Utility.CustomComponent;
using UnityEngine;
using Screen = UnityEngine.Screen;

public class UIMainMenuTab : ACachedMonoBehaviour, IScreenLifecycleEventSimple
{
    public static Action<int> ForceOpenTab { get; set; }
    public static Func<bool> IsActiveSwiping { get; set; }
    // Mỗi thứ che Home (popup, modal, toast, loading, chuyển màn) giữ khóa riêng; chỉ vuốt được tab khi không còn ai giữ.
    // Nhờ vậy đóng toast / loading không mở lại tab khi DailyReward, FillHeart... còn đang mở.
    private static readonly HashSet<object> TabLockOwners = new();
    public static bool IsTabLocked => TabLockOwners.Count > 0;

    public static void SetTabLock(object owner, bool locked)
    {
        if (locked) TabLockOwners.Add(owner);
        else TabLockOwners.Remove(owner);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetTabLocks()
    {
        TabLockOwners.Clear();
    }
    private enum SwipeStateType
    {
        Wait,
        Active,
        Disable
    }
    [field: SerializeField] public RectTransform SwipeRect { get; private set; }
    [field: SerializeField] public Transform CenterPosition { get; private set; }
    [field: SerializeField] public Transform RightPosition { get; private set; }
    [field: SerializeField] public Transform SelectTab { get; private set; }
    [field: SerializeField] public UITab LastTabUI { get; private set; }
    [field: SerializeField] public UITab CurrentTabUI { get; private set; }
    [field: SerializeField] public float CurrentTabIndex { get; private set; }
    [field: SerializeField] public float FlexibleWidthSelect { get; private set; }
    [field: SerializeField] public List<UITab> AllUITab { get; private set; } = new();
    [field: SerializeField] public List<TabButton> AllTabButton { get; private set; } = new();
    private MotionHandle MotionHandle { get; set; }
    private Vector3 CurrentPosition { get; set; }
    private Vector3 StartSwipePosition { get; set; }
    private Vector3 CurrentSwipePosition { get; set; }
    private SwipeStateType SwipeState { get; set; }
    private float StartSwipeIndex { get; set; }
    private float ThresholdSwipe { get; set; } = 10f;
    private bool IsSwiping { get; set; }
    private void Awake()
    {
        foreach (TabButton tabButton in AllTabButton)
        {
            tabButton.AddListener(OnClickTabButton);
        }
        ForceOpenTab += OpenTabByIndex;
        IsActiveSwiping = IsActiveSwipingCallback;
    }

    private bool IsActiveSwipingCallback()
    {
         return SwipeState == SwipeStateType.Active;
    }

    private void OnDestroy()
    {
        ForceOpenTab -= OpenTabByIndex;
    }
    private void OnInit()
    {
        CurrentTabIndex = AllUITab.Count / 2;
        OpenTab(AllUITab[AllUITab.Count / 2]);
        float flexibleWidth = GetFlexibleWidth(FlexibleWidthSelect, AllTabButton.Count);
        TabButton.SelectFlexibleWith = flexibleWidth;
    }
    private void OpenTabByIndex(int index)
    {
        if (index < 0 || index >= AllUITab.Count) return;
        OpenTab(AllUITab[index]);
    }
    private float GetFlexibleWidth(float widthRatio, int totalElements)
    {
        // Số phần tử còn lại
        int otherCount = totalElements - 1;

        // Giải phương trình: x / (otherCount + x) = widthRatio
        float x = (widthRatio * otherCount) / (1 - widthRatio);
        return x;
    }
    public async UniTask Initialize(Memory<object> args)
    {
        foreach (UITab uiTab in AllUITab)
        {
            await uiTab.SetVisible(false).SetInteractable(false).Initialize(args);
        }
        await UniTask.NextFrame(cancellationToken: this.GetCancellationTokenOnDestroy());
        OnInit();
    }
    private void Update()
    {
        HandleSwipe();
    }
    private void HandleSwipe()
    {
        if (IsTabLocked) return;
        // FishSort dùng Input System package: đọc con trỏ qua Pointer.current (chuột hoặc chạm).
        UnityEngine.InputSystem.Pointer pointer = UnityEngine.InputSystem.Pointer.current;
        if (pointer == null) return;
        Vector2 position = pointer.position.ReadValue();
        if (pointer.press.wasPressedThisFrame && SwipeState != SwipeStateType.Disable)
        {
            StartSwipe(position);
        }
        else if (pointer.press.isPressed && SwipeState != SwipeStateType.Disable)
        {
            ContinueSwipe(position);
        }
        else if (pointer.press.wasReleasedThisFrame)
        {
            EndSwipe();
        }
    }
    private void StartSwipe(Vector2 position)
    {
        IsSwiping = RectTransformUtility.RectangleContainsScreenPoint(SwipeRect, position);
        StartSwipePosition = position;
        CurrentSwipePosition = position;
    }
    private void ContinueSwipe(Vector2 position)
    {
        if (!IsSwiping) return;
        CurrentSwipePosition = position;
        Vector2 swipeDirection = CurrentSwipePosition - StartSwipePosition;
        if (swipeDirection.magnitude < ThresholdSwipe && SwipeState == SwipeStateType.Wait) return;
        if (SwipeState == SwipeStateType.Wait)
        {
            if (swipeDirection.sqrMagnitude < 1f) return;
            SwipeState = Mathf.Abs(swipeDirection.x) > Mathf.Abs(swipeDirection.y)
                ? SwipeStateType.Active
                : SwipeStateType.Disable;
            if (SwipeState == SwipeStateType.Active)
            {
                float fixedIndex = Mathf.Clamp(CurrentTabIndex, 0, AllUITab.Count - 1);
                for (int i = 0; i < AllUITab.Count; i++)
                {
                    AllUITab[i].SetVisible(Mathf.Abs(fixedIndex - i) < 1.1f).SetInteractable(false);
                }

                StartSwipeIndex = CurrentTabIndex;
                CurrentTabUI.ReleaseUI();
            }
        }

        if (SwipeState == SwipeStateType.Active)
        {
            float distance = StartSwipePosition.x - CurrentSwipePosition.x;
            float fixedIndex = Mathf.Clamp(StartSwipeIndex + distance / Screen.width, 0, AllUITab.Count - 1);
            UpdateTabUIPosition(fixedIndex);
        }
    }
    private void EndSwipe()
    {
        if (!IsSwiping) return;
        IsSwiping = false;
        if (SwipeState == SwipeStateType.Active)
        {
            if (Mathf.Abs(StartSwipeIndex - CurrentTabIndex) > 0.15f)
            {
                int currentTabIndex = AllUITab.IndexOf(CurrentTabUI);
                int nextTabIndex = currentTabIndex + (StartSwipeIndex < CurrentTabIndex ? 1 : -1);
                nextTabIndex = Mathf.Clamp(nextTabIndex, 0, AllUITab.Count - 1);
                // AllTabButton[nextTabIndex].ForceClick();
                OpenTab(AllUITab[nextTabIndex]);
            }
            else
            {
                OpenTab(CurrentTabUI);
            }
        }
        else
        {
            SwipeState = 0;
        }
    }
    public void OnClickTabButton(TabButton tabButton)
    {
        // Debug.Log("Click tab button: " + tabButton.name);
        int index = AllTabButton.IndexOf(tabButton);
        OpenTab(AllUITab[index]);
    }
    private Vector3 GetRightPosition()
    {
        return RightPosition.localPosition;
    }
    private Vector3 GetCenterPosition()
    {
        return CenterPosition.localPosition;
    }
    private void OpenTab(UITab tab)
    {
        // if (CurrentTabUI == tab) return;
        SwipeState = SwipeStateType.Disable;
        LastTabUI = CurrentTabUI;
        LastTabUI?.SetInteractable(false);

        CurrentTabUI = tab.SetVisible(true);

        CurrentPosition = CurrentTabUI.Transform.localPosition;
        float startIndex = CurrentTabIndex;
        float targetIndex = AllUITab.IndexOf(CurrentTabUI);
        MotionHandle.TryCancel();
        float time = Mathf.Clamp(Mathf.Abs(startIndex - targetIndex) * 0.3f, 0.3f, 0.6f);
        MotionHandle = LMotion.Create(startIndex, targetIndex, time)
            .WithEase(Ease.OutQuart)
            .WithOnComplete(OnUpdateTabUIPositionComplete)
            .Bind(UpdateTabUIPosition)
            .AddTo(this);
        return;
        void OnUpdateTabUIPositionComplete()
        {
            for (int i = 0; i < AllUITab.Count; i++)
            {
                if (AllUITab[i] == CurrentTabUI) continue;
                AllUITab[i].SetVisible(false);
            }

            LastTabUI?.OnTabExit(null);
            CurrentTabUI.OnTabEnter(null);
            CurrentTabUI.SetInteractable(true);
            SwipeState = SwipeStateType.Wait;
        }
    }
    
    private void UpdateTabUIPosition(float index)
    {
        CurrentTabIndex = index;
        for (int i = 0; i < AllUITab.Count; i++)
        {
            AllUITab[i].SetLocalPosition(GetCenterPosition() + GetRightPosition() * (i - CurrentTabIndex));
        }

        int lowerIndex = Mathf.Clamp(Mathf.FloorToInt(index), 0, AllUITab.Count);
        int upperIndex = Mathf.Clamp(Mathf.CeilToInt(index), 0, AllUITab.Count);
        float fraction = (index - lowerIndex) / 1;
        for (int i = 0; i < AllTabButton.Count; i++)
        {
            if (i == upperIndex || i == lowerIndex) continue;
            AllTabButton[i].SetFlexible(1);
        }

        AllTabButton[upperIndex].SetFlexible(1 - fraction);
        AllTabButton[lowerIndex].SetFlexible(fraction);
        SelectTab.position = Vector3.Lerp(AllTabButton[lowerIndex].Transform.position,
            AllTabButton[upperIndex].Transform.position, fraction);

    }

    public void Cleanup()
    {
        for (int i = 0; i < AllUITab.Count; i++)
        {
            AllUITab[i].OnTabExit(null);
        }
    }
}