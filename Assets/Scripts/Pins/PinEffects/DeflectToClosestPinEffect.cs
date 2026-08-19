using System;
using UnityEngine;

// https://share.gemini.google/0AGkQerN0q9f
[Serializable]
public class DeflectToClosestPinEffect : PinEffect
{
    [Tooltip("Optional speed multiplier applied to the ball after deflection.")]
    public float speedMultiplier = 1f;

    public override void Execute(PinBase pinBase, Collider target = null)
    {
        if (target == null) return;

        Rigidbody targetRb = target.attachedRigidbody;
        if (targetRb == null) return;

        GameObject[] allPins = GameObject.FindGameObjectsWithTag("Pin");
        GameObject closestPin = null;
        float closestDistanceSqr = Mathf.Infinity;
        Vector3 currentPos = pinBase.transform.position;

        foreach (GameObject pinGo in allPins)
        {
            if (pinGo == pinBase.gameObject) continue;

            Vector3 directionToPin = pinGo.transform.position - currentPos;
            float dSqrToPin = directionToPin.sqrMagnitude;

            if (dSqrToPin < closestDistanceSqr)
            {
                closestDistanceSqr = dSqrToPin;
                closestPin = pinGo;
            }
        }

        if (closestPin != null)
        {
            Vector3 currentVelocity = targetRb.linearVelocity;
            float currentSpeed = currentVelocity.magnitude;

            if (currentSpeed > 0.1f)
            {
                Vector3 directionToClosest = (closestPin.transform.position - target.transform.position).normalized;
                
                // Ensure the ball stays on the same vertical plane if needed, or allow true 3D deflection.
                // directionToClosest.y = 0; 
                // directionToClosest.Normalize();
                
                targetRb.linearVelocity = directionToClosest * (currentSpeed * speedMultiplier);
            }
        }
    }
}