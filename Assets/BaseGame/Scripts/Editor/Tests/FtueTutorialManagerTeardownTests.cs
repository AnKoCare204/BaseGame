using System.Collections.Generic;
using System.Reflection;
using Core.Manager;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// Thoát Play / đổi scene: container UI "Activities" có thể đã huỷ trước TutorialManager (thứ tự huỷ không cố định), lúc đó
/// ActivityContainer.Find ghi log lỗi. OnDestroy vẫn dọn lớp tối / khóa bấm nhưng không được đi tìm container để đóng khung chữ.
public class FtueTutorialManagerTeardownTests
{
    [Test]
    public void OnDestroy_WithoutUiContainers_LogsNoError()
    {
        GameObject go = EditorUtility.CreateGameObjectWithHideFlags("tutorial manager", HideFlags.HideAndDontSave, typeof(TutorialManager));
        var errors = new List<string>();
        void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception) errors.Add(message);
        }

        Application.logMessageReceived += OnLog;
        try
        {
            typeof(TutorialManager).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(go.GetComponent<TutorialManager>(), null);
        }
        finally
        {
            Application.logMessageReceived -= OnLog;
            Object.DestroyImmediate(go);
        }

        CollectionAssert.IsEmpty(errors);
    }
}
