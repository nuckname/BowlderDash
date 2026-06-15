using UnityEngine;

public class RoundStateManager : MonoBehaviour
{
    RoundBaseState currentState;

    // Instantiate all available states
    public FrameStartState FrameStart = new FrameStartState();
    public ThrowingBallState ThrowingBall = new ThrowingBallState();
    public PinPlacementState PinPlacement = new PinPlacementState();
    public ShopState Shop = new ShopState();

    void Start()
    {
        // Set the initial state
        currentState = FrameStart;
        currentState.EnterState(this);
    }

    void Update()
    {
        if (currentState != null)
        {
            currentState.UpdateState(this);
        }
    }

    public void SwitchState(RoundBaseState state)
    {
        currentState = state;
        currentState.EnterState(this);
    }

    void OnCollisionEnter(Collision collision)
    {
        currentState?.OnCollisionEnter(this, collision);
    }

    void OnCollisionExit(Collision collision)
    {
        currentState?.OnCollisionExit(this, collision);
    }

    void OnTriggerEnter(Collider other)
    {
        currentState?.OnTriggerEnter(this, other);
    }

    void OnTriggerExit(Collider other)
    {
        currentState?.OnTriggerExit(this, other);
    }
}