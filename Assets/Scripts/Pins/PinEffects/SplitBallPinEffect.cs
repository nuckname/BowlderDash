using System;
using UnityEngine;

// https://share.gemini.google/oyy5yFMYdH97

[Serializable]
public class SplitBallPinEffect : PinEffect
{
    [SerializeField] private float splitAngle = 85f;
    private bool hasTriggered = false;
    
    public override void Execute(PinBase pinBase, Collider target = null)
    {
        if (hasTriggered) return;
        if (pinBase == null || target == null) return;

        // Prevent other pins from being split if they get knocked into this pin, which causes a recursive spawning loop
        if (!target.CompareTag("Ball")) return;

        hasTriggered = true;

        Rigidbody originalBallRb = target.attachedRigidbody;
        if (originalBallRb == null) return;

        Vector3 currentVelocity = originalBallRb.linearVelocity;
        Vector3 originalPos = target.transform.position;
        Quaternion originalRot = target.transform.rotation;
        
        // Spawn left angled ball
        GameObject leftBall = UnityEngine.Object.Instantiate(target.gameObject, originalPos, originalRot);
        UnityEngine.Object.Destroy(leftBall, 5f);
        Rigidbody leftRb = leftBall.GetComponent<Rigidbody>();
        Collider leftCollider = leftBall.GetComponent<Collider>();
        if (leftRb != null)
        {
            leftRb.linearVelocity = Quaternion.Euler(0, -splitAngle, 0) * currentVelocity;
        }

        // Spawn right angled ball
        GameObject rightBall = UnityEngine.Object.Instantiate(target.gameObject, originalPos, originalRot);
        UnityEngine.Object.Destroy(rightBall, 5f);
        Rigidbody rightRb = rightBall.GetComponent<Rigidbody>();
        Collider rightCollider = rightBall.GetComponent<Collider>();
        if (rightRb != null)
        {
            rightRb.linearVelocity = Quaternion.Euler(0, splitAngle, 0) * currentVelocity;
        }
        
        // Prevent the balls from colliding with each other upon spawning
        if (leftCollider != null) Physics.IgnoreCollision(target, leftCollider);
        if (rightCollider != null) Physics.IgnoreCollision(target, rightCollider);
        if (leftCollider != null && rightCollider != null) Physics.IgnoreCollision(leftCollider, rightCollider);
        

        // Force the original ball to continue with its velocity, counteracting any impact physics
        originalBallRb.linearVelocity = currentVelocity; 
    }
}