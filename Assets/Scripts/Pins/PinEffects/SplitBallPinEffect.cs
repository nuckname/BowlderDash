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
        if (leftRb != null)
        {
            leftRb.linearVelocity = Quaternion.Euler(0, -splitAngle, 0) * currentVelocity;
        }

        // Spawn right angled ball
        GameObject rightBall = UnityEngine.Object.Instantiate(target.gameObject, originalPos, originalRot);
        UnityEngine.Object.Destroy(rightBall, 5f);
        Rigidbody rightRb = rightBall.GetComponent<Rigidbody>();
        if (rightRb != null)
        {
            rightRb.linearVelocity = Quaternion.Euler(0, splitAngle, 0) * currentVelocity;
        }

        // Force the original ball to continue with its velocity, counteracting any impact physics
        originalBallRb.linearVelocity = currentVelocity; 
    }
}