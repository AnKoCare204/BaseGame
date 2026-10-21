using System;
using Core.GamePlay;
using UnityEngine;
using UnityEngine.UI;

public class RectForceRebuildCanvas : MonoBehaviour
{
    [field: SerializeField] private RectTransform RectTransform {get; set;}

    private void Reset()
    {
        RectTransform = GetComponent<RectTransform>();
    }

    private void Start()
    {
        GameGlobalEvent.ForceRebuildCanvas += ForceRebuild;
    }

    private void OnDestroy()
    {
        GameGlobalEvent.ForceRebuildCanvas -= ForceRebuild;
    }

    private void ForceRebuild()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(RectTransform);
    }
}