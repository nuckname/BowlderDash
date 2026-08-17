using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldObjectToCard : MonoBehaviour
{
    [SerializeField] private CardDataSO cardData;
    private bool hasReturnedToDeck;
    
    private HexGrid _grid;

    private void Start()
    {
        GameObject hexManager = GameObject.FindGameObjectWithTag("HexManager");
        if (hexManager != null)
        {
            _grid = hexManager.GetComponent<HexGrid>();
        }
    }

    public void SetupCardData(CardDataSO data)
    {
        cardData = data;
        hasReturnedToDeck = false;
    }
    
    public void ReturnToHand()
    {
        if (HandManager.Instance != null && cardData != null && cardData.cardUIPrefab != null)
        {
            HandManager.Instance.AddCardToHand(cardData);
        }
        
        Destroy(gameObject);
    }

    /// <summary>
    /// Returns this placed card to the discard pile so it can be shuffled and drawn again.
    /// Destroying the root also clears child debris and visual effects.
    /// </summary>
    public void ReturnToDeck()
    {
        if (hasReturnedToDeck) return;
        hasReturnedToDeck = true;

        if (cardData != null && DeckManager.Instance != null)
        {
            DeckManager.Instance.AddToDiscardPile(cardData);
        }

        Destroy(gameObject);
    }
}
