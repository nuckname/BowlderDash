using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RoundStateManager : MonoBehaviour
{
    public RoundBaseState currentState;
    public PinBase[] allActivePinsInTheFrame;
    
    [Header("Managers")]
    public CameraPanToLocation cameraPanToLocation;
    public CameraFollowBallScript cameraFollowScript;
    public BallController ballController; 

    [Header("Core Dependencies")]
    public ScoreCalculator scoreCalculator;
    public GridManager grid;
    public ShopUIManager shopUI;
    public GameObject cardHand;
    public Text instructionText;
    public InGameScoreUi inGameScoreUi;
    
    [Header("Data Objects")]
    public RoundData roundData;
    public SessionData sessionData = new SessionData();
    // Let's put all our SOs here so we only have to refernce / update one list
    public List<CardDataSO> masterCardList;
    
    [Header("Round Vars")]
    public int currentRound = 0;
    public int amountOfCardsToDrawEachTurn;

    [HideInInspector] public bool hasDrawnCardsThisTurn;
    [HideInInspector] public bool isHandDrawReady;

    private Coroutine handDrawReadinessRoutine;
    
    // States
    public RoundStartState roundStartState = new RoundStartState();
    public ThrowingBallState throwingBallState = new ThrowingBallState();
    public  PinPlacementState pinPlacementState = new PinPlacementState();
    public ShopState shopState = new ShopState();
    public ScoringState scoringState = new ScoringState();
    public  RoundOverState roundOverState = new RoundOverState();
    public SpawnBossObjectsState spawnBossObjectsState = new SpawnBossObjectsState();
    public GameOverState gameOverState = new GameOverState();
    public RoundWinState roundWinState = new RoundWinState();
    
    [SerializeField] private GameObject floatingTextPrefab;

    private void Awake()
    {
        // Bind the local reference to the Singleton instance so they share the exact same data
        sessionData = SessionData.Instance;
    }


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
        
        // Debug testing
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (currentState == roundStartState) SwitchState(pinPlacementState);
            else if (currentState == pinPlacementState) SwitchState(throwingBallState);
            else if (currentState == throwingBallState) SwitchState(scoringState);
            else if (currentState == scoringState) SwitchState(shopState);
            else if (currentState == shopState) SwitchState(roundStartState);
        }
        
        if (Input.GetKeyDown(KeyCode.T))
        {
            AddTicketsToSession(10);
            Debug.Log("Added 10 tickets!");
        }
    }

    public void StartNewGame()
    {
        sessionData.StartBrandNewSession();
        
        grid.GenerateGrid();
        
        RoundScore roundInfo = roundData.GetRoundInfo(currentRound);
        
        inGameScoreUi.UpdateRoundToBeatText(roundInfo.scoreNeededToBeat);
        inGameScoreUi.UpdateScoreText(sessionData.currentScore);
        inGameScoreUi.UpdateBallsText(sessionData.ballsThrown, roundInfo.maxBallsAllowedToThrow);
        
        SwitchState(roundStartState);
    }

    public void SwitchState(RoundBaseState newState)
    {
        if (currentState != null) currentState.ExitState(this);
        currentState = newState;
        if (currentState != null) currentState.EnterState(this);
    }

    public void ResetHandDrawState()
    {
        if (handDrawReadinessRoutine != null)
        {
            StopCoroutine(handDrawReadinessRoutine);
            handDrawReadinessRoutine = null;
        }

        hasDrawnCardsThisTurn = false;
        isHandDrawReady = false;
    }

    public void DrawHandForCurrentTurn()
    {
        if (hasDrawnCardsThisTurn) return;

        hasDrawnCardsThisTurn = true;
        isHandDrawReady = false;

        DeckManager.Instance.Draw(amountOfCardsToDrawEachTurn);
        handDrawReadinessRoutine = StartCoroutine(WaitForHandDraw());
    }

    private System.Collections.IEnumerator WaitForHandDraw()
    {
        float drawInterval = DeckManager.Instance != null ? DeckManager.Instance.drawInterval : 0f;
        yield return new WaitForSeconds(amountOfCardsToDrawEachTurn * drawInterval);

        isHandDrawReady = true;
        handDrawReadinessRoutine = null;
    }
    
    public void AddTicketsToSession(int amount)
    {
        sessionData.AddTickets(amount);
    }

    public void AddBallThrownToSession()
    {
        sessionData.AddBallThrown();
        RoundScore roundInfo = roundData.GetRoundInfo(currentRound);
        inGameScoreUi.UpdateBallsText(sessionData.ballsThrown, roundInfo.maxBallsAllowedToThrow);
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
        foreach(PinBase pin in allActivePinsInTheFrame)
        {
            pin.AllowPinToFallOver = allow;
        }
    }
    
    public void AllowPinCollisionEffects(bool allow)
    {
        if(allActivePinsInTheFrame == null) return;
        
        foreach(PinBase pin in allActivePinsInTheFrame)
        {
            pin.AllowCollisionEffects = allow;
        }
    }
    
    public void GetAllPinsInCurrentFrame(RoundStateManager manager)
    {
        PinBase[] allPins = FindObjectsOfType<PinBase>(true);
        List<PinBase> activeGameplayPins = new List<PinBase>();

        // We do this so that we dont also grab the physic pins from the deck on the left and right of our screen.
        foreach (PinBase pin in allPins)
        {
            if (pin.enabled)
            {
                activeGameplayPins.Add(pin);
            }
        }

        manager.allActivePinsInTheFrame = activeGameplayPins.ToArray();

        InjectDependenciesIntoAllPins(manager.allActivePinsInTheFrame, manager);
    }
    
    private void InjectDependenciesIntoAllPins(PinBase[] pinBases, RoundStateManager manager)
    {
        foreach (PinBase pin in pinBases)
        {
            pin.InjectDependencies(manager.scoreCalculator);
        }
    }
    
    public void SpawnFloatingText(string text, Vector3 position)
    {
        Instantiate(floatingTextPrefab, position, Quaternion.identity).GetComponent<FloatingText>().Initialize(text);
    }

    /// <summary>
    /// Clears all placed card-backed pins between rounds. Card data returns once to the
    /// discard pile and destroying each root also removes child debris/effects.
    /// </summary>
    public void ClearPlacedPinsAndReturnCardsToDeck()
    {
        foreach (WorldObjectToCard placedObject in FindObjectsByType<WorldObjectToCard>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            placedObject.ReturnToDeck();
        }

        allActivePinsInTheFrame = System.Array.Empty<PinBase>();
    }

    /// <summary>
    /// Restores every deformable lane mesh before the next round begins.
    /// </summary>
    public void ResetLaneExploders()
    {
        foreach (exploder laneExploder in FindObjectsByType<exploder>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            laneExploder.ResetMesh();
        }
    }
    public void RefreshRuntimePools() { }

    public void TurnOnCardHand(bool turnOn) => cardHand.SetActive(turnOn);
    
    public void MoveCamera(CameraLocation location) => cameraPanToLocation.MoveCameraTo(location);
}
