using UnityEngine;

/// <summary>
/// Kéo cụm booster về vị trí "có banner" hay "không banner". ScreenInGame.LoadLevel gọi Apply một lần lúc vào level,
/// cùng quyết định với việc hiện banner (BannerRemoteConfigData.CanShow). Trước đó, và cả level tutorial đầu
/// (không chạy LoadLevel nên không có banner), cụm booster nằm ở vị trí không banner.
/// </summary>
public class NoAdsSwitchPosition : MonoBehaviour
{
    [field: SerializeField] private Transform Root {get; set;}
    [field: SerializeField] private Transform BannerActivePosition {get; set;}
    [field: SerializeField] private Transform BannerInactivePosition {get; set;}

    private bool isApplied;

    private void Start()
    {
        // LoadLevel có thể chạy trước Start (object bật muộn): không đè quyết định của level.
        if (!isApplied) Apply(false);
    }

    public void Apply(bool hasBanner)
    {
        isApplied = true;
        if (Root == null || BannerActivePosition == null || BannerInactivePosition == null) return;
        Root.position = hasBanner ? BannerActivePosition.position : BannerInactivePosition.position;
    }
}
