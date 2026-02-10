using System.Runtime.CompilerServices;
using UnityEngine;

namespace SDK
{
    public static class FirebaseLogger
    {
    private static bool IsShowing => true;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Log(string message)
    {
        if (!IsShowing) return;
        Debug.Log($"<color=#D84B20>[Firebase]</color> {message}");
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogWarning(string message)
    {
        if (!IsShowing) return;
        Debug.Log($"<color=#D84B20>[Firebase]</color><color=yellow>[Warning]</color> {message}");
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogError(string message)
    {
        if (!IsShowing) return;
        Debug.Log($"<color=#D84B20>[Firebase]</color><color=red>[Error]</color> {message}");
        }   
    }
}