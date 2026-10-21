using System.Collections.Generic;
using Core;
using Core.Manager;
using Core.UI.Modals;
using Core.UI.Screens;
using CoreData;
using Cysharp.Threading.Tasks;
using LitMotion;
using Manager;
using R3;
using TMPro;
using TW.Reactive.CustomComponent;
using TW.UGUI.Core.Modals;
using TW.UGUI.Core.Views;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Nút booster trên HUD (port từ FishHunt): bind số lượng reactive, khóa theo level mở khóa, tutorial nhận booster,
/// hết booster thì mở ModalBooster. Loại booster = ResourceType (một enum duy nhất).
/// </summary>
public class BoosterSelector : MonoBehaviour
{
    private static Dictionary<ResourceType, Button> ButtonMap { get; set; } = new();
    [field: SerializeField] public ResourceType BoosterType { get; private set; } = new();
    [field: SerializeField] public CanvasGroup BoosterCG { get; private set; }
    [field: SerializeField] public Image BoosterIcon { get; private set; }
    [field: SerializeField] public Image BoosterIconWaitClaim { get; private set; }
    [field: SerializeField] public Transform BoosterWrap { get; private set; }
    [field: SerializeField] public Reactive<int> BoosterAmount { get; private set; }
    [field: SerializeField] public TextMeshProUGUI BoosterAmountTxt { get; private set; }
    [field: SerializeField] public TextMeshProUGUI BoosterUnlockLvTxt { get; private set; }
    [field: SerializeField] public TextMeshProUGUI BoosterPriceTxt { get; private set; }
    [field: SerializeField] public GameObject BoosterAmountObj { get; private set; }
    [field: SerializeField] public GameObject AddMoreBoosterObj { get; private set; }
    [field: SerializeField] public GameObject UnlockedObj { get; private set; }
    [field: SerializeField] public GameObject LockedObj { get; private set; }
    [field: SerializeField] public GameObject UnlockWaitClaimObj { get; private set; }
    [field: SerializeField] public Button Button { get; private set; }
    [field: SerializeField] public Transform StateWrapTF { get; private set; }
    [field: SerializeField] public GameObject TutorialHand { get; private set; }
    [field: SerializeField] private AnimationCurve SuggestionCurve;
    [field: SerializeField] private float MultiplierAmplitudeSuggestion = 3f;
    [field: SerializeField] private float TimeLoop = 1f;
    [field: SerializeField] private GameObject EffectLight;
    private int level => GameManager.Instance.Level.Value;
    private BoosterInfoData boosterData;
    private bool isShowOnTutorial;

    private MotionHandle SuggestionHandle;
    private Vector3 originalPos;

    private void Awake()
    {
        BoosterAmount = InGameDataManager.Instance.InGameData.ResourceData.GetResource(BoosterType).Value;
        BoosterAmount.Subscribe(UpdateBoosterAmount).AddTo(this);
        Button.SetOnClickDestination(OnClickBooster);
        boosterData = ItemGlobalConfig.Instance.GetBoosterData(BoosterType) ?? new BoosterInfoData { ResourceType = BoosterType, Name = BoosterType.ToString(), LevelUnlock = 1 };
        if (boosterData.Sprite != null)
        {
            BoosterIcon.sprite = boosterData.Sprite;
            BoosterIconWaitClaim.sprite = boosterData.Sprite;
        }

        BoosterUnlockLvTxt.text = $"Lv.{boosterData.LevelUnlock}";
        SetState();
        isShowOnTutorial = false;
        GameManager.Instance.IsPauseGame.Subscribe(CheckPauseGame).AddTo(this);
        if (EffectLight != null) EffectLight.SetActive(false);
    }

    private void Start()
    {
        originalPos = transform.localPosition;
        ButtonMap.TryAdd(BoosterType, Button);
    }

    private void OnDestroy()
    {
        ButtonMap.Remove(BoosterType);
    }

    public static bool TryGetButton(ResourceType resourceType, out Button button)
    {
        return ButtonMap.TryGetValue(resourceType, out button);
    }

    public void OnChangeLevel(int currentLevel)
    {
        SetState();
    }

    private void CheckPauseGame(bool pause)
    {
        if (!pause && BoosterCG.alpha < 1)
        {
            BoosterCG.alpha = 1;
            Button.interactable = true;
        }

        if (pause)
        {
            BoosterCG.alpha = 0.5f;
            Button.interactable = false;
        }
    }

