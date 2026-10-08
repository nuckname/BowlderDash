using UnityEngine;

[DisallowMultipleComponent]
public sealed class GroundTicketPickup : MonoBehaviour
{
    private bool collected;

    // All balls share this claim, including callbacks before deferred destruction completes.
    public bool TryCollect()
    {
        if (collected || !isActiveAndEnabled) return false;
        collected = true;
        return true;
    }
}
