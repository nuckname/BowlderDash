using System.Collections.Generic;
using UnityEngine;

public class OnLaneSquareGrid : MonoBehaviour
{
    [Header("Grid Settings")]
    public int columns = 5;
    public int rows = 5;
    public float cellSize = 1f;
    public GameObject prefabToSpawn;

    [Header("Animation Settings")]
    public float ticketHeightOffset = 0.5f;
    
    private int amountToSpawn = 0;
    private List<GameObject> spawnedGridObjects = new List<GameObject>();

    public void GenerateGridForRound(RoundStateManager manager)
    {
        ClearGrid();
        UpdateAmountPerRound(manager);
        SpawnGrid();
    }

    private void UpdateAmountPerRound(RoundStateManager manager)
    {
        switch (manager.sessionData.currentRound)
        {
            case 0:
                amountToSpawn = 1;
                break;
            case 1:
                amountToSpawn = 1;
                break;
            case 2:
                amountToSpawn = 2;
                break;
            case 3:
                amountToSpawn = 3;
                break;
            case 4:
                amountToSpawn = 3;
                break;
            case 5:
                amountToSpawn = 3;
                break;
            case 6:
                amountToSpawn = 4;
                break;
            case 7:
                amountToSpawn = 4;
                break;
            case 8:
                amountToSpawn = 4;
                break;
            default:
                amountToSpawn = 1;
                break;
        }
    }

    private void SpawnGrid()
    {
        int maxAllowedToSpawn = Mathf.Min(amountToSpawn, columns * rows);

        for (int i = 0; i < maxAllowedToSpawn; i++)
        {
            int x = i % columns;
            int z = i / columns;
            
            Vector3 spawnPosition = transform.position + new Vector3(x * cellSize, 0, z * cellSize);
            GameObject spawnedObject = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity, transform);
            spawnedGridObjects.Add(spawnedObject);
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

            int x = i % columns;
            int z = i / columns;
            Vector3 basePosition = transform.position + new Vector3(x * cellSize, 0, z * cellSize);

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
    }
}