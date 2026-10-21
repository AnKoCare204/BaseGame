using System;
using CoreNotification;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using TW.UGUI.Core.Modals;
using TW.UGUI.MVPPattern;
using UnityEngine;
using UnityEngine.UI;

namespace UI.ModalProfile
{
    public class ModalProfile : Modal
    {
        [field: SerializeField] public ModalProfileContext.UIPresenter UIPresenter { get; private set; }
        protected override void Awake()
        {
            base.Awake();
            // The lifecycle event of the view will be added with priority 0.
            // Presenters should be processed after the view so set the priority to 1.
            AddLifecycleEvent(UIPresenter, 1);
            AddLifecycleEvent(new MainMenuPopupLifecycle(), 2);
        }
        public override async UniTask Initialize(Memory<object> args)
        {
            await base.Initialize(args);
        }

        [Serializable]
        public class ModalProfileContext
        {
            [HideLabel]
            [Serializable]
            public class UIModel : IAModel
            {
                [field: Title(nameof(UIModel))]
                public UniTask Initialize(Memory<object> args)
                {
                    return UniTask.CompletedTask;
                }
            }

            [HideLabel]
            [Serializable]
            public class UIView : IAView
            {
                [field: Title(nameof(UIView))]
                [field: SerializeField] public CanvasGroup MainView { get; private set; }
                [field: SerializeField] public Button CloseBtn { get; private set; }
                [field: SerializeField] public Button ExitBtn { get; private set; }
                [field: SerializeField] public Button OpenEditProfileBtn { get; private set; }
                [field: SerializeField] public GameObject PopupProfileEdit { get; private set; }
                [field: SerializeField] public GameObject PopupProfileView { get; private set; }
                [field: SerializeField] public PopupEditProfile PopupEditProfile { get; private set; }

                public UniTask Initialize(Memory<object> args)
                {
                    return UniTask.CompletedTask;
                }
            }

            [HideLabel]
            [Serializable]
            public class UIPresenter : IAPresenter, IModalLifecycleEventSimple
            {
                [field: SerializeField] public UIModel Model { get; private set; } = new();
                [field: SerializeField] public UIView View { get; set; } = new();

                public async UniTask Initialize(Memory<object> args)
                {
                    await Model.Initialize(args);
                    await View.Initialize(args);
                    View.CloseBtn.onClick.AddListener(OnCloseEditProfile);
                    View.ExitBtn.onClick.AddListener(OnCloseModal);
                    View.OpenEditProfileBtn.onClick.AddListener(OnOpenEditProfile);
                    View.PopupEditProfile.OnSave += OnCloseEditProfile;

                    if (args.Length > 0)
                    {
                        bool forceShowEditProfile = (bool)args.Span[0];
                        if (forceShowEditProfile)
                        {
                            OnOpenEditProfile();
                        }
                    }
                }
                public void OnCloseModal()
                {
                    ModalContainer.Find(ContainerKey.Modals).Pop(true);
                }
                public void OnOpenEditProfile()
                {
                    View.PopupProfileEdit.SetActive(true);
                    View.PopupProfileView.SetActive(false);
                }

                public void OnCloseEditProfile()
                {
                    View.PopupProfileEdit.SetActive(false);
                    View.PopupProfileView.SetActive(true);

                    ProfileData.Instance.OnChangeStatusNotification(new AvatarNotificationCondition(), -1);
                }
            }
        }
    }
}