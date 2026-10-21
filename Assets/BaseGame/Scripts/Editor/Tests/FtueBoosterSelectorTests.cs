using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// Add Slot mở giữa L3: lúc vào level nút đang khóa (SetState tắt icon). Luồng nhận (GetBoosterOnTutorial) phải bật lại icon, nếu không
/// nút đã mở hiện ô trống chỉ có số lượng. Luồng nhận đầu level không lộ lỗi vì nút đã mở sẵn từ SetState.
public class FtueBoosterSelectorTests
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

    [Test]
    public void ShowUnlockedArt_FromLockedState_ShowsIcon()
    {
        root = EditorUtility.CreateGameObjectWithHideFlags("BoosterSelector_AddSlot", HideFlags.HideAndDontSave, typeof(BoosterSelector));
        GameObject icon = Child("BoosterIcon", root.transform, typeof(Image));
        GameObject locked = Child("Lock", root.transform);
        GameObject unlocked = Child("Unlock", root.transform);
        icon.SetActive(false);   // trạng thái khóa của SetState
        unlocked.SetActive(false);

        var selector = root.GetComponent<BoosterSelector>();
        var so = new SerializedObject(selector);
        so.FindProperty("<BoosterIcon>k__BackingField").objectReferenceValue = icon.GetComponent<Image>();
        so.FindProperty("<LockedObj>k__BackingField").objectReferenceValue = locked;
        so.FindProperty("<UnlockedObj>k__BackingField").objectReferenceValue = unlocked;
        so.ApplyModifiedPropertiesWithoutUndo();

        selector.ShowUnlockedArt();

        Assert.IsTrue(icon.activeSelf);
        Assert.IsFalse(locked.activeSelf);
        Assert.IsTrue(unlocked.activeSelf);
    }
}
