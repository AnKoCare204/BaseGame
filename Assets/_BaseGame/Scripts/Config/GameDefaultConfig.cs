using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using SDK;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using TW.Reactive.CustomComponent;
using UnityEngine;
using UnityEngine.Serialization;

public static class GameDefaultConfig
{
    public static string LinkSheet => GetLinkSheet();
    

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetLinkSheet()
    {
        return "12QQjFYME-w_3HSdLXAlGgTlG2pPZWn5wedieETbQX_E";
    }
}

