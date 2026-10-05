using UnityEngine;

public class AlleyEdgeGravity : MonoBehaviour
{
    [Header("Edge Pull Settings")]
    public float downwardForce = 5f;
    public float pitwardNudge = 1.5f;

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Pin")) return;

        Rigidbody pinRb = other.attachedRigidbody;
        if (pinRb == null) return;

        // Wake the rigidbody in case it settled right on the lip
        if (pinRb.IsSleeping())
        {
            pinRb.WakeUp();
        }

        // Direction from the pin toward the center of the pit trigger (horizontal only)
        Vector3 toPit = transform.position - pinRb.position;
        toPit.y = 0f;
        toPit.Normalize();

        // Pull down and slightly toward the pit, offset from the pin's center so it tips over the edge
        Vector3 force = (Vector3.down * downwardForce) + (toPit * pitwardNudge);
        Vector3 pullPoint = pinRb.worldCenterOfMass + (toPit * 0.1f);

        pinRb.AddForceAtPosition(force, pullPoint, ForceMode.Acceleration);
    }
}