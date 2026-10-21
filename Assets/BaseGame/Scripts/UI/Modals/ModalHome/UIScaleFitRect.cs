using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

public class UIScaleFitRect : MonoBehaviour
{
    public static float GlobalScale {get; private set;}
    public static List<RectTransform> GlobalMainRect {get; private set;} = new List<RectTransform>();
    public static List<RectTransform> GlobalScaleRect {get; private set;} = new List<RectTransform>();
    
    [field: SerializeField] private RectTransform MainRect {get; set;}
    [field: SerializeField] private RectTransform ScaleRect {get; set;}

    private void Start()
    {
        GlobalMainRect.Add(MainRect);
        GlobalScaleRect.Add(ScaleRect);
    }
    private void OnDestroy()
    {
        GlobalMainRect.Remove(MainRect);
        GlobalScaleRect.Remove(ScaleRect);
    }
    public static void RefreshScale(Unit _)
    {
        UpdateRect();
    }

    public static void UpdateRect()
    {
        try
        {
            GlobalScale = 100f;
            for (int i = 0; i < GlobalMainRect.Count; i++)
            {
                if (GlobalScaleRect[i].rect.width <= 0.1f || GlobalScaleRect[i].rect.height <= 0.1f) continue;
                float scale1 = GlobalMainRect[i].rect.width / GlobalScaleRect[i].rect.width;
                float scale2 = GlobalMainRect[i].rect.height / GlobalScaleRect[i].rect.height;
                float scale = Mathf.Min(scale1, scale2);
                scale = Mathf.Min(scale, 1f);
                GlobalScale = Mathf.Min(GlobalScale, scale);
            }
            for (int i = 0; i < GlobalMainRect.Count; i++)
            {
                GlobalScaleRect[i].localScale = new Vector3(GlobalScale, GlobalScale, GlobalScale);
            }
        }
        catch (Exception)
        {
            // ignored
        }
    }
}