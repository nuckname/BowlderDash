using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ScoringState : RoundBaseState
{
    private bool hasScored = false;
    private bool pinsHaveSettled = false;
    private float checkInterval = 0.25f;
    private float timeSinceLastCheck = 0f;
    
    private float waitTimeBeforeScoring = 2f;
    
    // Floating text offsets
    private float yOffsetPinInHole = 16.0f; 
    private float yOffset = 4.0f;
    
    private RoundStateManager _manager;
    private float _textSpawnDelay = 1f;
    
    // Camera settings
    private float waitBeforeUiCamera = 1.5f;
    private float waitBeforePinPlacement = 2.0f;

    public override void EnterState(RoundStateManager manager)
    {
        manager.UpdateInstructionText("Waiting for pins to settle...");
        
        _manager = manager;
        
        hasScored = false;
        pinsHaveSettled = false;
        timeSinceLastCheck = 0f;

        manager.MoveCamera(CameraLocation.ScoringCameraLocation);
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
        yield return new WaitForSeconds(waitTimeBeforeScoring);
        
        foreach (PinBase pin in _manager.allActivePinsInTheFrame)
        {
            if (pin == null)
            {
                Debug.LogWarning("Pin is null for some reason.");
            }
            if (pin.IsFallen)
            {
                SpawnTextForSinglePin(pin);
                yield return new WaitForSeconds(_textSpawnDelay);
            }
        }

        // Once pins have settled, we start the wait timer before scoring
        yield return new WaitForSeconds(waitTimeBeforeScoring);

        CalculateScore();
        hasScored = true;
        GoToNextRound();
    }

    private bool AreAllPinsSettled()
    {
        int unsettledCount = 0;

        foreach (PinBase pin in _manager.allActivePinsInTheFrame)
        {
            if (pin != null && !pin.IsSettled) 
            {
                unsettledCount++;
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
        int maxBallsAllowed = _manager.roundData.GetRoundInfo(_manager.sessionData.currentRound).maxBallsAllowedToThrow;
        int scoreNeeded = _manager.roundData.GetRoundInfo(_manager.sessionData.currentRound).scoreNeededToBeat;
        int currentScore = _manager.sessionData.currentScore;
        
        if (currentScore >= scoreNeeded)
        {
            // Next Frame and go to shop
            Debug.Log("Win");
            Debug.Log("Win");
            Debug.Log("Win");
            Debug.Log("Win");
            Debug.Log("Win");
            Debug.Log("Win");
            _manager.SwitchState(_manager.roundWinState);
        }
        else if (ballsThrown >= maxBallsAllowed)
        {
            // Lost the game
            _manager.SwitchState(_manager.gameOverState);
        }
        else
        {
            // Else throw the ball again and try to reach the quota.
            
            // I want to show the in game UI before we go to the next round.
            _manager.StartCoroutine(TransitionToPinPlacementRoutine());
        }
    }
    
    private IEnumerator TransitionToPinPlacementRoutine()
    {
        yield return new WaitForSeconds(waitBeforeUiCamera);
        
        _manager.MoveCamera(CameraLocation.ShowInGameUiLocation);
        
        yield return new WaitForSeconds(waitBeforePinPlacement);
        
        _manager.SwitchState(_manager.roundStartState);
    }
    
    public override void FixedUpdateState(RoundStateManager manager) { }
    public override void OnCollisionEnter(RoundStateManager manager, Collision collision) { }
    public override void OnCollisionExit(RoundStateManager manager, Collision collision) { }
    public override void OnTriggerEnter(RoundStateManager manager, Collider other) { }
    public override void OnTriggerExit(RoundStateManager manager, Collider other) { }

    public override void ExitState(RoundStateManager manager)
    {
        ClearFallenOverPins(manager);
    }

    private static void ClearFallenOverPins(RoundStateManager manager)
    {
        foreach (PinBase pin in manager.allActivePinsInTheFrame)
        {
            if (pin.IsFallen)
            {
                GameObject.Destroy(pin.gameObject);
            }
        }
    }
}