using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using R3;
using Sirenix.OdinInspector;
using CoreNotification;
using LitMotion;
using Profile.ProfileItem;
using Core.UI.Activities;
using TW.UGUI.Core.Activities;
using TW.UGUI.Core.Views;


namespace UI.ModalProfile
{
    public class PopupEditProfile : MonoBehaviour
    {
        [Header("UI References")]
        [field: SerializeField] private TMP_Text lblID;
        [field: SerializeField] private Button btnSave;
        [field: SerializeField] private List<Sprite> listSpriteBtnSave;
        [field: SerializeField] private TextMeshProUGUI txtHowToCollect;
        [field: SerializeField] private GameObject gameObjectHowToCollect;
        [field: SerializeField] private Button btnCloseHowToCollect;
        [field: SerializeField] private TMP_InputField inputName;
        private ProfileCellUI curAvatarSelect;
        private ProfileCellUI curFrameAvatarSelect;
        private ProfileCellUI curBadgeSelect;
        private NameCellUI curNameSelect;
        [field: SerializeField] private List<ProfileCellUI> listAvatarCellUI;
        [field: SerializeField] private List<ProfileCellUI> listFrameAvatar;
        [field: SerializeField] private List<ProfileCellUI> listBadgeCellUI;
        [field: SerializeField] private List<NameCellUI> listNameCellUI;

        private bool isProcessing = false;
        private ProfileData originalProfile;
        [field: SerializeField] private UserAvatarFrame userAvatarFrame;
        [field: SerializeField] private UserNameEffect userNameEffect;

        [field: SerializeField] private List<Button> btnChangeTabs;
        [field: SerializeField] private List<GameObject> activeTabs;
        [field: SerializeField] private List<GameObject> activeContents;

        [ShowInInspector] private SerializableReactiveProperty<int> CurrentTab { get; set; } = new(0);
        public Action OnSave;

        private void Start()
        {
            var profileData = ProfileData.Instance;
            
            InitializeAvatarCells(profileData);
            InitializeFrameCells(profileData);
            //InitializeBadgeCells(profileData);
            //InitializeNameCells(profileData);
            InitializeUI(profileData);
            InitializeTabs();
        }

        private void InitializeAvatarCells(ProfileData profileData)
        {
            var sortedAvatarData = profileData.SortAvatarData();
            
            PopulateCellList(
                sortedAvatarData,
                listAvatarCellUI,
                (index, sortedIndex) =>
                {
                    var sprite = ProfileGlobalConfig.Instance.GetAvatarEditProfileSprite(sortedAvatarData[index]);
                    var sizeRectTransformProfile = ProfileGlobalConfig.Instance.GetSizeRectTransformAvatarEdit(sortedAvatarData[index]);
                    var isUnlocked = profileData.AvatarDatas.Find(x => x.AvatarIndex == sortedAvatarData[index]).IsUnlocked;
                    listAvatarCellUI[index].SetProfile(sortedAvatarData[index], sprite, isUnlocked, sizeRectTransformProfile);
                    listAvatarCellUI[index].ShowNotification(profileData.IsShowAvatarNotification.Value == sortedAvatarData[index]);
                },
                TypeProfile.Avatar
            );

            for (int i = 0; i < listAvatarCellUI.Count; i++)
            {
                ProfileCellUI profileCellUI = listAvatarCellUI[i];
                profileCellUI.AddListenerOnClickProfile(() => SelectAvatarCell(profileCellUI));
            }

            curAvatarSelect = listAvatarCellUI.Find(e => e.ProfileID == PlayerData.Instance.AvatarIndex.Value);
            if (curAvatarSelect != null)
                curAvatarSelect.ShowSelect(true);
            
            userAvatarFrame.SetUpAvatar(curAvatarSelect.ProfileID);
        }

