// using System;
// using System.Threading;
// using Cysharp.Threading.Tasks;
// using Spine;
// using Spine.Unity;

// namespace BaseGame.Common
// {
//     public static class SpineTrackEntryExtensions
//     {
//         public static UniTask WaitForCompleteAsync(this TrackEntry trackEntry, CancellationToken cancellationToken = default)
//         {
//             if (trackEntry == null)
//             {
//                 return UniTask.CompletedTask;
//             }

//             var tcs = new UniTaskCompletionSource();
//             AnimationState.TrackEntryDelegate handler = null;
//             handler = (entry) =>
//             {
//                 trackEntry.Complete -= handler;
//                 tcs.TrySetResult();
//             };

//             trackEntry.Complete += handler;

//             cancellationToken.Register(() =>
//             {
//                 trackEntry.Complete -= handler;
//                 tcs.TrySetCanceled();
//             });

//             return tcs.Task;
//         }

//         public static UniTask WaitForEndAsync(this TrackEntry trackEntry, CancellationToken cancellationToken = default)
//         {
//             if (trackEntry == null)
//             {
//                 return UniTask.CompletedTask;
//             }

//             var tcs = new UniTaskCompletionSource();
//             AnimationState.TrackEntryDelegate handler = null;
//             handler = (entry) =>
//             {
//                 trackEntry.End -= handler;
//                 tcs.TrySetResult();
//             };

//             trackEntry.End += handler;

//             cancellationToken.Register(() =>
//             {
//                 trackEntry.End -= handler;
//                 tcs.TrySetCanceled();
//             });

//             return tcs.Task;
//         }
//     }
// }

