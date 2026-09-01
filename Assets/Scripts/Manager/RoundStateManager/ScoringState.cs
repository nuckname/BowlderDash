using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ScoringState : RoundBaseState
{
    private bool hasScored = false;
    private bool pinsHaveSettled = false;
    private float checkInterval = 0.1f;
    private float timeSinceLastCheck = 0f;
    
    private float waitTimeBeforeScoring = 1.5f;
    
    private float maxWaitTimeForSettling = 5.0f;
    private float totalWaitTime = 0f;
    
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
        totalWaitTime = 0f;

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
        totalWaitTime += Time.deltaTime;

        // Check a certain amount of time to reduce performance overhead from checking every frame
        if (timeSinceLastCheck >= checkInterval)
        {
            timeSinceLastCheck = 0f;

            bool allPinsSettled = AreAllPinsSettled();
            bool settlementTimedOut = maxWaitTimeForSettling > 0f && totalWaitTime >= maxWaitTimeForSettling;

            if (allPinsSettled || settlementTimedOut)
            {
                if (settlementTimedOut && !allPinsSettled)
                {
                    Debug.LogWarning($"Pin settlement exceeded {maxWaitTimeForSettling:0.##} seconds; continuing to scoring.");
                }

                pinsHaveSettled = true;
                _manager.StartCoroutine(ProcessScoringRoutine());
            }
        }
    }

    private IEnumerator ProcessScoringRoutine()
    {
        yield return new WaitForSeconds(waitTimeBeforeScoring);

        if (_manager.TryApplyStrikeMultiplier())
        {
            _manager.SpawnStrikeFloatingText();
        }

        TriggerEndOfRoundEffects();

        int pinCount = _manager.pinsToScore.Count;
        
        if (pinCount <= 4)
        {
            _textSpawnDelay = 1.25f;
        }
        else if (pinCount > 5 && pinCount <= 8)
        {
            _textSpawnDelay = 0.75f;
        }
        else
        {
            _textSpawnDelay = 0.5f;
        }

        foreach (PinBase pin in _manager.pinsToScore)
        {
            SpawnTextForSinglePin(pin);
            yield return new WaitForSeconds(_textSpawnDelay);
        }

        // Once pins have settled, we start the wait timer before scoring
        yield return new WaitForSeconds(waitTimeBeforeScoring);

        CalculateScore();
        hasScored = true;
        GoToNextRound();
    }

    private void TriggerEndOfRoundEffects()
    {
        foreach (PinBase pin in _manager.allActivePinsInTheFrame)
        {
            if (pin != null && !pin.IsFallen)
            {
                pin.TriggerEndOfRound();
            }
        }
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
        List<PinEffect> combinedEffects = new List<PinEffect>();
        
        if (pin.onFallEffects != null) combinedEffects.AddRange(pin.onFallEffects);
        if (pin.onEndOfRoundEffects != null) combinedEffects.AddRange(pin.onEndOfRoundEffects);
        
        pin.TriggerTextForEffects(combinedEffects, _manager.SpawnPinFloatingText);
    }

    private void CalculateScore()
    {
        int finalScoreThisRound = _manager.scoreCalculator.CalculateFinalScore();
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
            // if we have ball throws left, give them more tickets plus a bonus for not using all their balls.
            // I have no idea how to tell the user this. Floating text?
            GiveBonusTickets(maxBallsAllowed, ballsThrown);

            bool isLastRound = _manager.roundData.IsLastRound(_manager.sessionData.currentRound);
            RoundBaseState nextState = isLastRound
                ? _manager.gameCompleteState
                : _manager.roundWinState;

            _manager.StartCoroutine(TransitionToPinPlacementRoutine(nextState, !isLastRound));
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
            _manager.StartCoroutine(TransitionToPinPlacementRoutine(_manager.roundStartState, false));
        }
    }

    private void GiveBonusTickets(int maxBallsAllowed, int ballsThrown)
    {
        int ballsLeft = maxBallsAllowed - ballsThrown;
        if (ballsLeft > 0)
        {
            int bonusTickets = (ballsLeft * _manager.gridManager.gridTicketManager.ticketsPerFrame) + 1;
            _manager.sessionData.AddTickets(bonusTickets);
        }
    }

    private IEnumerator TransitionToPinPlacementRoutine(RoundBaseState nextState, bool prepareNextRound)
    {
        // lets just update this before so the player doesnt see it
        _manager.inGameScoreUi.UpdateRoundText(_manager.sessionData.currentRound, _manager.roundData.GetMaxRoundNumber());
        
        yield return new WaitForSeconds(waitBeforeUiCamera);
        
        // we want to only incremental the UI here and only here.
        // We also want to move cameras to the TVs where the score is showing.
        
        RoundScore roundInfo = _manager.roundData.GetRoundInfo(_manager.sessionData.currentRound);
        
        _manager.MoveCamera(CameraLocation.BallsThrow);
        yield return new WaitForSeconds(1f);
        
        _manager.AddBallThrownToSession();
        yield return new WaitForSeconds(waitBeforeUiCamera);
        
        
        _manager.MoveCamera(CameraLocation.ScoreToBeat);
        yield return new WaitForSeconds(1f);
        
        _manager.inGameScoreUi.UpdateScoreText(_manager.sessionData.currentScore);
        _manager.inGameScoreUi.UpdateScoreToBeatText(roundInfo.scoreNeededToBeat);
        
        yield return new WaitForSeconds(waitBeforeUiCamera + 1.5f);

        if (prepareNextRound)
        {
            _manager.ResetUiForNewRound();
        }
        
        _manager.SwitchState(nextState);
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
    
    private void ClearFallenOverPins(RoundStateManager manager)
    {
        foreach (PinBase pin in manager.allActivePinsInTheFrame)
        {
            if (pin.IsFallen)
            {
                manager.PutRealWorldPinIntoDiscardPile(pin);
            }
        }
    }


}
