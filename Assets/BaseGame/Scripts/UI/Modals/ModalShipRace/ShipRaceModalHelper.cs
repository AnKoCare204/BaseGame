using Cysharp.Threading.Tasks;
using TW.UGUI.Core.Modals;

namespace Core.UI.Modals
{
    public enum ShipRaceModalState
    {
        ShowCurrentStage,
        ShowWinStage,
        ShowLoseStage,
    }

    /// <summary>Stage index -> prefab name. Keeps the mapping out of the modals themselves.</summary>
    public static class ShipRaceModalHelper
    {
        public static async UniTask ShowStageModal(int stageId, ShipRaceModalState state)
        {
            string modalName = stageId switch
            {
                0 => nameof(ModalShipRaceStage1Info),
                1 => nameof(ModalShipRaceStage2Info),
                _ => nameof(ModalShipRaceStage3Info),
            };
            ModalOptions options = new(modalName, backdropAlpha: 0f);
            await ModalContainer.Find(ContainerKey.Modals).PushAsync(options, state);
        }

        public static async UniTask ShowJoinModal(int stageId)
        {
            string modalName = stageId switch
            {
                0 => nameof(ModalJoinShipRaceStage1),
                1 => nameof(ModalJoinShipRaceStage2),
                _ => nameof(ModalJoinShipRaceStage3),
            };
            ModalOptions options = new(modalName);
            await ModalContainer.Find(ContainerKey.Modals).PushAsync(options);
        }

        public static UniTask ShowInfoActivity(int stageId)
        {
            // Hook the rules/info activity up here once its prefab exists
            return UniTask.CompletedTask;
        }
    }
}
