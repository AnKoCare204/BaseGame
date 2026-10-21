using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuSkipButton : MonoBehaviour
{
    public static Button MainButton { get; set; }
    [field: SerializeField] private Button Button { get; set; }

    private void Awake()
    {
        MainButton = Button;
    }
}