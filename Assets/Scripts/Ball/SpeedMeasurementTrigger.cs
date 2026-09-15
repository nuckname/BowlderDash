using UnityEngine;

public class SpeedMeasurementTrigger : MonoBehaviour
{
    public float lowSpeedThreshold = 10f; 

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