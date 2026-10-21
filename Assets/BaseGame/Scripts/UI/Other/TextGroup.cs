using Cysharp.Text;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

public class TextGroup : MonoBehaviour
{
    [field: SerializeField] private TextMeshProUGUI[] TextArray {get; set;}
    [field: SerializeField] private Color ColorTest {get; set;}
    [field: SerializeField] private TMPTextAnim[] TMPTextAnimArray {get; set;}
    [Button]
    public void PlayAnimation(string text)
    {
        for (int i = 0; i < TMPTextAnimArray.Length; i++)
        {
            TMPTextAnimArray[i].PlayAnimation(text);
        }
    }
    [Button]
    public void PlayAnimation()
    {
        for (int i = 0; i < TMPTextAnimArray.Length; i++)
        {
            TMPTextAnimArray[i].PlayAnimation();
        }
    }
    public void SetText(string text)
    {
        for (int i = 0; i < TextArray.Length; i++)
        {
            TextArray[i].SetText(text);
        }
    }
    public void SetTextFormat(string text, string t0)
    {
        for (int i = 0; i < TextArray.Length; i++)
        {
            TextArray[i].SetTextFormat(text, t0);
        }
    }

    public void SetTextFormat(string text, string t0, string t1)
    {
        for (int i = 0; i < TextArray.Length; i++)
        {
            TextArray[i].SetTextFormat(text, t0, t1);
        }
    }
}