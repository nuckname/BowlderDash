using UnityEngine;
using System.Collections.Generic;

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

    [Header("Spawn Pattern (Hex Axial Coordinates)")]
    [Tooltip("Leave empty for a single object. (0,0) is the center pin. X is roughly horizontal, Y is vertical diagonal.")]
    public List<Vector2Int> spawnPattern = new List<Vector2Int> { Vector2Int.zero };
}