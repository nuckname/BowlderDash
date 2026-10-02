using System;
using UnityEngine;

public class ForceBallReturnRotation : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<BallRampFollower>(out BallRampFollower ball))
        {
            Rigidbody rb = other.GetComponent<Rigidbody>();
            
            other.transform.rotation = Quaternion.Euler(0,90,0);
            
            rb.constraints = RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationX;
        }
    }
}
