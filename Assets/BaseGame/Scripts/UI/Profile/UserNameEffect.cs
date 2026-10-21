using System.Collections.Generic;
using Profile.NameEffects;
using R3;
using TMPro;
using UnityEngine;
using Sirenix.OdinInspector;
using TW.Reactive.CustomComponent;

namespace Profile.ProfileItem
{
    public class UserNameEffect : MonoBehaviour
    {
        [FoldoutGroup("Name Effect")][field: SerializeField] public bool autoGetCurrentNameEffect = false;

        /// <summary>
        /// Bộ vẽ hiệu ứng tên. Bỏ trống = giữ nguyên hành vi cũ (chỉ đổi chữ),
        /// nên mọi prefab chưa gắn host vẫn chạy y như trước.
        /// </summary>
        [FoldoutGroup("Name Effect")][SerializeField] private NameEffectHost nameEffectHost;
        [field: SerializeField] public TextMeshProUGUI txtName { get; private set; }
        [field: SerializeField] public Reactive<string> UserName { get; set; } = new("");
        [field: SerializeField] public Reactive<int> id { get; set; } = new(0);
        [ShowInInspector] private bool isCurrentUser = false;
        [ShowInInspector] private bool isOnlyChangeName = false;

        private int idEffectName = -1;

        private void Start()
        {
            UserName = PlayerData.Instance.UserName;
            UserName.Subscribe(SetUserNameCurrentUser).AddTo(this);

            id = PlayerData.Instance.EffectNameID;
            id.Skip(1).Subscribe(SetIdCurrentUser).AddTo(this);

            if (autoGetCurrentNameEffect)
            {
                InitUserNameEffect(PlayerData.Instance.UserName.Value, PlayerData.Instance.EffectNameID.Value);
                SetIsCurrentUser(true);
            }
        }

        public int GetID()
        {
            return idEffectName;
        }

        public void SetIsCurrentUser(bool isCurrentUser, bool isOnlyChangeName = false)
        {
            this.isCurrentUser = isCurrentUser;
            this.isOnlyChangeName = isOnlyChangeName;
        }

        private void SetUserNameCurrentUser(string name)
        {
            if (!isCurrentUser)
            {
                return;
            }

            if (isOnlyChangeName)
            {
                txtName.text = name;
                return;
            }

            InitUserNameEffect(name, idEffectName);
        }

        private void SetIdCurrentUser(int id)
        {
            if (!isCurrentUser || isOnlyChangeName)
            {
                return;
            }
            InitUserNameEffect(txtName.text, id);
        }

        public void InitUserNameEffect(string name, int id)
        {
            // Id nào đã đăng ký trong NameEffectDatabase thì hợp lệ, khỏi tra
            // ProfileGlobalConfig — GetDefaultNameData() dựng một List mới mỗi lần
            // gọi, mà hàm này chạy cho từng dòng khi cuộn bảng xếp hạng.
            NameEffectDatabase effectDatabase = NameEffectDatabase.Instance;
            bool knownEffect = effectDatabase != null && effectDatabase.Contains(id);

            if (!knownEffect && id >= ProfileGlobalConfig.Instance.GetDefaultNameData().Count)
            {
                id = 0;
            }

            this.idEffectName = id;

            if (string.IsNullOrEmpty(name))
            {
                name = "Player";
            }

            txtName.text = name;

            ApplyNameEffect(id);
        }

        /// <summary>
        /// Đẩy id xuống <see cref="NameEffectHost"/>. Host tự lo phần tra bảng,
        /// thuê view từ pool và trả style gốc khi cell bị tái sử dụng.
        /// </summary>
        private void ApplyNameEffect(int id)
        {
            if (nameEffectHost == null)
            {
                return;
            }

            if (nameEffectHost.Text == null)
            {
                nameEffectHost.SetText(txtName);
            }

            nameEffectHost.Apply(id);
        }
    }
}