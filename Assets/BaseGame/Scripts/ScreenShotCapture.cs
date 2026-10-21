using System;
using UnityEngine;
using Sirenix.OdinInspector;
using System.IO;
using Core;
using Core.Manager;
using Core.UI.Activities;
using Cysharp.Threading.Tasks;
using TW.UGUI.Core.Activities;
using TW.UGUI.Core.Screens;
using TW.UGUI.Core.Views;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class ScreenshotCapture : MonoBehaviour
{
#if UNITY_EDITOR
    public Camera targetCamera;
    public int width = 2160;
    public int height = 3840;
    public int startLevel = 1; // Thay đổi nếu bạn muốn bắt đầu từ level khác
    public int endLevel = 100; // Thay đổi nếu bạn muốn kết thúc ở level khác

    // Đặt đúng đường dẫn tuyệt đối
    private string saveFolder = @"C:\Users\tuanv\Downloads\Anh level";
    
    [Button]
    private void Reset()
    {
        targetCamera = GetComponent<Camera>();
    }
    
    [Header("Screenshot Settings")]
    public string screenshotName = "Screenshot";

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.sKey.wasPressedThisFrame)
        {
            CaptureAsync($"{screenshotName}.png").Forget();
        }
    }
    
    [Button]
    public async UniTask<string> CaptureAsync(string screenshotName)
    {
        if (targetCamera == null)
        {
            Debug.LogError("Chưa gán Camera để chụp!");
            return null;
        }

        Directory.CreateDirectory(saveFolder);
        string filePath = Path.Combine(saveFolder, screenshotName);

        // Đợi cuối frame để đảm bảo UI + scene render xong
        await UniTask.WaitForEndOfFrame();

        RenderTexture rt = new RenderTexture(width, height, 24);
        targetCamera.targetTexture = rt;

        Texture2D screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);

        // Force render camera
        targetCamera.Render();

        RenderTexture.active = rt;
        screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        screenshot.Apply();

        targetCamera.targetTexture = null;
        RenderTexture.active = null;

        Destroy(rt);

        byte[] bytes = screenshot.EncodeToPNG();
        await File.WriteAllBytesAsync(filePath, bytes);

        Debug.Log("Đã lưu ảnh tại: " + filePath);

        return filePath;
    }

    [Button]
    public async UniTask CaptureAllLevel()
    {
        for (int level = startLevel; level <= endLevel; level++)
        {
            GameManager.Instance.Level.Value = level;

            await LevelManager.Instance.LoadLevel();
            
            // 🔥 Đợi frame ổn định (rất quan trọng)
            await UniTask.WaitForSeconds((1f), cancellationToken: this.GetCancellationTokenOnDestroy());
            await UniTask.DelayFrame(2, cancellationToken: this.GetCancellationTokenOnDestroy());
            await UniTask.WaitForEndOfFrame();

            // 🔥 Capture đúng frame đã render
            await CaptureAsync($"Level_{level}.png");
        }
    }
        
#endif
}