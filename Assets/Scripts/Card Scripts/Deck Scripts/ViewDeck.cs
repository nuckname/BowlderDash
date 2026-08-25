using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ViewDeck : MonoBehaviour
{
    [Header("Deck Viewer Settings")]
    [SerializeField] private Transform deckViewerContainer;
    private List<GameObject> spawnedViewerCards = new List<GameObject>();
    [SerializeField] private DeckManager deckManager;
    
    private enum ActiveView { None, Draw, Total, Discard }
    private ActiveView _currentView = ActiveView.None;
    
    /// <summary>
    /// Clears the viewer and populates it with non-interactable visual representations of the given deck list.
    /// </summary>
    public void VisualizeDeck(List<CardDataSO> deckToView)
    {
        if (deckViewerContainer == null)
        {
            Debug.LogWarning("Deck Viewer Container is not assigned!");
            return;
        }

        // Clear currently displayed cards
        foreach (GameObject spawnedCard in spawnedViewerCards)
        {
            Destroy(spawnedCard);
        }
        spawnedViewerCards.Clear();

        // Spawn new visual representation for each card
        foreach (CardDataSO cardData in deckToView)
        {
            if (cardData.cardUIPrefab != null)
            {
                GameObject newVisualCard = Instantiate(cardData.cardVisualPrefab, deckViewerContainer);
                
                // Disable interactivity so they act purely as visual displays
                CardMovement movementScript = newVisualCard.GetComponent<CardMovement>();
                if (movementScript != null)
                {
                    movementScript.enabled = false;
                }

                // Inject text / sprite data
                if (movementScript != null)
                {
                    movementScript.cardData = cardData;
                }

                CardAnimator cardAnimator = newVisualCard.GetComponent<CardAnimator>();
                if (cardAnimator != null)
                {
                    cardAnimator.ApplyCardTint(cardData.cardTint);
                }

                spawnedViewerCards.Add(newVisualCard);
            }
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            ToggleView(ActiveView.Draw);
        }
        
        if (Input.GetKeyDown(KeyCode.D))
        {
            ToggleView(ActiveView.Total);
        }
        
        if (Input.GetKeyDown(KeyCode.S))
        {
            ToggleView(ActiveView.Discard);
        }
    }

    private void ToggleView(ActiveView viewToToggle)
    {
        // If we press the same key and the container is active, hide it
        if (_currentView == viewToToggle && deckViewerContainer.gameObject.activeSelf)
        {
            deckViewerContainer.gameObject.SetActive(false);
            _currentView = ActiveView.None;
        }
        // Otherwise, show it and generate the right deck
        else
        {
            deckViewerContainer.gameObject.SetActive(true);
            _currentView = viewToToggle;

            switch (_currentView)
            {
                case ActiveView.Draw:
                    ViewDrawPile();
                    break;
                case ActiveView.Total:
                    ViewTotalDeck();
                    break;
                case ActiveView.Discard:
                    ViewDiscardPile();
                    break;
            }
        }
    }

    public void ViewTotalDeck() => VisualizeDeck(deckManager.GetTotalDeck());
    
    public void ViewDrawPile() => VisualizeDeck(deckManager.GetDrawPile());
    
    public void ViewDiscardPile() => VisualizeDeck(deckManager.GetDiscardPile());
}
