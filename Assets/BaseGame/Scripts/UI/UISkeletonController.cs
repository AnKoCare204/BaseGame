using System;
using Spine.Unity;
using UnityEngine;

public class UISkeletonController : MonoBehaviour
{
   public UISkeletonData[] skeletonData;
}

[Serializable]
public class UISkeletonData
{
    public string[] animationName;
    public SkeletonGraphic skeletonGraphic;
}
