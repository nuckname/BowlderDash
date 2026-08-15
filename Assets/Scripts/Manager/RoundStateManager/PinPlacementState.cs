using System.Collections;
using UnityEngine;

public class PinPlacementState : RoundBaseState
{
    private bool _isTransitioning = false;

    public override void EnterState(RoundStateManager manager)
    {
        Debug.Log("Entering Pin Placement State");
        _isTransitioning = false;
        
        manager.MoveCamera(CameraLocation.PinCameraLocation);
        manager.TurnOnCardHand(true);
        
        PinOnPinCollisionLayer(false);
        
        manager.AllowPinCollisionEffects(false);
        
        DeckManager.Instance.Draw(manager.amountOfCardsToDrawEachTurn);
    }

    public override void UpdateState(RoundStateManager manager)
    {
        if (Input.GetKeyDown(KeyCode.Tab) && !_isTransitioning)
        {
            manager.StartCoroutine(HandleDiscardAndTransition(manager));
        }
    }
    
    private IEnumerator HandleDiscardAndTransition(RoundStateManager manager)
    {
        _isTransitioning = true;
        
        // Trigger the discard early so we can see the animation
        HandManager.Instance.DiscardHand();
        
        // Wait for the exact duration of the discard animation plus a bit
        yield return new WaitForSeconds(HandManager.Instance.discardAnimationDuration + HandManager.Instance.discardAnimationDurationBuffer);
        
        manager.SwitchState(manager.throwingBallState);
    }

    public override void FixedUpdateState(RoundStateManager manager) { }

    public override void OnCollisionEnter(RoundStateManager manager, Collision collision) { }
    public override void OnCollisionExit(RoundStateManager manager, Collision collision) { }
    public override void OnTriggerEnter(RoundStateManager manager, Collider other) { }
    public override void OnTriggerExit(RoundStateManager manager, Collider other) { }

    public override void ExitState(RoundStateManager manager)
    {
        manager.GetAllPinsInCurrentFrame(manager);

        PinOnPinCollisionLayer(true);
        
        manager.AllowPinsToFallOver(false);
    }
    
    private void PinOnPinCollisionLayer(bool turnOn)
    {
        int pinLayer = LayerMask.NameToLayer("Pin");

        // Turn collision on and off for pin layer.
        Physics.IgnoreLayerCollision(pinLayer, pinLayer, turnOn);
    }
}