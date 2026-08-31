using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class RoundStateManager : MonoBehaviour
{
    public RoundBaseState currentState;
    [Header("Pins")]
    public PinBase[] allActivePinsInTheFrame;
    
    // A queue of pins that need to be scored in ScoringState.cs
    public List<PinBase> pinsToScore = new List<PinBase>();

    [Header("Strike")]
    [SerializeField, Min(0f)] private float strikeMultiplierBonus = 0.5f;
    private readonly List<PinBase> strikeTargetsForCurrentThrow = new List<PinBase>();
    private bool strikeMultiplierAppliedForCurrentThrow;
    
    [Header("Managers")]
    public CameraPanToLocation cameraPanToLocation;
    public CameraFollowBallScript cameraFollowScript;
    public BallController ballController; 

    [Header("Core Dependencies")]
    public ScoreCalculator scoreCalculator;
    public GridManager gridManager;
    public ShopUIManager shopUI;
    public GameObject cardHand;
    public Text instructionText;
    public InGameScoreUi inGameScoreUi;
    
    [Header("Data Objects")]
    public RoundData roundData;
    public SessionData sessionData = new SessionData();
    
    // Let's put all our SOs here so we only have to refernce / update one list
    // Maybe not
    public List<CardDataSO> masterCardList;
    public List<CardDataSO> shopCardsToPickFrom;
    
    [Header("Round Vars")]
    public int amountOfCardsToDrawEachTurn;

    [HideInInspector] public bool hasDrawnCardsThisTurn;
    [HideInInspector] public bool isHandDrawReady;

    private Coroutine handDrawReadinessRoutine;

    public TextMeshProUGUI gameOverText;
    
    // States
    public RoundStartState roundStartState = new RoundStartState();
    public ThrowingBallState throwingBallState = new ThrowingBallState();
    public  PinPlacementState pinPlacementState = new PinPlacementState();
    public ShopState shopState = new ShopState();
    public ScoringState scoringState = new ScoringState();
    public  RoundOverState roundOverState = new RoundOverState();
    public SpawnBossObjectsState spawnBossObjectsState = new SpawnBossObjectsState();
    public GameOverState gameOverState = new GameOverState();
    public GameCompleteState gameCompleteState = new GameCompleteState();
    public RoundWinState roundWinState = new RoundWinState();

    [Header("Floating Text Prefabs")]
    [FormerlySerializedAs("floatingTextPrefab")]
    [SerializeField] private GameObject pinFloatingTextPrefab;
    [SerializeField] private GameObject bossFloatingTextPrefab;

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
        spawnBossObjectsState.ResetRuntimeState();
        sessionData.StartBrandNewSession();
        
        gridManager.GenerateGrid();
        
        RoundScore roundInfo = roundData.GetRoundInfo(sessionData.currentRound);
        
        inGameScoreUi.UpdateScoreToBeatText(roundInfo.scoreNeededToBeat);
        inGameScoreUi.UpdateScoreText(sessionData.currentScore);
        inGameScoreUi.UpdateBallsText(sessionData.ballsThrown, roundInfo.maxBallsAllowedToThrow);
        inGameScoreUi.UpdateRoundText(0, roundData.GetMaxRoundNumber());
        
        SwitchState(roundStartState);
    }

    // The player has completed a frame/round/they meet quota. call This:
    public void ResetUiForNewRound()
    {
        sessionData.RoundOverResetValues();

        RoundScore roundInfo = roundData.GetRoundInfo(sessionData.currentRound);
        
        inGameScoreUi.UpdateScoreToBeatText(roundInfo.scoreNeededToBeat);
        inGameScoreUi.UpdateScoreText(sessionData.currentScore);
        inGameScoreUi.UpdateBallsText(sessionData.ballsThrown, roundInfo.maxBallsAllowedToThrow);
    }
    
    public void ResetUiForQuotaNotMeetThisTurn()
    {
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
        RoundScore roundInfo = roundData.GetRoundInfo(sessionData.currentRound);
        inGameScoreUi.UpdateBallsText(sessionData.ballsThrown, roundInfo.maxBallsAllowedToThrow);
    }

    public void CaptureStrikeTargetsForCurrentThrow()
    {
        strikeTargetsForCurrentThrow.Clear();
        strikeMultiplierAppliedForCurrentThrow = false;

        if (allActivePinsInTheFrame == null) return;

        foreach (PinBase pin in allActivePinsInTheFrame)
        {
            if (pin != null && !pin.IsFallen && pin.CountsTowardStrike)
            {
                strikeTargetsForCurrentThrow.Add(pin);
            }
        }
    }

    public bool TryApplyStrikeMultiplier()
    {
        if (strikeMultiplierAppliedForCurrentThrow || strikeTargetsForCurrentThrow.Count == 0) return false;

        foreach (PinBase pin in strikeTargetsForCurrentThrow)
        {
            if (pin == null || !pin.IsFallen)
            {
                return false;
            }
        }

        scoreCalculator.AddMultiplier(strikeMultiplierBonus);
        strikeMultiplierAppliedForCurrentThrow = true;
        return true;
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
    
    public void PutRealWorldPinIntoDiscardPile(PinBase pin)
    {
        WorldObjectToCard cardBackedPin = pin.GetComponent<WorldObjectToCard>();
        if (cardBackedPin != null)
        {
            cardBackedPin.ReturnToDiscardPile();
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
    
    public void DeleteAllActivePins()
    {
        if (allActivePinsInTheFrame != null)
        {
            foreach (PinBase pin in allActivePinsInTheFrame)
            {
                PutRealWorldPinIntoDiscardPile(pin);
            }

            allActivePinsInTheFrame = System.Array.Empty<PinBase>();
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
    
    public void SpawnPinFloatingText(string text, Vector3 position)
    {
        SpawnFloatingText(pinFloatingTextPrefab, text, position);
    }

    public void SpawnBossFloatingText(string text, Vector3 position)
    {
        SpawnFloatingText(bossFloatingTextPrefab, text, position);
    }

    private static void SpawnFloatingText(GameObject prefab, string text, Vector3 position)
    {
        if (prefab == null)
        {
            Debug.LogWarning("Cannot spawn floating text because its prefab is not assigned");
            return;
        }

        GameObject floatingTextObject = Instantiate(prefab, position, Quaternion.identity);
        if (floatingTextObject.TryGetComponent(out FloatingText floatingText))
        {
            floatingText.Initialize(text);
            return;
        }

        Debug.LogWarning($"Floating text prefab '{prefab.name}' is missing a FloatingText component");
        Destroy(floatingTextObject);
    }

    /// <summary>
    /// Clears all placed card-backed pins between rounds. Card data returns once to the
    /// discard pile and destroying each root also removes child debris/effects.
    /// </summary>
    public void ClearPlacedPinsAndReturnCardsToDeck()
    {
        foreach (WorldObjectToCard placedObject in FindObjectsByType<WorldObjectToCard>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            placedObject.ReturnToDiscardPile();
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
    
    public void OnThrowBallButtonClicked()
    {
        SwitchState(throwingBallState);
    }
    
    public void AddPinToActiveList(PinBase newPin)
    {
        if (newPin == null) return;

        List<PinBase> activeGameplayPins = new List<PinBase>(allActivePinsInTheFrame);
        
        if (!activeGameplayPins.Contains(newPin))
        {
            activeGameplayPins.Add(newPin);
            allActivePinsInTheFrame = activeGameplayPins.ToArray();
            
            newPin.InjectDependencies(scoreCalculator);
        }
    }

    
    public void RefreshRuntimePools() { }

    public void TurnOnCardHand(bool turnOn) => cardHand.SetActive(turnOn);
    
    public void MoveCamera(CameraLocation location) => cameraPanToLocation.MoveCameraTo(location);
}
