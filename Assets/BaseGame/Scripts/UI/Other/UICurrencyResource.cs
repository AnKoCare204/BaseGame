using CoreData;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using R3;

namespace Core.UI.Other
{
    public class UICurrencyResource : MonoBehaviour
    {
        [field: SerializeField] public ResourceType CurrencyType { get; private set; } = ResourceType.None;
        [field: SerializeField] protected TextMeshProUGUI TextAmount { get; set; }
        [field: SerializeField] public Button ShowResourceBtn { get; set; }
        protected int Amount { get; set; }
        protected int AmountChange{ get; set; }

        private void Start()
        {
            GameResource gameResource = ResourceData.Instance.GetResource(CurrencyType);
            gameResource.Amount.Subscribe(OnResourceChange).AddTo(this);
            if (ShowResourceBtn != null)
            {
                ShowResourceBtn.SetOnClickDestination(ShowResource);
            }

            OnInit();
        }

        public virtual void OnInit()
        {
        }

        protected virtual void TestAddCurrency()
        {
        }


        protected virtual void OnResourceChange(int value)
        {
        }

        protected virtual UniTask ShowResource()
        {
            return UniTask.CompletedTask;
        }

        protected void ChangeText(int val)
        {
            TextAmount.SetTextFormat("{0}", val);
            Amount = val;
        }
    }
}