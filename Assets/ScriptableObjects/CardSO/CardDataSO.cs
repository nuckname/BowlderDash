using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PatternPiece
{
    [Tooltip("Hex Axial Coordinate offset from the origin (0,0)")]
    public Vector2Int offset;
    
    [Tooltip("The specific prefab to spawn at this offset")]
    public GameObject prefab;
}

[CreateAssetMenu(fileName = "NewCardData", menuName = "Cards/CardData")]
public class CardDataSO : ScriptableObject
{
    [Header("Card Identity")]
    public string cardID; // If we want unique cards or something
    [Tooltip("Presentation tint applied to the card art in hand, shop, and deck views.")]
    public Color cardTint = Color.white;

    [Header("Prefabs")]
    public GameObject cardUIPrefab; 
    public GameObject cardVisualPrefab; 
    [Tooltip("Fallback prefab if spawn pattern is empty")]
    public GameObject worldGameObjectPrefab; 
    
    [Header("Shop Settings")]
    public int baseCost = 5;

    [Header("Spawn Pattern (Hex Axial Coordinates)")]
    [Tooltip("Define the shape and unique prefabs for each piece.")]
    public List<PatternPiece> spawnPattern = new List<PatternPiece>();
}
