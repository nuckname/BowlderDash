using UnityEngine;

[CreateAssetMenu(fileName = "NewCardData", menuName = "Cards/CardData")]
public class CardDataSO : ScriptableObject
{
    [Header("Card Identity")]
    public string cardID; // If we want unique cards or something

    [Header("Prefabs")]
    public GameObject cardUIPrefab; 
    public GameObject cardVisualPrefab; 
    public GameObject worldGameObjectPrefab; 
    
    [Header("Shop Settings")]
    public int baseCost = 5;
}