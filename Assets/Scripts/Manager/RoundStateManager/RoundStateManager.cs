using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RoundStateManager : MonoBehaviour
{
    public RoundBaseState currentState;
    [Header("Pins")]
    public PinBase[] allActivePinsInTheFrame;
    
    // A queue of pins that need to be scored in ScoringState.cs
    public List<PinBase> pinsToScore = new List<PinBase>();

    [Header("Strike")]
    private float _strikeMultiplierBonus = 0.25f;
    [SerializeField] private GameObject strikeFloatingTextPrefab;
    [SerializeField] private Transform strikeFloatingTextSpawnPoint;
    private readonly List<PinBase> strikeTargetsForCurrentThrow = new List<PinBase>();
    private bool strikeMultiplierAppliedForCurrentThrow;
    
    [Header("Managers")]
    public CameraPanToLocation cameraPanToLocation;
    public CameraFollowBallScript cameraFollowScript;
    public BallController ballController; 
    public BallRampSpawner ballRampSpawner;
    
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

    public OnLaneSquareGrid onLaneSquareGrid;
    
    public List<CardDataSO> shopCardsToPickFrom;
    
    public AmountOfTicketsToSpawnOnLane amountOfTicketsToSpawnOnLane;
    
    [Header("Round Vars")]
    public int amountOfCardsToDrawEachTurn;

    [HideInInspector] public bool hasDrawnCardsThisTurn;
    [HideInInspector] public bool isHandDrawReady;

    private Coroutine handDrawReadinessRoutine;

    public TextMeshProUGUI gameOverText;

    [SerializeField] private GameObject clickButtonToGetBallGameObject;
    
    [Header("Leaderboard")]
    [SerializeField] private LeaderboardScreen leaderboardScreenPrefab;
    private LeaderboardScreen leaderboardScreen;
    private bool leaderboardPaused;
    private float timeScaleBeforeLeaderboard;
    private bool restartingFromLeaderboard;
    
    public TutorialManager tutorialManager;

    public GameObject postProcessingEffects;

    [SerializeField] private int howManyRoundsdoWeWantToClickTheBallFor = 2;
    
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
    public MainMenuState mainMenuState = new MainMenuState();
    private bool startedFromMenu;
    
    public bool IsPinPlacementActive => currentState == pinPlacementState;
    public bool IsLeaderboardActive => currentState is LeaderboardState;
    public bool HasSelectedBallForCurrentAttempt { get; private set; }

    [Header("Floating Text Prefabs")]
    [FormerlySerializedAs("floatingTextPrefab")]
    [SerializeField] private GameObject pinFloatingTextPrefab;
    [SerializeField] private GameObject bossFloatingTextPrefab;

    [Header("In World")] 
    [SerializeField] private GameObject maxLineGameObject;
    private void Awake()
    {
        // Bind the local reference to the Singleton instance so they share the exact same data
        sessionData = SessionData.Instance;
        if (ballController != null) ballController.Released += OnPrimaryBallReleased;
        PhysicsImpactAudio.SetWorldEligible(false);
    }

    private void Start()
    {
        GameAudio.Instance?.BeginMusicSession(gameObject.scene);
        SwitchState(mainMenuState);
    }

    void Update()
    {
        if (currentState != null)
        {
            currentState.UpdateState(this);
        }
        
    }

    private void StartNewGame()
    {
        PhysicsImpactAudio.SetWorldEligible(false);
        GameAudio.Instance?.RestoreLeaderboardPause(this);
        if (IsLeaderboardActive)
        {
            RestartFromLeaderboard();
            return;
        }

        spawnBossObjectsState.ResetRuntimeState();
        sessionData.StartBrandNewSession();
        
        shopUI.ToggleShop(false);
        
        gridManager.GenerateGrid();
        
        HasSelectedBallForCurrentAttempt = false;
        throwingBallState.cannotThrowBall = true;

        // must click on the ball ramp 
   
        TurnOnBallMesh(false);
        
        ballRampSpawner.SpawnBallsForRound(SessionData.Instance.currentRound);
        
        UpdateInGameScoreUi();
        TurnOnButtonToClickToGetBall(false);

        SwitchState(roundStartState);
    }

    private void UpdateInGameScoreUi()
    {
        RoundScore roundScore = roundData.GetRoundInfo(sessionData.currentRound);
        
        inGameScoreUi.UpdateScoreToBeatText(roundScore.scoreNeededToBeat);
        inGameScoreUi.UpdateScoreText(sessionData.currentScore);
        inGameScoreUi.UpdateBallsText(sessionData.ballsThrown, GetMaxBallsForCurrentRound());
        inGameScoreUi.UpdateRoundText(0, roundData.GetMaxRoundNumber());
    }

    // The player has completed a frame/round/they meet quota. call This:
    public void ResetUiForNewRound()
    {
        sessionData.RoundOverResetValues();

        RoundScore roundInfo = roundData.GetRoundInfo(sessionData.currentRound);
        
        inGameScoreUi.UpdateScoreToBeatText(roundInfo.scoreNeededToBeat);
        inGameScoreUi.UpdateScoreText(sessionData.currentScore);
        inGameScoreUi.UpdateBallsText(sessionData.ballsThrown, GetMaxBallsForCurrentRound());
    }
    
    public void SwitchState(RoundBaseState newState)
    {
        // Ignore late gameplay callbacks and repeated end-game transitions.
        if (IsLeaderboardActive) return;
        // The scene starts at the menu; gameplay callbacks cannot bypass Start.
        if (!startedFromMenu && newState != mainMenuState) return;
        if (newState != roundOverState && newState != scoringState)
            PhysicsImpactAudio.SetWorldEligible(false);
        if (currentState != null) currentState.ExitState(this);
        currentState = newState;
        // Request before EnterState, which can synchronously switch to another state.
        MusicPhase? musicPhase = GetMusicPhase(newState);
        if (musicPhase.HasValue)
            GameAudio.Instance?.Music?.RequestPhase(musicPhase.Value);
        if (currentState != null) currentState.EnterState(this);
    }

    private MusicPhase? GetMusicPhase(RoundBaseState state)
    {
        if (state == mainMenuState) return MusicPhase.MainMenu;

        // Keep ambience from round setup and pin placement through bowling and settlement.
        if (state == roundStartState || state == pinPlacementState || state == spawnBossObjectsState ||
            state == throwingBallState || state == roundOverState)
            return MusicPhase.ActiveBowling;

        // Scoring brings in the gameplay loop and keeps it through the win bridge to Shop.
        //if (state == scoringState || state == roundWinState) return MusicPhase.Gameplay;

        if (state == shopState) return MusicPhase.Shop;

        // The Gameplay loop continues across both terminal bridges and the paused leaderboard.
        if (state == gameOverState || state == gameCompleteState || state is LeaderboardState)
            return MusicPhase.Gameplay;

        return null;
    }
    
    public void PinPlacementVisuals(bool turnOn)
    {
        gridManager.hexGridVisuals.SetVisualsActive(turnOn);
        maxLineGameObject.SetActive(!turnOn);
    }

    public void ShowLeaderboardScreen(bool victory)
    {
        if (!IsLeaderboardActive || leaderboardScreen != null) return;

        var result = new LeaderboardEntry
        {
            totalScore = sessionData.totalScore,
            highestRoundScore = sessionData.highestRoundScore,
            highestRoundReached = sessionData.highestRoundReached
        };

        StopAllCoroutines();
        cameraFollowScript?.StopTracking();
        cameraPanToLocation?.CancelActiveMove();
        tutorialManager?.StopTutorial();
        inGameScoreUi?.StopAllFlashing();
        shopUI?.ToggleShop(false);
        if (cardHand != null) cardHand.SetActive(false);
        if (gameOverText != null) gameOverText.gameObject.SetActive(false);
        SetPinPlacementInteraction(false);

        // These components read keyboard/mouse input independently of the round state.
        DisableLeaderboardGameplayControls<HandInteractionManager>();
        DisableLeaderboardGameplayControls<ViewDeck>();
        DisableLeaderboardGameplayControls<CardToWorldSpawner>();
        DisableLeaderboardGameplayControls<HexGridExpander>();
        DisableLeaderboardGameplayControls<CardMovement>();

        timeScaleBeforeLeaderboard = Time.timeScale;
        GameAudio.Instance?.PauseForLeaderboard(this);
        leaderboardPaused = true;
        Time.timeScale = 0f;

        if (leaderboardScreenPrefab == null)
        {
            Debug.LogError("Assign the LeaderboardScreen prefab on RoundStateManager to display the finished game.");
            return;
        }

        leaderboardScreen = Instantiate(leaderboardScreenPrefab);
        SceneManager.MoveGameObjectToScene(leaderboardScreen.gameObject, gameObject.scene);
        leaderboardScreen.Show(result, victory, RestartFromLeaderboard, RestartFromLeaderboard);
    }

    private void DisableLeaderboardGameplayControls<T>() where T : MonoBehaviour
    {
        foreach (var control in FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (control.gameObject.scene != gameObject.scene) continue;
            control.StopAllCoroutines();
            control.enabled = false;
        }
    }

    private void RestartFromLeaderboard()
    {
        if (restartingFromLeaderboard) return;
        restartingFromLeaderboard = true;
        RestoreLeaderboardTimeScale();
        SceneManager.LoadScene(gameObject.scene.buildIndex);
    }

    private void RestoreLeaderboardTimeScale()
    {
        GameAudio.Instance?.RestoreLeaderboardPause(this);
        if (!leaderboardPaused) return;
        Time.timeScale = timeScaleBeforeLeaderboard;
        leaderboardPaused = false;
    }

    private void OnDestroy()
    {
        if (ballController != null) ballController.Released -= OnPrimaryBallReleased;
        PhysicsImpactAudio.SetWorldEligible(false);
        RestoreLeaderboardTimeScale();
    }

    private void OnPrimaryBallReleased(float launchSpeed)
    {
        if (currentState == throwingBallState) PhysicsImpactAudio.SetWorldEligible(true);
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
        inGameScoreUi.UpdateBallsText(sessionData.ballsThrown, GetMaxBallsForCurrentRound());
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

        scoreCalculator.AddMultiplier(_strikeMultiplierBonus);
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
       // GUI.color = Color.green;
      //  string stateName = currentState != null ? currentState.GetType().Name : "NULL";
       // GUI.Label(new Rect(300, 10, 300, 20), $"Current State: {stateName}");
    }

    public void AllowPinsToFallOver(bool allow)
    {
        foreach(PinBase pin in allActivePinsInTheFrame)
        {
            pin.AllowPinToFallOver = allow;
        }
    }
    
    public void PutRealWorldPinIntoDiscardPile(PinBase pin)
    {
        if (pin == null) return;
        
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

    public void SetPinPlacementInteraction(bool allow)
    {
        if (allActivePinsInTheFrame == null) return;

        foreach (PinBase pin in allActivePinsInTheFrame)
        {
            if (pin != null && pin.TryGetComponent(out DraggableSnap draggableSnap))
            {
                draggableSnap.SetInteractionEnabled(allow);
            }
        }
    }

    public void SetPinPlacementPhysics(bool placementMode)
    {
        if (allActivePinsInTheFrame == null) return;

        if (!placementMode)
        {
            Physics.SyncTransforms();
        }

        foreach (PinBase pin in allActivePinsInTheFrame)
        {
            if (pin == null) continue;

            if (placementMode)
            {
                pin.EnterPlacementPhysicsMode();
            }
            else
            {
                pin.EnterThrowPhysicsMode();
            }
        }
    }
    
    public void DeleteAllActivePins()
    {
        if (allActivePinsInTheFrame != null)
        {
            foreach (PinBase pin in allActivePinsInTheFrame)
            {
                if (pin != null)
                {
                    PutRealWorldPinIntoDiscardPile(pin);
                }
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
            pin.SetImpactAudioEligible(false);
            if (pin.enabled)
            {
                activeGameplayPins.Add(pin);
                if (pin.CanRegisterImpactAudio) pin.SetImpactAudioEligible(true);
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
    
    public int GetMaxBallsForCurrentRound()
    {
        RoundScore roundInfo = roundData.GetRoundInfo(sessionData.currentRound);
        return roundInfo.maxBallsAllowedToThrow;
    }
    
    public void SpawnPinFloatingText(string text, Vector3 position)
    {
        SpawnFloatingText(pinFloatingTextPrefab, text, position);
    }

    public void SpawnBossFloatingText(string text, Vector3 position)
    {
        SpawnFloatingText(bossFloatingTextPrefab, text, position);
    }

    public void SpawnStrikeFloatingText()
    {
        if (strikeFloatingTextSpawnPoint == null)
        {
            Debug.LogWarning("Cannot spawn strike floating text because its spawn point is not assigned");
            return;
        }

        string text = $"STRIKE!\n+{_strikeMultiplierBonus:0.##}x";
        SpawnFloatingText(strikeFloatingTextPrefab, text, strikeFloatingTextSpawnPoint.position);
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
        if (!IsPinPlacementActive) return;

        if (!isHandDrawReady)
        {
            UpdateInstructionText("Drawing cards...");
            return;
        }

        SwitchState(throwingBallState);
    }

    public bool TryRethrowCurrentBall(float cameraSpeed, float zOffset = 0f)
    {
        if (currentState != throwingBallState || !HasSelectedBallForCurrentAttempt ||
            ballController == null || !ballController.IsLaunched)
        {
            return false;
        }

        nextCameraSpeedOverride = cameraSpeed;
        throwingBallState.PrepareForRethrow(zOffset);
        SwitchState(throwingBallState);
        return true;
    }

    public void ClearSelectedBallForCurrentAttempt()
    {
        HasSelectedBallForCurrentAttempt = false;
    }
    
    public void OnPinPlacementButtonClicked()
    {
        SwitchState(pinPlacementState);
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
            newPin.SetImpactAudioEligible(true);
        }
    }

    
    // Turns off all children of the gameobjet reference, not the parent itself.
    public void TurnOnCardHand(bool turnOn)
    {
        // Turn off image to lazy to get a reference in editor
        cardHand.GetComponent<Image>().enabled = turnOn;
        
        foreach (Transform child in cardHand.transform)
        {
            child.gameObject.SetActive(turnOn);
        }
    }
    
    [HideInInspector] public float nextCameraSpeedOverride = -1f;
    public void MoveCamera(CameraLocation location)
    {
        cameraFollowScript.StopTracking();
        cameraPanToLocation.MoveCameraTo(location, nextCameraSpeedOverride);
        
        // Reset the override after using it so other camera pans return to normal speed
        nextCameraSpeedOverride = -1f;
    }

    public void StartBallCameraTracking()
    {
        if (!cameraPanToLocation.TryGetCameraTarget(CameraLocation.BallThrowingCameraLocation, out Transform trackingStartTransform))
        {
            Debug.LogWarning("Cannot start ball camera tracking because BallThrowingCameraLocation has no target.");
            return;
        }

        cameraPanToLocation.CancelActiveMove();
        cameraFollowScript.StartTracking(trackingStartTransform);
    }

    // Not sure how the ball is being turned off, easy way to do this.
    public void TurnOnBallMesh(bool turnOn)
    {
        GameObject.FindGameObjectWithTag("Ball").GetComponent<MeshRenderer>().enabled = turnOn;
    }

    public bool CheckKeepClickingTheBall()
    {
        if (sessionData.currentRound <= howManyRoundsdoWeWantToClickTheBallFor)
        {
            return true;   
        }
        
        return false;
        
    }

    // Called via button
    public void ClickButtonToGetBall()
    {
        if (currentState != throwingBallState || HasSelectedBallForCurrentAttempt) return;
        if (ballRampSpawner.ballsInRamp.Count == 0 || ballRampSpawner.ballsInRamp[0] == null) return;

        if (tutorialManager.enableTutorial)
        {
            tutorialManager.PlayThrowTutorial();
        }
        
        Material ballRampColour = ballRampSpawner.ballsInRamp[0].GetComponent<BallRampFollower>().getCurrentBallMaterial;
        // Really do need to cache the ball gameobject
        // Set the ball we throw to the colour we just clicked
        GameObject.FindGameObjectWithTag("Ball").GetComponent<MeshRenderer>().material = ballRampColour;
        
        Destroy(ballRampSpawner.ballsInRamp[0]); 
        
        ballRampSpawner.ballsInRamp.RemoveAt(0);

        HasSelectedBallForCurrentAttempt = true;
        throwingBallState.RefreshBallSelection(this);
    }

    public void TurnOnButtonToClickToGetBall(bool turnOn)
    {
        clickButtonToGetBallGameObject.SetActive(turnOn);
    }
    
    public void ClickSettingsButton()
    {
        if (currentState != mainMenuState) return;
        MoveCamera(CameraLocation.SettingCameraLocation);
    }
    
    public void ClickShowMainMenu()
    {
        if (currentState != mainMenuState) return;
        MoveCamera(CameraLocation.MainMenuCameraLocation);
    }
    
    public void ClickPlayButton()
    {
        if (startedFromMenu || currentState != mainMenuState) return;
        startedFromMenu = true;
        StartNewGame();
    }
    
    public void ClickLeaderboardsButton()
    {
        if (currentState != mainMenuState) return;
        MoveCamera(CameraLocation.LeaderboardCameraLocation);
    }
    public void OnSfxVolumeChanged(float value)
    {
        var audio = GameAudio.Instance;
        if (audio == null) return;
        audio.SetGameplayVolume(value);
        audio.SetUIVolume(value);
    }

    public void OnMusicVolumeChanged(float value)
    {
        GameAudio.Instance?.SetMusicVolume(value);
    }
}
