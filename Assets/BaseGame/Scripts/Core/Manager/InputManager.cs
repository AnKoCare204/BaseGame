using R3;
using R3.Triggers;
using TW.Utility.DesignPattern;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Core.Manager
{
    /// <summary>
    /// Input gameplay: đọc con trỏ (chuột / chạm) qua R3Input rồi chuyển cho Level đang chơi (OnPointerDown / OnPointerUp / OnClick).
    /// Không gửi khi đang pause hoặc con trỏ đè lên UI. Gameplay của từng game xử lý trong lớp kế thừa Level.
    /// </summary>
    public class InputManager : Singleton<InputManager>
    {
        [field: SerializeField] public R3Input R3Input { get; private set; } = new();
        public Vector2 PointerPosition => R3Input.PointerPosition;

        /// <summary>Mỗi lần nhấn xuống màn hình (kể cả trên UI, kể cả khi pause). Thống kê phiên level reset giờ đứng yên.</summary>
        public static event System.Action OnPointerPressed;

        private void Start()
        {
            R3Input.Enable();
            this.UpdateAsObservable()
                .Subscribe(OnUpdate)
                .AddTo(this);
#if UNITY_EDITOR
            this.UpdateAsObservable()
                .Where(_ => Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                .Subscribe(_ => Debug.Break())
                .AddTo(this);
#endif
        }

        private void OnDisable()
        {
            R3Input.Disable();
        }

        private void OnUpdate(Unit unit)
        {
            // WasClicked phải gọi mỗi frame để R3Input ghi thời điểm / vị trí nhấn.
            bool clicked = R3Input.WasClicked(unit);
            if (R3Input.WasPressedThisFrame(unit))
            {
                OnPointerPressed?.Invoke();
                if (TryGetLevel(out Level level) && !IsPointerOverUI()) level.OnPointerDown(PointerPosition);
            }

            if (R3Input.WasReleasedThisFrame(unit) && TryGetLevel(out Level releasedLevel)) releasedLevel.OnPointerUp(PointerPosition);
            if (clicked && TryGetLevel(out Level clickedLevel) && !IsPointerOverUI()) clickedLevel.OnClick(PointerPosition);
        }

        /// <summary>Level đang chơi (không pause), null nếu không có.</summary>
        private static bool TryGetLevel(out Level level)
        {
            level = null;
            if (LevelManager.Instance == null || LevelManager.Instance.IsPause) return false;
            level = LevelManager.Instance.CurrentLevel;
            return level != null;
        }

        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
