using UnityEngine;

public class SpeedMeasurementTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ball"))
        {
            Rigidbody ballRb = other.attachedRigidbody;
            
            if (ballRb != null)
            {
                float speed = ballRb.linearVelocity.magnitude;
                Debug.Log($"[Speed Trigger] Ball passed through! Speed: {speed:F2}");
            }
        }
    }
}