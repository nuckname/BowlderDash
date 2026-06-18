using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//https://www.youtube.com/watch?v=m2DIgUxA1UM

public class CardManager : MonoBehaviour
{
    [SerializeField] private List<PinCardData> cardData;
    [SerializeField] private PinHandCardView cardView;

    private List<Card> deck;

    private void DrawCard()
    {
        if (deck.Count == 0)
        {
            Debug.Log("Deck is empty!");
            return;
        }

        Card drawnCard = deck[0];
        deck.RemoveAt(0);
    
        //Display card
    }
}
