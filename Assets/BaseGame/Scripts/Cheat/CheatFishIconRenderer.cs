using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Chụp icon FishModel ngay lúc chạy cho panel cheat (CheatFishPicker): dựng prefab ở chỗ khuất trên layer riêng, một camera
/// orthographic riêng chụp vào RenderTexture. Không có file ảnh nào nên bản Release không nặng thêm byte nào, và ảnh luôn khớp
/// prefab mới nhất. Góc xoay = FishModel.IconRotation, cùng góc icon cá trên bể (Fish.SetAsIconPreview); tâm hộp bao đặt trùng
/// pivot như Fish.InitializeGraphic. Mỗi model khung vừa ảnh theo chiều chật hơn.
///
/// Model chỉ bật trong lúc camera.Render() rồi tắt và huỷ ngay, camera game không bao giờ thấy. Animator tắt TRƯỚC khi bật model
/// (OnEnable của FishProceduralAnimator đăng ký vào FishAnimationSystem), collider tắt để không dính raycast chạm cá.
/// RenderTexture giữ trong cache cả phiên; mất nội dung (IsCreated false, ví dụ app vào nền trên Android) thì chụp lại.
/// </summary>
public static class CheatFishIconRenderer
{
    /// <summary>Layer trống trong TagManager: chỉ camera chụp nhìn thấy.</summary>
    public const int IconLayer = 31;

    private const int IconSize = 256;
    private const float Padding = 1.2f;   // mesh collider là bản rút gọn, vây / đuôi mảnh có thể lòi ra ngoài hộp bao
    private static readonly Vector3 RigPosition = new Vector3(5000f, 5000f, 0f);

    private static readonly Dictionary<FishModel, RenderTexture> cache = new Dictionary<FishModel, RenderTexture>();
    private static Camera captureCamera;

    /// <summary>Ảnh của prefab, nền màu background. Chụp ở lần gọi đầu (hoặc khi ảnh cũ mất), sau đó lấy từ cache.</summary>
    public static RenderTexture GetIcon(FishModel prefab, Color background)
    {
        if (prefab == null) return null;
        if (cache.TryGetValue(prefab, out RenderTexture icon) && icon != null && icon.IsCreated()) return icon;

        if (icon == null)
        {
            // Ảnh giữ trong cache không MSAA, không depth (~256 KB / con); MSAA + depth chỉ nằm ở RT tạm lúc chụp.
            icon = new RenderTexture(Descriptor(1, 0)) { name = $"CheatFishIcon_{prefab.name}", hideFlags = HideFlags.HideAndDontSave };
            cache[prefab] = icon;
        }

        if (!icon.IsCreated()) icon.Create();
        RenderTexture capture = RenderTexture.GetTemporary(Descriptor(4, 24));
        try
        {
            Render(prefab, background, capture);
            Graphics.Blit(capture, icon);
        }
        finally
        {
            RenderTexture.ReleaseTemporary(capture);
        }

        return icon;
    }

    private static RenderTextureDescriptor Descriptor(int msaaSamples, int depthBits)
        => new RenderTextureDescriptor(IconSize, IconSize, RenderTextureFormat.ARGB32, depthBits)
        {
            sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear,
            msaaSamples = msaaSamples,
        };

    /// <summary>Orthographic size để hộp bao cỡ size vừa khung tỉ lệ aspect (rộng / cao), nới thêm padding lần.</summary>
    public static float FitOrthographicSize(Vector3 size, float aspect, float padding)
        => Mathf.Max(size.y * 0.5f, size.x * 0.5f / Mathf.Max(aspect, 0.01f)) * padding;

    private static void Render(FishModel prefab, Color background, RenderTexture target)
    {
        Camera camera = GetCamera();
        var holder = new GameObject("CheatFishIconHolder") { hideFlags = HideFlags.HideAndDontSave, layer = IconLayer };
        holder.SetActive(false);
        holder.transform.SetPositionAndRotation(RigPosition, Quaternion.Euler(prefab.IconRotation));
        try
        {
            FishModel model = Object.Instantiate(prefab, holder.transform);
            foreach (FishProceduralAnimator animator in model.GetComponentsInChildren<FishProceduralAnimator>(true)) animator.enabled = false;
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;

            model.transform.localPosition = -model.LocalBounds.center;
            model.transform.localRotation = Quaternion.identity;
            SetLayer(model.transform, IconLayer);
            holder.SetActive(true);

            Bounds bounds = GetWorldBounds(model);
            camera.transform.SetPositionAndRotation(bounds.center - Vector3.forward * (bounds.extents.z + 10f), Quaternion.identity);
            camera.farClipPlane = bounds.size.z + 20f;
            camera.orthographicSize = FitOrthographicSize(bounds.size, 1f, Padding);
            camera.backgroundColor = background;
            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = null;
        }
        finally
        {
            holder.SetActive(false);
            if (Application.isPlaying) Object.Destroy(holder);
            else Object.DestroyImmediate(holder);
        }
    }

    private static Camera GetCamera()
    {
        if (captureCamera != null) return captureCamera;

        var go = new GameObject("CheatFishIconCamera") { hideFlags = HideFlags.HideAndDontSave };
        captureCamera = go.AddComponent<Camera>();
        captureCamera.enabled = false;
        captureCamera.orthographic = true;
        captureCamera.cullingMask = 1 << IconLayer;
        captureCamera.clearFlags = CameraClearFlags.SolidColor;
        captureCamera.nearClipPlane = 0.1f;
        captureCamera.allowHDR = false;

        UniversalAdditionalCameraData data = go.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = false;
        data.renderShadows = false;
        data.antialiasing = AntialiasingMode.None;
        return captureCamera;
    }

    /// <summary>
    /// Hộp bao world của thân model đã xoay: đỉnh của mesh collider (khít dáng; bounds của SkinnedMeshRenderer phình to hơn thân
    /// nhiều nên con nhỏ xíu trong ảnh). Mesh collider không có hoặc không đọc được thì lấy 8 góc của FishModel.LocalBounds.
    /// </summary>
    private static Bounds GetWorldBounds(FishModel model)
    {
        MeshCollider meshCollider = model.MeshCollider;
        if (meshCollider != null && meshCollider.sharedMesh != null && meshCollider.sharedMesh.isReadable)
        {
            Matrix4x4 toWorld = meshCollider.transform.localToWorldMatrix;
            Vector3[] vertices = meshCollider.sharedMesh.vertices;
            if (vertices.Length > 0)
            {
                var fromMesh = new Bounds(toWorld.MultiplyPoint3x4(vertices[0]), Vector3.zero);
                foreach (Vector3 vertex in vertices) fromMesh.Encapsulate(toWorld.MultiplyPoint3x4(vertex));
                return fromMesh;
            }
        }

        Bounds local = model.LocalBounds;
        Matrix4x4 modelToWorld = model.transform.localToWorldMatrix;
        var box = new Bounds(modelToWorld.MultiplyPoint3x4(local.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = local.center + Vector3.Scale(local.extents,
                new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
            box.Encapsulate(modelToWorld.MultiplyPoint3x4(corner));
        }

        return box;
    }

    private static void SetLayer(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root) SetLayer(child, layer);
    }
}
