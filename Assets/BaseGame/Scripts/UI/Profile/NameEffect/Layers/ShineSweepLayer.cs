using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Profile.NameEffects
{
    /// <summary>
    /// Vệt sáng quét qua tên theo chu kỳ.
    ///
    /// Vệt chỉ hiện TRÊN NÉT CHỮ, không phải một thanh sáng đè lên cả khung: layer
    /// dựng một bản sao TMP của chính cái tên đó rồi dùng nó làm <see cref="Mask"/>
    /// (showMaskGraphic = false → shader TMP bật UNITY_UI_ALPHACLIP, chỉ pixel có
    /// glyph mới ghi stencil). Bản sao không được vẽ ra, chỉ để cắt hình.
    ///
    /// Quãng quét lấy từ <see cref="TMP_Text.textBounds"/> nên dừng đúng ở ký tự
    /// cuối cùng, tên ngắn hay dài đều quét vừa đủ chứ không chạy hết bề ngang khung.
    ///
    /// Chưa có art vẫn chạy: thiếu sprite thì tự sinh dải gradient bằng code.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ShineSweepLayer : NameEffectLayer
    {
        [Header("Vệt sáng")]
        [Tooltip("Bỏ trống = tự tạo Image con lúc chạy.")]
        [SerializeField] private Image streak;

        [Tooltip("Bỏ trống = dùng gradient sinh bằng code.")]
        [SerializeField] private Sprite streakSprite;

        [Tooltip("Material cho vệt sáng — gán material UI Additive để bừng sáng hơn.")]
        [SerializeField] private Material streakMaterial;

        [SerializeField] private Color streakColor = new Color(1f, 1f, 1f, 0.9f);

        [Tooltip("Bề ngang vệt sáng (px).")]
        [SerializeField] private float streakWidth = 60f;

        [Tooltip("Nới chiều cao vệt so với chiều cao chữ, để vệt nghiêng không hở trên/dưới.")]
        [SerializeField] private float heightPadding = 24f;

        [Tooltip("Độ nghiêng (độ).")]
        [SerializeField] private float tiltDegrees = 20f;

        [Header("Phạm vi quét")]
        [Tooltip("Cắt vệt sáng theo hình chữ. Tắt = quét cả khung như một thanh sáng.")]
        [SerializeField] private bool clipToGlyphs = true;

        [Tooltip("Nới hai đầu quãng quét so với ký tự đầu/cuối (px).")]
        [SerializeField] private float edgePadding;

        [Tooltip("Mask dùng material gốc của font (không bóng đổ / viền) để vệt sáng bám đúng nét chữ. Tắt = vệt loang cả sang bóng đổ.")]
        [SerializeField] private bool maskFaceOnly = true;

        [Header("Nhịp")]
        [SerializeField] private float sweepDuration = 0.55f;

        [Tooltip("Khoảng nghỉ ngẫu nhiên giữa hai lần quét (giây).")]
        [SerializeField] private Vector2 intervalRange = new Vector2(2.5f, 4f);

        [Tooltip("Trễ ngẫu nhiên lần đầu, để các dòng trong list không quét đồng loạt.")]
        [SerializeField] private float randomStartDelay = 1.5f;

        [Tooltip("Vị trí vệt sáng theo tiến độ quét (0 = đầu chữ, 1 = cuối chữ). Mặc định ease-in-out; để trống dùng SmoothStep. Cho phép vượt 0..1 để có kiểu vọt/nảy.")]
        [SerializeField] private AnimationCurve moveOverSweep = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Alpha theo tiến độ quét (0 → 1). Để trống dùng đường vào/ra mềm mặc định.")]
        [SerializeField] private AnimationCurve alphaOverSweep = new AnimationCurve();

        private RectTransform selfRect;
        private RectTransform streakRect;
        private TextMeshProUGUI maskText;
        private Mask glyphMask;
        private RectMask2D boxMask;

        private bool needsSetup;
        private bool needsMaskSync;
        private bool sweeping;
        private float timer;
        private float sweepTime;
        private float startX;
        private float endX;

        private static Sprite generatedSprite;

        protected override void OnPlay()
        {
            // Dựng mask/vệt sáng ở nhịp Tick đầu tiên chứ không phải ngay đây:
            // Play có thể được gọi từ OnEnable của view, mà tạo GameObject rồi
            // SetParent trong lúc Unity đang bật/tắt hierarchy là lỗi.
            needsSetup = true;
            needsMaskSync = true;

            sweeping = false;
            sweepTime = 0f;
            timer = Random.Range(0f, Mathf.Max(0f, randomStartDelay));

            HideStreak();
        }

        protected override void OnStop()
        {
            sweeping = false;
            HideStreak();
        }

        protected override void OnTextChanged()
        {
            needsMaskSync = true;

            // Tên mới dài ngắn khác nhau → quãng quét cũ không còn đúng. Cắt lần
            // quét đang chạy dở thay vì để vệt sáng đứng sai chỗ trên tên mới.
            if (sweeping)
                EndSweep();
        }

        protected override void OnTick(float deltaTime)
        {
            if (needsSetup)
            {
                EnsureVisuals();
                needsSetup = false;
            }

            if (needsMaskSync)
            {
                SyncMask();
                needsMaskSync = false;
            }

            if (streakRect == null)
                return;

            if (!sweeping)
            {
                timer -= deltaTime;
                if (timer <= 0f)
                    BeginSweep();

                return;
            }

            sweepTime += deltaTime;
            float progress = sweepDuration > 0f ? sweepTime / sweepDuration : 1f;

            if (progress >= 1f)
            {
                EndSweep();
                return;
            }

            Vector2 position = streakRect.anchoredPosition;
            // LerpUnclamped để curve vượt ngoài 0..1 vẫn dùng được (ease-back,
            // vọt qua rồi kéo lại) — ra ngoài nét chữ thì mask tự giấu đi.
            position.x = Mathf.LerpUnclamped(startX, endX, EvaluateMove(progress));
            streakRect.anchoredPosition = position;

            SetStreakAlpha(EvaluateAlpha(progress));
        }

        private void BeginSweep()
        {
            if (!ResolveSweepRange())
            {
                // Chưa đo được chữ (mesh chưa dựng xong) — thử lại ở nhịp sau.
                timer = 0.1f;
                return;
            }

            sweeping = true;
            sweepTime = 0f;

            Vector2 position = streakRect.anchoredPosition;
            position.x = startX;
            streakRect.anchoredPosition = position;

            streak.enabled = true;
            SetStreakAlpha(0f);
        }

        private void EndSweep()
        {
            sweeping = false;
            timer = Random.Range(intervalRange.x, Mathf.Max(intervalRange.x, intervalRange.y));
            HideStreak();
        }

        /// <summary>
        /// Đo vùng chữ thật rồi đặt kích thước + quãng chạy của vệt sáng.
        /// Toạ độ quy về tâm rect của text, khớp với anchor giữa của vệt sáng.
        /// </summary>
        private bool ResolveSweepRange()
        {
            TMP_Text source = Context.Text;
            if (source == null || streakRect == null)
                return false;

            Rect rect = source.rectTransform.rect;
            Bounds bounds = source.textBounds;

            bool measured = source.textInfo != null
                            && source.textInfo.characterCount > 0
                            && bounds.size.x > 0.01f;

            float left, right, centerY, height;

            if (measured)
            {
                left = bounds.min.x - rect.center.x;
                right = bounds.max.x - rect.center.x;
                centerY = bounds.center.y - rect.center.y;
                height = bounds.size.y;
            }
            else
            {
                // Chưa đo được: quét cả rect. Nếu rect cũng chưa có kích thước thì
                // chưa layout xong, chờ nhịp sau.
                if (rect.width <= 1f)
                    return false;

                left = -rect.width * 0.5f;
                right = rect.width * 0.5f;
                centerY = 0f;
                height = rect.height;
            }

            float halfStreak = streakWidth * 0.5f;
            startX = left - halfStreak - edgePadding;
            endX = right + halfStreak + edgePadding;

            streakRect.sizeDelta = new Vector2(streakWidth, height + heightPadding);
            streakRect.anchoredPosition = new Vector2(startX, centerY);
            return true;
        }

        /// <summary>Tiến độ thời gian → tiến độ quãng đường. Không phải tuyến tính.</summary>
        private float EvaluateMove(float progress)
        {
            return moveOverSweep != null && moveOverSweep.length > 0
                ? moveOverSweep.Evaluate(progress)
                : Mathf.SmoothStep(0f, 1f, progress);
        }

        private float EvaluateAlpha(float progress)
        {
            float shape = alphaOverSweep != null && alphaOverSweep.length > 0
                ? alphaOverSweep.Evaluate(progress)
                : Mathf.Sin(progress * Mathf.PI);

            return Mathf.Clamp01(shape) * streakColor.a;
        }

        private void SetStreakAlpha(float alpha)
        {
            if (streak == null)
                return;

            Color color = streakColor;
            color.a = alpha;
            streak.color = color;
        }

        private void HideStreak()
        {
            if (streak != null)
                streak.enabled = false;
        }

        private void EnsureVisuals()
        {
            if (selfRect == null)
                selfRect = (RectTransform)transform;

            RectTransform clipParent;

            if (clipToGlyphs)
            {
                EnsureGlyphMask();
                clipParent = maskText.rectTransform;

                // Prefab cũ có sẵn RectMask2D — tắt đi, giờ mask là hình chữ.
                if (boxMask == null)
                    boxMask = GetComponent<RectMask2D>();

                if (boxMask != null)
                    boxMask.enabled = false;
            }
            else
            {
                if (boxMask == null)
                {
                    boxMask = GetComponent<RectMask2D>();
                    if (boxMask == null)
                        boxMask = gameObject.AddComponent<RectMask2D>();
                }

                boxMask.enabled = true;
                clipParent = selfRect;

                if (glyphMask != null)
                    glyphMask.gameObject.SetActive(false);
            }

            if (streak == null)
                streak = CreateStreak(clipParent);

            if (streak == null)
                return;

            streakRect = streak.rectTransform;

            if (streakRect.parent != clipParent)
                streakRect.SetParent(clipParent, false);

            streak.sprite = streakSprite != null ? streakSprite : GetGeneratedSprite();
            streak.raycastTarget = false;
            streak.type = Image.Type.Simple;

            if (streakMaterial != null)
                streak.material = streakMaterial;

            streakRect.anchorMin = new Vector2(0.5f, 0.5f);
            streakRect.anchorMax = new Vector2(0.5f, 0.5f);
            streakRect.pivot = new Vector2(0.5f, 0.5f);
            streakRect.localRotation = Quaternion.Euler(0f, 0f, tiltDegrees);
            streakRect.localScale = Vector3.one;
        }

        private void EnsureGlyphMask()
        {
            if (maskText != null)
            {
                maskText.gameObject.SetActive(true);
                return;
            }

            var go = new GameObject("GlyphMask", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(Mask));
            go.transform.SetParent(transform, false);

            maskText = go.GetComponent<TextMeshProUGUI>();
            maskText.raycastTarget = false;

            glyphMask = go.GetComponent<Mask>();
            // false = bản sao không được vẽ, và đây cũng là điều kiện để Unity bật
            // UNITY_UI_ALPHACLIP — không có nó thì stencil ghi cả ô chữ nhật.
            glyphMask.showMaskGraphic = false;

            StretchFull(maskText.rectTransform);
        }

        /// <summary>
        /// Chép thuộc tính từ text gốc sang bản sao làm mask. Phải khớp cả font,
        /// cỡ, canh lề lẫn nội dung thì hình cắt mới trùng nét chữ đang hiển thị.
        /// </summary>
        private void SyncMask()
        {
            if (!clipToGlyphs || maskText == null)
                return;

            TMP_Text source = Context.Text;
            if (source == null)
                return;

            maskText.font = source.font;

            // Material của tên thường có Underlay (bóng đổ) — lấy nguyên si thì
            // stencil ăn cả vùng bóng, vệt sáng loang ra ngoài nét chữ. Material
            // gốc của font asset chỉ có mặt chữ, cắt sạch hơn hẳn.
            Material maskMaterial = maskFaceOnly && source.font != null
                ? source.font.material
                : source.fontSharedMaterial;

            maskText.fontSharedMaterial = maskMaterial != null ? maskMaterial : source.fontSharedMaterial;
            maskText.fontSize = source.fontSize;
            maskText.fontSizeMin = source.fontSizeMin;
            maskText.fontSizeMax = source.fontSizeMax;
            maskText.enableAutoSizing = source.enableAutoSizing;
            maskText.fontStyle = source.fontStyle;
            maskText.alignment = source.alignment;
            maskText.characterSpacing = source.characterSpacing;
            maskText.wordSpacing = source.wordSpacing;
            maskText.lineSpacing = source.lineSpacing;
            maskText.paragraphSpacing = source.paragraphSpacing;
            maskText.textWrappingMode = source.textWrappingMode;
            maskText.overflowMode = source.overflowMode;
            maskText.richText = source.richText;
            maskText.margin = source.margin;
            maskText.text = source.text;

            // Mask cắt theo alpha nên bản sao phải đục hoàn toàn, kệ màu chữ gốc.
            maskText.enableVertexGradient = false;
            maskText.color = Color.white;
        }

        private Image CreateStreak(Transform parent)
        {
            var go = new GameObject("Streak", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            return go.GetComponent<Image>();
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Dải sáng 1 chiều: lõi sắc ở giữa cộng quầng mềm hai bên. Sinh một lần,
        /// dùng chung cho mọi effect có shine.
        /// </summary>
        private static Sprite GetGeneratedSprite()
        {
            if (generatedSprite != null)
                return generatedSprite;

            const int width = 64;
            var texture = new Texture2D(width, 1, TextureFormat.RGBA32, false)
            {
                name = "NameEffect_ShineStreak",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[width];
            for (int i = 0; i < width; i++)
            {
                float u = i / (width - 1f);
                float distance = Mathf.Abs(u * 2f - 1f);
                float soft = Mathf.Clamp01(1f - distance);
                float core = Mathf.Pow(soft, 6f);
                float alpha = Mathf.Clamp01(soft * 0.35f + core * 0.85f);

                pixels[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            generatedSprite = Sprite.Create(texture, new Rect(0f, 0f, width, 1f), new Vector2(0.5f, 0.5f), 100f);
            generatedSprite.name = "NameEffect_ShineStreak";
            generatedSprite.hideFlags = HideFlags.HideAndDontSave;
            return generatedSprite;
        }
    }
}