        private void InitializeFrameCells(ProfileData profileData)
        {
            var sortedFrameData = profileData.SortFrameData();
            
            PopulateCellList(
                sortedFrameData,
                listFrameAvatar,
                (index, sortedIndex) =>
                {
                    var sprite = ProfileGlobalConfig.Instance.GetFrameEditProfileSprite(sortedFrameData[index]);
                    var sizeRectTransformProfile = ProfileGlobalConfig.Instance.GetSizeRectTransformFrameEdit(sortedFrameData[index]);
                    var isUnlocked = profileData.FrameDatas.Find(x => x.FrameIndex == sortedFrameData[index]).IsUnlocked;
                    listFrameAvatar[index].SetProfile(sortedFrameData[index], sprite, isUnlocked, sizeRectTransformProfile);
                    listFrameAvatar[index].ShowNotification(profileData.IsShowFrameNotification.Value == sortedFrameData[index]);
                },
                TypeProfile.Frame
            );

            for (int i = 0; i < listFrameAvatar.Count; i++)
            {
                ProfileCellUI profileCellUI = listFrameAvatar[i];
                profileCellUI.AddListenerOnClickProfile(() => SelectFrameAvatarCell(profileCellUI));
            }

            curFrameAvatarSelect = listFrameAvatar.Find(e => e.ProfileID == PlayerData.Instance.AvatarFrameIndex.Value);
            if (curFrameAvatarSelect != null)
                curFrameAvatarSelect.ShowSelect(true);
            
            userAvatarFrame.SetUpFrame(curFrameAvatarSelect.ProfileID);
        }

        private void InitializeBadgeCells(ProfileData profileData)
        {
            var sortedBadgeData = profileData.SortBadgeData();
            // Bỏ qua badge id = 0, chỉ hiển thị từ badge id = 1 trở đi.
            sortedBadgeData.RemoveAll(id => id == 0);
            
            PopulateCellList(
                sortedBadgeData,
                listBadgeCellUI,
                (index, sortedIndex) =>
                {
                    var sprite = ProfileGlobalConfig.Instance.GetBadgeEditProfileSprite(sortedBadgeData[index]);
                    var sizeRectTransformProfile = ProfileGlobalConfig.Instance.GetSizeRectTransformBadgeEdit(sortedBadgeData[index]);
                    var isUnlocked = profileData.BadgeDatas.Find(x => x.BadgeIndex == sortedBadgeData[index]).IsUnlocked;
                    listBadgeCellUI[index].SetProfile(sortedBadgeData[index], sprite, isUnlocked, sizeRectTransformProfile);
                    listBadgeCellUI[index].ShowNotification(profileData.IsShowBadgeNotification.Value == sortedBadgeData[index]);
                    // Listener đăng ký ở vòng lặp bên dưới (giống avatar/frame). Đăng ký cả ở
                    // đây thì AddListener cộng dồn → 1 lần bấm mở popup badge 2 lần.
                },
                TypeProfile.Badge
            );

            for (int i = 0; i < listBadgeCellUI.Count; i++)
            {
                ProfileCellUI profileCellUI = listBadgeCellUI[i];
                profileCellUI.AddListenerOnClickProfile(() => SelectBadgeCell(profileCellUI));
            }

            curBadgeSelect = listBadgeCellUI.Find(e => e.ProfileID == PlayerData.Instance.BadgeIndex.Value);
            if (curBadgeSelect != null)
            {
                curBadgeSelect.ShowSelect(true);
            }
            else
            {
                curBadgeSelect = new ProfileCellUI();
                curBadgeSelect.profileID = -1;
                userAvatarFrame.SetUpBadge(-1);
                return;
            }
            
            userAvatarFrame.SetUpBadge(curBadgeSelect.ProfileID);
        }

