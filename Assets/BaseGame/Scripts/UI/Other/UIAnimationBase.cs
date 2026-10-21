using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using Lofelt.NiceVibrations;
using Manager;
using UnityEngine;
using UnityEngine.Events;

public static class UIAnimationBase
{
    private static readonly Vector3 VectorRotate = new Vector3(0, 0, 3);

    public static async UniTask ButtonBasic(Transform trs, UnityAction actionCallBack = null)
    {
        // VibrationManager.Instance.CallHaptic(HapticPatterns.PresetType.LightImpact);
        if (trs != null)
        {
            CancellationToken cancellationToken = trs.GetCancellationTokenOnDestroy();
            await LMotion.Create(1, 0.9f, 0.08f)
                .Bind(x =>
                {
                    if (trs != null) // Null check before setting localScale
                    {
                        trs.localScale = new Vector3(x, x, x);
                    }
                }).ToValueTask(cancellationToken);

            await LMotion.Create(0.9f, 1.2f, 0.08f)
                .Bind(x =>
                {
                    if (trs != null) // Null check before setting localScale
                    {
                        trs.localScale = new Vector3(x, x, x);
                    }
                }).ToValueTask(cancellationToken);

            await LMotion.Create(1.2f, 1f, 0.08f)
                .WithOnComplete(() => { actionCallBack?.Invoke(); })
                .Bind(x =>
                {
                    if (trs != null) // Null check before setting localScale
                    {
                        trs.localScale = new Vector3(x, x, x);
                    }
                }).ToValueTask(cancellationToken);
        }
        else
        {
            actionCallBack?.Invoke();
        }
    }

    public static async UniTask ButtonClickDown(Transform trs, UnityAction actionCallBack = null)
    {
        await LMotion.Create(0, 0.9f, 0.1f).Bind(x => trs.localScale = new Vector3(x, x, x));
        LMotion.Create(Vector3.zero, VectorRotate, 0.1f)
            .WithOnComplete(() => { actionCallBack?.Invoke(); })
            .Bind(x => trs.eulerAngles = x).AddTo(trs);
    }

    public static async UniTask ButtonClickUp(Transform trs, UnityAction actionCallBack = null)
    {
        await LMotion.Create(0.9f, 1.2f, 0.1f).Bind(x => trs.localScale = new Vector3(x, x, x)).AddTo(trs);
        LMotion.Create(VectorRotate, -VectorRotate, 0.1f).Bind(x => trs.eulerAngles = x).AddTo(trs);
        await LMotion.Create(1.2f, 1f, 0.1f).Bind(x => trs.localScale = new Vector3(x, x, x)).AddTo(trs);
        LMotion.Create(-VectorRotate, Vector3.zero, 0.1f).WithOnComplete(() => { actionCallBack?.Invoke(); })
            .Bind(x => trs.eulerAngles = x).AddTo(trs);
    }

    public static async UniTask HighLight(Transform trs, UnityAction actionCallBack = null)
    {
        await LMotion.Create(1, 1.4f, 0.1f).Bind(x => trs.localScale = new Vector3(x, x, x)).AddTo(trs);
        LMotion.Create(1.4f, 1.2f, 0.1f)
            .WithOnComplete(() => { actionCallBack?.Invoke(); }).Bind(x => trs.localScale = new Vector3(x, x, x))
            .AddTo(trs);
    }

    public static async UniTask Pop(Transform trs, UnityAction actionCallBack = null)
    {
        await LMotion.Create(1, 1.1f, 0.1f).Bind(x => trs.localScale = new Vector3(x, x, x)).AddTo(trs);
        await LMotion.Create(1.1f, 0, 0.1f)
            .WithOnComplete(() => { actionCallBack?.Invoke(); }).Bind(x => trs.localScale = new Vector3(x, x, x))
            .AddTo(trs);
    }

    public static async UniTask Push(Transform trs, UnityAction actionCallBack = null)
    {
        trs.localScale = Vector3.zero;
        await LMotion.Create(0, 1.1f, 0.1f).Bind(x => trs.localScale = new Vector3(x, x, x)).AddTo(trs);
        await LMotion.Create(1.1f, 1, 0.1f)
            .WithOnComplete(() => { actionCallBack?.Invoke(); }).Bind(x => trs.localScale = new Vector3(x, x, x))
            .AddTo(trs);
    }
}