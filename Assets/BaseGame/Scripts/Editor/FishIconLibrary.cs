using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Kho icon cá của tool vẽ level: tra ảnh trong FishIconGlobalConfig.
///
/// Bản đầu dựng prefab rồi chụp model bằng PreviewRenderUtility. Cách đó phụ thuộc model, ánh sáng và cả thời điểm
/// gọi (Render giữa lúc ProcessEvent là lỗi "RenderSettings is NULL"), nên bảng cá đổi một cái là hỏng hiển thị.
/// Đọc sprite thì đồng bộ, không dựng gì, không huỷ gì - texture là asset của project nên TUYỆT ĐỐI không Destroy.
///
/// Loài chưa có ảnh trong bảng (hidden_fish / spawner_fish, các ID âm) mới lùi về AssetPreview của prefab; ảnh đó
/// nướng bất đồng bộ nên mới cần onReady để vẽ lại.
/// </summary>
public sealed class FishIconLibrary : IDisposable
{
    private readonly Dictionary<int, Texture> cache = new Dictionary<int, Texture>();

    /// <summary>Id đã tra mà không ra ảnh; nhớ lại để khỏi tra đi tra lại mỗi lần Repaint.</summary>
    private readonly HashSet<int> missing = new HashSet<int>();

    private readonly Action onReady;
    private bool disposed;

    /// <param name="onReady">Gọi khi AssetPreview của loài không có ảnh nướng xong, thường là Repaint của tool.</param>
    public FishIconLibrary(Action onReady)
    {
        this.onReady = onReady;
    }

    /// <summary>Icon của loài fishId; null khi loài đó không có ảnh trong bảng lẫn prefab để lấy ảnh.</summary>
    public Texture Get(int fishId)
    {
        if (disposed) return null;
        if (cache.TryGetValue(fishId, out Texture cached) && cached != null) return cached;
        if (missing.Contains(fishId)) return null;

        bool pending = false;
        Texture icon = LoadFromIconConfig(fishId);
        if (icon == null) icon = LoadAssetPreview(fishId, out pending);
        if (icon != null)
        {
            cache[fishId] = icon;
            return icon;
        }

        // Ảnh đang nướng thì để lần vẽ sau tra lại; chỉ loài thật sự không có ảnh mới ghi vào missing.
        if (!pending) missing.Add(fishId);
        return null;
    }

    /// <summary>Quên hết ảnh đã tra. Gọi sau khi quét lại FishIconGlobalConfig để tool nhận ảnh mới ngay.</summary>
    public void Refresh()
    {
        cache.Clear();
        missing.Clear();
    }

    public void Dispose()
    {
        disposed = true;
        // Không DestroyImmediate: mọi texture ở đây đều là asset của project, huỷ là hỏng file trong Project view.
        cache.Clear();
        missing.Clear();
    }

    /// <summary>
    /// Ảnh lấy từ bảng ánh xạ ID -> Sprite trong FishIconGlobalConfig.
    ///
    /// Dùng sprite.texture chứ không cắt theo textureRect: mỗi file trong FishInBubble chỉ chứa đúng một sprite phủ
    /// trọn ảnh. Ngày nào một file thành sprite sheet nhiều ô thì ô cá sẽ hiện cả tấm sheet - sai thì thấy ngay chứ
    /// không âm thầm, lúc đó mới cần vẽ bằng GUI.DrawTextureWithTexCoords.
    /// </summary>
    private static Texture LoadFromIconConfig(int fishId)
    {
        FishIconGlobalConfig config = FishIconGlobalConfig.Instance;
        if (config == null) return null;

        // Bảng ảnh đã đánh theo ID của FullFishModelConfigList nên tra thẳng. Không suy thêm từ số trên tên prefab:
        // số đó là ID của loài khác, tra theo nó là ra nhầm ảnh.
        Sprite sprite = config.GetIcon(fishId);
        return sprite != null ? sprite.texture : null;
    }

    /// <summary>
    /// Model của loài, hoặc null nếu bảng cá không có id này. GetFishModelConfigById khi tra hụt sẽ trả về entry đầu
    /// bảng chứ không báo gì, nên phải đối chiếu lại ID - không thì một FishId rác trong data sẽ hiện ra icon của
    /// loài đầu tiên y như thật.
    /// </summary>
    private static FishModel FindModel(int fishId)
    {
        FishModelGlobalConfig config = FishModelGlobalConfig.Instance;
        if (config == null || config.Count == 0) return null;

        FishModelConfig entry = config.GetFishModelConfigById(fishId);
        if (entry == null || entry.ID != fishId) return null;
        return entry.FishModel;
    }

    private Texture LoadAssetPreview(int fishId, out bool pending)
    {
        pending = false;
        FishModel model = FindModel(fishId);
        if (model == null) return null;

        Texture preview = AssetPreview.GetAssetPreview(model.gameObject);
        if (preview != null) return preview;

        // Chưa nướng xong: báo cho chỗ gọi đừng kết luận là thiếu ảnh, và hẹn vẽ lại để lần sau tra tiếp.
        if (!AssetPreview.IsLoadingAssetPreviews()) return null;
        pending = true;
        EditorApplication.delayCall += () => onReady?.Invoke();
        return null;
    }
}
