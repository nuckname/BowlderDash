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

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Y)) AddPinCardToDeck(Pins.Vase);
        if (Input.GetKeyDown(KeyCode.U)) AddPinCardToDeck(Pins.Normal);
        if (Input.GetKeyDown(KeyCode.I)) AddPinCardToDeck(Pins.Small);
        if (Input.GetKeyDown(KeyCode.O)) AddPinCardToDeck(Pins.Grenda);
        if (Input.GetKeyDown(KeyCode.P)) AddPinCardToDeck(Pins.Pinata);
    }
    
    public void AddPinCardToDeck(Pins pinToAdd)
    {
        foreach (PinCardMapping mapping in cardMappings)
        {
            if (mapping.pinType == pinToAdd)
            {
                if (mapping.cardData != null)
                {
                    DeckManager.Instance.AddCardToDeck(mapping.cardData);
                }
                else
                {
                    Debug.LogWarning($"CardDataSO is missing for {pinToAdd} in the Inspector");
                }
                return;
            }
        }

        Debug.LogWarning($"No mapping found for {pinToAdd}");
    }
}