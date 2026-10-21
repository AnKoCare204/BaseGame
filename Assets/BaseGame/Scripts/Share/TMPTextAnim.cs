using System;
using System.Text;
using LitMotion;
using Sirenix.OdinInspector;
using TMPro;
using TW.ACacheEverything;
using UnityEngine;

public partial class TMPTextAnim : MonoBehaviour
{
    [field: SerializeField] private TextMeshProUGUI MainText {get; set;}
    [field: SerializeField] public AnimationCurve ValueCurve {get; set;}
    [field: SerializeField] public float Duration {get; set;}
    [field: SerializeField] public float UnitDuration {get; set;}
    private string CurrentText { get; set; }
    private StringBuilder StringBuilderCache { get; set; } = new();
    private MotionHandle MotionHandle { get; set; }
    private float DelayPerUnit { get; set; }

    private void Reset()
    {
        MainText = GetComponent<TextMeshProUGUI>();
    }

    [Button]
    public void PlayAnimation(string text)
    {
        CurrentText = text;
        int length = text.Length;
        DelayPerUnit = (Duration - UnitDuration) / length;
        MotionHandle.TryCancel();
        MotionHandle = LMotion.Create(0f, Duration, Duration)
            .WithOnComplete(OnAnimCompleteCache)
            .Bind(OnAnimUpdateCache).AddTo(this);
    }
   
    [Button]
    public void PlayAnimation()
    {
        CurrentText = MainText.text;
        int length = MainText.text.Length;
        DelayPerUnit = (Duration - UnitDuration) / length;
        MotionHandle.TryCancel();
        MotionHandle = LMotion.Create(0f, Duration, Duration)
            .WithOnComplete(OnAnimCompleteCache)
            .Bind(OnAnimUpdateCache).AddTo(this);
    }
    [ACacheMethod]
    private void OnAnimUpdate(float progress)
    {
        for (int i = 0; i < CurrentText.Length; i++)
        {
            float unitProgress = Mathf.Clamp01((progress - DelayPerUnit * i)/UnitDuration);
            float scaleValue = ValueCurve.Evaluate(unitProgress) * 100;
            char c = CurrentText[i];
            StringBuilderCache.Append($"<size={scaleValue}%>{c}</size>");
        }
        MainText.SetText(StringBuilderCache.ToString());
        StringBuilderCache.Clear();
    }
    [ACacheMethod]
    private void OnAnimComplete()
    {
        MainText.SetText(CurrentText);
    }
}