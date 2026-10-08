using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldObjectToCard : MonoBehaviour
{
    [SerializeField] private CardDataSO cardData;
    private bool hasReturnedToDeck;
    
    private HexGrid _grid;
    
    [SerializeField] private List<WorldObjectToCard> _groupPeers = new List<WorldObjectToCard>();
    private bool _isBeingRemoved = false;
    
    [SerializeField] private bool canBePickedUp = true;
    public bool cannotBeReturnedToDeck = false;
    
    public List<WorldObjectToCard> GroupPeers => _groupPeers;

    private void Start()
    {
        GameObject hexManager = GameObject.FindGameObjectWithTag("HexManager");
        if (hexManager != null)
        {
            _grid = hexManager.GetComponent<HexGrid>();
        }
    }
    
    public void SetupCardData(CardDataSO data, List<WorldObjectToCard> peers)
    {
        cardData = data;
        _groupPeers = peers;
        hasReturnedToDeck = false;
    }
    
    public void ReturnToHand()
    {
        if (_isBeingRemoved || !canBePickedUp || cannotBeReturnedToDeck) return;
        
        // Flag all peers so we only add one card to the hand, preventing a loop
        foreach(var peer in _groupPeers)
        {
            if (peer != null) peer._isBeingRemoved = true;
        }

        HandManager.Instance.AddCardToHand(cardData);
        
        // Destroy the entire connected shape
        foreach(var peer in _groupPeers)
        {
            if (peer != null) Destroy(peer.gameObject);
        }
    }

    /// <summary>
    /// Returns this placed card to the discard pile so it can be shuffled and drawn again.
    /// Destroying the root also clears child debris and visual effects.
    /// </summary>
    public void ReturnToDiscardPile()
    {
        if (hasReturnedToDeck || _isBeingRemoved || cannotBeReturnedToDeck) return;
        hasReturnedToDeck = true;
        
        foreach(var peer in _groupPeers)
        {
            if (peer != null) peer._isBeingRemoved = true;
        }

        if (cardData != null && DeckManager.Instance != null)
        {
            DeckManager.Instance.AddToDiscardPile(cardData);
        }

        foreach(var peer in _groupPeers)
        {
            if (peer != null) Destroy(peer.gameObject);
        }
    }
}