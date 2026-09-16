using UnityEngine;

public class SpeedMeasurementTrigger : MonoBehaviour
{
    public float lowSpeedThreshold = 10f; 
    [SerializeField] private RoundStateManager roundStateManager;
    [SerializeField] private TutorialManager tutorialManager;
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ball"))
        {
            Rigidbody ballRb = other.attachedRigidbody;
            
            if (ballRb != null)
            {
                float speed = ballRb.linearVelocity.magnitude;
                Debug.Log($"[Speed Trigger] Ball passed through! Speed: {speed:F2}");

                if (speed < lowSpeedThreshold)
                {
                    if (SessionData.Instance.currentRound == 0)
                    {
                        tutorialManager.PlayTryThrowBallAgainTutorial();
                        
                        roundStateManager.SwitchState(roundStateManager.throwingBallState);
                        return;
                    }
                    
                    BallController ballController = ballRb.GetComponent<BallController>();
                    if (ballController != null)
                    {
                        ballController.Apply45DegreeDeflection();
                    }
                }
            }
        }
    }
}