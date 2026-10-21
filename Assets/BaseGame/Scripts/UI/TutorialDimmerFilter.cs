using UnityEngine;
using UnityEngine.UI;

namespace Core.Manager
{
    [RequireComponent(typeof(Image))]
    public class TutorialDimmerFilter : MonoBehaviour, ICanvasRaycastFilter
    {
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            return TutorialManager.Instance == null || !TutorialManager.Instance.IsAllowedFishHole(screenPoint);
        }
    }
}
