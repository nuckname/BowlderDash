using UnityEngine;

[System.Serializable]
public class MainMenuState : RoundBaseState
{
    [SerializeField] private GameObject mainMenuTvs;
    private RoundStateManager _roundStateManager;
    public override void EnterState(RoundStateManager manager)
    {
        _roundStateManager = manager;
        
        _roundStateManager.MoveCamera(CameraLocation.MainMenuCameraLocation);
        
        manager.TurnOnCardHand(false);
        
        mainMenuTvs.SetActive(true);
    }

    public override void UpdateState(RoundStateManager manager)
    {
    }

    public override void FixedUpdateState(RoundStateManager manager)
    {
    }

    public override void OnCollisionEnter(RoundStateManager manager, Collision collision)
    {
    }

    public override void OnCollisionExit(RoundStateManager manager, Collision collision)
    {
    }

    public override void OnTriggerEnter(RoundStateManager manager, Collider other)
    {
    }

    public override void OnTriggerExit(RoundStateManager manager, Collider other)
    {
    }

    public override void ExitState(RoundStateManager manager)
    {
        mainMenuTvs.SetActive(false);
    }


}
