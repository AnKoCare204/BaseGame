using System;

namespace Core.Tutorial
{
    /// <summary>Chữ hiển thị của FTUE (tiếng Anh, ngắn). Serialize trên TutorialManager để sửa không cần build code; sau này đưa vào bảng dịch.</summary>
    [Serializable]
    public class TutorialTexts
    {
        public string L1Match = "Tap fish that match the order!";
        public string L1Goal = "Fill every order to win!";
        public string L1Denied = "Match the order!";
        public string L2NoOrder = "No order for this one yet.";
        public string L2Waits = "It waits here.";
        public string L2FillOrder = "Complete an order to get a new one.";
        public string L2FlewIn = "Its order is here! It flew in by itself.";
        public string L2FullRule = "Don't fill all {0} slots!";
        public string Rescue = "Slots full! We packed them for you.";
    }
}
