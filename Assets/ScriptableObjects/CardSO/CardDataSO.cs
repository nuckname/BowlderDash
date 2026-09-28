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
    public string cardID; 

    [Header("Card Presentation")]
    [Tooltip("Player-facing card name shown in card UI.")]
    public string displayName;

    [Tooltip("Player-facing explanation of what the card or pin does.")]
    [TextArea(2, 5)]
    public string description;

    [Tooltip("Pin artwork displayed in the card's image area.")]
    public Sprite cardArtwork;
    public List<Sprite> pinImages;
    public PinLayoutType desiredLayout = PinLayoutType.None;
    
    [HideInInspector]
    public Color cardTintCardRarity = Color.white;

    [Header("Pin Randomisation Overrides")]
    public bool useCustomRandomisation;
    public Vector2 zPositionRange = new Vector2(-3, -30);
    public Vector2 zRotationRange = new Vector2(-5, -5);
    public Vector2 sizeRangeX = new Vector2(0.83f, 0.83f);
    public Vector2 sizeRangeY = new Vector2(0.83f, 0.83f);
    
    [Header("Prefabs")]
    public GameObject cardUIPrefab; 
    public GameObject cardVisualPrefab; 
    [Tooltip("Fallback prefab if spawn pattern is empty")]
    public GameObject worldGameObjectPrefab; 
    
    [Header("Shop Settings")]
    [Tooltip("Base cost of is set in CardDataSO. its based on rarity.")]
    public int baseCost;

    [Header("Spawn Pattern (Hex Axial Coordinates)")]
    [Tooltip("Define the shape and unique prefabs for each piece.")]
    public List<PatternPiece> spawnPattern = new List<PatternPiece>();
    
    public CardRarity rarity = CardRarity.Common;
    
    private void OnValidate()
    {
      //  zPositionRange = new Vector2(-3, -30);
       // zRotationRange = new Vector2(-5, -5);
       // sizeRangeX = new Vector2(0.83f, 0.83f);
       // sizeRangeY = new Vector2(0.83f, 0.83f);
    
    
        cardID = name;

        switch (rarity)
        {
            case CardRarity.Common:
                baseCost = 3;
                break;
            case CardRarity.Rare:
                baseCost = 3;
                break;
            case CardRarity.Legendary:
                baseCost = 4;
                break;
        }
    }
}
