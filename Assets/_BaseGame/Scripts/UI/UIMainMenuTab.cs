using System;
using System.Collections.Generic;
using Common;
using Cysharp.Threading.Tasks;
using LitMotion;
using Manager;
using SDK;
using TW.ACacheEverything;
using TW.UGUI.Core.Modals;
using TW.UGUI.Core.Screens;
using TW.Utility.CustomComponent;
using UnityEngine;
using Screen = UnityEngine.Screen;

public partial class UIMainMenuTab : ACachedMonoBehaviour, IScreenLifecycleEventSimple
{
    public static Action DisableMainMenuTab { get; set; }
    public static Action ActiveMainMenuTab { get; set; }

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
    [field: SerializeField] public TabButton ButtonShop { get; private set; }
    [field: SerializeField] public TabButton ButtonLeaderboard { get; private set; }
    [field: SerializeField] public TabButton ButtonHome { get; private set; }
    [field: SerializeField] public TabButton ButtonTeam { get; private set; }
    [field: SerializeField] public TabButton ButtonCollection { get; private set; }
    [field: SerializeField] public UITab LastTabUI { get; private set; }
    [field: SerializeField] public UITab CurrentTabUI { get; private set; }
    [field: SerializeField] public float CurrentTabIndex { get; private set; }
    [field: SerializeField] public List<UITab> OpenTabUI { get; private set; } = new();
    [field: SerializeField] public List<TabButton> AllTabButton { get; private set; } = new();
    [field: SerializeField] private UITab UIShop { get; set; }
    [field: SerializeField] private UITab UILeaderboard { get; set; }
    [field: SerializeField] private UITab UIHome { get; set; }
    [field: SerializeField] private UITab UITeam { get; set; }
    [field: SerializeField] private UITab UICollection { get; set; }
    private MotionHandle MotionHandle { get; set; }
    private Vector3 CurrentPosition { get; set; }
    private Vector3 StartSwipePosition { get; set; }
    private Vector3 CurrentSwipePosition { get; set; }
    private SwipeStateType SwipeState { get; set; }
    private float StartSwipeIndex { get; set; }
    private float ThresholdSwipe { get; set; } = 20f;
    private bool IsSwiping { get; set; }
    // [field: SerializeField] private Transform TutorialHand { get; set; }
    // [field: SerializeField] private TutorialType CurrentTutorial { get; set; }
    private bool IsDisableMainMenuTab { get; set; }

    private void Awake()
    {
        ButtonHome.AddListener(OnClickButtonHome);
        ButtonShop.AddListener(OnClickButtonShop);
        ButtonLeaderboard.AddListener(OnClickButtonLeaderboard);
        ButtonTeam.AddListener(OnClickButtonTeam);
        ButtonCollection.AddListener(OnClickButtonCollection);
    }

    public UniTask Initialize(Memory<object> args)
    {
        UIShop.SetVisible(false).SetInteractable(false);
        UILeaderboard.SetVisible(false).SetInteractable(false);
        UIHome.SetVisible(false).SetInteractable(false);
        UITeam.SetVisible(false).SetInteractable(false);
        UICollection.SetVisible(false).SetInteractable(false);
        // TODO: Add all tab UI to OpenTabUI and AllTabButton
        OpenTabUI = new List<UITab>() { UIShop, UILeaderboard, UIHome, UITeam, UICollection };
        AllTabButton = new List<TabButton>()
            { ButtonShop, ButtonLeaderboard, ButtonHome, ButtonTeam, ButtonCollection };
        //
        // OpenTabUI = new List<UITab>() { UIShop, UIHome, UILeaderboard };
        // AllTabButton = new List<TabButton>() {ButtonShop, ButtonHome, ButtonLeaderboard};

        ButtonHome.ForceClick();

        return UniTask.CompletedTask;
    }

    private void Start()
    {
        ActiveMainMenuTab += OnActiveMainMenuTab;
        DisableMainMenuTab += OnDisableMainMenuTab;
    }

    private void OnDestroy()
    {
        ActiveMainMenuTab -= OnActiveMainMenuTab;
        DisableMainMenuTab -= OnDisableMainMenuTab;
    }

    private void OnActiveMainMenuTab()
    {
        IsDisableMainMenuTab = false;
    }

    private void OnDisableMainMenuTab()
    {
        IsDisableMainMenuTab = true;
    }

    private void Update()
    {
        HandleSwipe();
    }
    private bool IsOpenAnyModal()
    {
        return ModalContainer.Find(ContainerKey.Modals).Modals.Count > 0;
    }

