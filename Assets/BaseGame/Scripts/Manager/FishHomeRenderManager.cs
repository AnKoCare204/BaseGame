using TW.Utility.DesignPattern;
using UnityEngine;

/// Quản lý cá trang trí màn Home: render cá vào RenderTexture qua camera ortho riêng, đặt vùng bơi và làn sâu cho từng cá,
/// làm trọng tài (IFishHomeArbiter) cho việc rời khung / tò mò, và nhận điểm chạm từ UI (HomeRenderTexture) để cá giật mình.
public class FishHomeRenderManager : Singleton<FishHomeRenderManager>, IFishHomeArbiter
{
    [field: SerializeField] public RenderTexture RenderTexture {get; set;}
    [field: SerializeField] private GameObject RendererGroup {get; set;}
    [field: SerializeField] private Camera RenderCamera {get; set;}
    [field: SerializeField] private FishHomeSwimmer[] Swimmers {get; set;}
    [field: SerializeField, Tooltip("Thu hẹp vùng bơi so với khung camera (đơn vị world). Âm = cho cá bơi lấn ra ngoài khung")]
    private Vector2 AreaPadding {get; set;}
    [field: SerializeField, Tooltip("Khoảng cách làn sâu (Z) giữa các cá để chúng không xuyên qua nhau")]
    private float LaneSpacing {get; set;} = 2f;
    [field: SerializeField, Tooltip("Số làn dư thêm vào dải làn (ngoài số cá) để cá vào lại có làn mới mà không trùng cá khác")]
    private int ExtraLanes {get; set;} = 2;
    [field: SerializeField, Tooltip("Tối đa bấy nhiêu cá ở ngoài khung cùng lúc (màn hình không bao giờ trống)")]
    private int MaxOutside {get; set;} = 1;
    [field: SerializeField, Tooltip("Tối đa bấy nhiêu cá tò mò (kể cả đang chờ tới lượt) cùng lúc")]
    private int MaxCurious {get; set;} = 1;
    [field: SerializeField, Tooltip("Giật mình khi nhấn: x = bán kính (đơn vị world quanh điểm chạm), y = độ mạnh 0..1")]
    private Vector2 TapStartle {get; set;} = new Vector2(3.5f, 1f);
    [field: SerializeField, Tooltip("Giật mình khi rê tay (mỗi gợn nhỏ): x = bán kính, y = độ mạnh 0..1; yếu hơn nhấn và không gây tò mò")]
    private Vector2 TrailStartle {get; set;} = new Vector2(2f, 0.5f);

    private int[] laneSlots; // slot làn của từng cá theo chỉ số trong Swimmers

    private int SlotCount => (Swimmers?.Length ?? 0) + Mathf.Max(0, ExtraLanes);

    protected override void Awake()
    {
        base.Awake();
        if (RendererGroup != null) RendererGroup.SetActive(false); // chỉ render khi ModalHome bật HomeRenderTexture (người mới vào thẳng level 1 không tốn 1 camera)
        ApplySwimArea();
    }

    public void StartRender()
    {
        if (RendererGroup == null) return;
        if (RenderTexture != null && !RenderTexture.IsCreated()) RenderTexture.Create();
        ApplySwimArea();
        RendererGroup.gameObject.SetActive(true);
    }

    public void StopRender()
    {
        if (RendererGroup == null) return; // HomeRenderTexture.OnDisable lúc thoát game: Instance có thể là bản Singleton tự sinh, chưa gán gì
        RendererGroup.gameObject.SetActive(false);
        if (RenderTexture != null && RenderTexture.IsCreated()) RenderTexture.Release(); // thả VRAM 1080x1920 khi không ở Home
    }

    /// Vùng bơi = khung nhìn của camera ortho, đổi sang local space của RendererGroup (cha của cá); mỗi cá một slot trong dải làn Z.
    /// Coi như camera nhìn thẳng theo +Z của RendererGroup và RendererGroup không scale.
    private void ApplySwimArea()
    {
        if (RenderCamera == null || RendererGroup == null || Swimmers == null) return;

        float aspect = RenderTexture != null ? (float)RenderTexture.width / RenderTexture.height : RenderCamera.aspect;
        float halfHeight = RenderCamera.orthographicSize;
        Vector2 halfSize = new Vector2(halfHeight * aspect, halfHeight) - AreaPadding;
        Vector2 center = RendererGroup.transform.InverseTransformPoint(RenderCamera.transform.position);

        if (laneSlots == null || laneSlots.Length != Swimmers.Length)
        {
            laneSlots = new int[Swimmers.Length];
            for (int i = 0; i < laneSlots.Length; i++) laneSlots[i] = FishHomeSwimMath.InitialSlot(i, Swimmers.Length, SlotCount);
        }

        for (int i = 0; i < Swimmers.Length; i++)
        {
            if (Swimmers[i] == null) continue;
            Swimmers[i].SetArea(center, halfSize, FishHomeSwimMath.LaneZ(laneSlots[i], SlotCount, LaneSpacing), this);
        }
    }

    /// Điểm chạm theo viewport 0..1 của camera render (HomeRenderTexture đổi từ pixel màn hình). isTrail = gợn của vệt rê tay:
    /// đẩy yếu hơn và không ghi điểm tò mò.
    public void Startle(Vector2 viewport, bool isTrail)
    {
        if (RenderCamera == null || RendererGroup == null || Swimmers == null) return;
        Vector3 world = RenderCamera.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, RenderCamera.nearClipPlane));
        Vector3 local = RendererGroup.transform.InverseTransformPoint(world);
        Vector2 point = new Vector2(local.x, local.y);
        Vector2 startle = isTrail ? TrailStartle : TapStartle;

        foreach (FishHomeSwimmer swimmer in Swimmers)
        {
            if (swimmer != null) swimmer.Startle(point, startle.x, startle.y);
        }
        if (isTrail) return;
        foreach (FishHomeSwimmer swimmer in Swimmers)
        {
            if (swimmer != null) swimmer.NoteTapPoint(point);
        }
    }

    /// Đếm cả cá đang bơi ra (chưa Outside), nếu không hai cá có thể cùng quyết định rời khung trong lúc chưa con nào ra hết.
    public bool CanLeave()
    {
        int outside = 0;
        foreach (FishHomeSwimmer swimmer in Swimmers)
        {
            if (swimmer != null && swimmer.IsOutsideOrLeaving) outside++;
        }
        return outside < MaxOutside;
    }

    public bool CanBeCurious()
    {
        int curious = 0;
        foreach (FishHomeSwimmer swimmer in Swimmers)
        {
            if (swimmer != null && swimmer.IsCuriousOrPending) curious++;
        }
        return curious < MaxCurious;
    }

    /// Làn mới cho cá vào lại: slot ngẫu nhiên không cá nào giữ, ưu tiên khác slot cũ; không còn slot trống thì giữ làn cũ.
    public float PickLane(FishHomeSwimmer who)
    {
        int index = System.Array.IndexOf(Swimmers, who);
        if (index < 0 || laneSlots == null) return who.LaneZ;

        bool[] occupied = new bool[SlotCount];
        for (int i = 0; i < laneSlots.Length; i++)
        {
            if (i != index) occupied[laneSlots[i]] = true;
        }
        laneSlots[index] = FishHomeSwimMath.PickFreeSlot(occupied, laneSlots[index], Random.value);
        return FishHomeSwimMath.LaneZ(laneSlots[index], SlotCount, LaneSpacing);
    }
}
