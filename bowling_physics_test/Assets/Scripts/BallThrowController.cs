using UnityEngine;
using UnityEngine.UI;

public enum ThrowState { Positioning, Aiming, SpinSelection, Powering, Rolling }

[RequireComponent(typeof(BallPhysics))]
public class BallThrowController : MonoBehaviour
{
    [Header("Component References")]
    public BallPhysics ballPhysics;
    public TrajectoryRenderer trajectoryRenderer;
    
    [Header("UI References")]
    [Tooltip("Standard Unity Text element to show current instruction.")]
    public Text instructionText;
    [Tooltip("Slider from -1 to 1 to visually show Spin.")]
    public Slider spinGauge;
    
    [Header("Positioning Settings")]
    [Tooltip("Max distance the ball can move left or right from its spawn.")]
    public float laneWidth = 1.0f; 
    public float positioningSpeed = 5f;
    
    [Header("Aiming Settings")]
    [Tooltip("Max degrees the ball can rotate from pointing straight down the lane.")]
    public float maxAimAngle = 30f; 
    public float aimSpeed = 50f;
    
    [Header("Spin Settings")]
    public float spinSelectionSpeed = 2f;
    [Tooltip("Maximum limit that the player can add spin. (e.g. 1, 5, 10).")]
    public float maxSpinValue = 5f;
    
    [Header("Power Vector Settings")]
    [Tooltip("Multiplier to convert mouse speed to physical Rigidbody force.")]
    public float powerMultiplier = 350f; 
    
    // Throw state variable tracking
    private ThrowState _state = ThrowState.Positioning;
    private Vector3 _initialPosition;
    private float _currentAimAngle = 0f;
    private float _currentSpin = 0f; 
    
    // Pull back mechanics tracking
    private bool _isPulling = false;
    private float _lowestYPosition;
    private float _pushStartTime;

    void Start()
    {
        if (ballPhysics == null) ballPhysics = GetComponent<BallPhysics>();
        
        // Auto assign TrajectoryRenderer to see if it exists 
        if (trajectoryRenderer == null) trajectoryRenderer = GetComponent<TrajectoryRenderer>();
        if (trajectoryRenderer != null) trajectoryRenderer.Initialize(ballPhysics);
        
        // Set up initial UI states
        if (spinGauge != null) 
        {
            spinGauge.minValue = -maxSpinValue;
            spinGauge.maxValue = maxSpinValue;
            spinGauge.value = 0f;
            spinGauge.gameObject.SetActive(false); // Hide spin gauge until spin phase
        }
        
        _initialPosition = transform.position;
        
        // Freeze ball visually while in the setup phases so gravity doesn't roll it early
        if (TryGetComponent<Rigidbody>(out var rb))
            rb.constraints = RigidbodyConstraints.FreezeAll;
            
        UpdateInstructionText("Bowling Setup: Move Mouse X to Position. Click to lock.");
    }

    public void UpdateInstructionText(string message)
    {
        if (instructionText != null)
        {
            instructionText.text = message;
        }
        Debug.Log(message);
    }

    void Update()
    {
        switch (_state)
        {
            case ThrowState.Positioning:  HandlePositioning(); break;
            case ThrowState.Aiming:       HandleAiming(); break;
            case ThrowState.SpinSelection:HandleSpinSelection(); break;
            case ThrowState.Powering:     HandlePowering(); break;
            case ThrowState.Rolling:      break; // Let physics take over completely
        }
        
        // Redraw trajectory curve continuously unless we've thrown the ball
        if (_state != ThrowState.Rolling && _state != ThrowState.Powering && trajectoryRenderer != null)
        {
            trajectoryRenderer.DrawTrajectory(transform.position, _currentAimAngle, _currentSpin);
        }
    }

    private void HandlePositioning()
    {
        float moveDelta = Input.GetAxis("Mouse X") * positioningSpeed * Time.deltaTime;
        
        Vector3 pos = transform.position + (Vector3.right * moveDelta);
        pos.x = Mathf.Clamp(pos.x, _initialPosition.x - laneWidth, _initialPosition.x + laneWidth);
        transform.position = pos;

        if (Input.GetMouseButtonDown(0))
        {
            _state = ThrowState.Aiming;
            Input.GetAxis("Mouse X"); // Clear buffer
            UpdateInstructionText("Position Locked. Move Mouse X to Rotate Aim. Click to lock.");
        }
    }

