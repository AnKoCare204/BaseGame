using System;
using Core.UI;
using Cysharp.Threading.Tasks;
using Manager;
using TMPro;
using TW.UGUI.Core.Modals;
using TW.UGUI.Core.Views;
using UnityEngine;
using UnityEngine.UI;

public class SettingCheatObject : MonoBehaviour
{
    [field: SerializeField] public Button NextLevelBtn {get; private set;}
    [field: SerializeField] public Button PreLevelBtn {get; private set;}
    [field: SerializeField] public Button LoadLevelBtn {get; private set;}
    [field: SerializeField] public Button WinLevelBtn {get; private set;}
    [field: SerializeField] public Button DeleteDataServerBtn {get; private set;}
    [field: SerializeField] public Button DeleteDataLocalBtn {get; private set;}
    [field: SerializeField] public Button ResetHotPotEventBtn {get; private set;}
    [field: SerializeField] public TMP_InputField LevelInput {get; private set;}
    [field: SerializeField] public TMP_InputField EventInput {get; private set;}

    private void Start()
    {
        NextLevelBtn.SetOnClickDestination(NextLevel);
        PreLevelBtn.SetOnClickDestination(PreLevel);
        LoadLevelBtn.SetOnClickDestination(LoadLevel);
        WinLevelBtn.SetOnClickDestination(WinLevel);
        DeleteDataServerBtn.SetOnClickDestination(DeleteDataServer);
        DeleteDataLocalBtn.SetOnClickDestination(DeleteDataLocal);
        ResetHotPotEventBtn.SetOnClickDestination(ResetHotPotEvent);
    }

    private void ShowInter()
    {
        // InGameAdsController.EventShowAdsInter?.Invoke("TestInter", null, null, null, true, false);
    }

    private void ShowBanner()
    {
        // InGameAdsController.EventShowBanner?.Invoke();
    }

    private void HideBanner()
    {
        // InGameAdsController.EventHideBanner?.Invoke();
    }

    private void NextLevel()
    {
        ModalContainer.Find(ContainerKey.Modals).PopAsync(true);
        GameManager.Instance.Level.Value++;
        // GameManager.Instance.PlayGame();
    }

    private void PreLevel()
    {
        ModalContainer.Find(ContainerKey.Modals).PopAsync(true);
        GameManager.Instance.Level.Value--;
        // GameManager.Instance.PlayGame();
    }

    private void LoadLevel()
    {
        if (int.TryParse(LevelInput.text, out int level))
        {
            ModalContainer.Find(ContainerKey.Modals).PopAsync(true);
            GameManager.Instance.Level.Value = level;
            // GameManager.Instance.PlayGame();
        }
    }

    private void WinLevel()
    {
        if (int.TryParse(EventInput.text, out int skewerResolve))
        {
            // GamePlayManager.Instance.SetSkewerResolveCount(skewerResolve);
        }
        ModalContainer.Find(ContainerKey.Modals).PopAsync(true);
        // GamePlayManager.Instance.GameComplete();
    }     
    public void DeleteDataServer()
    {
        AsyncDeleteDataServer().Forget();
    }

    private async UniTask AsyncDeleteDataServer()
    {
        // if(!UserDataManager.Instance.IsDownloadedDataFromServer())
        // {
        //     await UserDataManager.Instance.DownloadUserDataFromServer(false, false);
        // }
        await UniTask.NextFrame();
        Debug.Log("DeleteDataServer");
        // UserDataManager.Instance.DeleteDataUserFromServer().Forget();
    }

    private void DeleteDataLocal()
    {
        Debug.Log("DeleteDataLocal");
        InGameDataManager.Instance.ClearData();
    }
    private void ResetHotPotEvent()
    {
        // HotPotManager.Instance.ResetHotPotEvent();
    }
}