using UnityEngine;

public class StopGameObjectMovementOnEnter : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        Rigidbody rb = other.GetComponent<Rigidbody>();
        
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }
        
        PinBase pin = other.GetComponent<PinBase>();
        
        if (pin != null)
        {
            pin.IsSettled = true;
        }
    }
}
