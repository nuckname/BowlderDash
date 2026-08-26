using UnityEngine;

// Game is not over, the player hasnt reached their score quota yet and much throw another ball.
public class RoundOverState : RoundBaseState
{
    private float waitTimer = 0f;
    private float waitTime = 2.5f;

    public override void EnterState(RoundStateManager manager)
    {
        manager.UpdateInstructionText("Round Over! Click to continue.");
        
        waitTimer = 0f;

        foreach (PinBase pin in manager.allActivePinsInTheFrame)
        {
            // If pin HAS fallen over: Check its onFallEffects list
            if (pin.IsFallen)
            {
                foreach (PinEffect effect in pin.onFallEffects)
                {
                    if (effect is ScoreMultiplierEffect)
                    {
                        manager.pinsToScore.Add(pin);
                        break; // Stop searching this pin since we found one and added it
                    }
                }
            }
            // If pin HAS NOT fallen over: Check its onEndOfRoundEffects list
            else
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


        //manager.MoveCamera(CameraLocation.BallThrowingCameraLocation);
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
