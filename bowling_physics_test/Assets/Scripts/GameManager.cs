using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum GameState { FrameSetup, PinPlacement, PlayerReady, Rolling, CalculatingScore, EndFrame, GameOver }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Core Dependencies")]
    public BallThrowController throwController;
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
        if (modeSwitcher == null) modeSwitcher = FindObjectOfType<BowlingModeSwitcher>();
        _allPinsPool = Resources.FindObjectsOfTypeAll<Pin>();
        
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
        
        if (modeSwitcher != null) 
        {
            modeSwitcher.ApplyMode(false);
        }
        
        if (throwController != null)
        {
            throwController.UpdateInstructionText("Place your pins! Press TAB when ready.");
        }
        if (uiController != null) uiController.UpdateStandardUI(this);
    }

    private void ReadyPlayer()
    {
        _currentState = GameState.PlayerReady;
        
        if (modeSwitcher != null)
        {
            modeSwitcher.ApplyMode(true);
        }
        
        // Snap the Bowling Ball back into Position and turn its scripts back on
        if (throwController != null)
        {
            throwController.enabled = true;
            throwController.ResetThrowSetup(); // We'll add this to the Throw controller to snap it back
        }
        
        Debug.Log($"Frame {currentFrame} Throw {throwsThisFrame + 1}: Player Ready.");
    }
    
    /// <summary>
    /// Called directly by the BallThrowController's ExecuteThrow() method.
    /// Locks out inputs and starts the timer.
    /// </summary>
    public void NotifyBallRolled()
    {
        _currentState = GameState.Rolling;
        throwsThisFrame++;
        
        // Start waiting for the physics to settle down the lane
        StartCoroutine(WaitAndScore());
    }

    private IEnumerator WaitAndScore()
    {
        yield return new WaitForSeconds(6f);

        _currentState = GameState.CalculatingScore;
        
        // Grab the math off the Event-Driven Score Engine
        int frameScore = 0;
        if (scoreCalculator != null) frameScore = scoreCalculator.GetFinalScore();
        
        // Update total
        totalScore += frameScore;
        
        Debug.Log($"Tallying Frame! Points Earned: {frameScore}. Total: {totalScore}");
        
        if (uiController != null) uiController.UpdateStandardUI(this);

        // Standard 10-pin bowling allows 2 throws.
        if (throwsThisFrame >= 2)
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
            if (scoreCalculator != null) scoreCalculator.ResetFrameScore(); 
            
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
