using UnityEngine;
using TW.Reactive.CustomComponent;
using Sirenix.OdinInspector;
using R3;
using TW.Utility.DesignPattern;

public class ResourceInGameController : Singleton<ResourceInGameController>
{
    [field: SerializeField] private ReactiveValue<float> _cashValue = new(0);
    [HideInInspector] public ReactiveValue<float> CashValue => _cashValue;
    [field: SerializeField] private float timeSpawnCash = 4f;

    [HideInInspector] public ReactiveValue<float> ProgressBarSpawnCash = new(0);

    private float countTimeSpawnCash = 0f;

    void Update()
    {
        countTimeSpawnCash += Time.deltaTime;
        ProgressBarSpawnCash.Value = countTimeSpawnCash / timeSpawnCash;
        if (countTimeSpawnCash >= timeSpawnCash)
        {
            countTimeSpawnCash = 0f;
            ProgressBarSpawnCash.Value = 0f;
            _cashValue.Value += 1f;
        }
    }
}
