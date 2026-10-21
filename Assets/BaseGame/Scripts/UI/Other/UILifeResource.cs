using System;
using Core.UI.Modals;
using CoreData;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Game.Helper;
using R3;
using R3.Triggers;
using TMPro;
using TW.UGUI.Core.Modals;
using TW.UGUI.Core.Views;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.Other
{
    public class UILifeResource : UICurrencyResource
    {
        [field: SerializeField] private Image HeartIcon { get; set; }
        [field: SerializeField] private bool IsInFillLife { get; set; } = false;
        [field: SerializeField] private TextMeshProUGUI TextTime { get; set; }
        [field: SerializeField] private GameObject PlusObj { get; set; }
        [field: SerializeField] private bool IsClick { get; set; } = false;
        private bool RealValue { get; set; } 
        public int MaxLife => GameManager.Instance.MaxLife;
        protected override void OnResourceChange(int value)
        {
            if (GameManager.Instance.IsState(GameState.MainMenu))
            {
                RealValue = true;
            }
            Amount = RealValue ? value : value + 1;
            Amount = Mathf.Min(Amount, MaxLife);
            base.OnResourceChange(Amount);
        }

        public override void OnInit()
        {
            base.OnInit();
            if (!DefaultGlobalConfig.Instance.IsHeart)
            {
                gameObject.SetActive(false);
            }
            this.UpdateAsObservable().Subscribe(OnInfinityTimeChanged).AddTo(this);
            this.UpdateAsObservable().Subscribe(OnFillHeartTimeChanged).AddTo(this);

        }

        private void Update()
        {
            PlusObj.SetActive(IsClick && CanAddLife());
        }

        private void OnInfinityTimeChanged(Unit _)
        {
            if (PlayerResourceManager.Instance.HasInfiniteLife)
            {
                HeartIcon.sprite = ItemGlobalConfig.Instance.GetItemSprite(ResourceType.InfiniteLife);
                if (IsInFillLife)
                {
                    TextTime.text = "";
                }
                else
                {
                    TextTime.text = FormatTimeSpan(PlayerResourceManager.Instance.InfinityLifeExpire);
                    TextAmount.gameObject.SetActive(false);
                }
            }
            else
            {
                TextAmount.gameObject.SetActive(true);
                HeartIcon.sprite = ItemGlobalConfig.Instance.GetItemSprite(ResourceType.Life);
                TextAmount.SetTextFormat(MyCacheUI.textFormat, Amount);
            }
        }
        
        private void OnFillHeartTimeChanged(Unit _)
        {
            bool isFullLife = PlayerResourceManager.Instance.HasFullLife;
            bool isInfiniteLife = PlayerResourceManager.Instance.HasInfiniteLife;
            if ((isFullLife || Amount >= MaxLife) && !isInfiniteLife)
            {
                TextTime.text = "FULL";
                return;
            }
        
            if (isInfiniteLife) return;
        
            TextTime.text = PlayerResourceManager.Instance.TimeToNextLife.TimeSpanToStringUI();
        }
        
        private bool CanAddLife()
        {
            if (PlayerResourceManager.Instance.HasFullLife) return false;
            if (PlayerResourceManager.Instance.HasInfiniteLife) return false;
            return true;
        }

        public string FormatTimeSpan(TimeSpan timeSpan)
        {
            // Nếu tổng thời gian >= 1 giờ → hiển thị dạng hh:mm
            if (timeSpan.TotalHours >= 1)
            {
                return $"{(int)timeSpan.TotalHours}h:{timeSpan.Minutes:D2}m";
            }
            // Nếu < 1 giờ → hiển thị dạng mm:ss
            else
            {
                return $"{(int)timeSpan.TotalMinutes}m:{timeSpan.Seconds:D2}s";
            }
        }

        protected override async UniTask ShowResource()
        {
            if(!IsClick) return;
            if (!CanAddLife()) return;
            ViewOptions viewOptions = new ViewOptions(nameof(ModalFillHeart));
            await ModalContainer.Find(ContainerKey.Modals).PushAsync(viewOptions);
        }
    }
}