    private void SetState()
    {
        if (BoosterUnlockRules.IsSelectorUnlocked(boosterData, level, ItemGlobalConfig.Instance.IsBoosterClaimed(BoosterType)))
        {
            BoosterIcon.gameObject.SetActive(true);
            LockedObj.SetActive(false);
            UnlockedObj.SetActive(true);
            BoosterAmountObj.SetActive(BoosterAmount.Value > 0);
            AddMoreBoosterObj.SetActive(BoosterAmount.Value <= 0);
            Button.interactable = true;
        }
        else
        {
            BoosterIcon.gameObject.SetActive(false);
            LockedObj.SetActive(true);
            UnlockedObj.SetActive(false);
            BoosterAmountObj.SetActive(false);
            AddMoreBoosterObj.SetActive(false);
            Button.interactable = false;
        }

        UnlockWaitClaimObj.SetActive(false);
    }

    /// <summary>
    /// Hình của nút đã mở (luồng nhận booster). Bật cả icon: Add Slot nhận giữa L3 đi từ trạng thái khóa (SetState tắt icon), luồng nhận
    /// đầu level thì nút đã mở sẵn.
    /// </summary>
    public void ShowUnlockedArt()
    {
        BoosterIcon.gameObject.SetActive(true);
        LockedObj.SetActive(false);
        UnlockedObj.SetActive(true);
    }

    /// <summary>
    /// Tutorial mở khóa: booster bay từ popup về nút, tặng số lượng rồi (nếu bật) ép dùng thử; booster cần cá trong ô chờ (PackedUp) thì
    /// tự bấm sẵn BoosterInfoData.ForceUsePrefillCount con lên ô chờ trước.
    /// </summary>
    public async UniTask GetBoosterOnTutorial(Vector3 root)
    {
        LevelManager.Instance.SetPause(true);
        if (EffectLight != null) EffectLight.SetActive(true);
        Button.interactable = false;
        BoosterWrap.gameObject.SetActive(true);
        BoosterCG.alpha = 1f;
        StateWrapTF.localScale = Vector3.zero;
        ShowUnlockedArt();
        BoosterWrap.position = root;
        BoosterWrap.localScale = Vector3.one * 2f;
        // Phát lúc booster bắt đầu bay về nút (sau 1.5 s đứng khoe ở giữa màn).
        AudioManager.Instance.PlaySoundFx(AudioKey.SfxBoosterUnlock, 1.5f);
        await LSequence.Create()
            .AppendInterval(1.5f)
            .Append(LMotion.Create(BoosterWrap.localPosition, Vector3.zero, 0.75f)
                .WithEase(Ease.InBack)
                .Bind(x => BoosterWrap.localPosition = x))
            .Join(LMotion.Create(BoosterWrap.localScale, Vector3.one, 0.75f)
                .WithEase(Ease.InBack)
                .Bind(x => BoosterWrap.localScale = x)).Run().AddTo(this);

        if (LevelManager.Instance.CurrentLevel != null) LevelManager.Instance.CurrentLevel.SetIsClaimBooster(true);
        ScreenInGameContext.Events.HideBackGroundBlack?.Invoke();
        if (EffectLight != null) EffectLight.SetActive(false);
        UnlockWaitClaimObj.SetActive(false);
        await LMotion.Create(transform.localScale, Vector3.one * 1.1f, 0.1f)
            .WithEase(Ease.InQuad)
            .Bind(x => transform.localScale = x).AddTo(this);

        await LMotion.Create(transform.localScale, Vector3.one, 0.1f)
            .WithEase(Ease.OutQuad)
            .Bind(x => transform.localScale = x).AddTo(this);
        await LMotion.Create(StateWrapTF.localScale, Vector3.one, 0.1f)
            .WithEase(Ease.OutQuad)
            .Bind(x => StateWrapTF.localScale = x).AddTo(this);
        ActiveBooster();

        if (ForceUseBoosterRemoteConfigData.Instance.IsEnabled)
        {
            AddBoosterToForceUseAmount();
            // PackedUp chỉ dùng được khi ô chờ có cá: tự bấm sẵn ForceUsePrefillCount con (cách nhau như bấm tay) trước khi tay chỉ vào nút,
            // để lần dùng thử có tác dụng thật thay vì báo "No fish in the holding area!".
            if (boosterData.ForceUsePrefillCount > 0 && LevelManager.Instance.CurrentLevel != null)
                await LevelManager.Instance.CurrentLevel.PrefillHoldingAsync(boosterData.ForceUsePrefillCount);
            // OnClickBooster bỏ qua khi IsPause: mở pause để lần bấm ép dùng thử có tác dụng (overlay focus chặn mọi input khác).
            // Chuỗi intro của LevelManager chờ trọn luồng này nên không còn ai mở pause hộ như trước.
            LevelManager.Instance.SetPause(false);
            await GameManager.Instance.ForceUseBooster(BoosterType);
            await GameManager.Instance.ForceUseBoosterCompleted();
        }

        Button.interactable = true;
        LevelManager.Instance.SetPause(false);
    }

