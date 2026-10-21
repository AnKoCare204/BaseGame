using System;
using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
[ExecuteInEditMode]
#endif
public class ScaleImageFullScreen : MonoBehaviour
{
    [SerializeField] private RectTransform imgTransform;
    [SerializeField] private Vector2 sizeDefault = new(1080, 1920);
    [SerializeField] private RectTransform parentRect;

    private void Start()
    {
        ApplyScale();
    }
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (imgTransform == null)
            imgTransform = GetComponent<RectTransform>();
    }

    [SerializeField] private bool UpdateInEditor;
    private void Update()
    {
        if (UpdateInEditor)
            ApplyScale();
    }
#endif
    
    [Button("Apply Scale")]
    private void ApplyScale()
    {
        if (imgTransform == null || parentRect == null) return;
        Vector2 canvasSize = parentRect.rect.size;
        if (canvasSize.x <= 0 || canvasSize.y <= 0) return;
        float canvasRatio = canvasSize.x / canvasSize.y;
        float imageRatio = sizeDefault.x / sizeDefault.y;
        Vector2 newSize = canvasRatio > imageRatio
            ? new Vector2(canvasSize.x, canvasSize.x / imageRatio)
            : new Vector2(canvasSize.y * imageRatio, canvasSize.y);
        imgTransform.sizeDelta = newSize;
    }
}
