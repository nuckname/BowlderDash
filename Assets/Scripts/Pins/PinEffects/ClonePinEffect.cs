using System;
using UnityEngine;

[Serializable]
public class ClonePinEffect : PinEffect
{
    [SerializeField] private float velocityMultiplier = 1f;
    [SerializeField] private Vector3 spawnOffset = new Vector3(0, 0.5f, 0);

    public override void Execute(PinBase pinBase, Collider target = null)
    {
        if (pinBase == null || target == null) return;

        GameObject clone = UnityEngine.Object.Instantiate(
            pinBase.gameObject, 
            pinBase.transform.position + spawnOffset, 
            pinBase.transform.rotation
        );
        
        // Remove the cloning effect from the newly spawned clone to prevent infinite loops
        PinBase clonePinBase = clone.GetComponent<PinBase>();
        if (clonePinBase != null)
        {
            clonePinBase.onHitEffects.RemoveAll(effect => effect is ClonePinEffect);
        }
        
        Rigidbody cloneRb = clone.GetComponent<Rigidbody>();
        Rigidbody targetRb = target.attachedRigidbody;

        if (cloneRb != null && targetRb != null)
        {
            // Add velocity in the direction the pin base was hit
            cloneRb.linearVelocity = targetRb.linearVelocity * velocityMultiplier;
        }
    }
}