        private void InitializeNameCells(ProfileData profileData)
        {
            var nameUser = PlayerData.Instance.UserName.Value;
            var nameData = ProfileGlobalConfig.Instance.GetDefaultNameData();
            var sortedNameData = profileData.SortNameData();

            nameData.Sort((a, b) => sortedNameData.IndexOf(a.NameIndex).CompareTo(sortedNameData.IndexOf(b.NameIndex)));

            while (listNameCellUI.Count < nameData.Count)
            {
                var newCell = Instantiate(listNameCellUI[0], listNameCellUI[0].transform.parent);
                newCell.gameObject.SetActive(true);
                listNameCellUI.Add(newCell);
            }

            for (int i = 0; i < nameData.Count; i++)
            {
                listNameCellUI[i].gameObject.SetActive(true);
                listNameCellUI[i].ShowSelect(false);

                var isUnlocked = ProfileData.Instance.EffectNameDatas
                    .Find(x => x.NameIndex == nameData[i].NameIndex).IsUnlocked;

                listNameCellUI[i].SetIsCurrentUser(true, true);
                listNameCellUI[i].SetUserNameEffect(
                    nameData[i].NameIndex,
                    isUnlocked,
                    nameUser
                );
                listNameCellUI[i].ShowNotification(ProfileData.Instance.IsShowNameNotification.Value == nameData[i].NameIndex);

                if (!ProfileData.Instance.CheckUnlockProfileData(nameData[i].NameIndex, TypeProfile.Name) && ProfileGlobalConfig.Instance.hidenNameIds.Contains(nameData[i].NameIndex))
                {
                    listNameCellUI[i].gameObject.SetActive(false);
                }
            }

            for (int i = nameData.Count; i < listNameCellUI.Count; i++)
            {
                listNameCellUI[i].gameObject.SetActive(false);
            }

            // Ô name trước giờ không có handler chọn — bấm vào không đổi được gì.
            for (int i = 0; i < listNameCellUI.Count; i++)
            {
                NameCellUI nameCellUI = listNameCellUI[i];
                nameCellUI.AddListenerOnClickName(() => SelectNameCell(nameCellUI));
            }

            curNameSelect = listNameCellUI.Find(e => e.NameID == PlayerData.Instance.EffectNameID.Value);
            if (curNameSelect != null)
            {
                curNameSelect.ShowSelect(true);
            }
            else
            {
                curNameSelect = new NameCellUI();
                curNameSelect.nameID = -1;
                userNameEffect.InitUserNameEffect(nameUser, -1);
                return;
            }

            userNameEffect.InitUserNameEffect(nameUser, PlayerData.Instance.EffectNameID.Value);
        }

        private void InitializeUI(ProfileData profileData)
        {
            btnSave.interactable = false;
            btnSave.image.sprite = listSpriteBtnSave[1];
            btnSave.onClick.AddListener(SaveAction);
            btnCloseHowToCollect.onClick.AddListener(CloseHowToCollect);
            
            inputName.text = PlayerData.Instance.UserName.Value;
            
            originalProfile = profileData;
            
            SetupListeners();
        }

        private void InitializeTabs()
        {
            CurrentTab.Value = 0;
            CurrentTab.Skip(1).Subscribe(EventChangeTab).AddTo(this);

            for (int i = 0; i < btnChangeTabs.Count; i++)
            {
                int index = i;
                btnChangeTabs[i].onClick.AddListener(() => OnTabClick(index));
            }
        }

        private void PopulateCellList(List<int> sortedData, List<ProfileCellUI> cellList, System.Action<int, int> setupAction, TypeProfile typeProfile)
        {
            while (cellList.Count < sortedData.Count)
            {
                var newCell = Instantiate(cellList[0], cellList[0].transform.parent);
                newCell.gameObject.SetActive(true);
                cellList.Add(newCell);
            }

            for (int i = 0; i < sortedData.Count; i++)
            {
                cellList[i].gameObject.SetActive(true);
                cellList[i].ShowSelect(false);
                setupAction?.Invoke(i, sortedData[i]);
            }

            for (int i = sortedData.Count; i < cellList.Count; i++)
            {
                cellList[i].gameObject.SetActive(false);
            }

            for (int i = 0; i < sortedData.Count; i++)
            {
                if (typeProfile == TypeProfile.Avatar && !ProfileData.Instance.CheckUnlockProfileData(sortedData[i], TypeProfile.Avatar) && ProfileGlobalConfig.Instance.hidenAvatarIds.Contains(sortedData[i]))
                {
                    cellList[i].gameObject.SetActive(false);
                }
                if (typeProfile == TypeProfile.Frame && !ProfileData.Instance.CheckUnlockProfileData(sortedData[i], TypeProfile.Frame) && ProfileGlobalConfig.Instance.hidenFrameIds.Contains(sortedData[i]))
                {
                    cellList[i].gameObject.SetActive(false);
                }
                if (typeProfile == TypeProfile.Badge && !ProfileData.Instance.CheckUnlockProfileData(sortedData[i], TypeProfile.Badge) && ProfileGlobalConfig.Instance.hidenBadgeIds.Contains(sortedData[i]))
                {
                    cellList[i].gameObject.SetActive(false);
                }
            }
        }

