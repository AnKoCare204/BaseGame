using System;
using Cysharp.Threading.Tasks;

public interface IUITabLifecycleEvent
{
    UniTask Initialize(Memory<object> args);
    void OnTabEnter(Memory<object> args);
    void OnTabExit(Memory<object> args);
}