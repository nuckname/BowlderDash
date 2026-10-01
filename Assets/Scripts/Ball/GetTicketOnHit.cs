using System;
using UnityEngine;

public class GetTicketOnHit : MonoBehaviour
{
    [SerializeField] private bool justAVisualDontAddTickets = false;
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("GetTicketOnCollision"))
        {
            if (!justAVisualDontAddTickets)
            {
                SessionData.Instance.AddTickets(1);
            }

            // :)
            FindObjectOfType<RoundStateManager>().SpawnPinFloatingText("+1 Ticket", other.transform.position);
            Destroy(other.gameObject);
        }
    }
}
