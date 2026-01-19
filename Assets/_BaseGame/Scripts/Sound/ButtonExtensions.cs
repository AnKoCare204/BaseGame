using System;
using Core.Runner;
using Cysharp.Threading.Tasks;
using LitMotion;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI
{
    public static class ButtonExtensions
    {
        private static bool ButtonBlockAction { get; set; }
        public static IDisposable SetOnClickDestination(this Button self, Action onClick)
        {
            return self.onClick
                .AsObservable()
                .Subscribe(_ => ActionExecuteRunner.AddExecuteAble(new PlayerAction(0, onClick)), null!)
                .AddTo(self);
        }
        
        public static IDisposable SetOnClickDestination(this Button self, Func<UniTask> onClick)
        {
            return self.onClick
                .AsObservable()
                .Subscribe(_ => ActionExecuteRunner.AddExecuteAble(new PlayerTask(0, onClick)), null!)
                .AddTo(self);
        }

        public static void ClearOnClick(this Button self)
        {
            self.onClick.RemoveAllListeners();
        }

        public static bool TryTakeAction(this Component self)
        {
            if (ButtonBlockAction) return false;
            ButtonBlockAction = true;
            LMotion.Create(0, 1f, 1.5f)
                .WithOnComplete(ReleaseAction)
                .RunWithoutBinding();
            return true;
        }
        private static void ReleaseAction()
        {
            ButtonBlockAction = false;
        }
    }
}