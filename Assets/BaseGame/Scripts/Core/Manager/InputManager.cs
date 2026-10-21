using System.Collections.Generic;
using R3;
using R3.Triggers;
using TW.Utility.DesignPattern;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Core.Manager
{
    /// <summary>
    /// Input gameplay. Chọn cá theo kiểu đè - rê - thả: nhấn xuống thì con cá bấm được dưới tay hiện viền (Fish.SetHighlighted),
    /// rê tay chưa nhấc thì viền chuyển sang con đang chỉ vào (rung nhẹ mỗi lần đổi con), nhấc tay thì pick con đang có viền;
    /// nhấc tay mà không con nào có viền thì hủy lượt, không chọn gì. Chọn bể cho booster (Magnet / Swap) và bấm bóng đá / băng
    /// vẫn theo click (nhấn - thả nhanh, R3Input.WasClicked). Mỗi lần nhấn xuống trong vùng chơi (không trên UI) phát
    /// hiệu ứng nổ bong bóng TapEffect tại điểm chạm, bất kể có trúng cá / bóng hay không.
    /// </summary>
    public class InputManager : Singleton<InputManager>
    {
        [field: SerializeField] public R3Input R3Input { get; private set; } = new();

        [field: SerializeField] private TapEffect TapEffectPrefab { get; set; }
        public Vector2 PointerPosition => R3Input.PointerPosition;

        /// <summary>Mỗi lần nhấn xuống màn hình (kể cả trên UI, kể cả khi pause). IdleHintController / thống kê phiên level reset giờ đứng yên.</summary>
        public static event System.Action OnPointerPressed;

        /// <summary>Con cá dưới tay đang đè (đại diện nhóm có viền); null khi không đè hoặc dưới tay không có cá bấm được.</summary>
        public Fish HighlightedFish { get; private set; }

        /// <summary>Nhóm cá đang có viền: bóng thường một con, FishSchoolBubble cả đàn (viền đỏ khi đàn thiếu chỗ).</summary>
        public IReadOnlyList<Fish> HighlightedGroup => highlighted;

        private readonly List<Fish> highlighted = new();
        private readonly List<Fish> pickBuffer = new();
        private bool highlightDenied;                    // nhóm đang viền đỏ: đàn FishSchool không đủ chỗ bể + ô chờ
        private readonly List<Fish> lingering = new();   // viền đỏ còn giữ SchoolDenyLinger giây sau khi nhấc tay
        private float lingerUntil;

        /// <summary>Đang trong một lượt đè tay chọn cá (từ lúc nhấn tới lúc nhấc).</summary>
        private bool isPicking;

        private void Start()
        {
            R3Input.Enable();
            this.UpdateAsObservable()
                .Subscribe(OnUpdate)
                .AddTo(this);
#if UNITY_EDITOR
            this.UpdateAsObservable()
                .Where(_ => Keyboard.current.spaceKey.wasPressedThisFrame)
                .Subscribe(_ => Debug.Break())
                .AddTo(this);
#endif
        }

        private void OnDisable()
        {
            R3Input.Disable();
            CancelPick();
            EndDenyLinger();
        }

        private void OnUpdate(Unit unit)
        {
            if (lingering.Count > 0 && Time.time >= lingerUntil) EndDenyLinger();
            // WasClicked phải gọi mỗi frame để R3Input ghi thời điểm / vị trí nhấn.
            bool clicked = R3Input.WasClicked(unit);
            if (R3Input.WasPressedThisFrame(unit))
            {
                OnPointerPressed?.Invoke();
                PlayTapEffect();
                BeginPick();
            }
            if (isPicking) UpdatePick();
            bool picked = R3Input.WasReleasedThisFrame(unit) && EndPick();
            if (clicked && !picked) OnClick();
        }

        /// <summary>
        /// Click nhanh không dính cá: chọn bể cho booster đang chờ đích, bấm ô bể khóa (dấu +) để dùng / mua UnlockTank,
        /// hoặc bấm bóng đá / băng / vỏ.
        /// </summary>
        private void OnClick()
        {
            if (!TryGetLevel(out Level level) || IsPointerOverUI()) return;
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsRestrictingTaps) return;

            if (BoosterManager.Instance.IsSelectingTarget)
            {
                if (CameraManager.Instance.TryGetTank(out TankSlotView tank)) BoosterManager.Instance.OnTankPicked(tank);
                return;
            }

            if (BoosterManager.Instance.IsUsingBooster) return;
            if (CameraManager.Instance.TryGetFish(out _)) return; // cá chỉ chọn qua luồng đè - thả
            if (CameraManager.Instance.TryGetTank(out TankSlotView lockedTank) && lockedTank.IsLocked)
            {
                BoosterManager.Instance.OnLockedTankTapped(lockedTank);
                return;
            }

            if (CameraManager.Instance.TryGetBubble(out BubbleView bubble)) level.OnBubbleTapped(bubble);
        }

        /// <summary>
        /// Nhấn xuống trong vùng chơi (đang chơi, không pause, không trên UI): phát hiệu ứng nổ bong bóng tại điểm chạm,
        /// kể cả khi trúng cá / bóng hay đang dùng booster. Chỉ hình, không kèm âm thanh.
        /// </summary>
        private void PlayTapEffect()
        {
            if (TapEffectPrefab == null || !TryGetLevel(out _) || IsPointerOverUI()) return;
            if (!CameraManager.Instance.TryGetMousePosition(out Vector3 point)) return;
            TapEffect.Play(TapEffectPrefab, point, transform);
        }

        /// <summary>Nhấn xuống: mở lượt đè tay nếu đang chơi, không trên UI và không có booster đang chạy / chờ chọn bể.</summary>
        private void BeginPick()
        {
            CancelPick();
            if (!TryGetLevel(out _) || IsPointerOverUI()) return;
            if (BoosterManager.Instance.IsUsingBooster) return;
            isPicking = true;
        }

        /// <summary>
        /// Mỗi frame đang đè: viền nhóm cá sẽ bay nếu bấm con dưới tay (nới thêm FishPickPadding; bóng thường một con, FishSchoolBubble
        /// cả đàn), bỏ viền khi không có. Đàn FishSchool bấm được nhưng thiếu chỗ thì viền đỏ cả đàn và nhuộm đỏ ô chờ (bấm sẽ bị từ chối).
        /// </summary>
        private void UpdatePick()
        {
            if (!TryGetLevel(out Level level) || BoosterManager.Instance.IsUsingBooster)
            {
                CancelPick();
                return;
            }

            Fish fish = null;
            bool denied = false;
            pickBuffer.Clear();
            if (!IsPointerOverUI() && CameraManager.Instance.TryGetFish(out Fish hit, GamePlayGlobalConfig.Instance.FishPickPadding))
            {
                if (level.TryGetPickGroup(hit, pickBuffer, out denied) || denied) fish = hit;
            }

            SetHighlighted(fish, pickBuffer, denied, level);
        }

        /// <summary>
        /// Nhấc tay: pick con đang có viền, không có thì hủy lượt. Nhóm đang viền đỏ thì giữ viền đỏ + ô chờ đỏ thêm SchoolDenyLinger giây
        /// (PerformPick sẽ bị từ chối: rung + tiếng từ chối như bấm hụt). Trả về true nếu đã pick.
        /// </summary>
        private bool EndPick()
        {
            if (!isPicking) return false;
            Fish fish = HighlightedFish;
            bool denied = highlightDenied;
            if (highlightDenied) BeginDenyLinger();
            CancelPick();
            if (denied && fish != null) Core.GamePlay.GameGlobalEvent.OnFishTapDenied?.Invoke(fish);
            return fish != null && PerformPick(fish);
        }

        private void CancelPick()
        {
            isPicking = false;
            SetHighlighted(null, null, false, null);
        }

        /// <summary>Đổi nhóm cá có viền (group rỗng / null = chỉ con fish); rung nhẹ mỗi lần viền chuyển sang một con hoặc đổi màu; viền đỏ (denied) kèm tiếng SfxTapDenied.</summary>
        private void SetHighlighted(Fish fish, List<Fish> group, bool denied, Level level)
        {
            if (fish == HighlightedFish && denied == highlightDenied) return;
            ClearHighlight();
            HighlightedFish = fish;
            highlightDenied = denied;
            if (fish == null) return;
            EndDenyLinger();   // đè lại khi viền đỏ cũ còn giữ: bỏ viền cũ, nhóm mới quyết định màu
            if (group != null) highlighted.AddRange(group);
            if (highlighted.Count == 0) highlighted.Add(fish);
            foreach (Fish f in highlighted)
            {
                if (f == null) continue;
                if (denied) f.SetDenied(true);
                else f.SetHighlighted(true);
            }

            if (denied && level != null) level.SetHoldingDeny(true);
            if (denied && AudioManager.Instance != null) AudioManager.Instance.PlaySoundFx(AudioKey.SfxTapDenied);   // viền đỏ: kèm tiếng từ chối như bấm hụt
            if (VibrationManager.Instance != null) VibrationManager.Instance.CallHaptic(HapticType.LightImpact);
        }

        /// <summary>Tắt viền của nhóm đang đè; ô chờ trả màu trừ khi viền đỏ đang được giữ sau nhấc tay (lingering).</summary>
        private void ClearHighlight()
        {
            bool wasDenied = highlightDenied;
            foreach (Fish f in highlighted)
            {
                if (f == null) continue;
                if (wasDenied) f.SetDenied(false);
                else f.SetHighlighted(false);
            }

            highlighted.Clear();
            HighlightedFish = null;
            highlightDenied = false;
            if (wasDenied && lingering.Count == 0 && TryGetCurrentLevel(out Level level)) level.SetHoldingDeny(false);
        }

        /// <summary>Chuyển nhóm viền đỏ đang đè sang trạng thái giữ: cá và ô chờ vẫn đỏ tới hết SchoolDenyLinger giây rồi EndDenyLinger tắt.</summary>
        private void BeginDenyLinger()
        {
            EndDenyLinger();
            lingering.AddRange(highlighted);
            highlighted.Clear();
            highlightDenied = false;   // ClearHighlight không còn gì để tắt; viền đỏ + ô chờ đỏ tắt ở EndDenyLinger
            lingerUntil = Time.time + GamePlayGlobalConfig.Instance.SchoolDenyLinger;
        }

        private void EndDenyLinger()
        {
            if (lingering.Count == 0) return;
            foreach (Fish f in lingering)
            {
                if (f != null) f.SetDenied(false);
            }

            lingering.Clear();
            if (TryGetCurrentLevel(out Level level)) level.SetHoldingDeny(false);
        }

        /// <summary>Level đang chơi kể cả khi đang pause (để trả màu ô chờ), null nếu chưa có.</summary>
        private static bool TryGetCurrentLevel(out Level level)
        {
            level = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevel : null;
            return level != null;
        }

        public bool PerformPick(Fish fish)
        {
            if (fish == null) return false;
            return TryGetLevel(out Level level) && level.OnFishTapped(fish);
        }

        /// <summary>Level đang chơi (không pause), null nếu không có.</summary>
        private static bool TryGetLevel(out Level level)
        {
            level = null;
            if (LevelManager.Instance.IsPause) return false;
            level = LevelManager.Instance.CurrentLevel;
            return level != null;
        }

        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
