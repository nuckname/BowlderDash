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
    
    private HexCell _currentHoveredCell;
    [SerializeField] private float previewHeightOffset = 1.5f;

    [Header("Invalid Placement Visualization")]
    [SerializeField] private Material invalidPlacementMaterial;
    private bool _isValidPlacement = false;
    private List<bool> _pinValidityStates = new List<bool>();
    private Dictionary<Renderer, Material[]> _originalMaterials = new Dictionary<Renderer, Material[]>();

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
                    _currentHoveredCell = originCell;
                    _isValidPlacement = true; // Assume true until a specific pin fails

                    for (int i = 0; i < pattern.Count; i++)
                    {
                        if (i >= _spawnedWorldObjects.Count) continue;

                        HexCell targetCell = _grid.GetCellWithOffset(originCell, pattern[i].offset);
                        
                        // Check if THIS specific pattern piece is off-grid or occupied
                        bool isThisPinValid = targetCell != null && targetCell.occupant == null;
                        
                        if (!isThisPinValid)
                        {
                            _isValidPlacement = false; // Block overall placement if any pin fails
                        }

                        Vector3 offset = _grid.GetWorldOffsetFromAxial(pattern[i].offset);
                        
                        _spawnedWorldObjects[i].SetActive(true);
                        _spawnedWorldObjects[i].transform.position = new Vector3(
                            _currentHoveredCell.center.x + offset.x, 
                            _currentHoveredCell.center.y + previewHeightOffset + offset.y, 
                            _currentHoveredCell.center.z + offset.z);

                        // Only update visuals if the validity state for this specific pin changed
                        if (_pinValidityStates.Count > i && _pinValidityStates[i] != isThisPinValid)
                        {
                            UpdatePreviewVisuals(_spawnedWorldObjects[i], isThisPinValid);
                            _pinValidityStates[i] = isThisPinValid;
                        }
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

    private void CreatePreview()
    {
        _currentHoveredCell = null;
        _originalMaterials.Clear();
        _pinValidityStates.Clear();

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
                    GameObject previewObject = Instantiate(prefabToInstantiate);
                    if (previewObject.TryGetComponent(out PinBase previewPin))
                    {
                        previewPin.SetImpactAudioRegistrationBlocked(true);
                        previewPin.EnterPlacementPhysicsMode();
                    }

                    foreach (var renderer in previewObject.GetComponentsInChildren<Renderer>())
                    {
                        _originalMaterials[renderer] = renderer.materials;
                    }

                    _spawnedWorldObjects.Add(previewObject);
                    _pinValidityStates.Add(true); // Default to true when spawned
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
        _originalMaterials.Clear();
        _pinValidityStates.Clear();
    }

    private void TryPlaceObject()
    {
        if (_spawnedWorldObjects.Count == 0 || _currentHoveredCell == null || !_isValidPlacement)
        {
            return;
        }

        var pattern = GetActivePattern();
        List<HexCell> targetCells = new List<HexCell>();

        for (int i = 0; i < pattern.Count; i++)
        {
            HexCell target = _grid.GetCellWithOffset(_currentHoveredCell, pattern[i].offset);
            targetCells.Add(target);
        }

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
                UpdatePreviewVisuals(_spawnedWorldObjects[i], true);

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
                
                PinBase pin = _spawnedWorldObjects[i].GetComponent<PinBase>();
                if (pin != null)
                {
                    pin.SetImpactAudioRegistrationBlocked(false);
                    pin.OnPlaced();
                }
            }
        }

        _spawnedWorldObjects.Clear();
        _originalMaterials.Clear();
        _pinValidityStates.Clear();
        _cardMovement.SuccessfulPlay();
    }

    private void UpdatePreviewVisuals(GameObject previewObject, bool isValid)
    {
        var colliders = previewObject.GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            col.enabled = isValid;
        }

        var renderers = previewObject.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            if (isValid)
            {
                if (_originalMaterials.TryGetValue(renderer, out Material[] originalMats))
                {
                    renderer.materials = originalMats;
                }
            }
            else if (invalidPlacementMaterial != null)
            {
                Material[] invalidMats = new Material[renderer.materials.Length];
                for (int j = 0; j < invalidMats.Length; j++)
                {
                    invalidMats[j] = invalidPlacementMaterial;
                }
                renderer.materials = invalidMats;
            }
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
        _originalMaterials.Clear();
        _pinValidityStates.Clear();
    }
}
