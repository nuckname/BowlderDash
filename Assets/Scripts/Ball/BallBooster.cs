using UnityEngine;

public class BallBooster : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ball"))
        {
            Rigidbody rb = other.GetComponent<Rigidbody>();
            
            rb.AddForce(rb.linearVelocity.normalized, ForceMode.Impulse);
           
        }
    }
}