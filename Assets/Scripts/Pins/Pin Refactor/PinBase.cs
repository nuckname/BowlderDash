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
    public float minHitVelocity = 3f;

    public bool hasEnterTheHoleAtTheBack = false;
    
    // Im doing it like this because not all pin prefabs are the same.
    // Universal pin drag
    [SerializeField] protected float fallenDrag = 0.25f;
    [SerializeField] protected float fallenAngularDrag = 2.5f;
    
    private float _originalDrag;
    private float _originalAngularDrag;
    
    [Header("Effects")]
    [SerializeReference] public List<PinEffect> onHitEffects = new List<PinEffect>();
    [SerializeReference] public List<PinEffect> onFallEffects = new List<PinEffect>();
    [SerializeReference] public List<PinEffect> onEndOfRoundEffects = new List<PinEffect>();
    
    [SerializeField] protected bool allowPinToFallOver = false;
    [SerializeField] protected bool allowPinToBePickedUp = false;
    [SerializeField] protected bool allowCollisionEffects = false;

    [Header("Disable this box collider on hit")]
    [SerializeField] private BoxCollider boxColliderToDisable;
    
    public bool _isFallen = false;
    public bool IsFallen => _isFallen;
    
    protected bool _hasBeenHit = false;

    protected Rigidbody _rb;
    protected Renderer _renderer;
    protected Collider _collider;
    
    protected Vector3 _startPos;
    protected Quaternion _startRot;
    
    private bool _isForcedSettled = false;

    // dependency injection 
    public ScoreCalculator FrameScoreCalculator { get; private set; }
    
    public bool AllowPinToFallOver
    {
        get => allowPinToFallOver;
        set
        {
            allowPinToFallOver = value;
        }
    }
    
    // Checks if the physics engine has put the pin to sleep
    public bool IsSettled
    {
        set
        {
            _isForcedSettled = value;
        }
        get
        {
            if (_isForcedSettled) return true;
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
        
        if (_rb != null)
        {
            _originalDrag = _rb.linearDamping;
            _originalAngularDrag = _rb.angularDamping;
        }
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
        
        HandleHit(other);
    }

    protected virtual void HandleHit(Collider other)
    {
        if (_hasBeenHit || !allowCollisionEffects) return;

        bool isAnotherPin = other.GetComponent<PinBase>() != null;

        if (other.CompareTag("Ball") || isAnotherPin)
            
        {
            _hasBeenHit = true;
            
            if (boxColliderToDisable != null)
            {
                boxColliderToDisable.enabled = false;
            }

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
        
        if (_rb != null)
        {
            _rb.linearDamping = fallenDrag;
            _rb.angularDamping = fallenAngularDrag;
        }
        
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

    protected static void StopParticles(ParticleSystem ps)
    {
        if (ps != null)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}