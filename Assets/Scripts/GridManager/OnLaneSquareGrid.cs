using System.Collections.Generic;
using UnityEngine;

//ai https://share.gemini.google/peTaimaCZwKu

public class OnLaneSquareGrid : MonoBehaviour
{
    [Header("Line Settings")]
    public int lineLength = 10;
    public float spacing = 1.5f; 
    public Vector2 spawnArea = new Vector2(1f, 1f); 
    public GameObject ticketPrefab;

    [Header("Curve Settings")]
    public List<AnimationCurve> laneCurves = new List<AnimationCurve>(); 
    public float curveMultiplier = 5f;

    [Header("Animation Settings")]
    public float ticketHeightOffset = 0.5f;
    
    [Header("Debug Tools")]
    public int debugAmountToSpawn = 10;
    
    private int _amountToSpawn;
    private List<GameObject> spawnedGridObjects = new List<GameObject>();
    private List<Vector3> ticketOffsets = new List<Vector3>();
    private AnimationCurve _currentCurve;

    [SerializeField] private AmountOfTicketsToSpawnOnLane amountToTicketsSpawnOnLane;
    
    public void GenerateGridForRound(RoundStateManager manager)
    {
        ClearGrid();
        UpdateAmountPerRound(manager);
        SelectRandomCurve();
        SpawnGrid();
    }

    private void UpdateAmountPerRound(RoundStateManager manager)
    {
        _amountToSpawn = amountToTicketsSpawnOnLane.GetAmountToSpawn(manager.sessionData.currentRound);
    }

    private void SelectRandomCurve()
    {
        if (laneCurves != null && laneCurves.Count > 0)
        {
            int randomIndex = Random.Range(0, laneCurves.Count);
            _currentCurve = laneCurves[randomIndex];
        }
        else
        {
            _currentCurve = AnimationCurve.Linear(0, 0, 1, 0);
        }
    }

    private void SpawnGrid()
    {
        int maxAllowedToSpawn = Mathf.Min(_amountToSpawn, lineLength);

        for (int i = 0; i < maxAllowedToSpawn; i++)
        {
            float randomX = Random.Range(-spawnArea.x / 2f, spawnArea.x / 2f);
            float randomZ = Random.Range(-spawnArea.y / 2f, spawnArea.y / 2f);
            Vector3 randomOffset = new Vector3(randomX, 0, randomZ);
            ticketOffsets.Add(randomOffset);
            
            float zPosition = i * spacing;
            float curveOffset = _currentCurve.Evaluate((float)i / Mathf.Max(1, maxAllowedToSpawn - 1)) * curveMultiplier;
            
            // Fixed missing operators from original copy-paste
            Vector3 spawnPosition = transform.position 
                                    + (transform.forward * zPosition) 
                                    + (transform.right * curveOffset) 
                                    + randomOffset;

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

            float zPosition = i * spacing;
            // Fixed missing operator from original copy-paste
            float curveOffset = _currentCurve.Evaluate((float)i / Mathf.Max(1, spawnedGridObjects.Count - 1)) * curveMultiplier;

            // Fixed missing operators from original copy-paste
            Vector3 basePosition = transform.position 
                                   + (transform.forward * zPosition) 
                                   + (transform.right * curveOffset) 
                                   + ticketOffsets[i];

            // Fixed missing operators from original copy-paste
            float bob = Mathf.Sin((Time.time * 3f) + i) * 0.025f;
            obj.transform.position = basePosition + Vector3.up * (ticketHeightOffset + bob);
            obj.transform.Rotate(Vector3.up, 60f * Time.deltaTime, Space.World);
        }
    }
    
    [ContextMenu("Clear Grid")]
    public void ClearGrid()
    {
        foreach (GameObject gridObj in spawnedGridObjects)
        {
            if (gridObj != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(gridObj);
                }
                else
                {
                    DestroyImmediate(gridObj);
                }
            }
        }
        
        spawnedGridObjects.Clear();
        ticketOffsets.Clear();
    }
    
    [ContextMenu("Debug Generate Grid")]
    public void DebugGenerateGrid()
    {
        ClearGrid();
        _amountToSpawn = debugAmountToSpawn;
        SelectRandomCurve();
        SpawnGrid();
    }
}