        private void OnTabClick(int index)
        {
            CurrentTab.Value = index;
        }

        private void EventChangeTab(int tabIndex)
        {
            for (int i = 0; i < activeTabs.Count; i++)
            {
                activeTabs[i].SetActive(i == tabIndex);
            }

            for (int i = 0; i < activeContents.Count; i++)
            {
                activeContents[i].SetActive(i == tabIndex);
            }

            switch (tabIndex)
            {
                case 0:
                    ProfileData.Instance.OnChangeStatusNotification(new AvatarNotificationCondition(), -1);
                    break;
                case 1:
                    ProfileData.Instance.OnChangeStatusNotification(new FrameNotificationCondition(), -1);
                    break;
                case 2:
                    ProfileData.Instance.OnChangeStatusNotification(new NameNotificationCondition(), -1);
                    break;
                case 3:
                    ProfileData.Instance.OnChangeStatusNotification(new BadgeNotificationCondition(), -1);
                    break;
                default:
                    break;
            }

            ProfileData.Instance.OnChangeStatusNotification(new AvatarNotificationCondition(), -1);

            ChangeTabEditProfile();
        }

        private void CloseHowToCollect()
        {
            btnCloseHowToCollect.interactable = false;
            LMotion.Create(1f, 0f, 0.3f)
                .WithEase(Ease.InBack)
                .WithOnComplete(() =>
                {
                    gameObjectHowToCollect.SetActive(false);
                })
                .Bind(x => gameObjectHowToCollect.transform.localScale = Vector3.one * x)
                .AddTo(gameObjectHowToCollect);
        }

        private void SetupListeners()
        {
            inputName.onValueChanged.AddListener((_) => UpdateSaveButtonState());
        }

