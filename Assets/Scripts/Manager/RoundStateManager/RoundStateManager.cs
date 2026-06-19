using UnityEngine;
using UnityEngine.UI;

public class RoundStateManager : MonoBehaviour
{
    public RoundBaseState currentState;
    public GameObject[] allActivePinsInTheFrame;
    public Camera targetCamera;
    public CameraManager cameraManager;
    
    public RoundStartState roundStart = new RoundStartState();
    public ThrowingBallState ThrowingBall = new ThrowingBallState();
    public PinPlacementState PinPlacement = new PinPlacementState();
    public ShopState Shop = new ShopState();
    public ScoringState ScoringState = new ScoringState();

    public Text instructionText;

    [Header("Core Dependencies")]
    public ScoreCalculator scoreCalculator;
    public HexGridSnapper grid;
    
    public ShopUIManager shopUI;

    [Header("Data Objects")]
    public GameStateData gameState;
    public TicketEconomyData ticketEconomy;
    
    [Header("Round Vars")]
    public int amountOfBallsThrown = 0;
    public int currentRound = 0;

    public GameObject cardHand;

    void Start()
    {
        // Initialize the UI so it knows about this State Manager
        if (shopUI != null)
        {
            shopUI.Initialize(this);
        }
        
        StartNewGame();
    }

    void Update()
    {
        if (currentState != null)
        {
            currentState.UpdateState(this);
        }
        
        // Debug testing
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (currentState == roundStart) SwitchState(PinPlacement);
            else if (currentState == PinPlacement) SwitchState(ThrowingBall);
            else if (currentState == ThrowingBall) SwitchState(ScoringState);
            else if (currentState == ScoringState) SwitchState(Shop);
            else if (currentState == Shop) SwitchState(roundStart);
        }
    }

    public void StartNewGame()
    {
        gameState.ResetState();
        ticketEconomy.ResetState();
        
        SwitchState(roundStart);
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

    public int CalculateFrameScoreTarget(int frame) { return 30; }
    
    public void AllowPinsToFallOver(bool allow)
    {
        foreach(GameObject pin in allActivePinsInTheFrame)
        {
            pin.GetComponent<Pin>().AllowPinToFallOver = allow;
        }
    }

    public void ResetLaneExploders() { }
    public void RefreshRuntimePools() { }

    public void TurnOnCardHand(bool turnOn)
    {
        cardHand.SetActive(turnOn);
    }
    
    public void MoveCamera(CameraLocation location)
    {
        cameraManager.MoveCameraTo(location);
    }
}