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
        
        foreach (PinBase pin in manager.allActivePinsInTheFrame ?? System.Array.Empty<PinBase>())
        {
            if (pin == null || !pin.IsActiveGameplayPin) continue;

            // If pin HAS fallen over: Check its onFallEffects list
            if (pin.IsFallen)
            {
                foreach (PinEffect effect in pin.onFallEffects)
                {
                    if (effect is ScoreMultiplierEffect || effect is MultiEffect || effect is AdjacentPinEffect || effect is GutterMultiEffect)
                    {
                        manager.pinsToScore.Add(pin);
                        break; // Stop searching this pin since we found one and added it
                    }
                }
                
                // Gutter Multi Effect ? is there a better way to do this so that we dont have to search through ever pin. 
                foreach (PinEffect effect in pin.onFallEffects)
                {
                    if (effect is GutterMultiEffect)
                    {
                        if (pin.hasEnterTheHoleAtTheBack)
                        {
                            manager.pinsToScore.Add(pin);
                            break; 
                        }
                    }
                }
            }

            // Pin is standing
            if (!pin.toScorePinFallOver && !pin.IsFallen)
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
