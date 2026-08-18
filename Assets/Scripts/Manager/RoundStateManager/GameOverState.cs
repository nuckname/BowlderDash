using UnityEngine;

[System.Serializable]
public class GameOverState : RoundBaseState
{
    public override void EnterState(RoundStateManager manager)
    {
        // Sometimes its hard to tell when you lose the game for now.
        Debug.LogWarning("GAME IS OVER");
        Debug.LogWarning("GAME IS OVER");
        Debug.LogWarning("GAME IS OVER");
        Debug.LogWarning("GAME IS OVER");
        Debug.LogWarning("GAME IS OVER");
        Debug.LogWarning("GAME IS OVER");
        Debug.LogWarning("GAME IS OVER");
        Debug.LogWarning("GAME IS OVER");
        Debug.LogWarning("GAME IS OVER");
    }

    public override void UpdateState(RoundStateManager manager)
    {
        
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