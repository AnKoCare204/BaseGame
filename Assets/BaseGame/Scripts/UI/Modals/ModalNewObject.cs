using System;
using Core.UI.Screens;
using CoreData;
using Cysharp.Threading.Tasks;
using TW.UGUI.MVPPattern;
using UnityEngine;
using R3;
using Sirenix.OdinInspector;
using TMPro;
using TW.UGUI.Core.Modals;
using UnityEngine.UI;

namespace Core.UI.Modals
{
    public class ModalNewObject : Modal
    {
        [field: SerializeField] public ModalNewObjectContext.UIPresenter UIPresenter { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            AddLifecycleEvent(UIPresenter, 1);
        }

        public override async UniTask Initialize(Memory<object> args)
        {
            await base.Initialize(args);
        }
    }


    [Serializable]
    public class ModalNewObjectContext
    {
        public static class Events
        {
            public static Action SampleEvent { get; set; }
        }

        [HideLabel]
        [Serializable]
        public class UIModel : IAModel
        {
            [field: Title(nameof(UIModel))]
            [field: SerializeField]
            public SerializableReactiveProperty<int> SampleValue { get; private set; }
            [field: SerializeField] public ResourceType BoosterType { get; private set; }
            [field: SerializeField] public bool IsBooster { get; private set; }

            public UniTask Initialize(Memory<object> args)
            {
                string temp = args.Length > 0 ? args.Span[0].ToString() : string.Empty;
                if (!string.IsNullOrEmpty(temp))
                {
                    string objectType = temp.Substring(0,temp.IndexOf('_'));
                    if(objectType == "Booster")
                    {
                        string extracted = temp.Substring(temp.IndexOf('_') + 1);
                        BoosterType = (ResourceType)Enum.Parse(typeof(ResourceType), extracted);
                        IsBooster = true;
                    }
                }
                return UniTask.CompletedTask;
            }
        }

        [HideLabel]
        [Serializable]
        public class UIView : IAView
        {
            [field: Title(nameof(UIView))]
            [field: SerializeField]
            public CanvasGroup MainView { get; private set; }
            [field: SerializeField] public TextMeshProUGUI ObjNameTxt { get; private set; }
            [field: SerializeField] public TextMeshProUGUI ObjDesTxt { get; private set; }
            [field: SerializeField] public Image ObjIcon { get; private set; }
            [field: SerializeField] public Button ClaimBtn { get; private set; }

            public UniTask Initialize(Memory<object> args)
            {
                string temp = args.Length > 0 ? args.Span[0].ToString() : string.Empty;
                if (!string.IsNullOrEmpty(temp))
                {
                    string objectType = temp.Substring(0,temp.IndexOf('_'));
                    // Debug.Log($"{temp} {objectType} {temp.Substring(temp.IndexOf('_') + 1)}");
                    if(objectType == "Booster")
                    {
                        string extracted = temp.Substring(temp.IndexOf('_') + 1);
                        ResourceType boosterType = (ResourceType)Enum.Parse(typeof(ResourceType), extracted);
                        BoosterInfoData boosterData = ItemGlobalConfig.Instance.GetBoosterData(boosterType);
                        ObjNameTxt.text = boosterData.Name;
                        ObjDesTxt.text = boosterData.Description;
                        ObjIcon.sprite = boosterData.Sprite;
                    }
                }
                
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
                View.ClaimBtn.SetOnClickDestination(OnButtonCloseClick);
                ScreenInGameContext.Events.OnUnlockWaitClaimBooster?.Invoke(Model.BoosterType);
            }
            
            private async UniTask OnButtonCloseClick()
            {
                if (Model.IsBooster)
                {
                    ScreenInGameContext.Events.ClaimBooster?.Invoke(Model.BoosterType);
                }
                await ModalContainer.Find(ContainerKey.Modals).PopAsync(true);
            }
        }
    }
}