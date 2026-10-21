using System.Collections;
using System.Collections.Generic;
using Manager;
using UnityEngine;
using UnityEngine.UI;

public class LevelUnitList : MonoBehaviour
{
    public List<LevelUnit> levelUnits;

    public void Init()
    {
        int level = InGameDataManager.Instance.InGameData.UserData.CurrentLevel;
        ClearAllUnits();
        int count = levelUnits.Count - 1;
        for (int i = 0; i < levelUnits.Count; i++)
        {
            levelUnits[count - i].gameObject.SetActive(true);
            levelUnits[count - i].Init(level + i);
        }
    }
    
    void ClearAllUnits()
    {
        for (var i = 0; i < levelUnits.Count; i++)
        {
            levelUnits[i].gameObject.SetActive(false);
        }
    }
}
