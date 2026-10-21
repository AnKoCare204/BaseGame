using UnityEngine;
using R3;
using UnityEngine.UI;

namespace CoreNotification
{
    public class ProfileNotification : MonoBehaviour
    {
        [field: SerializeReference] private IProfileNotificationCondition Condition { get; set; }
        [field: SerializeField] private GameObject MainNotification { get; set; }
        private void Awake()
        {
            Condition.Initialize();
            Condition.IsShowNotification.Subscribe(SetNotification).AddTo(this);
        }

        private void OnDestroy()
        {
            // Gỡ subscription mà Condition.Initialize() đã tạo vào các Reactive global của ProfileData.
            // Bỏ bước này thì mỗi lần prefab được Instantiate lại là rò rỉ thêm một bộ subscriber.
            Condition?.Dispose();
        }

        private void OnEnable()
        {
            SetNotification(Condition.IsShowNotification.Value);
        }

        private void SetNotification(int isShow)
        {
            MainNotification.SetActive(isShow >= 0);
        }

        private void HideNotification()
        {
            Condition.IsShowNotification.Value = -1;
        }
    }
}