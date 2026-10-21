using System;
using Core;
using UnityEngine;
using UnityEngine.UI;

public class CheatTreetop : MonoBehaviour
{
    [field:  SerializeField] public InputField inputField;
    [field: SerializeField] public Button CheatButton;

    public void Start()
    {
        if (GameManager.Instance.BuildType == BuildType.Release)
        {
            gameObject.SetActive(false);
            return;
        }

        CheatButton.SetOnClickDestination(OnBtnCheatClicked);
    }

    private void OnBtnCheatClicked()
    {
        if (int.TryParse(inputField.text, out int point))
        {
#if CHEAT_ONLY
            TreetopManager.Instance.AddCustomPoint(point);
#endif
        }
    }
}
