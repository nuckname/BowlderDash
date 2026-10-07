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
    public bool hasEnterGutter = false;
    
    // Im doing it like this because not all pin prefabs are the same.
    // Universal pin drag
    [SerializeField] protected float fallenDrag = 0.25f;
    [SerializeField] protected float fallenAngularDrag = 2.5f;
    
    private float _originalDrag;
    private float _originalAngularDrag;
    private bool _originalUseGravity;
    private bool _originalIsKinematic;
    private RigidbodyConstraints _originalConstraints;
    private bool _physicsStateCaptured;
    
    [Header("Effects")]
    [SerializeReference] public List<PinEffect> onPlaceEffects = new List<PinEffect>();
    [SerializeReference] public List<PinEffect> onHitEffects = new List<PinEffect>();
    [SerializeReference] public List<PinEffect> onFallEffects = new List<PinEffect>();
    // Look into this
    [SerializeReference] public List<PinEffect> onEndOfRoundEffects = new List<PinEffect>();

    [Header("Strike")]
    [SerializeField] private bool excludeFromStrike = false;
    public bool CountsTowardStrike => !excludeFromStrike;
    
    [Header("Floating Text Offsets")]
    [SerializeField] protected float yOffset = 4.0f;
    protected float yOffsetPinInHole = 13.0f;
    
    [SerializeField] protected bool allowPinToFallOver = false;
    [SerializeField] protected bool allowPinToBePickedUp = false;
    [SerializeField] protected bool allowCollisionEffects = false;

    [Header("Disable this box collider on hit")]
    [SerializeField] private BoxCollider boxColliderToDisable;
    
    public bool allowPinSelection;
    
    public bool _isFallen = false;
    public bool IsFallen => _isFallen;

    public bool toScorePinFallOver = false;
    
    protected bool _hasBeenHit = false;

    protected Rigidbody _rb;
    protected Renderer _renderer;
    protected Collider _collider;

    private bool _impactAudioRegistrationBlocked;
    private bool _missingRigidbodyReported;

    public bool IsActiveGameplayPin => isActiveAndEnabled && !_impactAudioRegistrationBlocked;
    
    protected Vector3 _startPos;
    protected Quaternion _startRot;
    
    private bool _isForcedSettled = false;

    public bool isClone = false;
    
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
            if (!IsActiveGameplayPin || _isForcedSettled) return true;

            // force End of round effects to settle as we dont need to hit them ?
            if (onEndOfRoundEffects != null && onEndOfRoundEffects.Count > 0) return true;

            if (_rb == null) _rb = GetComponent<Rigidbody>();
            if (_rb == null)
            {
                if (!_missingRigidbodyReported)
                {
                    Debug.LogWarning("Pin settlement skipped because its Rigidbody is missing.", this);
                    _missingRigidbodyReported = true;
                }
                return true;
            }
            
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

    public void SetImpactAudioEligible(bool eligible)
    {
        foreach (PhysicsImpactAudio reporter in GetComponentsInChildren<PhysicsImpactAudio>(true))
        {
            reporter.SetInstanceEligible(eligible);
        }
    }

    public void SetImpactAudioRegistrationBlocked(bool blocked)
    {
        _impactAudioRegistrationBlocked = blocked;
        if (blocked) SetImpactAudioEligible(false);
    }

    public bool CanRegisterImpactAudio => !_impactAudioRegistrationBlocked;

    protected virtual void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _renderer = GetComponentInChildren<Renderer>();
        _collider = GetComponent<Collider>();

        allowPinSelection = true;
        
        _startPos = transform.position;
        _startRot = transform.rotation;
        
        if (_rb != null)
        {
            _originalDrag = _rb.linearDamping;
            _originalAngularDrag = _rb.angularDamping;
            _originalUseGravity = _rb.useGravity;
            _originalIsKinematic = _rb.isKinematic;
            _originalConstraints = _rb.constraints;
            _physicsStateCaptured = true;
        }
    }

    public void EnterPlacementPhysicsMode()
    {
        if (_rb == null || !_physicsStateCaptured) return;

        if (!_rb.isKinematic)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        _rb.useGravity = false;
        _rb.isKinematic = true;
        _rb.Sleep();
        _isForcedSettled = true;
    }

    public void EnterThrowPhysicsMode()
    {
        if (_rb == null || !_physicsStateCaptured) return;

        _rb.constraints = _originalConstraints;
        _rb.linearDamping = _originalDrag;
        _rb.angularDamping = _originalAngularDrag;
        _rb.isKinematic = _originalIsKinematic;
        _rb.useGravity = _originalUseGravity;

        if (!_rb.isKinematic)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.Sleep();
        }

        _isForcedSettled = false;
    }
    
    public virtual void OnPlaced()
    {
        if (onPlaceEffects != null && onPlaceEffects.Count > 0)
        {
            foreach (PinEffect effect in onPlaceEffects)
            {
                effect?.Execute(this);
            }
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
        
        if (other.CompareTag("Gutter"))
        {
            hasEnterGutter = true;
        }
        
        HandleHit(other);
    }
    
    public virtual void TriggerTextForEffects(List<PinEffect> effectsToProcess, System.Action<string, Vector3> spawnTextCallback)
    {
        float offset = hasEnterTheHoleAtTheBack ? yOffsetPinInHole : yOffset;
        Vector3 spawnPos = transform.position + new Vector3(0, offset, 0);

        foreach (PinEffect effect in effectsToProcess)
        {
            string effectText = effect.GetEffectText();
            
            if (!string.IsNullOrEmpty(effectText))
            {
                spawnTextCallback?.Invoke(effectText, spawnPos);
            }
        }
    }
    protected virtual void HandleHit(Collider other)
    {
        if ((_hasBeenHit && !isClone) || !allowCollisionEffects) return;

        bool isAnotherPin = other.GetComponent<PinBase>() != null;

        if (other.CompareTag("Ball") || isAnotherPin)
            
        {
            _hasBeenHit = true;
            isClone = false; // Turn the clone flag off so it can only be hit one extra time
            
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
    
    public virtual void TriggerFall()
    {
        _isFallen = true;
        
        if (_rb != null)
        {
            _rb.linearDamping = fallenDrag;
            _rb.angularDamping = fallenAngularDrag;
        }

        // I dont like getting the compoent like this but I dont know another way.
        GridManager gridManager = FindObjectOfType<GridManager>();
        if (gridManager != null && gridManager.TryCollectTicketForPin(this, out int ticketValue))
        {
            SessionData.Instance.AddTickets(ticketValue);
        }
    }

    protected static void StopParticles(ParticleSystem ps)
    {
        if (ps != null)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
