public interface IRemoteConfigData
{
    KeyRemoteConfig KeyRemoteConfig { get; }
    void LoadData();
    void ResetData();
    string ToJson();
    void FromJson(string json);
}