    public void OnUnlockClaim()
    {
        UnlockWaitClaimObj.SetActive(true);
        StateWrapTF.localScale = Vector3.zero;
        BoosterWrap.gameObject.SetActive(false);
    }

    private void ActiveBooster()
    {
        BoosterAmountObj.SetActive(true);
        AddMoreBoosterObj.SetActive(BoosterAmount.Value <= 0);
        Button.interactable = true;
        isShowOnTutorial = true;
    }

    /// <summary>Tặng số booster khi mở khóa (GDD: Initial*Amount) nếu kho đang ít hơn.</summary>
    private void AddBoosterToForceUseAmount()
    {
        int target = Mathf.Max(1, boosterData.UnlockGiftAmount);
        int amountToAdd = target - BoosterAmount.Value;
        if (amountToAdd <= 0) return;
        PlayerResourceManager.Instance.AddResourceValue(BoosterType, amountToAdd, ResourcePos.ingame.ToString(), ResourcePos.free.ToString());
    }

    public void ShowTutorialHand(Transform hint)
    {
        hint.gameObject.SetActive(!LockedObj.activeSelf);
        if (TutorialHand != null) hint.position = TutorialHand.transform.position;
    }

    public void HideTutorialHand()
    {
        if (TutorialHand != null) TutorialHand.SetActive(false);
    }

    private void UpdateBoosterAmount(int amount)
    {
        BoosterAmountTxt.text = $"{amount}";
        AddMoreBoosterObj.SetActive(amount <= 0 && !LockedObj.activeSelf);
        BoosterAmountObj.SetActive(amount > 0 && !LockedObj.activeSelf);
    }

    private async UniTask OnClickBooster()
    {
        if (LevelManager.Instance.IsPause) return;
        await SelectBooster("ingame");
    }

    public async UniTask SelectBooster(string placement)
    {
        // Guard: callback delay (reward ads) có thể chạy khi UI đã bị destroy cùng level.
        if (this == null || LockedObj == null) return;
        if (TutorialHand != null) TutorialHand.SetActive(false);
        if ((GameManager.Instance.IsPauseGame.Value && !isShowOnTutorial) || LockedObj.activeSelf) return;
        if (!GameManager.Instance.IsState(GameState.InGame)) return;

        if (BoosterAmount.Value > 0)
        {
            BoosterManager.Instance.UseBoosterOrNotify(BoosterType);   // dùng hoặc báo lý do bị chặn (dùng chung với ô bể khóa)
        }
        else
        {
            ViewOptions options = new ViewOptions(nameof(ModalBooster));
            await ModalContainer.Find(ContainerKey.Modals).PushAsync(options, this);
        }
    }

    public async UniTask DelayUseBooster()
    {
        await UniTask.Delay(100, cancellationToken: this.GetCancellationTokenOnDestroy());
        SelectBooster("ingame").Forget();
    }

    public void SetSuggestion(bool value)
    {
        SuggestionHandle.TryCancel();
        if (value)
        {
            StartSuggestionCycle();
        }
        else
        {
            transform.localPosition = originalPos;
        }
    }

    private void StartSuggestionCycle()
    {
        SuggestionHandle.TryCancel();
        SuggestionHandle = LMotion.Create(0f, 1f, 0.5f)
            .WithEase(Ease.Linear)
            .WithOnComplete(() =>
            {
                transform.localPosition = originalPos;
                SuggestionHandle = LMotion.Create(0f, 0f, TimeLoop)
                    .WithOnComplete(StartSuggestionCycle)
                    .RunWithoutBinding()
                    .AddTo(this);
            })
            .Bind(OnSuggestionUpdate)
            .AddTo(this);
    }

    private void OnSuggestionUpdate(float value)
    {
        float offsetX = SuggestionCurve.Evaluate(value);
        transform.localPosition = originalPos + Vector3.right * offsetX * MultiplierAmplitudeSuggestion;
    }
}
