using UnityEngine;
using UnityEngine.UI;

public class RoundStateManager : MonoBehaviour
{
    public RoundBaseState currentState;

    public GameObject[] allActivePinsInTheFrame;
    
    public Camera targetCamera;
    
    public FrameStartState FrameStart = new FrameStartState();
    public ThrowingBallState ThrowingBall = new ThrowingBallState();
    public PinPlacementState PinPlacement = new PinPlacementState();
    public ShopState Shop = new ShopState();
    public ScoringState ScoringState = new ScoringState();

    public Text instructionText;

    [Header("Core Dependencies")]
    // Temp, refactor these later
    public ScoreCalculator scoreCalculator;
    public GameUIController uiController;
    public PinHandInventory pinHandInventory;
    public PinShopController shopController;
    public HexGridSnapper grid;

    [Header("Data Objects")]
    public GameStateData gameState;
    public TicketEconomyData ticketEconomy;

    void Start()
    {
        StartNewGame();
    }

    void Update()
    {
        if (currentState != null)
        {
            currentState.UpdateState(this);
        }
    }

    public void StartNewGame()
    {
        // Reset the data in the Scriptable Objects
        gameState.ResetState();
        ticketEconomy.ResetState();
        
        pinHandInventory?.InitializeOwnedInventoryForNewGame();
        pinHandInventory?.ClearSavedLayout();
        shopController?.HideShop();
        
        SwitchState(FrameStart);
    }

    public void SwitchState(RoundBaseState newState)
    {
        if (currentState != null)
        {
            currentState.ExitState(this);
        }

        currentState = newState;

        if (currentState != null)
        {
            currentState.EnterState(this);
        }
    }

    void OnCollisionEnter(Collision collision) => currentState?.OnCollisionEnter(this, collision);
    void OnCollisionExit(Collision collision) => currentState?.OnCollisionExit(this, collision);
    public void OnTriggerEnter(Collider other) => currentState?.OnTriggerEnter(this, other);
    void OnTriggerExit(Collider other) => currentState?.OnTriggerExit(this, other);
    
    public void UpdateInstructionText(string newText)
    {
        if (instructionText != null) instructionText.text = newText;
    }
    
    void OnGUI()
    {
        GUI.color = Color.green;
        string stateName = currentState != null ? currentState.GetType().Name : "NULL";
        GUI.Label(new Rect(300, 10, 300, 20), $"Current State: {stateName}");
    }

    public int CalculateFrameScoreTarget(int frame)
    {
        // Placeholder
        return 30;
    }
    
    public void AllowPinsToFallOver(bool allow)
    {
        foreach(GameObject pin in allActivePinsInTheFrame)
        {
            pin.GetComponent<Pin>().allowPinToFallOver = allow;
        }
    }

    public void ResetLaneExploders() { }
    public void RefreshRuntimePools() { }
}