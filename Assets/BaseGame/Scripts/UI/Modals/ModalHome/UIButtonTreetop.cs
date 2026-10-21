using System;
using Core.UI.Modals;
using Cysharp.Threading.Tasks;
using Game.Helper;
using TW.UGUI.Core.Modals;
using UnityEngine;
using UnityEngine.UI;
using Sirenix.Utilities;
using TMPro;

public class UIButtonTreetop : MonoBehaviour
{
    [field: SerializeField] public Button Button { get; set; }
    [field: SerializeField] public Button ButtonLock { get; set; }
    private TreetopData Data => TreetopData.Instance;
    [field: SerializeField] public GameObject GOTime { get; private set; }
    [field: SerializeField] public TextMeshProUGUI TxtTimeEndEvent { get; private set; }
    [field: SerializeField] public GameObject GOLock { get; private set; }
    [field: SerializeField] public GameObject GOUnlock { get; private set; }
    [field: SerializeField] public TextMeshProUGUI TxtLevelUnlock { get; private set; }
    private void Start()
    {
        Button.SetOnClickDestination(OnClick);
        ButtonLock.SetOnClickDestination(OnClickBtnLock);
        TreetopManager.OnInitializeComplete += OnInitializeComplete;
        Init();
    }

    private void OnDestroy()
    {
        // TreetopManager.OnInitializeComplete is static, so without this the destroyed button stays
        // subscribed. The next Initialized() call then reaches a dead MonoBehaviour and throws on
        // the first gameObject access - the NullReferenceException seen in OnInitializeComplete.
        TreetopManager.OnInitializeComplete -= OnInitializeComplete;
    }

    private void OnInitializeComplete()
    {
        TxtLevelUnlock.SetText("Level {0}", TreetopRemoteConfig.Instance.LevelUnlock);
        if (!TreetopManager.Instance.IsInitialize) return;
        gameObject.SetActive(true);
        if (!TreetopManager.Instance.IsEventUnlock)
        {
            GOUnlock.SetActive(false);
            GOLock.SetActive(true);
            return;
        }
        GOUnlock.SetActive(true);
        GOLock.SetActive(false);
    }

    private void Init()
    {
        TxtLevelUnlock.SetText("Level {0}", TreetopRemoteConfig.Instance.LevelUnlock);
        gameObject.SetActive(false);
        if (!TreetopManager.Instance.IsInitialize) return;
        gameObject.SetActive(true);
        if (!TreetopManager.Instance.IsEventUnlock)
        {
            GOUnlock.SetActive(false);
            GOLock.SetActive(true);
            return;
        }
        if(TreetopData.Instance.EventEndTimeUtc.IsNullOrWhitespace()) return;
        GOUnlock.SetActive(true);
        GOLock.SetActive(false);
    }

    private void Update()
    {
        if (!TreetopManager.Instance.IsInitialize) return;
        if (!TreetopManager.Instance.IsEventUnlock) return;
        if(TreetopData.Instance.EventEndTimeUtc.IsNullOrWhitespace()) return;
        TimeSpan time = TreetopData.Instance.EventEndTimeUtc.StringToDateTime() - DateTimeManager.UtcNow;
        TxtTimeEndEvent.SetText(time.TimeSpanToStringUI());
    }

    private void OnUnlockTreetop(bool isUnlock)
    {
        this.gameObject.SetActive(isUnlock);
    }
    private async UniTask OnClick()
    {
        ModalOptions modalTreetop = new(nameof(ModalTreetop), backdropAlpha: 0f);
        if (TreetopManager.Instance.LastShowUILevel == Data.Streak)
        {
            //TreetopManager.Instance.SetLastShowUILevel(Data.Streak);
            await ModalContainer.Find(ContainerKey.Modals).PushAsync(modalTreetop,ModalTreetop.ModalState.ShowCurrent,Data.Streak);
        }
        else
        {
            await ModalContainer.Find(ContainerKey.Modals).PushAsync(modalTreetop,ModalTreetop.ModalState.ShowWin,TreetopManager.Instance.LastShowUILevel);
            TreetopManager.Instance.ClearShowUI();
        }
    }
    private void OnClickBtnLock()
    {
        UIManager.Instance.ShowInfo($"Reach Level {TreetopRemoteConfig.Instance.LevelUnlock } to unlock Ocean Ascension!");
    }
}
