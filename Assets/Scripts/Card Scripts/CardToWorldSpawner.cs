using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CardMovement))]
public class CardToWorldSpawner : MonoBehaviour
{
    [Header("World Interaction")]
    // Object we want to spawn
    [SerializeField] private LayerMask worldPlacementMask; 

    private List<GameObject> _spawnedWorldObjects = new List<GameObject>();
    private GridManager _grid;
    private CardMovement _cardMovement;
    
    private HexCell _lastValidOrigin;
    [SerializeField] private float previewHeightOffset = 1.5f;

    private void Awake()
    {
        _cardMovement = GetComponent<CardMovement>();
        _grid = GameObject.FindGameObjectWithTag("HexManager").GetComponent<GridManager>();

        // Subscribe to events from the movement script
        _cardMovement.OnEnterPlayArea.AddListener(CreatePreview);
        _cardMovement.OnExitPlayArea.AddListener(DestroyPreview);
        _cardMovement.OnDropInPlayArea.AddListener(TryPlaceObject);
    }

    private void Update()
    {
        if (_cardMovement.isPreviewingInWorld && _spawnedWorldObjects.Count > 0)
        {
            // Move the WORLD object using Raycasting
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, worldPlacementMask))
            {
                HexCell originCell = _grid.GetNearestCell(hit.point);
                var pattern = GetActivePattern();

                if (originCell != null)
                {
                    bool isValidPlacement = true;

                    for (int i = 0; i < pattern.Count; i++)
                    {
                        HexCell targetCell = _grid.GetCellWithOffset(originCell, pattern[i].offset);
                        
                        // Check if any part of the pattern is off-grid or occupied
                        if (targetCell == null || targetCell.occupant != null)
                        {
                            isValidPlacement = false;
                            break;
                        }
                    }

                    // Update our anchor point only if the entire shape fits
                    if (isValidPlacement)
                    {
                        _lastValidOrigin = originCell;
                    }

                    // Draw the preview locked to the last valid cell we hovered over
                    if (_lastValidOrigin != null)
                    {
                        for (int i = 0; i < _spawnedWorldObjects.Count; i++)
                        {
                            if (i >= pattern.Count) continue;

                            Vector3 offset = _grid.GetWorldOffsetFromAxial(pattern[i].offset);
                            
                            _spawnedWorldObjects[i].SetActive(true);
                            _spawnedWorldObjects[i].transform.position = new Vector3(
                                _lastValidOrigin.center.x + offset.x, 
                                _lastValidOrigin.center.y + previewHeightOffset + offset.y, 
                                _lastValidOrigin.center.z + offset.z);
                        }
                    }
                    else
                    {
                        // Hide preview entirely if we haven't hit a valid cell yet this drag
                        for (int i = 0; i < _spawnedWorldObjects.Count; i++)
                        {
                            _spawnedWorldObjects[i].SetActive(false);
                        }
                    }
                }
            }
        }
    }

    private void CreatePreview()
    {
        _lastValidOrigin = null;

        if (_spawnedWorldObjects.Count == 0 && _cardMovement.cardData != null)
        {
            var pattern = GetActivePattern();

            for (int i = 0; i < pattern.Count; i++)
            {
                // Fallback to the old single prefab if the pattern piece didn't assign one
                GameObject prefabToInstantiate = pattern[i].prefab != null 
                    ? pattern[i].prefab 
                    : _cardMovement.cardData.worldGameObjectPrefab;

                if (prefabToInstantiate != null)
                {
                    _spawnedWorldObjects.Add(Instantiate(prefabToInstantiate));
                }
            }
        }
    }

    private void DestroyPreview()
    {
        foreach (var obj in _spawnedWorldObjects)
        {
            if (obj != null) Destroy(obj);
        }
        _spawnedWorldObjects.Clear();
    }

    private void TryPlaceObject()
    {
        if (_spawnedWorldObjects.Count == 0)
        {
            _cardMovement.ReturnToHand();
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        HexCell originCell = null;
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, worldPlacementMask))
        {
             originCell = _grid.GetNearestCell(hit.point);
        }

        if (originCell == null)
        {
            DestroyPreview();
            _cardMovement.ReturnToHand();
            return;
        }

        var pattern = GetActivePattern();
        List<HexCell> targetCells = new List<HexCell>();
        bool canPlaceAll = true;

        for (int i = 0; i < pattern.Count; i++)
        {
            HexCell target = _grid.GetCellWithOffset(originCell, pattern[i].offset);
            
            // Attempt to reserve the cell for our spawned object
            // If any cell in the pattern is invalid or occupied, the whole placement fails (Tetris rules)
            if (target == null || target.occupant != null)
            {
                canPlaceAll = false;
                break;
            }
            targetCells.Add(target);
        }

        if (canPlaceAll && targetCells.Count == _spawnedWorldObjects.Count)
        {
            List<WorldObjectToCard> groupedPins = new List<WorldObjectToCard>();
            
            // Gather all components to link them together
            for (int i = 0; i < _spawnedWorldObjects.Count; i++)
            {
                WorldObjectToCard returnScript = _spawnedWorldObjects[i].GetComponent<WorldObjectToCard>();
                if (returnScript != null)
                {
                    groupedPins.Add(returnScript);
                }
            }

            for (int i = 0; i < _spawnedWorldObjects.Count; i++)
            {
                bool reserved = _grid.TryReserveCell(targetCells[i], _spawnedWorldObjects[i].transform);
                if (reserved)
                {
                    // FIX: Force the pin down from the previewHeightOffset so it sits perfectly on the hex floor
                    _spawnedWorldObjects[i].transform.position = new Vector3(
                        targetCells[i].center.x, 
                        targetCells[i].center.y, 
                        targetCells[i].center.z
                    );

                    WorldObjectToCard returnScript = _spawnedWorldObjects[i].GetComponent<WorldObjectToCard>();
                    if (returnScript != null)
                    {
                        // Inject the pin with card data and its sibling group.
                        returnScript.SetupCardData(_cardMovement.cardData, groupedPins);
                    }
                }
            }

            _spawnedWorldObjects.Clear();
            _cardMovement.SuccessfulPlay();
        }
        else
        {
            DestroyPreview();
            _cardMovement.ReturnToHand();
        }
    }
    
    // Helper method to safely return the pattern or a fallback if the list is empty
    private List<PatternPiece> GetActivePattern()
    {
        var pattern = _cardMovement.cardData.spawnPattern;
        
        if (pattern == null || pattern.Count == 0)
        {
            return new List<PatternPiece> { 
                new PatternPiece { offset = Vector2Int.zero, prefab = _cardMovement.cardData.worldGameObjectPrefab } 
            };
        }
        
        return pattern;
    }

    private void OnDestroy()
    {
        // Clean up preview if destroyed mid-drag
        foreach (var obj in _spawnedWorldObjects)
        {
            if (obj != null) Destroy(obj);
        }
        _spawnedWorldObjects.Clear();
    }
}