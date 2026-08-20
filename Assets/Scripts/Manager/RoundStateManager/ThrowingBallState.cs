using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ThrowingBallState : RoundBaseState, IPathDrawListener
{
    public PathDrawer pathDrawer;
    public ThrowTuningDataSO tuningData;

    private RoundStateManager currentManager;
    private bool ballHasBeenReleased;

    private ThrowingDebugUI _debugUI;
    public string debugCurrentPhase = "Initializing";

    public override void EnterState(RoundStateManager manager)
    {
        currentManager = manager;
        ballHasBeenReleased = false;
        
        manager.ballController.ResetBall();
        manager.MoveCamera(CameraLocation.BallThrowingCameraLocation);
        
        manager.TurnOnCardHand(false);
        
        manager.AllowPinCollisionEffects(true);
        
        manager.AllowPinsToFallOver(true);
        
        manager.UpdateInstructionText("Draw the ball's path! Start from the ball.");
        
        pathDrawer.Begin(this);

        debugCurrentPhase = "Waiting for player to draw path...";
        GameObject debugGo = new GameObject("ThrowingDebugUI");
        _debugUI = debugGo.AddComponent<ThrowingDebugUI>();
        _debugUI.throwingState = this;
    }

    public override void UpdateState(RoundStateManager manager)
    {
        if (Input.GetKeyDown(KeyCode.Tab) && !ballHasBeenReleased)
        {
            debugCurrentPhase = "Tab pressed, switching to Pin Placement State.";
            manager.SwitchState(manager.pinPlacementState);
            return;
        }

        if (Input.GetMouseButtonDown(1) && !ballHasBeenReleased)
        {
            debugCurrentPhase = "Right click pressed, drawing cancelled.";
            OnDrawCancelled();
        }
    }

    public override void FixedUpdateState(RoundStateManager manager) { }

    public void OnDrawCompleted(List<CurvePoint> path, float drawDuration)
    {
        if (!currentManager.isHandDrawReady)
        {
            debugCurrentPhase = "Draw completed but hand draw not ready, cancelling.";
            OnDrawCancelled();
            return;
        }

        pathDrawer.Stop();
        ballHasBeenReleased = true;
        
        float taperStartTime = tuningData.totalMinTime + Mathf.Max(0f, tuningData.powerFalloffDelay);
        float taperEndTime = Mathf.Max(taperStartTime + 0.0001f, tuningData.totalMaxTime);
        float t = (drawDuration <= taperStartTime) ? 1f : 1f - Mathf.InverseLerp(taperStartTime, taperEndTime, drawDuration);
        
        float launchSpeed = Mathf.Lerp(tuningData.forceMinSpeed, tuningData.forceMaxSpeed, t);
        
        OnInstructionUpdated("Ball Released!");
        
        // Execute throw
        currentManager.ballController.ExecuteThrow(path, launchSpeed);

        if (HandManager.Instance != null)
        {
            HandManager.Instance.DiscardHand();
        }

        // Camera
        currentManager.cameraFollowScript.StartTracking();

        debugCurrentPhase = "Ball released! Waiting to hit end trigger...";
    }

    public void OnDrawCancelled()
    {
        pathDrawer.ResetDrawing();
        currentManager.ballController.ResetBall();
        
        currentManager.cameraFollowScript.StopTracking();
        
        OnInstructionUpdated("Draw the ball's path! Start from the ball.");

        ballHasBeenReleased = false;
        debugCurrentPhase = "Draw cancelled. Waiting for player to draw again...";
    }

    public void OnInstructionUpdated(string message)
    {
        currentManager.UpdateInstructionText(message);
    }
    
    public override void ExitState(RoundStateManager manager)
    {
        pathDrawer.Stop();
        
        manager.cameraFollowScript.StopTracking();

        if (ballHasBeenReleased)
        {
            manager.AddBallThrownToSession();
        }
        
        currentManager = null;

        if (_debugUI != null)
        {
            GameObject.Destroy(_debugUI.gameObject);
        }
    }

    public override void OnCollisionEnter(RoundStateManager manager, Collision collision) { }
    public override void OnCollisionExit(RoundStateManager manager, Collision collision) { }

    public override void OnTriggerEnter(RoundStateManager manager, Collider other)
    {
        if (other.gameObject.CompareTag("Ball"))
        {
            debugCurrentPhase = "End trigger hit! Switching to Scoring State.";
            manager.SwitchState(manager.roundOverState);
        }
    }

    public override void OnTriggerExit(RoundStateManager manager, Collider other)
    {
        
    }
}

public class ThrowingDebugUI : MonoBehaviour
{
    public ThrowingBallState throwingState;

    private void OnGUI()
    {
        if (throwingState == null) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 24;
        style.normal.textColor = Color.yellow;
        style.alignment = TextAnchor.UpperCenter;
        style.fontStyle = FontStyle.Bold;

        string debugText = "--- THROWING STATE DEBUG ---\n" +
                           "Current Phase: " + throwingState.debugCurrentPhase;

        GUI.Label(new Rect(Screen.width / 2f - 300f, 20f, 600f, 200f), debugText, style);
    }
}