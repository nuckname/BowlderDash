using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class ScoringState : RoundBaseState
{
    private bool hasScored = false;
    private float checkInterval = 1f;
    private float timeSinceLastCheck = 0f;

    private RoundStateManager _manager;

    public override void EnterState(RoundStateManager manager)
    {
        manager.UpdateInstructionText("Waiting for pins to settle...");
        
        _manager = manager;
        
        hasScored = false;
        timeSinceLastCheck = 0f;
    }

    public override void UpdateState(RoundStateManager manager) 
    { 
        if (hasScored) return;

        timeSinceLastCheck += Time.deltaTime;

        // Only run the settlement check every 1 second
        if (timeSinceLastCheck >= checkInterval)
        {
            timeSinceLastCheck = 0f;

            if (AreAllPinsSettled())
            {
                CalculateScore();
                hasScored = true;
                GoToNextRound();
            }
        }
    }

    private bool AreAllPinsSettled()
    {
        foreach (Pin pin in _manager.allActivePinsInTheFrame)
        {
            if (!pin.IsSettled) 
            {
                // If even one pin is still moving, we keep waiting
                return false; 
            }
        }
        return true;
    }

    private void GoToNextRound()
    {
        int ballsThrown = PlayerSheet.Instance.GetAmountOfBallsThrown();
        int maxBallsAllowed = _manager.roundData.GetRoundInfo(_manager.currentRound).maxBallsAllowedToThrow;
        int scoreNeeded = _manager.roundData.GetRoundInfo(_manager.currentRound).scoreNeededToBeat;
        int currentScore = PlayerSheet.Instance.GetScore();
        
        if (ballsThrown == maxBallsAllowed)
        {
            _manager.SwitchState(_manager.roundOverState);
        }
        
        if (currentScore >= scoreNeeded)
        {
            // Next Frame and go to shop
            _manager.SwitchState(_manager.shopState);

        }
        else
        {
            _manager.SwitchState(_manager.roundOverState);
        }
        
    }
    
    private void CalculateScore()
    {
        int scoreThisRound = 0;

        foreach (Pin pin in _manager.allActivePinsInTheFrame)
        {
            if (pin.IsFallen)
            {
                foreach (PinEffect effect in pin.onFallEffects)
                {
                    if (effect is ScoreMultiplierEffect scoreEffect)
                    {
                        string scoreText = scoreEffect.scoreValue.ToString();
                        
                        _manager.SpawnFloatingText(scoreText, pin.transform.position);
                        
                        break;
                    }
                }

                _manager.scoreCalculator.AddStandardPinFallen();
            }
        }

        int finalScoreThisRound = _manager.scoreCalculator.CalculateFinalScore();
        
        PlayerSheet.Instance.AddScore(finalScoreThisRound);
        
        // need to save this and add to it. 
        _manager.uiManager.totalScoreText.text = finalScoreThisRound.ToString();
    }
    public override void FixedUpdateState(RoundStateManager manager) { }
    public override void OnCollisionEnter(RoundStateManager manager, Collision collision) { }
    public override void OnCollisionExit(RoundStateManager manager, Collision collision) { }
    public override void OnTriggerEnter(RoundStateManager manager, Collider other) { }
    public override void OnTriggerExit(RoundStateManager manager, Collider other) { }

    public override void ExitState(RoundStateManager manager)
    {
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