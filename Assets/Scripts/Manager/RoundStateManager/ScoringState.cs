using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ScoringState : RoundBaseState
{
    private bool hasScored = false;
    private bool pinsHaveSettled = false;
    private float checkInterval = 0.25f;
    private float timeSinceLastCheck = 0f;
    
    private float waitTimeBeforeScoring = 0.2f;
    
    // Floating text offsets
    private float yOffsetPinInHole = 16.0f; 
    private float yOffset = 4.0f;
    
    private RoundStateManager _manager;
    private float _textSpawnDelay = 1f;

    private ScoringDebugUI _debugUI;
    public string debugCurrentPhase = "Initializing";
    public string debugUnsettledPins = "";

    public override void EnterState(RoundStateManager manager)
    {
        manager.UpdateInstructionText("Waiting for pins to settle...");
        
        _manager = manager;
        
        hasScored = false;
        pinsHaveSettled = false;
        timeSinceLastCheck = 0f;

        debugCurrentPhase = "Waiting for pins to settle...";
        GameObject debugGo = new GameObject("ScoringDebugUI");
        _debugUI = debugGo.AddComponent<ScoringDebugUI>();
        _debugUI.scoringState = this;
        _debugUI.manager = manager;
    }

    public override void UpdateState(RoundStateManager manager) 
    { 
        if (hasScored) return;

        if (!pinsHaveSettled)
        {
            HandlePinSettlement();
        }
    }

    private void HandlePinSettlement()
    {
        timeSinceLastCheck += Time.deltaTime;

        // Check a certain amount of time to reduce performance overhead from checking every frame
        if (timeSinceLastCheck >= checkInterval)
        {
            timeSinceLastCheck = 0f;

            if (AreAllPinsSettled())
            {
                pinsHaveSettled = true;
                _manager.StartCoroutine(ProcessScoringRoutine());
            }
        }
    }

    private IEnumerator ProcessScoringRoutine()
    {
        foreach (PinBase pin in _manager.allActivePinsInTheFrame)
        {
            if (pin.IsFallen)
            {
                debugCurrentPhase = "Spawning text for: " + pin.gameObject.name;
                SpawnTextForSinglePin(pin);
                yield return new WaitForSeconds(_textSpawnDelay);
            }
        }

        // Once pins have settled, we start the wait timer before scoring
        debugCurrentPhase = "Waiting before final score calc...";
        yield return new WaitForSeconds(waitTimeBeforeScoring);

        CalculateScore();
        hasScored = true;
        debugCurrentPhase = "Finished scoring, going to next round.";
        GoToNextRound();
    }

    private bool AreAllPinsSettled()
    {
        debugUnsettledPins = "";
        int unsettledCount = 0;

        foreach (PinBase pin in _manager.allActivePinsInTheFrame)
        {
            if (!pin.IsSettled) 
            {
                unsettledCount++;
                debugUnsettledPins += pin.gameObject.name + "\n";
            }
        }

        if (unsettledCount > 0)
        {
            // If even one pin is still moving, we keep waiting
            return false; 
        }
        return true;
    }

    private void SpawnTextForSinglePin(PinBase pin)
    {
        foreach (PinEffect effect in pin.onFallEffects)
        {
            if (effect is ScoreMultiplierEffect scoreEffect)
            {
                string scoreText = scoreEffect.scoreValue.ToString();

                if (pin.hasEnterTheHoleAtTheBack)
                {
                    Vector3 spawnPos = pin.transform.position + new Vector3(0, yOffsetPinInHole, 0);
                    _manager.SpawnFloatingText(scoreText, spawnPos);
                }
                else
                {
                    Vector3 spawnPos = pin.transform.position + new Vector3(0, yOffset, 0);
                    _manager.SpawnFloatingText(scoreText, spawnPos);
                }
            }
        }
    }

    private void CalculateScore()
    {
        foreach (PinBase pin in _manager.allActivePinsInTheFrame)
        {
            if (pin == null)
            {
                Debug.LogError("Pin is null for some reason gl");
                break;
            }
            
            if (pin.IsFallen)
            {
                _manager.scoreCalculator.AddStandardPinFallen();
            }
        }
        
        int finalScoreThisRound = _manager.scoreCalculator.CalculateFinalScore();
        
        _manager.inGameScoreUi.UpdateScoreText(finalScoreThisRound);
        _manager.sessionData.AddScore(finalScoreThisRound);
    }

    private void GoToNextRound()
    {
        int ballsThrown = _manager.sessionData.ballsThrown;
        int maxBallsAllowed = _manager.roundData.GetRoundInfo(_manager.currentRound).maxBallsAllowedToThrow;
        int scoreNeeded = _manager.roundData.GetRoundInfo(_manager.currentRound).scoreNeededToBeat;
        int currentScore = _manager.sessionData.currentScore;
        
        if (currentScore >= scoreNeeded)
        {
            // Next Frame and go to shop
            _manager.SwitchState(_manager.roundWinState);
        }
        else if (ballsThrown >= maxBallsAllowed)
        {
            // Lost the game
            _manager.SwitchState(_manager.gameOverState);
        }
        else
        {
            _manager.SwitchState(_manager.roundOverState);
        }
    }
    
    public override void FixedUpdateState(RoundStateManager manager) { }
    public override void OnCollisionEnter(RoundStateManager manager, Collision collision) { }
    public override void OnCollisionExit(RoundStateManager manager, Collision collision) { }
    public override void OnTriggerEnter(RoundStateManager manager, Collider other) { }
    public override void OnTriggerExit(RoundStateManager manager, Collider other) { }

    public override void ExitState(RoundStateManager manager)
    {
        if (_debugUI != null)
        {
            GameObject.Destroy(_debugUI.gameObject);
        }

        if(manager.currentRound == manager.roundData.GetMaxRoundNumber())
        {
            // then we check score to beat
            // if beat next state -> shop or something
        }
        else
        {
            // if not beat then restart game.
        }
    }
}

// Just AI slop its an OnGui so its UI at the top, temp
public class ScoringDebugUI : MonoBehaviour
{
    public ScoringState scoringState;
    public RoundStateManager manager;

    private void OnGUI()
    {
        if (scoringState == null || manager == null) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 24;
        style.normal.textColor = Color.red;
        style.alignment = TextAnchor.UpperCenter;
        style.fontStyle = FontStyle.Bold;

        string debugText = "--- SCORING STATE DEBUG ---\n" +
                           "Current Phase: " + scoringState.debugCurrentPhase + "\n\n" +
                           "Unsettled Pins:\n" + (string.IsNullOrEmpty(scoringState.debugUnsettledPins) ? "None (All Settled)" : scoringState.debugUnsettledPins);

        GUI.Label(new Rect(Screen.width / 2f - 300f, 20f, 600f, 800f), debugText, style);
    }
}