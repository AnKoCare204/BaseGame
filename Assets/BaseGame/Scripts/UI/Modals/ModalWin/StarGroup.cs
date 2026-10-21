using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using LitMotion;
using UnityEngine;

public class StarGroup : MonoBehaviour
{
    public float delay;
    public CanvasGroup starCG;
    public Transform starTrs;
    public Transform starStart;
    public Transform starEnd;

    private void Awake()
    {
        starCG.alpha = 0f;
        starTrs.position = starStart.position;
        starTrs.eulerAngles = starStart.eulerAngles;
    }

    public async UniTask RunAnim()
    {
        starCG.alpha = 0f;
        starTrs.position = starStart.position;
        starTrs.eulerAngles = starStart.eulerAngles;
        await UniTask.Delay((int)(delay * 1000), cancellationToken: this.GetCancellationTokenOnDestroy());
        LMotion.Create(0f, 1f, 0.35f).Bind(x => starCG.alpha = x).AddTo(starTrs);
        LMotion.Create(starStart.position, starEnd.position, 0.35f).WithEase(Ease.OutBack).Bind(x => starTrs.position = x).AddTo(starTrs);
        LMotion.Create(starStart.eulerAngles, starEnd.eulerAngles, 0.35f).WithEase(Ease.OutBack).Bind(x => starTrs.eulerAngles = x).AddTo(starTrs);
    }
}