        private void UpdateSaveButtonState(TypeProfile typeChangeProfile = TypeProfile.None)
        {
            if (isProcessing)
            {
                btnSave.interactable = false;
                btnSave.image.sprite = listSpriteBtnSave[1];
                return;
            }

            bool hasChanges = false;

            if (!string.IsNullOrEmpty(inputName.text) && inputName.text != originalProfile.GetUserName())
            {
                hasChanges = true;
            }

            if (typeChangeProfile == TypeProfile.Avatar || typeChangeProfile == TypeProfile.None)
            {
                if (curAvatarSelect != null && curAvatarSelect.ProfileID != PlayerData.Instance.AvatarIndex.Value)
                    hasChanges = true;

                if (!ProfileData.Instance.AvatarDatas[curAvatarSelect.ProfileID].IsUnlocked)
                {
                    hasChanges = false;
                    // Sử dụng localized text với parameter
                    // Trong Unity editor, bạn cần set text ID cho "Collect from {0}"
                    // Ví dụ: textId = 999 (bạn cần thay số này bằng ID thực tế)
                    var collectTextData = "Collect from {0}"; // GameManager.Instance.GetTextData(394);
                    txtHowToCollect.SetTextFormat(collectTextData, ProfileGlobalConfig.Instance.UserAvatarDatas[curAvatarSelect.ProfileID].HowToCollect);

                    gameObjectHowToCollect.SetActive(true);
                    LMotion.Create(0f, 1f, 0.3f)
                        .WithEase(Ease.OutBack)
                        .WithOnComplete(() =>
                        {
                            btnCloseHowToCollect.interactable = true;
                        })
                        .Bind(x => gameObjectHowToCollect.transform.localScale = Vector3.one * x)
                        .AddTo(gameObjectHowToCollect);
                }

                btnSave.interactable = hasChanges;
                btnSave.image.sprite = listSpriteBtnSave[hasChanges ? 0 : 1];
            }

            if (typeChangeProfile == TypeProfile.Frame || typeChangeProfile == TypeProfile.None)
            {
                if (curFrameAvatarSelect != null && curFrameAvatarSelect.ProfileID != PlayerData.Instance.AvatarFrameIndex.Value)
                    hasChanges = true;
                if (!ProfileData.Instance.FrameDatas[curFrameAvatarSelect.ProfileID].IsUnlocked)
                {
                    hasChanges = false;
                    // Sử dụng localized text với parameter
                    // Trong Unity editor, bạn cần set text ID cho "Collect from {0}"
                    // Ví dụ: textId = 999 (bạn cần thay số này bằng ID thực tế)
                    var collectTextData = "Collect from {0}"; // GameManager.Instance.GetTextData(394);
                    txtHowToCollect.SetTextFormat(collectTextData, ProfileGlobalConfig.Instance.UserFrameDatas[curFrameAvatarSelect.ProfileID].HowToCollect);

                    gameObjectHowToCollect.SetActive(true);
                    LMotion.Create(0f, 1f, 0.3f)
                        .WithEase(Ease.OutBack)
                        .WithOnComplete(() =>
                        {
                            btnCloseHowToCollect.interactable = true;
                        })
                        .Bind(x => gameObjectHowToCollect.transform.localScale = Vector3.one * x)
                        .AddTo(gameObjectHowToCollect);
                }

                btnSave.interactable = hasChanges;
                btnSave.image.sprite = listSpriteBtnSave[hasChanges ? 0 : 1];
            }

            if (typeChangeProfile == TypeProfile.Name || typeChangeProfile == TypeProfile.None || (curNameSelect != null && curNameSelect.NameID != -1))
            {
                if (curNameSelect == null)
                {
                    return;
                }
                if (curNameSelect != null && curNameSelect.NameID != PlayerData.Instance.EffectNameID.Value)
                    hasChanges = true;
                if (ProfileData.Instance.EffectNameDatas[curNameSelect.NameID].IsUnlocked == false)
                {
                    hasChanges = false;
                    // Sử dụng localized text với parameter
                    // Trong Unity editor, bạn cần set text ID cho "Collect from {0}"
                    // Ví dụ: textId = 999 (bạn cần thay số này bằng ID thực tế)
                    var collectTextData = "Collect from {0}"; // GameManager.Instance.GetTextData(394);
                    txtHowToCollect.SetTextFormat(collectTextData, ProfileGlobalConfig.Instance.UserNameDatas[curNameSelect.NameID].HowToCollect);

                    gameObjectHowToCollect.SetActive(true);
                    LMotion.Create(0f, 1f, 0.3f)
                        .WithEase(Ease.OutBack)
                        .WithOnComplete(() =>
                        {
                            btnCloseHowToCollect.interactable = true;
                        })
                        .Bind(x => gameObjectHowToCollect.transform.localScale = Vector3.one * x)
                        .AddTo(gameObjectHowToCollect);
                }

                btnSave.interactable = hasChanges;
                btnSave.image.sprite = listSpriteBtnSave[hasChanges ? 0 : 1];
            }

            if (typeChangeProfile == TypeProfile.Badge || typeChangeProfile == TypeProfile.None || (curBadgeSelect != null && curBadgeSelect.ProfileID != -1))
            {
                if (curBadgeSelect == null)
                {
                    return;
                }
                if (curBadgeSelect != null && curBadgeSelect.ProfileID != PlayerData.Instance.BadgeIndex.Value)
                    hasChanges = true;
                if (!ProfileData.Instance.BadgeDatas[curBadgeSelect.ProfileID].IsUnlocked)
                {
                    hasChanges = false;
                    var collectTextData = "Collect from {0}"; // GameManager.Instance.GetTextData(394);
                    txtHowToCollect.SetTextFormat(collectTextData, ProfileGlobalConfig.Instance.UserBadgeDatas[curBadgeSelect.ProfileID].HowToCollect);

                    gameObjectHowToCollect.SetActive(true);
                    LMotion.Create(0f, 1f, 0.3f)
                        .WithEase(Ease.OutBack)
                        .WithOnComplete(() =>
                        {
                            btnCloseHowToCollect.interactable = true;
                        })
                        .Bind(x => gameObjectHowToCollect.transform.localScale = Vector3.one * x)
                        .AddTo(gameObjectHowToCollect);
                }

                btnSave.interactable = hasChanges;
                btnSave.image.sprite = listSpriteBtnSave[hasChanges ? 0 : 1];
            }
        }

