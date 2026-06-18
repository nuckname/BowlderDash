using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ThrowingBallState : RoundBaseState, IPathDrawListener
{
    public PathDrawer pathDrawer;
    public BallController ballController;
    public ThrowTuningDataSO tuningData;

    private RoundStateManager currentManager;

    public override void EnterState(RoundStateManager manager)
    {
        currentManager = manager;
        ballController.ResetBall();

        manager.MoveCamera(CameraLocation.BallThrowingCameraLocation);
        
        manager.TurnOnCardHand(false);
        
        manager.AllowPinsToFallOver(true);
        
        manager.UpdateInstructionText("Draw the ball's path! Start from the ball.");
        
        // Hand ourselves over as the Listener via strict contract
        pathDrawer.Begin(this);
    }

    public override void UpdateState(RoundStateManager manager)
    {
        // Cancel/Reset throw drawing on Right Click
        if (Input.GetMouseButtonDown(1))
        {
            OnDrawCancelled();
        }
    }

    public override void FixedUpdateState(RoundStateManager manager) { }

    public void OnDrawCompleted(List<CurvePoint> path, float drawDuration)
    {
        pathDrawer.Stop();
        
        // Calculate final speed
        float taperStartTime = tuningData.totalMinTime + Mathf.Max(0f, tuningData.powerFalloffDelay);
        float taperEndTime = Mathf.Max(taperStartTime + 0.0001f, tuningData.totalMaxTime);
        float t = (drawDuration <= taperStartTime) ? 1f : 1f - Mathf.InverseLerp(taperStartTime, taperEndTime, drawDuration);
        
        float launchSpeed = Mathf.Lerp(tuningData.forceMinSpeed, tuningData.forceMaxSpeed, t);
        
        OnInstructionUpdated("Ball Released!");
        
        // Execute the throw physics
        ballController.ExecuteThrow(path, launchSpeed);
    }

    public void OnDrawCancelled()
    {
        pathDrawer.ResetDrawing();
        ballController.ResetBall();
        OnInstructionUpdated("Draw the ball's path! Start from the ball.");
    }

    public void OnInstructionUpdated(string message)
    {
        currentManager.UpdateInstructionText(message);
    }
    
    public override void ExitState(RoundStateManager manager)
    {
        pathDrawer.Stop();
        currentManager = null;
    }

    public override void OnCollisionEnter(RoundStateManager manager, Collision collision) { }
    public override void OnCollisionExit(RoundStateManager manager, Collision collision) { }

    public override void OnTriggerEnter(RoundStateManager manager, Collider other)
    {
        // When the ball hits the end of the map.
        // Search BallCollisionTrigger
        if (other.gameObject.CompareTag("Ball"))
        {
            manager.SwitchState(manager.ScoringState);
        }
    }
    public override void OnTriggerExit(RoundStateManager manager, Collider other) { }


}