using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class DeckManager : MonoBehaviour
{
    public static DeckManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private DeckDisplayUi deckDisplayUi;
    [SerializeField] private HandManager handManager;
    private ShowPhysicDeck _showPhysicDeck;
    [SerializeField] private bool showPhysicalDeck = false;

    [Header("Deck Settings")]
    public float drawInterval = 0.15f;
    [SerializeField] private List<CardDataSO> startingDeck = new List<CardDataSO>();
    [SerializeField] private List<CardDataSO> drawPile = new List<CardDataSO>();
    
    public List<CardDataSO> totalDeck = new List<CardDataSO>();
    
    [SerializeField] private List<CardDataSO> discardPile = new List<CardDataSO>();

    public List<CardDataSO> GetStartingDeck() => startingDeck;
    public List<CardDataSO> GetTotalDeck() => totalDeck;
    public List<CardDataSO> GetDrawPile() => drawPile;
    public List<CardDataSO> GetDiscardPile() => discardPile;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (showPhysicalDeck)
        {
            _showPhysicDeck = GetComponent<ShowPhysicDeck>();
        }
        
        totalDeck = new List<CardDataSO>(startingDeck);
        
        drawPile = new List<CardDataSO>(startingDeck);
        ShuffleDeck(drawPile);
    }

    public void UpdateUIDeckCounts()
    {
        if (deckDisplayUi != null)
        {
            deckDisplayUi.UpdateDeckCount(drawPile.Count);
            deckDisplayUi.UpdateDiscardCount(discardPile.Count);
        }

        if (showPhysicalDeck && _showPhysicDeck != null)
        {
            _showPhysicDeck.UpdatePhysicalPiles();
        }
        

    }

    /// <summary>
    /// Draws a specified number of cards from the current deck and adds them to the player's hand.
    /// Stops drawing if the deck becomes empty.
    /// </summary>
    public void Draw(int amount)
    {
        StartCoroutine(DrawCardsRoutine(amount));
    }

    private IEnumerator DrawCardsRoutine(int amount)
    {
        for (int i = 0; i < amount; i++)
        {
            if (drawPile.Count == 0)
            {
                ShuffleDiscardIntoDeck();
                
                // If it's still empty after shuffling, we are completely out of cards
                if (drawPile.Count == 0)
                {
                    Debug.LogWarning("Deck and discard pile are empty!");
                    break;
                }
            }

            CardDataSO drawnCard = drawPile[0];
            drawPile.RemoveAt(0);

            if (deckDisplayUi != null)
                deckDisplayUi.UpdateDeckCount(drawPile.Count);

            if (_showPhysicDeck != null)
                _showPhysicDeck.UpdatePhysicalPiles();

            handManager.AddDrawnCardToHand(drawnCard);

            yield return new WaitForSeconds(drawInterval);
        }
    }
    
    /// <summary>
    /// Finds a single instance of a card by its ID and safely removes it from the entire deck system.
    /// </summary>
    public void RemoveCardByID(string targetID)
    {
        CardDataSO cardToRemove = totalDeck.FirstOrDefault(card => card.cardID == targetID);
        
        if (cardToRemove != null)
        {
            totalDeck.Remove(cardToRemove);
            
            if (drawPile.Contains(cardToRemove))
            {
                drawPile.Remove(cardToRemove);
            }
            else if (discardPile.Contains(cardToRemove))
            {
                discardPile.Remove(cardToRemove);
            }
            
            UpdateUIDeckCounts();
        }
    }

    public void RemoveFromDiscardPile(CardDataSO discardedCard)
    {
        if (discardPile.Contains(discardedCard))
        {
            discardPile.Remove(discardedCard);
            UpdateUIDeckCounts();
        }
    }
    
    public void ShuffleDiscardIntoDeck()
    {
        if (discardPile.Count == 0) return;

        drawPile.AddRange(discardPile);
        discardPile.Clear();

        ShuffleDeck(drawPile);
        UpdateUIDeckCounts();
    }

    private void ShuffleDeck(List<CardDataSO> deckToShuffle)
    {
        for (int i = 0; i < deckToShuffle.Count; i++)
        {
            CardDataSO temp = deckToShuffle[i];
            int randomIndex = Random.Range(i, deckToShuffle.Count);
            deckToShuffle[i] = deckToShuffle[randomIndex];
            deckToShuffle[randomIndex] = temp;
        }
    }

    /// <summary>
    /// Adds a specific card data directly into the active draw deck.
    /// </summary>
    public void AddCardToDeck(CardDataSO newCard)
    {
        drawPile.Add(newCard);
        totalDeck.Add(newCard);
        
        if (deckDisplayUi != null)
            deckDisplayUi.UpdateDeckCount(drawPile.Count);

        if (_showPhysicDeck != null)
            _showPhysicDeck.UpdatePhysicalPiles();
    }
    
    public void AddToDiscardPile(CardDataSO discardedCard)
    {
        discardPile.Add(discardedCard);
        UpdateUIDeckCounts();
    }
}
