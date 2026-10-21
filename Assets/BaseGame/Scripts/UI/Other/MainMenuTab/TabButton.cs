using System;
using Core.UI;
using LitMotion;
using TW.ACacheEverything;
using UnityEngine;
using UnityEngine.UI;

public partial class TabButton : MonoBehaviour
{
    private Transform TransformCache { get; set; }
    public Transform Transform => TransformCache ??= transform;
    private static float DefaultTransitionTime { get; } = 0.3f;
    public static float SelectFlexibleWith { get; set; } = 1.5f;
    private static float DeselectFlexibleWith { get; } = 1;
    [field: SerializeField] public Button MainButton { get; private set; }
    [field: SerializeField] public LayoutElement LayoutElement { get; private set; }
    [field: SerializeField] public CanvasGroup SelectGroup { get; private set; }
    [field: SerializeField] public CanvasGroup DeselectGroup { get; private set; }
    //[field: SerializeField] public CanvasGroup MovingCavasGroup { get; private set; }
    [field: SerializeField] public Transform MovingGroup { get; private set; }
    [field: SerializeField] public float FlexibleWith { get; private set; }
    [field: SerializeField] private Transform DeselectMovePosition { get; set; }
    [field: SerializeField] private Transform SelectMovePosition { get; set; }
    private MotionHandle TransitionHandle { get; set; }
    private Action<TabButton> btnAction;

    public void AddListener(Action<TabButton> action)
    {
        MainButton.SetOnClickDestination(() => action(this));
        // MainButton.onClick.AddListener(() => action(this));
        btnAction = action;
    }

    public void ForceClick()
    {
        //MainButton.onClick.Invoke();
        btnAction?.Invoke(this);
    }

    private TabButton OnSelect()
    {
        TransitionHandle.TryCancel();
        TransitionHandle = LMotion.Create(FlexibleWith, SelectFlexibleWith, DefaultTransitionTime)
            .WithEase(Ease.InOutSine)
            .Bind(OnFlexibleWithChangedCache)
            .AddTo(this);
        SelectGroup.alpha = 1;
        DeselectGroup.alpha = 0;
        return this;
    }

    private TabButton OnDeselect()
    {
        TransitionHandle.TryCancel();
        TransitionHandle = LMotion.Create(FlexibleWith, DeselectFlexibleWith, DefaultTransitionTime)
            .WithEase(Ease.InOutSine)
            .Bind(OnFlexibleWithChangedCache)
            .AddTo(this);
        SelectGroup.alpha = 0;
        DeselectGroup.alpha = 1;
        return this;
    }
    [ACacheMethod]
    private void OnFlexibleWithChanged(float value)
    {
        FlexibleWith = value;
        LayoutElement.flexibleWidth = value;
        float alpha = Mathf.InverseLerp(DeselectFlexibleWith, SelectFlexibleWith, value);
        float rounded = alpha;
        SelectGroup.alpha = rounded;
        DeselectGroup.alpha = 1 - rounded;
        MovingGroup.position = Vector3.Lerp(DeselectMovePosition.position, SelectMovePosition.position, alpha);

        //float movingCanvasAlpha = Mathf.Clamp(alpha, 0.7f, 1f);
        //MovingCavasGroup.alpha = movingCanvasAlpha;
    }

    public void SetFlexible(float value)
    {
        OnFlexibleWithChanged(Mathf.Lerp(SelectFlexibleWith,DeselectFlexibleWith, value));
    }
}