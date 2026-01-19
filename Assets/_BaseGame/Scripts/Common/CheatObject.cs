using System;
using System.Collections;
using System.Collections.Generic;
using Manager;
using UnityEngine;

public class CheatObject : MonoBehaviour
{
    private void Start()
    {
        gameObject.SetActive(GameManager.Instance.BuildType == BuildType.Cheat);
    }

    private void OnEnable()
    {
        gameObject.SetActive(GameManager.Instance.BuildType == BuildType.Cheat);
    }
}
