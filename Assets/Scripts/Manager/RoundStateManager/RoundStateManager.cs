using UnityEngine;
using UnityEngine.UI;

public class RoundStateManager : MonoBehaviour
{
    public RoundBaseState currentState;
    public Pin[] allActivePinsInTheFrame;
    
    [Header("Managers")]
    public CameraPanToLocation cameraPanToLocation;
    public CameraFollowBallScript cameraFollowScript;
    public BallController ballController; 

    [Header("Core Dependencies")]
    public ScoreCalculator scoreCalculator;
    public HexGridSnapper grid;
    public ShopUIManager shopUI;
    public GameObject cardHand;
    public Text instructionText;
    public UIManager uiManager;
    
    [Header("Data Objects")]
    public TicketEconomyData ticketEconomy;
    public RoundData roundData;
    
    [Header("Round Vars")]
    public int currentRound = 0;

    // States
    public RoundStartState roundStart = new RoundStartState();
    public ThrowingBallState ThrowingBall = new ThrowingBallState();
    public PinPlacementState PinPlacement = new PinPlacementState();
    public ShopState Shop = new ShopState();
    public ScoringState ScoringState = new ScoringState();

    void Start()
    {
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
        ticketEconomy.ResetState();
        SwitchState(roundStart);
    }

    public void SwitchState(RoundBaseState newState)
    {
        if (currentState != null) currentState.ExitState(this);
        currentState = newState;
        if (currentState != null) currentState.EnterState(this);
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
        foreach(Pin pin in allActivePinsInTheFrame)
        {
            pin.AllowPinToFallOver = allow;
        }
    }
    
    public void GetAllPinsInCurrentFrame(RoundStateManager manager)
    {
        manager.allActivePinsInTheFrame = FindObjectsOfType<Pin>();

        InjectDependenciesIntoAllPins(manager.allActivePinsInTheFrame, manager);
    }
    
    private void InjectDependenciesIntoAllPins(Pin[] pinBases, RoundStateManager manager)
    {
        foreach (Pin pin in pinBases)
        {
            pin.InjectDependencies(manager.scoreCalculator);
        }
    }

    public void ResetLaneExploders() { }
    public void RefreshRuntimePools() { }

    public void TurnOnCardHand(bool turnOn) => cardHand.SetActive(turnOn);
    
    public void MoveCamera(CameraLocation location) => cameraPanToLocation.MoveCameraTo(location);
}