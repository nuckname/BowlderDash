using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting.FullSerializer;

public class ScoringState : RoundBaseState
{
    private bool hasScored = false;
    private bool pinsHaveSettled = false;
    private float checkInterval = 0.1f;
    private float timeSinceLastCheck = 0f;
    
    private float waitTimeBeforeScoring = 1.0f;
    
    private float maxWaitTimeForSettling = 5.0f;
    private float totalWaitTime = 0f;
    
    private RoundStateManager _manager;
    private float _textSpawnDelay = 1f;
    private bool _skipAnimation = false;
    
    // Camera settings
    private float waitBeforeUiCamera = 2f;
    private float waitBeforePinPlacement = 2.0f;
    
    private float scoreDisplayHoldTime = 1.5f;
    public override void EnterState(RoundStateManager manager)
    {
        manager.UpdateInstructionText("Waiting for pins to settle...");
        
        _manager = manager;
        
        hasScored = false;
        pinsHaveSettled = false;
        _skipAnimation = false;
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
            
            if (Input.GetMouseButtonDown(0))
            {
                _skipAnimation = true;
            }

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
        yield return SkippableWait(waitTimeBeforeScoring);

        if (_manager.TryApplyStrikeMultiplier())
        {
            _manager.SpawnStrikeFloatingText();
            yield return SkippableWait(waitTimeBeforeScoring + 1f);
        }

        ExecutePinScoringEffects();

        int pinCount = _manager.pinsToScore.Count;
        
        if (pinCount <= 4)
        {
            _textSpawnDelay = 0.6f;
        }
        else if (pinCount > 5 && pinCount <= 8)
        {
            _textSpawnDelay = 0.4f;
        }
        else
        {
            _textSpawnDelay = 0.25f;
        }

        foreach (PinBase pin in _manager.pinsToScore)
        {
            SpawnTextForSinglePin(pin);
            if (!_skipAnimation)
            {
                yield return SkippableWait(_textSpawnDelay);
            }
        }

        // Once pins have settled, we start the wait timer before scoring
        yield return SkippableWait(waitTimeBeforeScoring);

        CalculateScore();
        hasScored = true;
        GoToNextRound();
    }

    private IEnumerator SkippableWait(float waitTime)
    {
        float timer = 0f;
        while (timer < waitTime && !_skipAnimation)
        {
            timer += Time.deltaTime;
            yield return null;
        }
    }

    private void ExecutePinScoringEffects()
    {
        foreach (PinBase pin in _manager.allActivePinsInTheFrame)
        {
            if (pin != null)
            {
                if (pin.IsFallen)
                {
                    if (pin.onFallEffects != null)
                    {
                        foreach (PinEffect effect in pin.onFallEffects)
                        {
                            effect.Execute(pin);
                        }
                    }
                }
                else
                {
                    if (pin.onEndOfRoundEffects != null)
                    {
                        foreach (PinEffect effect in pin.onEndOfRoundEffects)
                        {
                            effect.Execute(pin);
                        }
                    }
                }
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
        RoundScore currentRoundScore = _manager.roundData.GetRoundInfo(_manager.sessionData.currentRound);
        int maxBallsAllowed = currentRoundScore.maxBallsAllowedToThrow;
        int scoreNeeded = currentRoundScore.scoreNeededToBeat;
        
        int ballsThrown = _manager.sessionData.ballsThrown;
        int currentScore = _manager.sessionData.currentScore;
        int currentRound = _manager.sessionData.currentRound;

        if (currentScore >= scoreNeeded)
        {
            // if we have ball throws left, give them more tickets plus a bonus for not using all their balls.
            GiveBonusTickets(maxBallsAllowed, ballsThrown, currentRound);

            bool isLastRound = _manager.roundData.IsLastRound(currentRound);
            RoundBaseState nextState = isLastRound ? _manager.gameCompleteState : _manager.roundWinState;
            
            _manager.StartCoroutine(new EndOfRoundSCameraSequence().Execute(_manager, nextState, !isLastRound, true, false, currentRoundScore));
        }
        // This works, we are updated the ball throw late so we need the +1, after we check this
        else if (ballsThrown >= maxBallsAllowed)
        {
            // Lost the game
            _manager.StartCoroutine(new EndOfRoundSCameraSequence().Execute(_manager, 
                _manager.gameOverState,
                false,
                false,
                true,
                currentRoundScore));
        }
        else
        {
            // Else throw the ball again and try to reach the quota.
            _manager.StartCoroutine(new EndOfRoundSCameraSequence().Execute(_manager,
                _manager.roundStartState,
                false,
                false,
                false,
                currentRoundScore));
        }
    }
    
    // If we complete the round early, we want to give them extra tickets for not using all their balls.
    // This is a bonus for being efficient.
    private void GiveBonusTickets(int maxBallsAllowed, int ballsThrown, int currentRound)
    {
        int ballsLeft = maxBallsAllowed - ballsThrown;
        if (ballsLeft > 0)
        {
            int bonusTickets = (ballsLeft * 
            (_manager.gridManager.gridTicketManager.ticketsPerFrame + _manager.amountOfTicketsToSpawnOnLane.GetAmountToSpawn(currentRound))) 
            + 2 /*2 is the bonus for complete it early*/ + currentRound;
            
            _manager.sessionData.AddTickets(bonusTickets);
        }
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

            // Return vase when it has scored
            if (pin.onEndOfRoundEffects is ScoreMultiplierEffect && !pin._isFallen)
            {
                manager.PutRealWorldPinIntoDiscardPile(pin);
            }
    }
    }
    
    
}