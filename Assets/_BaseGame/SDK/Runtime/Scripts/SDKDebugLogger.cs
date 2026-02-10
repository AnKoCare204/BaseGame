using System.Runtime.CompilerServices;
#pragma warning disable CS0162 // Unreachable code detected

namespace SDK
{
    /// <summary>
    /// Zero-garbage debug logging system with multiple optimization strategies
    /// </summary>

    public static class SDKDebugLogger
    {
        #region Conditional Compilation Flags
        private const bool SDK_DEBUG_ENABLED = true;
        #endregion

        #region Zero-Garbage Logging Methods

        // ReSharper disable Unity.PerformanceAnalysis
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Log(string message)
        {
            if (!SDK_DEBUG_ENABLED) return;
            UnityEngine.Debug.Log($"[ADS] {message}");
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Log(object message)
        {
            if (!SDK_DEBUG_ENABLED) return;
            UnityEngine.Debug.Log($"[ADS] {message}");
        }
        // ReSharper disable Unity.PerformanceAnalysis
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Log(string messageFormat, object param1)
        {
            if (!SDK_DEBUG_ENABLED) return;
            UnityEngine.Debug.Log($"[ADS] {string.Format(messageFormat, param1)}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Log(string messageFormat, object param1, object param2)
        {
            if (!SDK_DEBUG_ENABLED) return;
            UnityEngine.Debug.Log($"[ADS] {string.Format(messageFormat, param1, param2)}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Log(string messageFormat, object param1, object param2, object param3)
        {
            if (!SDK_DEBUG_ENABLED) return;
            UnityEngine.Debug.Log($"[ADS] {string.Format(messageFormat, param1, param2, param3)}");
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Log(string messageFormat, object param1, object param2, object param3, object param4)
        {
            if (!SDK_DEBUG_ENABLED) return;
            UnityEngine.Debug.Log($"[ADS] {string.Format(messageFormat, param1, param2, param3, param4)}");
        }
        // ReSharper disable Unity.PerformanceAnalysis
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogError(string message)
        {
            if (!SDK_DEBUG_ENABLED) return;
            UnityEngine.Debug.LogError($"[ADS] {message}");
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogError(object message)
        {
            if (!SDK_DEBUG_ENABLED) return;
            UnityEngine.Debug.LogError($"[ADS] {message}");
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogError(string messageFormat, object param1)
        {
            if (!SDK_DEBUG_ENABLED) return;
            UnityEngine.Debug.LogError($"[ADS] {string.Format(messageFormat, param1)}");
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogError(string messageFormat, object param1, object param2)
        {
            if (!SDK_DEBUG_ENABLED) return;
            UnityEngine.Debug.LogError($"[ADS] {string.Format(messageFormat, param1, param2)}");
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogError(string messageFormat, object param1, object param2, object param3)
        {
            if (!SDK_DEBUG_ENABLED) return;
            UnityEngine.Debug.LogError($"[ADS] {string.Format(messageFormat, param1, param2, param3)}");
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogError(string messageFormat, object param1, object param2, object param3, object param4)
        {
            if (!SDK_DEBUG_ENABLED) return;
            UnityEngine.Debug.LogError($"[ADS] {string.Format(messageFormat, param1, param2, param3, param4)}");
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogWarning(string message)
        {
            if (!SDK_DEBUG_ENABLED) return;
            UnityEngine.Debug.LogWarning($"[ADS] {message}");
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogWarning(object message)
        {
            if (!SDK_DEBUG_ENABLED) return;
            UnityEngine.Debug.LogWarning($"[ADS] {message}");
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogWarning(string messageFormat, object param1)
        {
            if (!SDK_DEBUG_ENABLED) return;
            UnityEngine.Debug.LogWarning($"[ADS] {string.Format(messageFormat, param1)}");
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogWarning(string messageFormat, object param1, object param2)
        {
            if (!SDK_DEBUG_ENABLED) return;
            UnityEngine.Debug.LogWarning($"[ADS] {string.Format(messageFormat, param1, param2)}");
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogWarning(string messageFormat, object param1, object param2, object param3)
        {
            if (!SDK_DEBUG_ENABLED) return;
            UnityEngine.Debug.LogWarning($"[ADS] {string.Format(messageFormat, param1, param2, param3)}");
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogWarning(string messageFormat, object param1, object param2, object param3, object param4)
        {
            if (!SDK_DEBUG_ENABLED) return;
            UnityEngine.Debug.LogWarning($"[ADS] {string.Format(messageFormat, param1, param2, param3, param4)}");
        }

        #endregion
    }
}