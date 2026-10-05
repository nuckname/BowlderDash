using System.Collections.Generic;
using UnityEngine;


//ai https://share.gemini.google/peTaimaCZwKu

public class OnLaneSquareGrid : MonoBehaviour
{
    [Header("Line Settings")]
    public int lineLength = 10;
    public float cellSize = 1f;
    public GameObject ticketPrefab;

    [Header("Animation Settings")]
    public float ticketHeightOffset = 0.5f;
    
    private int _amountToSpawn;
    private List<GameObject> spawnedGridObjects = new List<GameObject>();
    private List<Vector3> ticketOffsets = new List<Vector3>();

    [SerializeField] private AmountOfTicketsToSpawnOnLane amountToTicketsSpawnOnLane;
    
    public void GenerateGridForRound(RoundStateManager manager)
    {
        ClearGrid();
        UpdateAmountPerRound(manager);
        SpawnGrid();
    }

    private void UpdateAmountPerRound(RoundStateManager manager)
    {
        _amountToSpawn = amountToTicketsSpawnOnLane.GetAmountToSpawn(manager.sessionData.currentRound);
    }

    private void SpawnGrid()
    {
        int maxAllowedToSpawn = Mathf.Min(_amountToSpawn, lineLength);

        for (int i = 0; i < maxAllowedToSpawn; i++)
        {
            float randomX = Random.Range(0f, cellSize);
            float randomZ = Random.Range(0f, cellSize);
            Vector3 randomOffset = new Vector3(randomX, 0, randomZ);
            ticketOffsets.Add(randomOffset);
            
            Vector3 spawnPosition = transform.position + new Vector3(0, 0, i * cellSize) + randomOffset;
            GameObject spawnedObject = Instantiate(ticketPrefab, spawnPosition, Quaternion.identity, transform);
            spawnedGridObjects.Add(spawnedObject);
            spawnedObject.tag = "GetTicketOnCollision";
        }
    }

    // Copy and pasted from GridTicketManager.cs so it acts a similar way.
    private void Update()
    {
        if (!Application.isPlaying) return;

        for (int i = 0; i < spawnedGridObjects.Count; i++)
        {
            GameObject obj = spawnedGridObjects[i];
            if (obj == null) continue;

            Vector3 basePosition = transform.position + new Vector3(0, 0, i * cellSize) + ticketOffsets[i];

            float bob = Mathf.Sin((Time.time * 3f) + i) * 0.025f;
            obj.transform.position = basePosition + Vector3.up * (ticketHeightOffset + bob);
            obj.transform.Rotate(Vector3.up, 60f * Time.deltaTime, Space.World);
        }
    }
    
    public void ClearGrid()
    {
        foreach (GameObject gridObj in spawnedGridObjects)
        {
            if (gridObj != null)
            {
                Destroy(gridObj);
            }
        }
        
        spawnedGridObjects.Clear();
        ticketOffsets.Clear();
    }
}