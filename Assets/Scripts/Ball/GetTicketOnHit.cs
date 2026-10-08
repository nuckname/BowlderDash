using UnityEngine;

public class GetTicketOnHit : MonoBehaviour
{
    [SerializeField] private bool justAVisualDontAddTickets = false;

    [Header("Audio")]
    [SerializeField] private SoundCueSO ticketPickupCue;

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;

        // Older ticket prefabs gain the same shared claim at runtime until configured in the editor.
        GroundTicketPickup pickup = other.GetComponentInParent<GroundTicketPickup>();
        if (pickup == null && other.CompareTag("GetTicketOnCollision"))
            pickup = other.gameObject.AddComponent<GroundTicketPickup>();
        if (pickup == null || !pickup.CompareTag("GetTicketOnCollision") || !pickup.TryCollect()) return;

        Vector3 position = pickup.transform.position;
        Destroy(pickup.gameObject);

        if (!justAVisualDontAddTickets) SessionData.Instance.AddTickets(1);

        // Use the persistent pool so removing the ticket cannot cut off its sound.
        if (ticketPickupCue != null && !ticketPickupCue.isUI)
            GameAudio.Instance?.Play(ticketPickupCue, position);
        FindObjectOfType<RoundStateManager>()?.SpawnPinFloatingText("+1 Ticket", position);
    }
}
