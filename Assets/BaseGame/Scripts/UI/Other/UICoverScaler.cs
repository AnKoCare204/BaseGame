using System;
using UnityEngine;

public class UICoverScaler : MonoBehaviour
{
    [field: SerializeField] private Vector2 ReferenceSize { get; set; } = new(1080, 1920);
    [field: SerializeField] private RectTransform RectTransform { get; set; }
    [field: SerializeField] GameObject Rect1 { get; set; }
    [field: SerializeField] GameObject Rect2 { get; set; }
    private void Awake()
    {
        RectTransform = GetComponent<RectTransform>();
        Apply();
    }

    private void Reset()
    {
        RectTransform = GetComponent<RectTransform>();
    }
#if UNITY_EDITOR
    private void Update()
    {
        Apply();
    }
#endif

    private void Apply()
    {
        float screenRatio = (float)Screen.width / Screen.height;
        float referenceRatio = ReferenceSize.x / ReferenceSize.y;

        Vector2 size = Vector2.zero;
        if (screenRatio > referenceRatio)
        {
            Rect1.SetActive(true);
            Rect2.SetActive(false);
        }
        else
        {
            Rect1.SetActive(false);
            Rect2.SetActive(true);
        }

        RectTransform.sizeDelta = size;
    }
}