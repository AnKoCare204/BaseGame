using System;
using Manager;
using R3;
using TMPro;
using TW.Reactive.CustomComponent;
using UnityEngine;

public class LocalizeText : MonoBehaviour
{
    public int textId;
    public TextMeshProUGUI txt;
    private ReactiveValue<string> Language;

    private void Reset()
    {
        txt = GetComponent<TextMeshProUGUI>();
    }

    private void Awake()
    {
        if(!txt)
        {
            txt = GetComponent<TextMeshProUGUI>();
        }
        if (Language == null)
        {
            Language = InGameDataManager.Instance.InGameData.SettingDataSave.GetLanguage();
            Language.ReactiveProperty.Subscribe(OnLanguageChanged).AddTo(this);
        }
    }

    public void SetTextId(int id)
    {
        textId = id;
        OnLanguageChanged(Language.Value);
    }

    private void OnLanguageChanged(string language)
    {
        try
        {
            TextData data = LanguageGlobalConfig.Instance.GetTextData(language, textId);
            if(data!=null)
                txt.text = $"{data.textValue}";
            TMP_FontAsset font = LanguageGlobalConfig.Instance.GetTextFont(language);
            if(font != null && txt.font.name != font.name)
            {
                // Debug.Log($"Set font {font.name} and old font {txt.font.name} for textId {textId} and language {language}");
                txt.font = font;
            }
        }
        catch (Exception e)
        {
            string trace = "";
            Transform current = transform;
            while (current != null)
            {
                trace = current.name + "/" + trace;
                current = current.parent;
            }

            Debug.Log(trace);
            Debug.LogError($"Error setting localized text for textId {textId} and language {language}: {e.Message}");
        }
    }
}

public static class LocalizeTextExtension
{
    public static void SetLocalizeText(this TextMeshProUGUI txt, int id)
    {
        TextData textData = GameManager.Instance.GetTextData(id);
        txt.SetText(textData.textValue);
        TMP_FontAsset font = GameManager.Instance.GetFont();
        if (font != null && txt.font != font)
        {
            txt.font = font;
        }
    }
    public static void SetLocalizeTextFormat(this TextMeshProUGUI txt, int id, params object[] args)
    {
        TextData textData = GameManager.Instance.GetTextData(id);
        txt.SetText(string.Format(textData.textValue, args));
        TMP_FontAsset font = GameManager.Instance.GetFont();
        if (font != null && txt.font.name != font.name)
        {
            txt.font = font;
        }
    }
}