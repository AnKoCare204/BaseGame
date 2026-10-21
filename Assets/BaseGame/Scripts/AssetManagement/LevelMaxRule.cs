using System;

namespace CoreData
{
    /// <summary>Nút Play ở Home nên làm gì với level hiện tại.</summary>
    public enum LevelPlayDecision
    {
        Play,
        ComingSoon,
        UnstableNetwork,
    }

    /// <summary>Level tối đa chơi được, tách thành hàm thuần để test.</summary>
    public static class LevelMaxRule
    {
        /// <summary>
        /// remoteMax nhỏ hơn hoặc bằng 0 = chưa nhận Remote Config, dùng bakedMax (số level có trong bản build).
        /// Kết quả không thấp hơn level người chơi đã tới, trong phạm vi bakedMax: hạ remoteMax không khoá người chơi cũ.
        /// </summary>
        public static int Resolve(int remoteMax, int bakedMax, int currentLevel)
        {
            int baseMax = remoteMax > 0 ? remoteMax : bakedMax;
            int reached = Math.Min(currentLevel, bakedMax);
            return Math.Max(baseMax, reached);
        }

        /// <summary>
        /// level trong dải chơi được thì chơi. Vượt dải: nếu level lớn hơn số bake mà Remote Config chưa đọc được thì chưa
        /// biết CDN có level đó hay không, báo người chơi thử lại; các trường hợp còn lại là hết level.
        /// </summary>
        public static LevelPlayDecision DecidePlay(int level, int maxPlayable, int bakedMax, bool remoteConfigLoaded)
        {
            if (level >= 1 && level <= maxPlayable) return LevelPlayDecision.Play;
            if (level > bakedMax && !remoteConfigLoaded) return LevelPlayDecision.UnstableNetwork;
            return LevelPlayDecision.ComingSoon;
        }
    }
}
