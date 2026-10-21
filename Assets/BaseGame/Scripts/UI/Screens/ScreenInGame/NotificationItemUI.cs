using Cysharp.Threading.Tasks;
using LitMotion;
using TMPro;
using UnityEngine;

public class NotificationItemUI : MonoBehaviour
{
    [field: SerializeField] public TextMeshProUGUI TextNotification { get; private set; }
    [field: SerializeField] public CanvasGroup CanvasGroup { get; private set; }

    public void ShowNotification(string message)
    {
        TextNotification.text = message;
        PlayAnimation().Forget();
    }

    private async UniTask PlayAnimation()
    {
        gameObject.SetActive(true);

        // Reset state
        transform.localScale = Vector3.one * 0.8f;
        CanvasGroup.alpha = 0f;
        var startPos = Vector3.zero;
        transform.localPosition = startPos;

        // Fade + scale in cùng lúc
        var fadeIn = LMotion.Create(0f, 1f, 0.3f)
            .WithEase(Ease.OutCubic)
            .Bind(a => CanvasGroup.alpha = a)
            .AddTo(this)
            .ToUniTask();

        var scaleIn = LMotion.Create(Vector3.one * 0.8f, Vector3.one, 0.3f)
            .WithEase(Ease.OutBack)
            .Bind(s => transform.localScale = s)
            .AddTo(this)
            .ToUniTask();

        await UniTask.WhenAll(fadeIn, scaleIn);

        // Move up nhẹ trong 1.5s (tạo cảm giác trôi)
        var moveUp = LMotion.Create(startPos, startPos + new Vector3(0, 100f, 0), 1f)
            .WithEase(Ease.OutCubic)
            .Bind(p => transform.localPosition = p)
            .AddTo(this)
            .ToUniTask();

        // Giữ yên trong 1.5s
        await UniTask.Delay(1000, cancellationToken: this.GetCancellationTokenOnDestroy());
        await moveUp;

        // Fade out + move lên thêm chút
        var fadeOut = LMotion.Create(1f, 0f, 0.4f)
            .WithEase(Ease.InCubic)
            .Bind(a => CanvasGroup.alpha = a)
            .AddTo(this)
            .ToUniTask();

        var moveDisappear = LMotion.Create(transform.localPosition, transform.localPosition + new Vector3(0, 40f, 0), 0.4f)
            .WithEase(Ease.InCubic)
            .Bind(p => transform.localPosition = p)
            .AddTo(this)
            .ToUniTask();

        await UniTask.WhenAll(fadeOut, moveDisappear);

        gameObject.SetActive(false);
    }
}
