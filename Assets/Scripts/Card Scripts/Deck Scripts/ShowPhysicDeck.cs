using System.Collections.Generic;
using UnityEngine;

public class ShowPhysicDeck : MonoBehaviour
{
    [Header("References")]
    public RoundStateManager roundStateManager;
    public Transform drawPileSpawnPoint;
    public Transform discardPileSpawnPoint;

    [Header("Spawn Settings")]
    public Vector3 spawnRandomRange = new Vector3(0.5f, 0f, 0.5f);
    public float minSpawnDistance = 0.2f;
    public int maxSpawnAttempts = 25;
    
    private DeckManager deckManager;
    private List<GameObject> spawnedDrawPins = new List<GameObject>();
    private List<GameObject> spawnedDiscardPins = new List<GameObject>();

    private void Awake()
    {
        deckManager = GetComponent<DeckManager>();
    }

    public void UpdatePhysicalPiles()
    {
        ClearPiles();
        
        SpawnPile(deckManager.GetDrawPile(), drawPileSpawnPoint, spawnedDrawPins);
        SpawnPile(deckManager.GetDiscardPile(), discardPileSpawnPoint, spawnedDiscardPins);
    }

    // https://share.gemini.google/gsBkFqIv2rnD
    private void SpawnPile(List<CardDataSO> pileData, Transform spawnPoint, List<GameObject> trackingList)
    {
        if (spawnPoint == null) return;

        List<Vector3> usedPositions = new List<Vector3>();

        for (int i = 0; i < pileData.Count; i++)
        {
            CardDataSO cardData = pileData[i];
            
            if (cardData != null && cardData.worldGameObjectPrefab != null)
            {
                Vector3 spawnPos = spawnPoint.position;
                bool positionFound = false;

                for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
                {
                    Vector3 randomOffset = new Vector3(
                        Random.Range(-spawnRandomRange.x, spawnRandomRange.x),
                        Random.Range(-spawnRandomRange.y, spawnRandomRange.y),
                        Random.Range(-spawnRandomRange.z, spawnRandomRange.z)
                    );
                    
                    Vector3 testPos = spawnPoint.position + randomOffset;
                    bool overlapping = false;

                    foreach (Vector3 usedPos in usedPositions)
                    {
                        if (Vector3.Distance(testPos, usedPos) < minSpawnDistance)
                        {
                            overlapping = true;
                            break;
                        }
                    }

                    if (!overlapping)
                    {
                        spawnPos = testPos;
                        positionFound = true;
                        break;
                    }
                }

                if (!positionFound)
                {
                    Vector3 randomOffset = new Vector3(
                        Random.Range(-spawnRandomRange.x, spawnRandomRange.x),
                        Random.Range(-spawnRandomRange.y, spawnRandomRange.y),
                        Random.Range(-spawnRandomRange.z, spawnRandomRange.z)
                    );
                    spawnPos = spawnPoint.position + randomOffset;
                }

                usedPositions.Add(spawnPos);
                
                GameObject spawnedPin = Instantiate(cardData.worldGameObjectPrefab, spawnPos, spawnPoint.rotation);
                
                DisableGameplayComponents(spawnedPin);
                
                trackingList.Add(spawnedPin);
            }
        }
    }
    
    private void DisableGameplayComponents(GameObject pinObject)
    {
        // Maybe some pins don't have all the components, so we check if they exist before disabling them
        if (pinObject.TryGetComponent(out PinBase pinBase))
        {
            // pinBase.enabled = false;
        }

        if (pinObject.TryGetComponent(out DraggableSnap draggableSnap))
        {
            draggableSnap.enabled = false;
        }

        if (pinObject.TryGetComponent(out WorldObjectToCard worldObjectToSpawn))
        {
            worldObjectToSpawn.enabled = false;
        }
    }

    public void ClearPiles()
    {
        foreach (GameObject pin in spawnedDrawPins)
        {
            if (pin != null) Destroy(pin);
        }
        spawnedDrawPins.Clear();

        foreach (GameObject pin in spawnedDiscardPins)
        {
            if (pin != null) Destroy(pin);
        }
        spawnedDiscardPins.Clear();
    }
}