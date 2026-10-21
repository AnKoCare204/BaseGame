using Core.Tutorial;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// HUD tối khi tutorial làm tối (spec bản sửa mục 2.1): hạ alpha nhóm gốc; phần đang sáng có CanvasGroup riêng bỏ qua nhóm cha.
public class FtueHudDimTests
{
    private GameObject root;

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
    }

    private (CanvasGroup main, GameObject part) Make()
    {
        root = EditorUtility.CreateGameObjectWithHideFlags("hud", HideFlags.HideAndDontSave, typeof(RectTransform), typeof(CanvasGroup));
        GameObject part = EditorUtility.CreateGameObjectWithHideFlags("progress", HideFlags.HideAndDontSave, typeof(RectTransform));
        part.transform.SetParent(root.transform, false);
        return (root.GetComponent<CanvasGroup>(), part);
    }

    [Test]
    public void Dim_LowersAlpha_UndimRestoresOne()
    {
        (CanvasGroup main, _) = Make();
        HudDim.SetDim(main, true, 0.3f);
        Assert.AreEqual(0.3f, main.alpha, 1e-4f);
        HudDim.SetDim(main, false, 0.3f);
        Assert.AreEqual(1f, main.alpha, 1e-4f);
    }

    [Test]
    public void Dim_StopsHudTaps_UndimRestoresThem()
    {
        (CanvasGroup main, _) = Make();
        HudDim.SetDim(main, true, 0.3f);
        Assert.IsFalse(main.blocksRaycasts);
        HudDim.SetDim(main, false, 0.3f);
        Assert.IsTrue(main.blocksRaycasts);
    }

    [Test]
    public void Undim_KeepsTheWinLock()
    {
        (CanvasGroup main, _) = Make();
        main.interactable = false;   // ScreenInGame khoá HUD lúc PrepareWin
        HudDim.SetDim(main, true, 0.3f);
        HudDim.SetDim(main, false, 0.3f);
        Assert.IsFalse(main.interactable);
    }

    [Test]
    public void LitPart_IgnoresParentGroup_UntilUnlit()
    {
        (_, GameObject part) = Make();
        HudDim.SetLit(part, true);
        Assert.IsTrue(part.GetComponent<CanvasGroup>().ignoreParentGroups);
        HudDim.SetLit(part, false);
        Assert.IsFalse(part.GetComponent<CanvasGroup>().ignoreParentGroups);
    }

    [Test]
    public void Unlit_WithoutGroup_AddsNothing()
    {
        (_, GameObject part) = Make();
        HudDim.SetLit(part, false);
        Assert.IsNull(part.GetComponent<CanvasGroup>());
        HudDim.SetLit(null, true);   // không lỗi
    }
}
