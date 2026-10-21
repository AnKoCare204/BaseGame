using TW.Utility.DesignPattern;
using UnityEngine;

/// <summary>Stub xin đánh giá (Google Play In-App Review / iOS StoreKit). Gắn plugin thật sau.</summary>
public class AppReviewManager : Singleton<AppReviewManager>
{
    public void AllInOneFlowClick()
    {
        Debug.Log("[AppReviewStub] AllInOneFlowClick");
    }

    public void RequestReview()
    {
        Debug.Log("[AppReviewStub] RequestReview");
    }
}
