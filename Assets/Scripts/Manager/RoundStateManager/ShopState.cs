using UnityEngine;

[System.Serializable]
public class ShopState : RoundBaseState
{
    public override void EnterState(RoundStateManager manager)
    {
        manager.MoveCamera(CameraLocation.ShopCameraLocation);
        manager.UpdateInstructionText("Shop phase. Spend tickets, then click Continue.");

        manager.shopUI.Show();
    }

    public override void UpdateState(RoundStateManager manager) { }
    public override void FixedUpdateState(RoundStateManager manager) { }
    public override void OnCollisionEnter(RoundStateManager manager, Collision collision) { }
    public override void OnCollisionExit(RoundStateManager manager, Collision collision) { }
    public override void OnTriggerEnter(RoundStateManager manager, Collider other) { }
    public override void OnTriggerExit(RoundStateManager manager, Collider other) { }
    
    public override void ExitState(RoundStateManager manager)
    {
        if (manager.shopUI != null)
        {
            manager.shopUI.Hide();
        }
    }
}