        public void SelectAvatarCell(ProfileCellUI avatarCellUI)
        {
            if (curAvatarSelect != null)
                curAvatarSelect.ShowSelect(false);
            curAvatarSelect = avatarCellUI;
            curAvatarSelect.ShowSelect(true);
            UpdateSaveButtonState(TypeProfile.Avatar);
            userAvatarFrame.SetUpAvatar(curAvatarSelect.ProfileID);
        }
        public void SelectFrameAvatarCell(ProfileCellUI avatarCellUI)
        {
            if (curFrameAvatarSelect != null)
                curFrameAvatarSelect.ShowSelect(false);
            curFrameAvatarSelect = avatarCellUI;
            curFrameAvatarSelect.ShowSelect(true);
            UpdateSaveButtonState(TypeProfile.Frame);
            userAvatarFrame.SetUpFrame(curFrameAvatarSelect.ProfileID);
        }
        /// <summary>
        /// Bấm 1 badge → mở popup xem badge 3D; chọn thật sự chỉ xảy ra khi bấm Equip trong đó.
        /// Dùng cho thao tác của người chơi; code nội bộ muốn đổi badge thì gọi thẳng
        /// <see cref="ApplyBadgeSelection"/> để không bật popup ngoài ý muốn.
        /// </summary>
        public void SelectBadgeCell(ProfileCellUI badgeCellUI)
        {
            if (badgeCellUI == null) return;
            ShowBadgeInfoAsync(badgeCellUI).Forget();
        }

        private async UniTaskVoid ShowBadgeInfoAsync(ProfileCellUI badgeCellUI)
        {
            int badgeId = badgeCellUI.ProfileID;
            bool isUnlocked = ProfileData.Instance.CheckUnlockProfileData(badgeId, TypeProfile.Badge);

            //var args = new ActivityCollectionBadgeInfoContext.BadgeInfoArgs
            //{
            //    BadgeId = badgeId,
            //    // Badge chưa mở khoá vẫn xem được 3D, chỉ không Equip được.
            //    CanEquip = isUnlocked,
            //    OnEquip = _ => ApplyBadgeSelection(badgeCellUI),
            //};

            //await ActivityContainer.Find(ContainerKey.Activities)
            //    .ShowAsync(new ViewOptions(nameof(ActivityCollectionBadgeInfo)), args);
        }

        /// <summary>Đổi badge đang chọn trong form (không mở popup).</summary>
        private void ApplyBadgeSelection(ProfileCellUI badgeCellUI)
        {
            if (badgeCellUI == null) return;

            if (curBadgeSelect != null)
                curBadgeSelect.ShowSelect(false);
            curBadgeSelect = badgeCellUI;
            curBadgeSelect.ShowSelect(true);
            UpdateSaveButtonState(TypeProfile.Badge);
            userAvatarFrame.SetUpBadge(curBadgeSelect.ProfileID);
        }
        public void SelectNameCell(NameCellUI nameCellUI)
        {
            if (curNameSelect != null)
                curNameSelect.ShowSelect(false);
            curNameSelect = nameCellUI;
            curNameSelect.ShowSelect(true);
            UpdateSaveButtonState(TypeProfile.Name);
            userNameEffect.InitUserNameEffect(inputName.text, curNameSelect.NameID);
        }

        public void SaveAction()
        {
            if (isProcessing) return;
            string newName = inputName.text;
            if (string.IsNullOrEmpty(newName) || newName.Length < 2 || newName.Length > 12)
            {
                UIManager.Instance.ShowError("Username must be between 2 and 12 characters long.");
                return;
            }
            if (!PlayerData.IsValidUserName(newName))
            {
                UIManager.Instance.ShowInfo("Username can only use Latin letters, numbers and symbols like !@#$%^&*(){}[].");
                return;
            }
            SaveActionAsync().Forget();
        }

