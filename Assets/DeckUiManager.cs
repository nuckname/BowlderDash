using UnityEngine;
using TMPro;

public class DeckUiManager : MonoBehaviour
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
            deckCountText.text = currentDeckCount.ToString();
        }
    }

    public void UpdateDiscardCount(int count)
    {
        currentDiscardCount = count;
        if (discardCountText != null)
        {
            discardCountText.text = currentDiscardCount.ToString();
        }
    }

    public void IncrementDiscardCount(int amount = 1)
    {
        currentDiscardCount += amount;
        UpdateDiscardCount(currentDiscardCount);
    }
}