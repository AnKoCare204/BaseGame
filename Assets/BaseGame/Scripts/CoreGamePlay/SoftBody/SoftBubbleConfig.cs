using UnityEngine;

/// <summary>Tham số vật lý dùng chung cho nhiều SoftBubble. Chỉnh trong Inspector, có tác dụng ngay khi Play.</summary>
[CreateAssetMenu(menuName = "FishSort/Soft Bubble Config", fileName = "SoftBubbleConfig")]
public class SoftBubbleConfig : ScriptableObject
{
    [Header("Mesh")]
    [Tooltip("Lưới bong bóng lưu sẵn (lưới mô phỏng và lưới hiển thị, bán kính 1). Đổi độ phân giải bằng nút 'Dựng lại mesh' trên asset đó, " +
             "bóng đang chạy cần spawn lại. Thiếu thì SoftBubble sinh tạm icosphere lúc runtime và báo warning")]
    public SoftBubbleMesh MeshAsset;

    [Header("Đàn hồi (substep 360 Hz, xem SoftBubbleWorld.SubstepHz)")]
    [Tooltip("Độ mềm của cạnh bề mặt. 0 = cứng tuyệt đối. 0.00001 mềm vừa, 0.0001 gần như không giữ hình. Tăng để bề mặt nhũn hơn")]
    [Range(0f, 0.0001f)] public float EdgeCompliance = 0.00002f;

    [Tooltip("Giảm chấn cho cạnh (0..0.95): phần vận tốc co giãn của cạnh bị triệt mỗi substep. Chỉ có tác dụng khi EdgeCompliance > 0")]
    [Range(0f, 0.95f)] public float EdgeDamping = 0.3f;

    [Tooltip("Độ mềm của ràng buộc thể tích. 0 = giữ thể tích tuyệt đối (thạch, nước). Thang rất nhỏ: 0.00000002 đã nén được rõ, 0.0000001 gần như không giữ thể tích")]
    [Range(0f, 0.0000002f)] public float VolumeCompliance = 0.00000005f;

    [Tooltip("Giảm chấn cho thể tích (0..0.95): phần vận tốc nén/nở bị triệt mỗi substep. Chỉ có tác dụng khi VolumeCompliance > 0")]
    [Range(0f, 0.95f)] public float VolumeDamping = 0.6f;

    [Tooltip("Độ mềm của nan hoa nối tâm với từng đỉnh, tạo khối đặc kiểu thạch. 0 = cứng, bóng gần như không bẹt. " +
             "0.00001 cứng như cao su, 0.0001 mềm như thạch, 0.001 gần như không có nan hoa")]
    [Range(0f, 0.001f)] public float RadialCompliance = 0.0003f;

    [Tooltip("Giảm chấn cho nan hoa (0..0.95): phần vận tốc nén/giãn nan hoa bị triệt mỗi substep. Cao thì hồi hình êm, thấp thì rung như thạch vài nhịp")]
    [Range(0f, 0.95f)] public float RadialDamping = 0.6f;

    [Tooltip("Thể tích mục tiêu so với lúc nghỉ. 1 = bằng, lớn hơn 1 = căng phồng")]
    [Range(0.5f, 1.5f)] public float Pressure = 1f;

    [Tooltip("Lực kéo về hình cầu mỗi substep (0..1). Càng cao càng cứng, càng ít lún và hồi hình càng nhanh. Khoảng hợp lý 0.02 đến 0.3")]
    [Range(0f, 1f)] public float ShapeStiffness = 0.015f;

    [Tooltip("Cho phép bóng xoay (lăn). Tắt thì nội dung bên trong luôn giữ hướng")]
    public bool AllowRotation = false;

    [Header("Môi trường nước")]
    public float GravityScale = 1f;

    [Tooltip("Gia tốc nổi hướng lên (m/s2)")]
    public float Buoyancy = 0f;

    [Tooltip("Cản chuyển động tổng thể (1/s). Khoảng hợp lý 0 đến 5")]
    [Min(0f)] public float LinearDrag = 1.5f;

    [Tooltip("Dập tắt rung nội bộ (1/s). Cao = hết rung nhanh. Khoảng hợp lý 2 đến 20")]
    [Min(0f)] public float InternalDamping = 10f;

    [Tooltip("Tốc độ particle tối đa (m/s). Để thấp sẽ làm bóng chui khỏi sàn rất chậm. Nên từ 10 trở lên")]
    [Min(1f)] public float MaxSpeed = 20f;

