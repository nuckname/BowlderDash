using UnityEngine;

[System.Serializable]
public class ShopState : RoundBaseState
{
    public override void EnterState(RoundStateManager manager)
    {
        manager.MoveCamera(CameraLocation.ShopCameraLocation);
        manager.UpdateInstructionText("Shop phase. Spend tickets, then click Continue.");

        manager.shopUI.ToggleShop(true, manager);
        
        DeckManager.Instance.ShuffleDiscardIntoDeck();
        
        manager.DeleteAllActivePins();
        
        manager.ResetLaneExploders();
        
        manager.postProcessingEffects.SetActive(false);
    }

    public override void UpdateState(RoundStateManager manager) { }
    public override void FixedUpdateState(RoundStateManager manager) { }
    public override void OnCollisionEnter(RoundStateManager manager, Collision collision) { }
    public override void OnCollisionExit(RoundStateManager manager, Collision collision) { }
    public override void OnTriggerEnter(RoundStateManager manager, Collider other) { }
    public override void OnTriggerExit(RoundStateManager manager, Collider other) { }
    
    public override void ExitState(RoundStateManager manager)
    {
        manager.shopUI.CloseRouletteForShopExit();
        manager.shopUI.ToggleShop(false);
        manager.postProcessingEffects.SetActive(true);
        

    }
}
