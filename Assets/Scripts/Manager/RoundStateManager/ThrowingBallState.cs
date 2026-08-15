using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ThrowingBallState : RoundBaseState, IPathDrawListener
{
    public PathDrawer pathDrawer;
    public ThrowTuningDataSO tuningData;

    private RoundStateManager currentManager;

    public override void EnterState(RoundStateManager manager)
    {
        currentManager = manager;
        
        manager.ballController.ResetBall();
        manager.MoveCamera(CameraLocation.BallThrowingCameraLocation);
        
        manager.TurnOnCardHand(false);
        
        manager.AllowPinCollisionEffects(true);
        
        manager.AllowPinsToFallOver(true);
        
        manager.UpdateInstructionText("Draw the ball's path! Start from the ball.");
        
        pathDrawer.Begin(this);
    }

    public override void UpdateState(RoundStateManager manager)
    {
        if (Input.GetMouseButtonDown(1))
        {
            OnDrawCancelled();
        }
    }

    public override void FixedUpdateState(RoundStateManager manager) { }

    public void OnDrawCompleted(List<CurvePoint> path, float drawDuration)
    {
        pathDrawer.Stop();
        
        float taperStartTime = tuningData.totalMinTime + Mathf.Max(0f, tuningData.powerFalloffDelay);
        float taperEndTime = Mathf.Max(taperStartTime + 0.0001f, tuningData.totalMaxTime);
        float t = (drawDuration <= taperStartTime) ? 1f : 1f - Mathf.InverseLerp(taperStartTime, taperEndTime, drawDuration);
        
        float launchSpeed = Mathf.Lerp(tuningData.forceMinSpeed, tuningData.forceMaxSpeed, t);
        
        OnInstructionUpdated("Ball Released!");
        
        // Execute throw
        currentManager.ballController.ExecuteThrow(path, launchSpeed);

        // Camera
        currentManager.cameraFollowScript.StartTracking();
    }

    public void OnDrawCancelled()
    {
        pathDrawer.ResetDrawing();
        currentManager.ballController.ResetBall();
        
        currentManager.cameraFollowScript.StopTracking();
        
        OnInstructionUpdated("Draw the ball's path! Start from the ball.");
    }

    public void OnInstructionUpdated(string message)
    {
        currentManager.UpdateInstructionText(message);
    }
    
    public override void ExitState(RoundStateManager manager)
    {
        pathDrawer.Stop();
        
        manager.cameraFollowScript.StopTracking();

        manager.AddBallThrownToSession();
        
        currentManager = null;
    }

    public override void OnCollisionEnter(RoundStateManager manager, Collision collision) { }
    public override void OnCollisionExit(RoundStateManager manager, Collision collision) { }

    public override void OnTriggerEnter(RoundStateManager manager, Collider other)
    {
        if (other.gameObject.CompareTag("Ball"))
        {
            manager.SwitchState(manager.scoringState);
        }
    }

    public override void OnTriggerExit(RoundStateManager manager, Collider other)
    {
        
    }
}