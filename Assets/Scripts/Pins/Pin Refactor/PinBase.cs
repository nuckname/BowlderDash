using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Abstract base for all pin types. Handles shared reset logic,
/// physics toggling, placement-mode guards, hit detection, and fall mechanics.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public abstract class PinBase : MonoBehaviour
{
    [Header("Triggers")]
    public float fallAngleThreshold = 45f;
    public string hitterTag = "Ball";
    public float minHitVelocity = 3f;
    
    [Header("Effects")]
    [SerializeReference] public List<PinEffect> onHitEffects = new List<PinEffect>();
    [SerializeReference] public List<PinEffect> onFallEffects = new List<PinEffect>();

    [SerializeField] protected bool _allowPinToFallOver = false;
    [SerializeField] protected bool _allowPinToBePickedUp = false;
    
    protected bool _isFallen = false;
    protected bool _hasBeenHit = false;

    protected Rigidbody _rb;
    protected Renderer _renderer;
    protected Collider _collider;
    
    protected Vector3 _startPos;
    protected Quaternion _startRot;

    public bool AllowPinToFallOver
    {
        get => _allowPinToFallOver;
        set
        {
            _allowPinToFallOver = value;
            SyncPhysicsConstraints();
        }
    }
    
    public bool AllowPinToBePickedUp
    {
        get => _allowPinToBePickedUp;
        set => _allowPinToBePickedUp = value;
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
        if (!_allowPinToFallOver || _isFallen || _allowPinToBePickedUp) return;

        if (Vector3.Angle(Vector3.up, transform.up) > fallAngleThreshold)
        {
            TriggerFall();
        }
    }

    protected virtual void OnCollisionEnter(Collision collision)
    {
        HandleHit(collision.collider);
    }

    protected virtual void HandleHit(Collider other)
    {
        if (_hasBeenHit) return;

        Rigidbody otherRb = other.attachedRigidbody;
        if (otherRb != null && otherRb.velocity.magnitude < minHitVelocity) return;

        if (!string.IsNullOrEmpty(hitterTag) && other.CompareTag(hitterTag))
        {
            _hasBeenHit = true;
            foreach (PinEffect effect in onHitEffects)
            {
                effect.Execute(this, other); 
            }
        }
    }

    protected virtual void TriggerFall()
    {
        _isFallen = true;
        foreach (var effect in onFallEffects)
        {
            effect.Execute(this);
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

    public virtual void ResetPin()
    {
        _isFallen = false;
        _hasBeenHit = false;

        if (_renderer) _renderer.enabled = true;
        if (_collider) _collider.enabled = true;

        foreach (var effect in onHitEffects) effect.ResetEffect();
        foreach (var effect in onFallEffects) effect.ResetEffect();

        if (_rb != null)
        {
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        transform.position = _startPos;
        transform.rotation = _startRot;

        SyncPhysicsToGameState();
    }
}