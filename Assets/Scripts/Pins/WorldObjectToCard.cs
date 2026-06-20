using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldObjectToCard : MonoBehaviour
{
    [Header("Card Spawning")]
    [Tooltip("The UI Card Prefab to spawn back into the hand.")]
    [SerializeField] private GameObject dummyCardPrefab;

    private HexGridSnapper _hexGrid;

    private void Start()
    {
        GameObject hexManager = GameObject.FindGameObjectWithTag("HexManager");
        if (hexManager != null)
        {
            _hexGrid = hexManager.GetComponent<HexGridSnapper>();
        }
    }

    public void ReturnToHand()
    {
        // Spawn the dummy card into the UI using our new centralized method
        if (HorizontalCardHolder.Instance != null && dummyCardPrefab != null)
        {
            HorizontalCardHolder.Instance.AddCardToHand(dummyCardPrefab);
        }

        // Un-reserve the Hex Cell so a new object can be placed there later
        if (_hexGrid != null)
        {
            HexGridSnapper.Cell cell = _hexGrid.GetNearestCell(transform.position);
            if (cell != null)
            {
                // will need a method in HexGridSnapper to clear the reservation.
                //? _hexGrid.FreeCell(cell);
            }
        }

        Destroy(gameObject);
    }
}
