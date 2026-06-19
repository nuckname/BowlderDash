using UnityEngine;

[RequireComponent(typeof(CardMovement))]
public class CardWorldSpawner : MonoBehaviour
{
    [Header("World Interaction")]
    // Object we want to spawn
    [SerializeField] private GameObject worldGameObjectPrefab; 
    [SerializeField] private LayerMask worldPlacementMask; 

    private GameObject spawnedWorldObject;
    private HexGridSnapper _hexGrid;
    private CardMovement _cardMovement;

    private void Awake()
    {
        _cardMovement = GetComponent<CardMovement>();
        _hexGrid = GameObject.FindGameObjectWithTag("HexManager").GetComponent<HexGridSnapper>();

        // Subscribe to events from the movement script
        _cardMovement.OnEnterPlayArea.AddListener(CreatePreview);
        _cardMovement.OnExitPlayArea.AddListener(DestroyPreview);
        _cardMovement.OnDropInPlayArea.AddListener(TryPlaceObject);
    }

    private void Update()
    {
        if (_cardMovement.isPreviewingInWorld && spawnedWorldObject != null)
        {
            // Move the WORLD object using Raycasting
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, worldPlacementMask))
            {
                HexGridSnapper.Cell nearestCell = _hexGrid.GetNearestCell(hit.point);
                if (nearestCell != null)
                {
                    spawnedWorldObject.transform.position = nearestCell.center;
                }
            }
        }
    }

    private void CreatePreview()
    {
        if (spawnedWorldObject == null && worldGameObjectPrefab != null)
        {
            spawnedWorldObject = Instantiate(worldGameObjectPrefab);
        }
    }

    private void DestroyPreview()
    {
        if (spawnedWorldObject != null)
        {
            Destroy(spawnedWorldObject);
            spawnedWorldObject = null;
        }
    }

    private void TryPlaceObject()
    {
        if (spawnedWorldObject == null)
        {
            _cardMovement.ReturnToHand();
            return;
        }

        bool successfullyPlaced = false;
        HexGridSnapper.Cell targetCell = _hexGrid.GetNearestCell(spawnedWorldObject.transform.position);
        
        // Attempt to reserve the cell for our spawned object
        if (targetCell != null)
        {
            successfullyPlaced = _hexGrid.TryReserveCell(targetCell, spawnedWorldObject.transform);
        }

        if (successfullyPlaced)
        {
            spawnedWorldObject = null; // Unlink it so it stays in the world
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
        if (spawnedWorldObject != null)
            Destroy(spawnedWorldObject);
    }
}