    private void HandleAiming()
    {
        float angleDelta = Input.GetAxis("Mouse X") * aimSpeed * Time.deltaTime;
        
        _currentAimAngle += angleDelta;
        _currentAimAngle = Mathf.Clamp(_currentAimAngle, -maxAimAngle, maxAimAngle);
        
        // Visually rotate the ball to point down the aimed path
        transform.localEulerAngles = new Vector3(0, _currentAimAngle, 0);

        if (Input.GetMouseButtonDown(0))
        {
            _state = ThrowState.SpinSelection;
            Input.GetAxis("Mouse X"); // Clear buffer
            UpdateInstructionText($"Aim Locked ({_currentAimAngle:F1} deg). Move Mouse X to apply Spin (-{maxSpinValue} to {maxSpinValue}). Click to lock.");
            
            if (spinGauge != null) spinGauge.gameObject.SetActive(true); // Turn on the spin ui gauge visually!
        }
    }

    private void HandleSpinSelection()
    {
        float spinDelta = Input.GetAxis("Mouse X") * spinSelectionSpeed * Time.deltaTime;
        _currentSpin = Mathf.Clamp(_currentSpin + spinDelta, -maxSpinValue, maxSpinValue);
        
        // Live update the UI slider so the player can see what their mouse is doing
        if (spinGauge != null) spinGauge.value = _currentSpin;

        if (Input.GetMouseButtonDown(0))
        {
            _state = ThrowState.Powering;
            
            // Turn off trajectory and UI now that we are locking the throw in
            if (trajectoryRenderer != null) trajectoryRenderer.Hide();
            if (spinGauge != null) spinGauge.gameObject.SetActive(false);
            
            UpdateInstructionText($"Spin Locked! Click to hold, pull mouse DOWN, then rapidly swipe UP to throw!");
        }
    }

    private void HandlePowering()
    {
        if (Input.GetMouseButtonDown(0))
        {
            _isPulling = true;
            _lowestYPosition = Input.mousePosition.y;
            _pushStartTime = Time.time;
        }
        else if (Input.GetMouseButton(0) && _isPulling)
        {
            float currentMouseY = Input.mousePosition.y;
            
            // Constantly track the bottom of their pullback stroke
            if (currentMouseY <= _lowestYPosition)
            {
                _lowestYPosition = currentMouseY;
                _pushStartTime = Time.time; // Reset timer whenever they slide lower
            }
        }
        else if (Input.GetMouseButtonUp(0) && _isPulling)
        {
            _isPulling = false;
            float releaseYPosition = Input.mousePosition.y;
            
            float pushDistance = releaseYPosition - _lowestYPosition;
            float timeTaken = Time.time - _pushStartTime;

            // Enforce a minimum distance so accidental clicks don't throw 
            if (pushDistance > 20f && timeTaken > 0.01f) 
            {
                // Speed = Distance/Time, divide by screen height so resolution doesn't distort it.
                float pushSpeedNormalized = (pushDistance / Screen.height) / timeTaken;

                float throwPower = pushSpeedNormalized * powerMultiplier;
                
                ExecuteThrow(throwPower);
            }
            else
            {
                UpdateInstructionText("Throw too weak. Pull back further and swipe UP faster. Try again.");
            }
        }
    }

    private void ExecuteThrow(float forceValue)
    {
        _state = ThrowState.Rolling;
        
        // Calculate the world-space vector for the throw based on the current Aim Angle
        Vector3 throwDirection = Quaternion.Euler(0, _currentAimAngle, 0) * Vector3.forward;
        
        // Pass power vector to the BallPhysics script
        ballPhysics.ThrowBall(throwDirection * forceValue, _currentSpin);
        
        UpdateInstructionText($"THROWN! Power: {forceValue:F1} | Spin: {_currentSpin:F2}");
        
        // Inform the Global loop that this phase is over
        if (GameManager.Instance != null)
        {
            GameManager.Instance.NotifyBallRolled();
        }
    }
    
    /// <summary>
    /// Called by the GameManager to prep the ball for the next throw
    /// </summary>
    public void ResetThrowSetup()
    {
        _state = ThrowState.Positioning;
        _currentAimAngle = 0f;
        _currentSpin = 0f;
        
        transform.position = _initialPosition;
        transform.localEulerAngles = Vector3.zero;
        
        if (spinGauge != null)
        {
            spinGauge.value = 0f;
            spinGauge.gameObject.SetActive(false);
        }
        
        if (trajectoryRenderer != null) trajectoryRenderer.Show();
        
        if (TryGetComponent<Rigidbody>(out var rb))
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }
        
        UpdateInstructionText("Frame Ready: Move Mouse X to Position. Click to lock.");
    }
}
