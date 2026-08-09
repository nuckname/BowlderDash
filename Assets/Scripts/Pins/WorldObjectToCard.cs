using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldObjectToCard : MonoBehaviour
{
    [SerializeField] private CardDataSO cardData;
    
    private HexGrid _grid;

    private void Start()
    {
        GameObject hexManager = GameObject.FindGameObjectWithTag("HexManager");
        if (hexManager != null)
        {
            _grid = hexManager.GetComponent<HexGrid>();
        }
    }

    public void SetupCardData(CardDataSO data)
    {
        cardData = data;
    }
    
    public void ReturnToHand()
    {
        if (HandManager.Instance != null && cardData != null && cardData.cardUIPrefab != null)
        {
            HandManager.Instance.AddCardToHand(cardData);
        }
        
        Destroy(gameObject);
    }
}
