using UnityEngine;

// Game is not over, the player hasnt reached their score quota yet and much throw another ball.
public class RoundOverState : RoundBaseState
{
    private float waitTimer = 0f;
    private float waitTime = 0.75f;

    public override void EnterState(RoundStateManager manager)
    {
        PhysicsImpactAudio.SetWorldEligible(true);
        waitTimer = 0f;

        manager.AddBallThrownToSession();
        manager.ClearSelectedBallForCurrentAttempt();
        
        if (manager.throwingBallState.CompletedEarly)
            manager.SwitchState(manager.scoringState);
    }

    public void CollectPinsToScore(RoundStateManager manager)
    {
        foreach (PinBase pin in manager.allActivePinsInTheFrame ?? System.Array.Empty<PinBase>())
        {
            if (pin == null || !pin.IsActiveGameplayPin) continue;

            // If pin HAS fallen over: Check its onFallEffects list
            if (pin.IsFallen && pin.onFallEffects != null)
            {
                foreach (PinEffect effect in pin.onFallEffects)
                {
                    if (effect is ScoreMultiplierEffect || effect is MultiEffect || effect is AdjacentPinEffect)
                    {
                        manager.pinsToScore.Add(pin);
                        break; // Stop searching this pin since we found one and added it
                    }
                    
                    // Gutter Effect
                    if (effect is GutterMultiEffect)
                    {
                        if (pin.hasEnterTheHoleAtTheBack || pin.hasEnterGutter)
                        {
                            manager.pinsToScore.Add(pin);
                            break; 
                        }
                    }
                }
            }

            // Pin is standing
            if (!pin.toScorePinFallOver && !pin.IsFallen && pin.onEndOfRoundEffects != null)
            {
                foreach (PinEffect effect in pin.onEndOfRoundEffects)
                {
                    if (effect is ScoreMultiplierEffect)
                    {
                        manager.pinsToScore.Add(pin);
                        break; 
                    }
                }
            }
           
        }
    }

    public override void UpdateState(RoundStateManager manager)
    {
        waitTimer += Time.deltaTime;

        if (waitTimer >= waitTime || Input.GetMouseButtonDown(0))
        {
            manager.SwitchState(manager.scoringState);
        }
    }

    public override void FixedUpdateState(RoundStateManager manager)
    {
    }

    public override void OnCollisionEnter(RoundStateManager manager, Collision collision)
    {
    }

    public override void OnCollisionExit(RoundStateManager manager, Collision collision)
    {
    }

    public override void OnTriggerEnter(RoundStateManager manager, Collider other)
    {
    }

    public override void OnTriggerExit(RoundStateManager manager, Collider other)
    {
    }

    public override void ExitState(RoundStateManager manager)
    {
    }
    

}
