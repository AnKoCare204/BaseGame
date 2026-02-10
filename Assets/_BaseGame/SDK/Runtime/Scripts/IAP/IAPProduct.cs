using System;

public class IAPProduct
{
    public string ProductId;
    public Action OnSuccess;
    public Action OnFail;

    public IAPProduct(string productId, Action onSuccess, Action onFail)
    {
        ProductId = productId;
        OnSuccess = onSuccess;
        OnFail = onFail;
    }
}