using System.Collections.Generic;
using UnityEngine;
[System.Serializable]
public struct PinCardMapping
{
    public Pins pinType;
    public CardDataSO cardData;
}

public class DeckModifierManager : MonoBehaviour
{
    [Header("Card Database")]
    [Tooltip("Map the enums to their specific CardDataSO here")]
    [SerializeField] private List<PinCardMapping> cardMappings = new List<PinCardMapping>();

    public void AddPinCardToDeck(Pins pinToAdd)
    {
        foreach (PinCardMapping mapping in cardMappings)
        {
            if (mapping.pinType == pinToAdd)
            {
                if (mapping.cardData != null)
                {
                    CardHandController.Instance.AddCardToDeck(mapping.cardData);
                    Debug.Log($"Added {mapping.cardData.cardName} to the deck.");
                }
                else
                {
                    Debug.LogWarning($"CardDataSO is missing for {pinToAdd} in the Inspector!");
                }
                return;
            }
        }

        Debug.LogWarning($"No mapping found for {pinToAdd}");
    }
}