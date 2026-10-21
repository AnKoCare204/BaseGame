using System;
using Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LanguageBtn : MonoBehaviour
{
    public Button Button;
    public TextMeshProUGUI txt;
    public string LanguageCode;
    public Action callback;

    private void Start()
    {
        Button.onClick.AddListener(ChangeLanguage);
    }

    public void Init(string languageCode, Action callback = null)
    {
        LanguageCode = languageCode;
        this.callback = callback;
        if (txt != null)
        {
            txt.text = LanguageCode;
        }
    }
    
    void ChangeLanguage()
    {
        callback?.Invoke();
        InGameDataManager.Instance.InGameData.SettingData.SetLanguage(LanguageCode);
    }
}
