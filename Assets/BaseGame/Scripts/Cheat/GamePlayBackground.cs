using System;
using UnityEngine;

public class GamePlayBackground : MonoBehaviour
{
    public static Action OnChangeBg { get; set; }
    [field: SerializeField] private GameObject NormalBg { get; set; }
    [field: SerializeField] private GameObject SpecialBg { get; set; }
    public int CurrentBgIndex { get; set; }

    private void Start()
    {
        CurrentBgIndex = 0;
        NormalBg.SetActive(true);
        SpecialBg.SetActive(false);
        OnChangeBg = OnChangeBgAction;
    }

    private void OnDestroy()
    {
        OnChangeBg = null;
    }

    public void OnChangeBgAction()
    {
        CurrentBgIndex = CurrentBgIndex == 0 ? 1 : 0;
        NormalBg.SetActive(CurrentBgIndex == 0);
        SpecialBg.SetActive(CurrentBgIndex == 1);
    }
}