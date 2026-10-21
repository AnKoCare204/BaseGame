using Sirenix.OdinInspector;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace Profile.ProfileItem
{
    public class ProfileItemPrefab : MonoBehaviour
    {
        private static readonly int FadeAmount = Shader.PropertyToID("_FadeAmount"); 
        [field: SerializeField] public Material HotPotFadeMaterial { get; private set; }


        [field: SerializeField] public int ItemId { get; private set; }

        [field: SerializeField] public Image ItemImage { get; private set; }

        [field: SerializeField] public ProfileAnimationController AnimationController { get; private set; }

        [field: SerializeField] public AnimationConfig AnimationConfig { get; private set; }

        [PreviewField]
        [field: SerializeField] public Sprite ItemSprite { get; private set; }

        private Material ItemMaterial { get; set; }

        public void SetupItem()
        {
            // Setup image
            if (ItemImage != null && ItemSprite != null)
            {
                ItemImage.sprite = ItemSprite;
                ItemImage.gameObject.SetActive(true);
            }
            else if (ItemImage != null)
            {
                ItemImage.gameObject.SetActive(false);
            }

            // Setup animation
            if (AnimationController != null)
            {
                if (AnimationConfig != null && AnimationConfig.HasAnimation())
                {
                    ItemImage.gameObject.SetActive(false);
                    AnimationController.SetActive(true);
                    AnimationController.Initialize(AnimationConfig);
                }
                else
                {
                    AnimationController.SetActive(false);
                    ItemImage.gameObject.SetActive(true);
                }
            }
        }

        public void Reset()
        {
            if (ItemImage != null)
            {
                ItemImage.sprite = null;
                ItemImage.gameObject.SetActive(false);
            }

            if (AnimationController != null)
            {
                AnimationController.Reset();
            }
        }

        public void SetFadeAmount(float fadeAmount)
        {
            
            if (ItemImage != null)
            {
                if (ItemMaterial == null)
                {
                    ItemMaterial = Instantiate(HotPotFadeMaterial);
                    ItemImage.material = ItemMaterial;
                }

                ItemMaterial.SetFloat(FadeAmount, fadeAmount);
            }

            if (AnimationController != null && AnimationController.IsActive())
            {
                AnimationController.SetActive(fadeAmount > 0.5f);
            }
        }
    }
}

