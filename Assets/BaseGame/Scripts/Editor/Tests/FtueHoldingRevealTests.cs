using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// L2 hiện hàng ô chờ có animation bung. Người chơi bấm cá X ngay lúc đó: cá giữ chỗ trong đàn của ô (FishSchool.ReserveArrival chia
/// cho lossyScale của Anchor). Anchor mà đang scale 0 thì bộ điểm phình khổng lồ, cá hạ ở z ~6600 sau camera và coi như biến mất.
/// Nên chỉ phần hình (Body / Warning) được bung, Anchor giữ đúng scale suốt animation.
public class FtueHoldingRevealTests
{
    private GameObject root;

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
    }

    private static GameObject Child(string name, Transform parent, params System.Type[] components)
    {
        GameObject go = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave, components);
        go.transform.SetParent(parent, false);
        return go;
    }

    /// Dựng như HoldingSlot.prefab: HoldingSlot / Graphic / {Anchor, Body, Warning}.
    private HoldingSlotView MakeSlot(out Transform body)
    {
        root = EditorUtility.CreateGameObjectWithHideFlags("HoldingSlot", HideFlags.HideAndDontSave, typeof(HoldingSlotView));
        Transform graphic = Child("Graphic", root.transform).transform;
        Transform anchor = Child("Anchor", graphic).transform;
        GameObject bodyGo = Child("Body", graphic, typeof(SpriteRenderer));
        bodyGo.transform.localScale = new Vector3(1.3f, 1.3f, 1f);
        GameObject warningGo = Child("Warning", graphic, typeof(SpriteRenderer));

        var slot = root.GetComponent<HoldingSlotView>();
        var so = new SerializedObject(slot);
        so.FindProperty("<Anchor>k__BackingField").objectReferenceValue = anchor;
        so.FindProperty("<Body>k__BackingField").objectReferenceValue = bodyGo.GetComponent<SpriteRenderer>();
        so.FindProperty("<Warning>k__BackingField").objectReferenceValue = warningGo.GetComponent<SpriteRenderer>();
        so.ApplyModifiedPropertiesWithoutUndo();
        body = bodyGo.transform;
        return slot;
    }

    [Test]
    public void PlayReveal_KeepsFishAnchorAtFullScale()
    {
        HoldingSlotView slot = MakeSlot(out Transform body);
        slot.PlayReveal(0f);
        Assert.AreEqual(1f, slot.AnchorTransform.lossyScale.x, 1e-4f);
        Assert.AreEqual(Vector3.zero, body.localScale);   // phần hình bắt đầu bung từ 0
    }
}
