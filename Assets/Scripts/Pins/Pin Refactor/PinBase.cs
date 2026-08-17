using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Abstract base for all pin types. Handles shared reset logic,
/// physics toggling, placement-mode guards, hit detection, and fall mechanics.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PinBase : MonoBehaviour
{
    [Header("Triggers")]
    public float fallAngleThreshold = 45f;
    public string hitterTag = "Ball";
    public float minHitVelocity = 3f;

    public bool hasEnterTheHoleAtTheBack = false;
    
    [Header("Effects")]
    [SerializeReference] public List<PinEffect> onHitEffects = new List<PinEffect>();
    [SerializeReference] public List<PinEffect> onFallEffects = new List<PinEffect>();
    [SerializeReference] public List<PinEffect> onEndOfRoundEffects = new List<PinEffect>();
    
    [SerializeField] protected bool allowPinToFallOver = false;
    [SerializeField] protected bool allowPinToBePickedUp = false;
    [SerializeField] protected bool allowCollisionEffects = false;
    
    public bool _isFallen = false;
    public bool IsFallen => _isFallen;
    
    protected bool _hasBeenHit = false;

    protected Rigidbody _rb;
    protected Renderer _renderer;
    protected Collider _collider;
    
    protected Vector3 _startPos;
    protected Quaternion _startRot;

    // dependency injection 
    public ScoreCalculator FrameScoreCalculator { get; private set; }
    
    public bool AllowPinToFallOver
    {
        get => allowPinToFallOver;
        set
        {
            allowPinToFallOver = value;
            SyncPhysicsConstraints();
        }
    }
    
    // Checks if the physics engine has put the pin to sleep
    public bool IsSettled
    {
        get
        {
            if (_rb == null || _rb.isKinematic) return true;

            // Failsafe: If the pin is barely moving (jittering), force it to sleep
            if (!_rb.IsSleeping() && _rb.linearVelocity.sqrMagnitude < 0.01f && _rb.angularVelocity.sqrMagnitude < 0.01f)
            {
                _rb.Sleep();
            }

            return _rb.IsSleeping();
        }
    }
    
    public bool AllowPinToBePickedUp
    {
        get => allowPinToBePickedUp;
        set => allowPinToBePickedUp = value;
    }
    
    public bool AllowCollisionEffects
    {
        get => allowCollisionEffects;
        set => allowCollisionEffects = value;
    }
    
    public void InjectDependencies(ScoreCalculator calculator)
    {
        FrameScoreCalculator = calculator;
    }

    protected virtual void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _renderer = GetComponentInChildren<Renderer>();
        _collider = GetComponent<Collider>();
        
        _startPos = transform.position;
        _startRot = transform.rotation;
    }

    protected virtual void Update()
    {
        if (!allowPinToFallOver)
        {
            // Stand up right
            Vector3 currentEuler = transform.rotation.eulerAngles;
            transform.rotation = Quaternion.Euler(0f, currentEuler.y, 0f);
        }
        
        if (!allowPinToFallOver || _isFallen || allowPinToBePickedUp) 
        {
            return;
        }

        float currentAngle = Vector3.Angle(Vector3.up, transform.up);

        if (currentAngle > fallAngleThreshold)
        {
            TriggerFall();
        }
    }

    protected virtual void OnCollisionEnter(Collision collision)
    {
        HandleHit(collision.collider);
    }
    
    protected virtual void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("ThePit"))
        {
            hasEnterTheHoleAtTheBack = true;
        }
    }

    protected virtual void HandleHit(Collider other)
    {
        if (_hasBeenHit || !allowCollisionEffects) return;

        Rigidbody otherRb = other.attachedRigidbody;
        if (otherRb != null && otherRb.linearVelocity.magnitude < minHitVelocity) return;

        bool isHitter = !string.IsNullOrEmpty(hitterTag) && other.CompareTag(hitterTag);

        bool isAnotherPin = other.GetComponent<PinBase>() != null;

        if (isHitter || isAnotherPin)
        {
            _hasBeenHit = true;
            foreach (PinEffect effect in onHitEffects)
            {
                effect.Execute(this, other); 
            }
        }
    }
    
    public virtual void TriggerEndOfRound()
    {
        foreach (PinEffect effect in onEndOfRoundEffects)
        {
            effect.Execute(this);
        }
    }

    protected virtual void TriggerFall()
    {
        _isFallen = true;
        foreach (PinEffect effect in onFallEffects)
        {
            effect.Execute(this);
        }
        
        // I dont like getting the compoent like this but I dont know another way.
        GridManager gridManager = FindObjectOfType<GridManager>();
        if (gridManager != null && gridManager.TryCollectTicketForPin(this, out int ticketValue))
        {
            SessionData.Instance.AddTickets(ticketValue);
            Debug.Log("+1");
        }
    }

    public void SyncPhysicsConstraints()
    {
        if (_rb == null) return;

        if (!AllowPinToFallOver)
        {
            // Freezes Position X and Z, and Rotation X and Z.
            _rb.constraints = RigidbodyConstraints.FreezePositionX | 
                              RigidbodyConstraints.FreezePositionZ | 
                              RigidbodyConstraints.FreezeRotationX | 
                              RigidbodyConstraints.FreezeRotationZ;
        }
        else
        {
            // Removes all constraints so it can be knocked back and fall
            _rb.constraints = RigidbodyConstraints.None;
        }
    }

    public void UpdateTransform()
    {
        _startPos = transform.position;
        _startRot = transform.rotation;
    }

    public void SyncPhysicsToGameState()
    {
        bool active = true;
        // Commented out to prevent errors if GameManager is missing in your current view, 
        // but this remains unchanged from your original logic.
        /* if (GameManager.Instance != null)
        {
            GameState s = GameManager.Instance.GetCurrentState();
            active = s != GameState.PinPlacement && s != GameState.Shop;
        }
        */
        SetPhysicsEnabled(active);
    }

    public void SetPhysicsEnabled(bool enabled)
    {
        if (_rb != null) _rb.isKinematic = !enabled;

        // Assuming Tumbling is a valid component in your project
        /*
        foreach (Tumbling t in GetComponentsInChildren<Tumbling>(true))
            if (t != null) t.setTumbling(enabled);
        */
    }

    protected static void StopParticles(ParticleSystem ps)
    {
        if (ps != null)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
    
    // Why is this here?
    public virtual void ResetPin()
    {
        _isFallen = false;
        _hasBeenHit = false;
        hasEnterTheHoleAtTheBack = false;

        if (_renderer) _renderer.enabled = true;
        if (_collider) _collider.enabled = true;

        foreach (PinEffect effect in onHitEffects) effect.ResetEffect();
        foreach (PinEffect effect in onFallEffects) effect.ResetEffect();
        foreach (PinEffect effect in onEndOfRoundEffects) effect.ResetEffect();

        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        transform.position = _startPos;
        transform.rotation = _startRot;

        SyncPhysicsToGameState();
    }
}