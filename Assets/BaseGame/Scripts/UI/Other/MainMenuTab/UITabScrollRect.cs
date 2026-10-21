using UnityEngine;
using UnityEngine.UI;

public class UITabScrollRect : MonoBehaviour
{
    [field: SerializeField] private ScrollRect ScrollRect {get; set;}

    private void Reset()
    {
        ScrollRect = GetComponent<ScrollRect>();
    }

    private void Awake()
    {
        UITab.RegisterScrollRect(ScrollRect);
    }
    private void OnDestroy()
    {
        UITab.UnregisterScrollRect(ScrollRect);
    }
}