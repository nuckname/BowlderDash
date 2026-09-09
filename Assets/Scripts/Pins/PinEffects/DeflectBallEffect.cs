using System;
using UnityEngine;

// https://share.gemini.google/4c6xNtaQMlZ8
[Serializable]
public class DeflectBallEffect : PinEffect
{
    [Tooltip("The angle in degrees to deflect the ball (e.g., 45).")]
    public float deflectAngle = 45f;
    
    [Tooltip("If true, randomly chooses left or right. If false, uses deflectToRight.")]
    public bool randomizeDirection = true;
    
    [Tooltip("If randomizeDirection is false, true = right, false = left.")]
    public bool deflectToRight = true;

    [Tooltip("Optional speed multiplier applied to the ball after deflection.")]
    public float speedMultiplier = 1f;

    public override void Execute(PinBase pinBase, Collider target = null)
    {
        if (target == null) return;

        Rigidbody targetRb = target.attachedRigidbody;
        if (targetRb == null) return;

        float angle = deflectAngle;
        
        if (randomizeDirection)
        {
            if (UnityEngine.Random.value < 0.5f)
            {
                angle = -angle;
            }
        }
        else if (!deflectToRight)
        {
            angle = -angle;
        }

        Vector3 currentVelocity = targetRb.linearVelocity;
        float currentSpeed = currentVelocity.magnitude;

        if (currentSpeed > 0.1f)
        {
            Vector3 currentDir = currentVelocity.normalized;
            Vector3 newDir = Quaternion.Euler(0f, angle, 0f) * currentDir;
            
            targetRb.linearVelocity = newDir * (currentSpeed * speedMultiplier);
        }
    }
}