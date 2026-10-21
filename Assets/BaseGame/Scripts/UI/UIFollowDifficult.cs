using Core.GamePlay;
using Core.Manager;
using R3;
using UnityEngine;
using UnityEngine.UI;

public class UIFollowDifficult : MonoBehaviour
{
    [SerializeField] private Sprite[] sprFollowDifficult;
    [SerializeField] private Image sprDifficult;
    
    private void Start()
    {
        var currentLevelDifficulty = LevelManager.Instance.currentLevelDifficulty;
        currentLevelDifficulty.Subscribe(ChangeLevelDifficult).AddTo(this);
    }

    private void ChangeLevelDifficult(LevelDifficulty dChange)
    {
        sprDifficult.sprite = sprFollowDifficult[(int)dChange];
    }
}
