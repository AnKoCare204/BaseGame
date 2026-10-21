using System;
using System.Collections.Generic;
using UnityEngine;

/// Chuỗi đốt (gốc -> chóp) của một chi: tay bạch tuộc / sao biển, cuống mắt cua...
[Serializable]
public class BoneChain
{
    public Transform[] Segments = Array.Empty<Transform>();
}

/// Chuỗi đốt có bên + trước / sau: chân cua, chân chèo rùa, râu / chân tôm.
[Serializable]
public class SidedChain
{
    [Range(-1, 1)] public int Side;   // -1 trái, +1 phải (theo tên artist; lúc Build lấy theo dấu x root space nếu có)
    public bool Front;                 // chân trước (F) hay sau (B)
    public int Pair;                   // số thứ tự cặp (tôm: Leg_<cặp>_...), 0 nếu không có
    public Transform[] Segments = Array.Empty<Transform>();
}

/// Tiện ích tìm bone theo tên + cây cho các animator loài không phải cá (bạch tuộc, sao biển, cua, rùa, tôm, đồ vật).
/// Mọi phép so tên không phân biệt hoa thường; bone *_end là chóp không có skin, không bao giờ được trả về làm đốt.
public static class CreatureRigUtil
{
    public static bool IsEnd(Transform t) => t.name.EndsWith("_end", StringComparison.OrdinalIgnoreCase);
    public static bool NameStarts(Transform t, string prefix) => t.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    public static bool NameEnds(Transform t, string suffix) => t.name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
    public static bool NameHas(Transform t, string part) => t.name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;

    /// -1 nếu tên kết thúc bằng _L, +1 nếu _R, 0 nếu không có bên.
    public static int SideFromName(Transform t) => NameEnds(t, "_L") ? -1 : NameEnds(t, "_R") ? 1 : 0;

    /// Bone nông nhất dưới root có tên bắt đầu bằng prefix (Spine_1 thay vì Spine_2).
    public static Transform FindShallowest(Transform root, string prefix)
    {
        Transform best = null;
        int bestDepth = int.MaxValue;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == root || !NameStarts(t, prefix) || IsEnd(t)) continue;
            int d = 0;
            for (Transform p = t.parent; p != null && p != root; p = p.parent) d++;
            if (d < bestDepth) { bestDepth = d; best = t; }
        }
        return best;
    }

    /// Con trực tiếp có tên bắt đầu bằng prefix (không tính *_end), theo thứ tự sibling.
    public static List<Transform> Children(Transform parent, string prefix)
    {
        var list = new List<Transform>();
        if (parent == null) return list;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform c = parent.GetChild(i);
            if (NameStarts(c, prefix) && !IsEnd(c)) list.Add(c);
        }
        return list;
    }

    public static Transform Child(Transform parent, string prefix)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform c = parent.GetChild(i);
            if (NameStarts(c, prefix) && !IsEnd(c)) return c;
        }
        return null;
    }

    /// Chuỗi đốt từ root: root rồi lần lượt con đầu tiên không phải *_end, tới khi hết con. Nhánh phụ bỏ qua; tên trùng không sao.
    public static Transform[] Chain(Transform root)
    {
        var list = new List<Transform>();
        for (Transform t = root; t != null; t = FirstSegmentChild(t)) list.Add(t);
        return list.ToArray();
    }

    private static Transform FirstSegmentChild(Transform t)
    {
        for (int i = 0; i < t.childCount; i++)
        {
            Transform c = t.GetChild(i);
            if (!IsEnd(c)) return c;
        }
        return null;
    }

    /// Chóp của đốt cuối: con *_end nếu có, không thì chính nó.
    public static Transform Tip(Transform last)
    {
        if (last == null) return null;
        for (int i = 0; i < last.childCount; i++)
            if (IsEnd(last.GetChild(i))) return last.GetChild(i);
        return last;
    }

    /// Đốt cuối khác null của chuỗi (mảng có thể có ô trống do sửa tay).
    public static Transform Last(Transform[] segments)
    {
        if (segments == null) return null;
        for (int i = segments.Length - 1; i >= 0; i--) if (segments[i] != null) return segments[i];
        return null;
    }

    public static Transform First(Transform[] segments)
    {
        if (segments == null) return null;
        foreach (Transform t in segments) if (t != null) return t;
        return null;
    }

    /// Vị trí root space (space = transform của animator).
    public static Vector3 Local(Transform space, Transform t) => space.InverseTransformPoint(t.position);

    /// Hướng đơn vị root space từ a tới b; fallback khi hai bone trùng chỗ.
    public static Vector3 Dir(Transform space, Transform a, Transform b, Vector3 fallback)
    {
        Vector3 d = Local(space, b) - Local(space, a);
        return d.sqrMagnitude > 1e-8f ? d.normalized : fallback;
    }

    /// Trục đơn vị vuông góc với dir và nằm ngang (⊥ up): xoay quanh trục này = vung chi lên xuống. fallback khi dir song song up.
    public static Vector3 SwingAxis(Vector3 dir, Vector3 fallback)
    {
        Vector3 a = Vector3.Cross(Vector3.up, dir);
        return a.sqrMagnitude > 1e-8f ? a.normalized : fallback;
    }

    /// Dấu bên theo x root space (+1 phải, -1 trái); |x| quá nhỏ thì lấy theo Side đặt sẵn, cuối cùng +1.
    public static float SideSign(Vector3 rootPos, int namedSide)
    {
        if (Mathf.Abs(rootPos.x) > 1e-4f) return rootPos.x >= 0f ? 1f : -1f;
        return namedSide != 0 ? Mathf.Sign(namedSide) : 1f;
    }
}
