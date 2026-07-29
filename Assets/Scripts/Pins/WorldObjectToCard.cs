using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldObjectToCard : MonoBehaviour
{
    [Header("Card Spawning")]
    [Tooltip("The UI Card Prefab to spawn back into the hand.")]
    [SerializeField] private GameObject dummyCardPrefab;

    private HexGrid _grid;

    private void Start()
    {
        GameObject hexManager = GameObject.FindGameObjectWithTag("HexManager");
        if (hexManager != null)
        {
            _grid = hexManager.GetComponent<HexGrid>();
        }
    }

    public void ReturnToHand()
    {
        // Spawn the dummy card into the UI using our new centralized method
        if (HorizontalCardHolder.Instance != null && dummyCardPrefab != null)
        {
            HorizontalCardHolder.Instance.AddCardToHand(dummyCardPrefab);
        }
        
        Destroy(gameObject);
    }
}
