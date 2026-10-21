using Cysharp.Threading.Tasks;
using UnityEngine;
using ZBase.UnityScreenNavigator.Core;

public class Launcher : UnityScreenNavigatorLauncher
{
    // Placeholder image shown before UI containers exist. The single-scene boot
    // covers startup with LoadingManager's CanvasFirstLoading instead.
    [SerializeField] private GameObject loadngFake;

    protected override void Start()
    {
        base.Start();
        if (loadngFake != null)
        {
            loadngFake.SetActive(false);
        }
        UIManager.Instance.InitOverlayActivities().Forget();
    }
    
    private void Update()
    {
        UIScaleFitRect.UpdateRect();
    }
}
