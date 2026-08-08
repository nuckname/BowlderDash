using UnityEngine;

[CreateAssetMenu(fileName = "NewCardData", menuName = "Cards/CardData")]
public class CardDataSO : ScriptableObject
{
    [Header("Card Identity")]
    public string cardID;
    public string cardName;

    [Header("Prefabs")]
    public GameObject cardUIPrefab; 
    public GameObject cardVisualPrefab; 
    public GameObject worldGameObjectPrefab; 

    [Header("Card Costs")]
    public CardPaidWithBowlingPinSO cardPaidWithBowlingPinSO;
}