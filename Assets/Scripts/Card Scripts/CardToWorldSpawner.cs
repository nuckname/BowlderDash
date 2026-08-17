using UnityEngine;

[RequireComponent(typeof(CardMovement))]
public class CardToWorldSpawner : MonoBehaviour
{
    [Header("World Interaction")]
    // Object we want to spawn
    [SerializeField] private LayerMask worldPlacementMask; 

    private GameObject _spawnedWorldObject;
    private GridManager _grid;
    private CardMovement _cardMovement;

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
        if (_cardMovement.isPreviewingInWorld && _spawnedWorldObject != null)
        {
            // Move the WORLD object using Raycasting
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, worldPlacementMask))
            {
                HexCell nearestCell = _grid.GetNearestCell(hit.point);
                if (nearestCell != null && nearestCell.occupant == null)
                {
                    _spawnedWorldObject.transform.position = new Vector3(
                        nearestCell.center.x, 
                        nearestCell.center.y + 0.75f, 
                        nearestCell.center.z);
                }
            }
        }
    }

    private void CreatePreview()
    {
        if (_spawnedWorldObject == null && _cardMovement.cardData != null && _cardMovement.cardData.worldGameObjectPrefab != null)
        {
            _spawnedWorldObject = Instantiate(_cardMovement.cardData.worldGameObjectPrefab);
        }
    }
    private void DestroyPreview()
    {
        if (_spawnedWorldObject != null)
        {
            Destroy(_spawnedWorldObject);
            _spawnedWorldObject = null;
        }
    }

    private void TryPlaceObject()
    {
        if (_spawnedWorldObject == null)
        {
            _cardMovement.ReturnToHand();
            return;
        }

        bool successfullyPlaced = false;
        HexCell targetCell = _grid.GetNearestCell(_spawnedWorldObject.transform.position);
        
        // Attempt to reserve the cell for our spawned object
        if (targetCell != null)
        {
            successfullyPlaced = _grid.TryReserveCell(targetCell, _spawnedWorldObject.transform);
        }

        if (successfullyPlaced)
        {
            WorldObjectToCard returnScript = _spawnedWorldObject.GetComponent<WorldObjectToCard>();
            if (returnScript != null)
            {
                // Inject the pin with card data.
                returnScript.SetupCardData(_cardMovement.cardData);
            }

            _spawnedWorldObject = null;
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
        if (_spawnedWorldObject != null)
            Destroy(_spawnedWorldObject);
    }
}