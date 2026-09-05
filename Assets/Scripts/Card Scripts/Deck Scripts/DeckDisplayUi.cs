using UnityEngine;
using TMPro;

public class DeckDisplayUi : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI deckCountText;
    [SerializeField] private TextMeshProUGUI discardCountText;

    private int currentDeckCount = 0;
    private int currentDiscardCount = 0;

    public void UpdateDeckCount(int count)
    {
        currentDeckCount = count;
        if (deckCountText != null)
        {
            deckCountText.text = "Draw\nPile:\n" + currentDeckCount;
        }
    }

    public void UpdateDiscardCount(int count)
    {
        currentDiscardCount = count;
        if (discardCountText != null)
        {
            discardCountText.text = "Discard\nPile:\n" + currentDiscardCount;
        }
    }

    public void IncrementDiscardCount(int amount = 1)
    {
        currentDiscardCount += amount;
        UpdateDiscardCount(currentDiscardCount);
    }
}