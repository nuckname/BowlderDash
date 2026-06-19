using UnityEngine;
using System.Collections.Generic;

public class Pin : PinBase
{
    [Header("Triggers")]
    public float fallAngleThreshold = 45f;
    public string hitterTag = "Ball";
    public float minHitVelocity = 3f;
    
    [Header("Effects")]
    [SerializeReference] public List<PinEffect> onHitEffects = new List<PinEffect>();
    [SerializeReference] public List<PinEffect> onFallEffects = new List<PinEffect>();

    [SerializeField] private bool _allowPinToFallOver = false;
    private bool _isFallen = false;
    private bool _hasBeenHit = false;

    private Renderer _renderer;
    private Collider _collider;

    public bool AllowPinToFallOver
    {
        get => _allowPinToFallOver;
        set
        {
            _allowPinToFallOver = value;
            SyncPhysicsConstraints();
        }
    }
    
    public void SyncPhysicsConstraints()
    {
        if (_rb == null) return;

        if (!AllowPinToFallOver)
        {
            // Freezes Position X and Z. 
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
    
    protected override void Awake()
    {
        base.Awake();
        _renderer = GetComponentInChildren<Renderer>();
        _collider = GetComponent<Collider>();
    }

    void Update()
    {
        if (!_allowPinToFallOver || _isFallen) return;

        if (Vector3.Angle(Vector3.up, transform.up) > fallAngleThreshold)
        {
            TriggerFall();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleHit(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleHit(collision.collider);
    }

    private void HandleHit(Collider other)
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

    private void TriggerFall()
    {
        _isFallen = true;
        foreach (var effect in onFallEffects)
        {
            effect.Execute(this);
        }
        NotifyFallen();
    }

    public void DisableVisualsAndPhysics()
    {
        if (_renderer) _renderer.enabled = false;
        if (_collider) _collider.enabled = false;
        if (_rb) _rb.isKinematic = true;
    }

    public override void ResetPin()
    {
        _isFallen = false;
        _hasBeenHit = false;

        if (_renderer) _renderer.enabled = true;
        if (_collider) _collider.enabled = true;

        foreach (var effect in onHitEffects) effect.ResetEffect();
        foreach (var effect in onFallEffects) effect.ResetEffect();

        base.ResetPin();

//        GetComponent<explodingPin>()?.ResetPin();
  //      GetComponent<vasePin>()?.ResetPin();
    }
}