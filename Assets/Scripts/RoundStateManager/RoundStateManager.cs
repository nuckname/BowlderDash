using UnityEngine;
using UnityEngine.UI;

public class RoundStateManager : MonoBehaviour
{
    public RoundBaseState currentState;

    public Camera targetCamera;
    
    // Instantiate all available states
    public FrameStartState FrameStart = new FrameStartState();
    public ThrowingBallState ThrowingBall = new ThrowingBallState();
    public PinPlacementState PinPlacement = new PinPlacementState();
    public ShopState Shop = new ShopState();
    public ScoringState ScoringState = new ScoringState();

    public Text instructionText;
    
    
    void Start()
    {
        // Set the initial state
        currentState = PinPlacement;
        currentState.EnterState(this);
    }

    void Update()
    {
        if (currentState != null)
        {
            currentState.UpdateState(this);
        }
    }

    public void SwitchState(RoundBaseState newState)
    {
        if (currentState != null)
        {
            currentState.ExitState(this);
        }

        currentState = newState;

        if (currentState != null)
        {
            currentState.EnterState(this);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        currentState?.OnCollisionEnter(this, collision);
    }

    void OnCollisionExit(Collision collision)
    {
        currentState?.OnCollisionExit(this, collision);
    }

    public void OnTriggerEnter(Collider other)
    {
        currentState?.OnTriggerEnter(this, other);
    }

    void OnTriggerExit(Collider other)
    {
        currentState?.OnTriggerExit(this, other);
    }
    
    public void UpdateInstructionText(string newText)
    {
        instructionText.text = newText;
    }
    
    // debug what currrent state we are in. 
    void OnGUI()
    {
        GUI.color = Color.green;
        string stateName = currentState != null ? currentState.GetType().Name : "NULL";
        GUI.Label(new Rect(300, 10, 300, 20), $"Current State: {stateName}");
    }
}