using System;
using System.Collections.Generic;
using Manager;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using TMPro;
using TW.Utility.Extension;
using UnityEngine;

[CreateAssetMenu(fileName = "LanguageGlobalConfig", menuName = "GlobalConfigs/LanguageGlobalConfig")]
[GlobalConfig("Assets/Resources/GlobalConfig/")]
public class LanguageGlobalConfig : GlobalConfig<LanguageGlobalConfig>
{
    private string linkSheetId;
    private string sheet = $"Language";
    public List<SystemLanguage> listLanguage;
    public List<LanguageContainer> languageContainers;
    
    public SystemLanguage GetLanguage(SystemLanguage language)
    {
        if(listLanguage.Contains(language))
            return language;
        return SystemLanguage.English;
    }
    
    [Button]
    public async void FetchLanguageData()
    {
        linkSheetId = GameDefaultConfig.LinkSheet;
        string requestData = await ABakingSheet.GetCsv(linkSheetId, sheet);
        List<Dictionary<string, string>> tableData = await ABakingSheet.GetDataTable(linkSheetId, sheet);
        for (int i = 0; i < tableData.Count; i++)
        {
            int id = -1;
            if (!string.IsNullOrEmpty(tableData[i]["Id"]))
            {
                id = int.Parse(tableData[i]["Id"]);
            }
            else
                continue;
            if(id == -1) continue;
            for (int j = 0; j < listLanguage.Count; j++)
            {
                try
                {
                    var text = tableData[i][listLanguage[j].ToString()];
                    if (!string.IsNullOrEmpty(text))
                    {
                        TextData textData = FetchTextData(listLanguage[j].ToString(), id);
                        textData.textValue = text;
                    }
                }
                catch (Exception _)
                {

                }
            }
        }
    }
    
    public TextData FetchTextData(string language, int textId)
    {
        LanguageContainer languageContainer = GetLanguageContainer(language);
        var textValue = languageContainer.GetTextData(textId);
        return textValue;
    }
    
    
    public LanguageContainer GetLanguageContainer(string language)
    {
        LanguageContainer languageContainer = languageContainers.Find(x => x.language.ToString() == language);
        if (languageContainer == null)
        {
            languageContainer = new LanguageContainer();
            languageContainer.language = (SystemLanguage)Enum.Parse(typeof(SystemLanguage), language);
            languageContainer.textData = new List<TextData>();
            languageContainers.Add(languageContainer);
        }
        return languageContainer;
    }

    public TextData GetTextData(string language, int textId)
    {
        LanguageContainer languageContainer = GetLanguageContainer(language);
        var textValue = languageContainer.GetTextData(textId);
        if (textValue == null || string.IsNullOrEmpty(textValue.textValue))
        {
            languageContainer = GetLanguageContainer(SystemLanguage.English.ToString());
            textValue = languageContainer.GetTextData(textId);
        }
        return textValue;
    }
    /// <summary>
    /// get current text data based on the current language setting in InGameDataManager
    /// </summary>
    /// <param name="textId"></param>
    /// <returns></returns>
    public TextData GetCurrentTextData(int textId)
    {
        string language = InGameDataManager.Instance.InGameData.SettingDataSave.GetLanguage().Value;
        LanguageContainer languageContainer = GetLanguageContainer(language);
        var textValue = languageContainer.GetTextData(textId);
        if (textValue == null || string.IsNullOrEmpty(textValue.textValue))
        {
            languageContainer = GetLanguageContainer(SystemLanguage.English.ToString());
            textValue = languageContainer.GetTextData(textId);
        }
        return textValue;
    }
    public TMP_FontAsset GetTextFont(string language)
    {
        LanguageContainer languageContainer = GetLanguageContainer(language);
        return languageContainer.font;
    }

    /// <summary>
    /// get current font based on the current language setting in InGameDataManager
    /// </summary>
    /// <returns></returns>
    public TMP_FontAsset GetCurrentFont()
    {
        string currentLanguage = InGameDataManager.Instance.InGameData.SettingDataSave.GetLanguage().Value;
        LanguageContainer languageContainer = GetLanguageContainer(currentLanguage);
        if (languageContainer != null)
        {
            return languageContainer.font;
        }
        return null;
    }
    public TextData GetTextData(string language, string root)
    {
        LanguageContainer languageContainer = GetLanguageContainer(SystemLanguage.English.ToString());
        int id = languageContainer.GetTextId(root);
        languageContainer = GetLanguageContainer(language);
        var textValue = languageContainer.GetTextData(id);
        
        if (textValue == null || string.IsNullOrEmpty(textValue.textValue))
        {
            languageContainer = GetLanguageContainer(SystemLanguage.English.ToString());
            textValue = languageContainer.GetTextData(id);
        }
        return textValue;
    }
}

[System.Serializable]
public class LanguageContainer
{
    public SystemLanguage language;
    public TMP_FontAsset font;
    public List<TextData> textData;
    
    public TextData GetTextData(int textId)
    {
        TextData data = textData.Find(x => x.textId == textId);
        if (data == null)
        {
            data = new TextData();
            data.textId = textId;
            data.textValue = "";
            textData.Add(data);
        }
        return data;
    }
    public int GetTextId(string textValue)
    {
        TextData data = textData.Find(x => x.textValue == textValue);
        if (data != null)
            return data.textId;
        return 0; // Not found
    }
}

[System.Serializable]
public class TextData
{
    public int textId;
    public string textValue;
}