    private void HandleSwipe()
    {
        if (IsDisableMainMenuTab) return;
        if (IsOpenAnyModal()) return;
        if (Input.GetKeyDown(KeyCode.Mouse0) && SwipeState != SwipeStateType.Disable)
        {
            StartSwipe(Input.mousePosition);
        }
        else if (Input.GetKey(KeyCode.Mouse0) && SwipeState != SwipeStateType.Disable)
        {
            ContinueSwipe(Input.mousePosition);
        }
        else if (Input.GetKeyUp(KeyCode.Mouse0))
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
                float fixedIndex = Mathf.Clamp(CurrentTabIndex, 0, OpenTabUI.Count - 1);
                for (int i = 0; i < OpenTabUI.Count; i++)
                {
                    OpenTabUI[i].SetVisible(Mathf.Abs(fixedIndex - i) < 1.1f).SetInteractable(false);
                }

                StartSwipeIndex = CurrentTabIndex;
                CurrentTabUI.ReleaseUI();
            }
        }

        if (SwipeState == SwipeStateType.Active)
        {
            float distance = StartSwipePosition.x - CurrentSwipePosition.x;
            float fixedIndex = Mathf.Clamp(StartSwipeIndex + distance / Screen.width, 0, OpenTabUI.Count - 1);
            UpdateTabUIPosition(fixedIndex);
        }
    }

    private void EndSwipe()
    {
        if (!IsSwiping) return;
        IsSwiping = false;
        if (SwipeState == SwipeStateType.Active)
        {
            if (Mathf.Abs(StartSwipeIndex - CurrentTabIndex) > 0.1f)
            {
                int currentTabIndex = OpenTabUI.IndexOf(CurrentTabUI);
                int nextTabIndex = currentTabIndex + (StartSwipeIndex < CurrentTabIndex ? 1 : -1);
                nextTabIndex = Mathf.Clamp(nextTabIndex, 0, OpenTabUI.Count - 1);
                AllTabButton[nextTabIndex].ForceClick();
            }
            else
            {
                OpenTabSwipe(CurrentTabUI);
            }
        }
        else
        {
            SwipeState = 0;
        }
    }

    private void OnClickButtonHome(TabButton tabButton)
    {
        OpenTab(UIHome);

        // CardCollectionManager.Instance.CheckCardReceived();
    }

    private void OnClickButtonShop(TabButton tabButton)
    {
        OpenTab(UIShop);
    }

    private void OnClickButtonLeaderboard(TabButton tabButton)
    {
        // if (CurrentTutorial == TutorialType.Leaderboard)
        // {
        //     TutorialHand.gameObject.SetActive(false);
        //     InGameDataManager.Instance.InGameData.ResourceDataSave.AddTutorial(TutorialType.Leaderboard);
        // }

        OpenTab(UILeaderboard);
    }

    private void OnClickButtonTeam(TabButton tabButton)
    {
        // if (CurrentTutorial == TutorialType.Team)
        // {
        //     TutorialHand.gameObject.SetActive(false);
        //     InGameDataManager.Instance.InGameData.ResourceDataSave.AddTutorial(TutorialType.Team);
        // }

        OpenTab(UITeam);
    }

    private void OnClickButtonCollection(TabButton tabButton)
    {
        OpenTab(UICollection);

        // CardCollectionManager.Instance.CheckCardReceived();
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
        if (CurrentTabUI == tab) return;
        SwipeState = SwipeStateType.Disable;
        LastTabUI = CurrentTabUI;
        LastTabUI?.SetInteractable(false);

        CurrentTabUI = tab.SetVisible(true);

        CurrentPosition = CurrentTabUI.Transform.localPosition;
        float startIndex = CurrentTabIndex;
        float targetIndex = OpenTabUI.IndexOf(CurrentTabUI);
        MotionHandle.TryStop();
        float time = Mathf.Clamp(Mathf.Abs(startIndex - targetIndex) * 0.3f, 0.3f, 0.6f);
        MotionHandle = LMotion.Create(startIndex, targetIndex, time)
            .WithEase(Ease.OutQuart)
            .WithOnComplete(OnUpdateTabUIPositionCompleteCache)
            .Bind(UpdateTabUIPositionCache);
        return;
    }

    [ACacheMethod]
    private void OnUpdateTabUIPositionComplete()
    {
        for (int i = 0; i < OpenTabUI.Count; i++)
        {
            if (OpenTabUI[i] == CurrentTabUI) continue;
            OpenTabUI[i].SetVisible(false);
        }

        CurrentTabUI.SetInteractable(true);
        SwipeState = SwipeStateType.Wait;
    }

    private void OpenTabSwipe(UITab tab)
    {
        SwipeState = SwipeStateType.Disable;
        LastTabUI = CurrentTabUI;
        LastTabUI?.SetInteractable(false);

        CurrentTabUI = tab.SetVisible(true);

        CurrentPosition = CurrentTabUI.Transform.localPosition;
        float startIndex = CurrentTabIndex;
        float targetIndex = OpenTabUI.IndexOf(CurrentTabUI);
        MotionHandle.TryStop();
        float time = Mathf.Clamp(Mathf.Abs(startIndex - targetIndex) * 0.3f, 0.3f, 0.6f);
        MotionHandle = LMotion.Create(startIndex, targetIndex, time)
            .WithEase(Ease.OutQuart)
            .WithOnComplete(OnUpdateTabSwipeUIPositionCompleteCache)
            .Bind(UpdateTabUIPositionCache);
    }

    [ACacheMethod]
    private void OnUpdateTabSwipeUIPositionComplete()
    {
        for (int i = 0; i < OpenTabUI.Count; i++)
        {
            if (OpenTabUI[i] == CurrentTabUI) continue;
            OpenTabUI[i].SetVisible(false);
        }

        CurrentTabUI.SetInteractable(true);
        CurrentTabUI.OnTabEnter();
        LastTabUI?.OnTabExit();
        SwipeState = SwipeStateType.Wait;
    }

    [ACacheMethod]
    private void UpdateTabUIPosition(float index)
    {
        try
        {
            CurrentTabIndex = index;
            for (int i = 0; i < OpenTabUI.Count; i++)
            {
                OpenTabUI[i].SetLocalPosition(GetCenterPosition() + GetRightPosition() * (i - CurrentTabIndex));
            }

            int lowerIndex = Mathf.Clamp(Mathf.FloorToInt(index), 0, OpenTabUI.Count);
            int upperIndex = Mathf.Clamp(Mathf.CeilToInt(index), 0, OpenTabUI.Count);
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
        catch (Exception _)
        {
            // ignored
        }
    }
}