using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelUnit : MonoBehaviour
{
    public RectTransform rectTransform;
    public Image bgImg;
    public Sprite clearedSpr;
    public Sprite lockedSpr;
    public TextMeshProUGUI levelTxt;
    public GameObject contentObj;
    public Image markerImage;
    public Image pictureImage;

    public int CurrentLevel = 1;
    
    public void Init(int level)
    {
        contentObj.SetActive(false);
        if(level < CurrentLevel)
        {
            bgImg.sprite = clearedSpr;
            levelTxt.text = "";
        }
        else if (level == CurrentLevel)
        {
            
        }
        else
        {
            bgImg.sprite = lockedSpr;
        }
    }
}