        private async UniTaskVoid SaveActionAsync()
        {
            string newName = inputName.text;
            CheckCode(newName);
            isProcessing = true;
            btnSave.interactable = false;
            btnSave.image.sprite = listSpriteBtnSave[1];
            PlayerData.Instance.UserName.Value = inputName.text;

            PlayerData.Instance.AvatarIndex.Value = curAvatarSelect.ProfileID;
            PlayerData.Instance.AvatarFrameIndex.Value = curFrameAvatarSelect.ProfileID;
            if (curBadgeSelect != null)
            {
                PlayerData.Instance.BadgeIndex.Value = curBadgeSelect.ProfileID;
            }
            else
            {
                PlayerData.Instance.BadgeIndex.Value = -1;
            }
            // Name effect trước giờ bị bỏ sót ở đây nên chọn xong Save là mất.
            PlayerData.Instance.EffectNameID.Value = curNameSelect != null ? curNameSelect.NameID : -1;
            PlayerData.Instance.SavePlayerData();

            isProcessing = false;
            btnSave.interactable = true;
            btnSave.image.sprite = listSpriteBtnSave[0];

            OnSave?.Invoke();
            //if (PlayerDataManager.Instance == null)
            //{
            //    isProcessing = false;
            //    btnSave.interactable = true;
            //    btnSave.image.sprite = listSpriteBtnSave[0];
            //    return;
            //}

            //UIManager.Instance.ShowLoadingBlock();

            //bool isHandled = false;

            //void HandleSuccess()
            //{
            //    if (isHandled) return;
            //    isHandled = true;
            //    isProcessing = false;
            //    UIManager.Instance.HideLoadingBlock();
            //    UIManager.Instance.ShowSuccess("Your profile has been updated successfully.");
            //}

            //void HandleFailure()
            //{
            //    if (isHandled) return;
            //    isHandled = true;
            //    isProcessing = false;
            //    UIManager.Instance.HideLoadingBlock();
            //    UIManager.Instance.ShowError("Server error. Please try again.");
            //    btnSave.interactable = true;
            //    btnSave.image.sprite = listSpriteBtnSave[0];
            //}

            //UniTask uploadTask = PlayerDataManager.Instance.UploadUserDataToServer(
            //    false,
            //    false,
            //    HandleSuccess,
            //    HandleFailure);

            //int winIndex = await UniTask.WhenAny(
            //    uploadTask,
            //    UniTask.Delay(TimeSpan.FromSeconds(3), cancellationToken: this.GetCancellationTokenOnDestroy()));

            //if (winIndex != 0)
            //{
            //    HandleFailure();
            //}
        }

        private void UpdateUserNameFromPlatform()
        {
            
        }

        private void CheckCode(string code)
        {
            // if (code == "e3676a6ea88e53a6")
            // {
            //     PlayerResourceManager.Instance.SetResourceValue(CurrencyType.Money, 0);
            // }
        }

        private void OnDisable()
        {

        }

        public void ChangeTabEditProfile()
        {
            if (curAvatarSelect != null)
            {
                if (ProfileData.Instance.AvatarDatas[curAvatarSelect.ProfileID].IsUnlocked == false)
                {
                    ProfileCellUI avatarCellUI = listAvatarCellUI.Find(e => e.profileID == PlayerData.Instance.AvatarIndex.Value);
                    SelectAvatarCell(avatarCellUI);
                }
            }
            if (curFrameAvatarSelect != null)
            {
                if (ProfileData.Instance.FrameDatas[curFrameAvatarSelect.ProfileID].IsUnlocked == false)
                {
                    ProfileCellUI avatarCellUI = listFrameAvatar.Find(e => e.profileID == PlayerData.Instance.AvatarFrameIndex.Value);
                    SelectFrameAvatarCell(avatarCellUI);
                }
            }
            if(curNameSelect != null)
            {
                if (ProfileData.Instance.EffectNameDatas[curNameSelect.NameID].IsUnlocked == false)
                {
                    if (PlayerData.Instance.EffectNameID.Value >= 0)
                    {
                        NameCellUI nameCellUI = listNameCellUI.Find(e => e.nameID == PlayerData.Instance.EffectNameID.Value);
                        SelectNameCell(nameCellUI);
                    }
                    else
                    {
                        userNameEffect.InitUserNameEffect(inputName.text, -1);
                        curNameSelect = null;
                    }
                }
            }
            if (curBadgeSelect != null)
            {
                if (ProfileData.Instance.BadgeDatas[curBadgeSelect.ProfileID].IsUnlocked == false)
                {
                    if (PlayerData.Instance.BadgeIndex.Value >= 0)
                    {
                        ProfileCellUI badgeCellUI = listBadgeCellUI.Find(e => e.profileID == PlayerData.Instance.BadgeIndex.Value);
                        // Đổi ngầm (badge đang chọn bị khoá) — không mở popup badge 3D.
                        ApplyBadgeSelection(badgeCellUI);
                    }
                    else
                    {
                        userAvatarFrame.SetUpBadge(-1);
                        curBadgeSelect = null;
                    }
                }
            }
        }
    }
}