    [Header("Chạm bề mặt (SoftBodyShape)")]
    [Tooltip("Bán kính mỗi particle: độ dày khi chạm shape, bán kính hạt khi chạm bóng khác. 0 = tự động bằng 30% cạnh lưới. " +
             "Particle mô phỏng nằm trên mặt cầu Radius trừ giá trị này, mesh hiển thị được đẩy ra đúng bằng giá trị này nên bề mặt nhìn thấy chạm sát")]
    [Range(0f, 0.2f)] public float ParticleRadius = 0f;

    /// <summary>Hệ số bán kính particle tự động so với cạnh lưới. 0.3 đủ kín để particle bóng khác không lọt qua tam giác.</summary>
    public const float AutoParticleRadiusFactor = 0.3f;

    /// <summary>Bán kính particle thực dùng cho một bóng có bán kính hiển thị visualRadius và lưới data.</summary>
    public float ResolveParticleRadius(float visualRadius, Icosphere.Data data)
    {
        if (ParticleRadius > 0f) return Mathf.Min(ParticleRadius, visualRadius * 0.5f);
        float k = AutoParticleRadiusFactor * data.MeanEdgeLength;
        float simRadius = visualRadius / (1f + k);
        return k * simRadius;
    }

    [Tooltip("Độ mềm khi chạm bề mặt. 0 = không lún vào sàn. 0.00001 lún nhẹ, 0.0001 sàn rất mềm")]
    [Range(0f, 0.0001f)] public float SurfaceCompliance = 0f;

    [Range(0f, 1f)] public float SurfaceFriction = 0.3f;

    [Tooltip("Phần vận tốc tiến vào vật đang chạm của cả bóng bị triệt mỗi substep (0..1). 1 = dừng ngay khi chạm, cứng như đá. " +
             "Thấp (0.05 đến 0.2) thì động lượng còn ép bóng bẹt lại kiểu thạch, phần nảy do VolumeDamping và InternalDamping lo")]
    [Range(0f, 1f)] public float ContactDamping = 0.02f;

    [Header("Chạm bóng khác")]
    public ContactSettings Contact = new();

    [Header("Ngủ (sleep)")]
    [Tooltip("Bóng đứng yên đủ lâu và có vật đỡ ngược lực nổi (bóng khác, shape, cạnh cứng) sẽ ngủ: bỏ qua mô phỏng, hết rung hoàn toàn, " +
             "tự thức khi bị chạm, bị tác động hay mất vật đỡ")]
    public bool EnableSleep = true;

    [Tooltip("Tốc độ particle trung bình (m/s) dưới ngưỡng này thì coi là đứng yên")]
    [Min(0.001f)] public float SleepThreshold = 0.05f;

    [Tooltip("Đứng yên liên tục bao lâu (giây) thì ngủ")]
    [Min(0f)] public float SleepDelay = 0.3f;

    public SoftBubbleParams ToParams()
    {
        float edgeD = Mathf.Clamp(EdgeDamping, 0f, 0.95f);
        float volD = Mathf.Clamp(VolumeDamping, 0f, 0.95f);
        float radD = Mathf.Clamp(RadialDamping, 0f, 0.95f);
        return new SoftBubbleParams
        {
            EdgeCompliance = Mathf.Max(0f, EdgeCompliance),
            VolumeCompliance = Mathf.Max(0f, VolumeCompliance),
            EdgeDampingGamma = edgeD / (1f - edgeD),
            VolumeDampingGamma = volD / (1f - volD),
            RadialCompliance = Mathf.Max(0f, RadialCompliance),
            RadialDampingGamma = radD / (1f - radD),
            Pressure = Mathf.Max(0.01f, Pressure),
            ShapeStiffness = Mathf.Clamp01(ShapeStiffness),
            AllowRotation = AllowRotation ? 1 : 0,
            GravityScale = GravityScale,
            Buoyancy = Buoyancy,
            LinearDrag = Mathf.Max(0f, LinearDrag),
            InternalDamping = Mathf.Max(0f, InternalDamping),
            MaxSpeed = Mathf.Max(1f, MaxSpeed),
            SurfaceCompliance = Mathf.Max(0f, SurfaceCompliance),
            SurfaceFriction = Mathf.Clamp01(SurfaceFriction),
            ContactDamping = Mathf.Clamp01(ContactDamping),
            SleepEnabled = EnableSleep ? 1 : 0,
            SleepThreshold = Mathf.Max(0.001f, SleepThreshold),
            SleepDelay = Mathf.Max(0f, SleepDelay)
        };
    }
}
