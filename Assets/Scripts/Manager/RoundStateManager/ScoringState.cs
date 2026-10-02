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
        PhysicsImpactAudio.SetWorldEligible(true);
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
        
        /*
        if (Input.GetMouseButtonDown(0))
        {
            _skipAnimation = true;
        }
        */
        
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
                PhysicsImpactAudio.SetWorldEligible(false);
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
            yield return SkippableWait(waitTimeBeforeScoring);
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
        manager.TurnOnBallMesh(false);
        ClearFallenOverPins(manager);
    }
    
    private void ClearFallenOverPins(RoundStateManager manager)
    {
        List<WorldObjectToCard> completedGroups = new List<WorldObjectToCard>();
        List<PinBase> individualPinsToClear = new List<PinBase>();

        foreach (PinBase pin in manager.allActivePinsInTheFrame)
        {
            if (pin == null) continue;

            if (pin.IsFallen)
            {
                WorldObjectToCard cardObj = pin.GetComponent<WorldObjectToCard>();
                bool allGroupPinsFallen = true;

                if (cardObj != null && cardObj.GroupPeers != null)
                {
                    foreach (var peer in cardObj.GroupPeers)
                    {
                        // Because we destroy individual pins later, if 'peer' is null, 
                        // it means it was previously destroyed (cleared). So we only check surviving peers.
                        if (peer != null)
                        {
                            PinBase peerPin = peer.GetComponent<PinBase>();
                            // If any peer in the group still exists and hasn't fallen, the group isn't complete
                            if (peerPin != null && !peerPin.IsFallen)
                            {
                                allGroupPinsFallen = false;
                                break;
                            }
                        }
                    }
                }

                if (allGroupPinsFallen)
                {
                    if (cardObj != null)
                    {
                        completedGroups.Add(cardObj);
                    }
                }
                else
                {
                    // Group isn't finished yet, just mark this individual fallen pin to be cleared from the lane
                    individualPinsToClear.Add(pin);
                }
            }

            // Return vase when it has scored
            if (pin.onEndOfRoundEffects is ScoreMultiplierEffect && !pin._isFallen)
            {
                manager.PutRealWorldPinIntoDiscardPile(pin);
            }
        }

        // Return fully knocked over groups to the discard pile
        foreach (var groupCard in completedGroups)
        {
            if (groupCard != null)
            {
                // This handles returning to deck and destroying the remaining objects in the group
                groupCard.ReturnToDiscardPile(); 
            }
        }

        // Clear the individual fallen pins that haven't finished their group yet
        foreach (var pin in individualPinsToClear)
        {
            if (pin != null)
            {
                Object.Destroy(pin.gameObject);
            }
        }
    }
}
