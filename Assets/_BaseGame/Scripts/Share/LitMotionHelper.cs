using LitMotion;

namespace Common
{
    public static class LitMotionHelper
    {
        public static void Forget(this MotionHandle handle)
        {
            
        }

        public static void TryStop(this MotionHandle handle)
        {
            if (handle.IsActive())
            {
                handle.Cancel();
            }
        }
    }
}