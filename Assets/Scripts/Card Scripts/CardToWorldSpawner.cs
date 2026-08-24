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
                var pattern = _cardMovement.cardData.spawnPattern;
                
                if (pattern == null || pattern.Count == 0)
                    pattern = new List<Vector2Int> { Vector2Int.zero };

                if (originCell != null)
                {
                    bool isValidPlacement = true;

                    for (int i = 0; i < pattern.Count; i++)
                    {
                        HexCell targetCell = _grid.GetCellWithOffset(originCell, pattern[i]);
                        
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
                            Vector3 offset = _grid.GetWorldOffsetFromAxial(pattern[i]);
                            
                            _spawnedWorldObjects[i].SetActive(true);
                            _spawnedWorldObjects[i].transform.position = new Vector3(
                                _lastValidOrigin.center.x + offset.x, 
                                _lastValidOrigin.center.y + 0.75f + offset.y, 
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

        if (_spawnedWorldObjects.Count == 0 && _cardMovement.cardData != null && _cardMovement.cardData.worldGameObjectPrefab != null)
        {
            var pattern = _cardMovement.cardData.spawnPattern;
            if (pattern == null || pattern.Count == 0)
                pattern = new List<Vector2Int> { Vector2Int.zero };

            for (int i = 0; i < pattern.Count; i++)
            {
                _spawnedWorldObjects.Add(Instantiate(_cardMovement.cardData.worldGameObjectPrefab));
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

        var pattern = _cardMovement.cardData.spawnPattern;
        if (pattern == null || pattern.Count == 0)
            pattern = new List<Vector2Int> { Vector2Int.zero };

        List<HexCell> targetCells = new List<HexCell>();
        bool canPlaceAll = true;

        for (int i = 0; i < pattern.Count; i++)
        {
            HexCell target = _grid.GetCellWithOffset(originCell, pattern[i]);
            
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
            for (int i = 0; i < _spawnedWorldObjects.Count; i++)
            {
                bool reserved = _grid.TryReserveCell(targetCells[i], _spawnedWorldObjects[i].transform);
                if (reserved)
                {
                    WorldObjectToCard returnScript = _spawnedWorldObjects[i].GetComponent<WorldObjectToCard>();
                    if (returnScript != null)
                    {
                        // Inject the pin with card data.
                        returnScript.SetupCardData(_cardMovement.cardData);
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