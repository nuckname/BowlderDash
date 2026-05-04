using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum GameState { FrameSetup, PinPlacement, PlayerReady, Rolling, CalculatingScore, EndFrame, GameOver }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Core Dependencies")]
    public DrawBowlManager throwController;
    public ScoreCalculator scoreCalculator;
    public GameUIController uiController;
    public BowlingModeSwitcher modeSwitcher;
    
    [Header("Game State")]
    public int maxFrames = 10;
    public int currentFrame = 1;
    public int throwsThisFrame = 0;
    public int totalScore = 0;
    
    // Internal state tracking
    private GameState _currentState;

    public GameState GetCurrentState() => _currentState;
    
    // An array of every pin loaded in the scene during Frame 1
    private Pin[] _allPinsPool;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (throwController == null) throwController = FindObjectOfType<DrawBowlManager>();
        if (modeSwitcher == null) modeSwitcher = FindObjectOfType<BowlingModeSwitcher>();
        _allPinsPool = Resources.FindObjectsOfTypeAll<Pin>();

        if (modeSwitcher == null)
        {
            Debug.LogWarning("No BowlingModeSwitcher found. Falling back to GameManager TAB handling without camera or object-group toggles.");
        }
        
        StartNewGame();
    }

    void Update()
    {
        // Quick debugging reset key
        if (Input.GetKeyDown(KeyCode.R))
        {
            Debug.Log("Resetting Game...");
            StartNewGame();
        }

        if (modeSwitcher == null && Input.GetKeyDown(KeyCode.Tab) && CanToggleMode())
        {
            if (_currentState == GameState.PinPlacement)
            {
                ReadyPlayer();
            }
            else if (_currentState == GameState.PlayerReady)
            {
                EnterPinPlacement();
            }
        }
    }

    public void StartNewGame()
    {
        totalScore = 0;
        currentFrame = 1;
        StartFrame();
    }

    private void StartFrame()
    {
        _currentState = GameState.FrameSetup;
        throwsThisFrame = 0;

        
        
        // Zero out Math engine
        if (scoreCalculator != null) scoreCalculator.ResetFrameScore();

        Debug.Log("Current State: Frame Setup");
        
        foreach(var p in _allPinsPool) 
        {
            if (p.gameObject.scene.isLoaded)
            {
                p.gameObject.SetActive(true);
                p.ResetPin();
                
            }
        }
        

        if (uiController != null) uiController.UpdateStandardUI(this);

        

        EnterPinPlacement();
    }

    private void EnterPinPlacement()
    {
        _currentState = GameState.PinPlacement;

        Debug.Log("Current State: Pin Placement");

        if (throwController != null)
        {
            throwController.ResetThrowSetup();
            throwController.enabled = false;
        }
        
        if (modeSwitcher != null) 
        {
            modeSwitcher.ApplyMode(false);
        }
        
        if (throwController != null)
        {
            throwController.UpdateInstructionText("Click and drag to arrange pins. Press TAB when ready.");
        }
        if (uiController != null) uiController.UpdateStandardUI(this);
    }

    private void ReadyPlayer()
    {
        _currentState = GameState.PlayerReady;

        Debug.Log("Current State: Player Ready");
        
        if (modeSwitcher != null)
        {
            modeSwitcher.ApplyMode(true);
        }
        
        // Snap the Bowling Ball back into Position and turn its scripts back on
        if (throwController != null)
        {
            throwController.enabled = true;
            throwController.ResetThrowSetup();
        }

        if (uiController != null) uiController.UpdateStandardUI(this);
        
        Debug.Log($"Frame {currentFrame} Throw {throwsThisFrame + 1}: Player Ready.");
    }
    
    /// <summary>
    /// Called by the active bowling controller once the ball has been released.
    /// Locks out inputs and starts the timer.
    /// </summary>
    public void NotifyBallRolled()
    {
        _currentState = GameState.Rolling;
        throwsThisFrame++;

        Debug.Log("Current State: Rolling");
        
        // Start waiting for the physics to settle down the lane
        StartCoroutine(WaitAndScore());
    }

    private IEnumerator WaitAndScore()
    {
        yield return new WaitForSeconds(6f);

        _currentState = GameState.CalculatingScore;
        Debug.Log("Current State: Calculating Score");

        // 1. Count active pins to determine if we got a Strike!
        int totalPinsActiveAtStart = 0;
        int fallenPinsThisThrow = 0;
        
        foreach(var p in _allPinsPool)
        {
            if (p.gameObject.scene.isLoaded && p.gameObject.activeInHierarchy)
            {
                totalPinsActiveAtStart++;
                if (p.IsFallen()) fallenPinsThisThrow++;
            }
        }

        bool isStrike = (throwsThisFrame == 1 && fallenPinsThisThrow == totalPinsActiveAtStart && totalPinsActiveAtStart > 0);

        if (isStrike && scoreCalculator != null)
        {
            scoreCalculator.ApplyStrikeBonus();
        }

        int frameScore = 0;
        if (scoreCalculator != null) frameScore = scoreCalculator.GetFinalScore();

        totalScore += frameScore;
        
        Debug.Log($"Tallying Frame! Points Earned: {frameScore}. Total: {totalScore}");
        
        if (uiController != null) uiController.UpdateStandardUI(this);

        // Standard 10-pin bowling allows 2 throws, but a Strike finishes the round instantly!
        if (throwsThisFrame >= 2 || isStrike)
        {
            // Frame is totally over, reset!
            currentFrame++;
            
            if (currentFrame > maxFrames)
            {
                _currentState = GameState.GameOver;
                Debug.Log($"Game Over! Final Score: {totalScore}");
            }
            else
            {
                StartFrame();
            }
        }
        else
        {
            ClearFallenPins();
            ReadyPlayer();
        }
    }

    private void ClearFallenPins()
    {
        foreach(var p in _allPinsPool)
        {
            if (p.gameObject.scene.isLoaded && p.IsFallen()) 
            {
                p.gameObject.SetActive(false);
            }
        }
    }

    public bool CanToggleMode()
    {
        // Only allow toggling mode before the first throw of a frame
        Debug.Log($"Checking if mode can be toggled... Throws this frame: {throwsThisFrame}, Current State: {_currentState}");
        return throwsThisFrame == 0 && (_currentState == GameState.PinPlacement || _currentState == GameState.PlayerReady);
    
    }

    public void OnModeChanged(bool isBowling)
    {
        if (isBowling && _currentState == GameState.PinPlacement)
        {
            ReadyPlayer();
        }
        else if (!isBowling && _currentState == GameState.PlayerReady)
        {
            EnterPinPlacement();
        }
    }
}
