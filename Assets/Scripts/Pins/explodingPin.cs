using UnityEngine;
using System.Collections;

public class ExplodingPin : PinBase
{
    // Slightly more complex pin so going to have states.
    private enum ExplodeState { Idle, KnockedDown, Detonating, Exploded }
    private ExplodeState _state = ExplodeState.Idle;

    [Header("Explosion Settings")]
    public float explosionForce  = 15f;
    public float explosionRadius = 5f;
    public float upwardModifier  = 1.0f;

    [Header("Trigger Settings")]
    public float tipAngleThreshold = 30f;
    public float detonationDelay   = 1.0f;

    [Header("FX")]
    public ParticleSystem explosionParticles;

    private exploder   _targetExploder;
    private Coroutine  _detonationCoroutine;
    private Renderer   _renderer;
    private MeshCollider _meshCollider;

    protected override void Awake()
    {
        base.Awake();
        _renderer = GetComponent<Renderer>();
        _meshCollider = GetComponent<MeshCollider>();

        GameObject exploderObj = GameObject.FindGameObjectWithTag("Exploder");
        _targetExploder = exploderObj.GetComponent<exploder>();
    }

    private void Update()
    {
        //if (IsInPlacementMode() || _state != ExplodeState.Idle) return;

        if (Vector3.Angle(transform.up, Vector3.up) > tipAngleThreshold)
            TransitionTo(ExplodeState.KnockedDown);
    }

    private void OnCollisionEnter(Collision collision)
    {
        //if (IsInPlacementMode() || _state != ExplodeState.Idle) return;

        if (collision.relativeVelocity.magnitude > 3f
            && Vector3.Angle(transform.up, Vector3.up) > 10f)
        {
            TransitionTo(ExplodeState.KnockedDown);
        }
    }

    // Small State Machine 
    private void TransitionTo(ExplodeState next)
    {
        _state = next;

        switch (next)
        {
            case ExplodeState.KnockedDown:
                _detonationCoroutine = StartCoroutine(DetonationSequence());
                break;

            case ExplodeState.Detonating:
                Detonate();
                break;
        }
    }

    private IEnumerator DetonationSequence()
    {
        yield return new WaitForSeconds(detonationDelay);
        _detonationCoroutine = null;

        if (_state == ExplodeState.KnockedDown)
            TransitionTo(ExplodeState.Detonating);
    }

    private void Detonate()
    {
        _state = ExplodeState.Exploded;

        Vector3 pos = transform.position;

        foreach (Collider hit in Physics.OverlapSphere(pos, explosionRadius))
        {
            Debug.LogWarning("Explosion hard coded breaks vase");
            //hit.GetComponentInParent<VaseEffect>()?.TryShatterFromExplosion();
            hit.GetComponentInParent<TicketPin>()?.TryAwardFromExplosion(hit);

            Rigidbody rb = hit.GetComponent<Rigidbody>();
            if (rb != null && rb != _rb)
                rb.AddExplosionForce(explosionForce, pos, explosionRadius,
                                     upwardModifier, ForceMode.Impulse);
        }

        _targetExploder?.AddExplosionPoint(pos);
        ApplyExplosionFeedback();
    }

    private void ApplyExplosionFeedback()
    {
        Debug.Log($"<color=orange><b>[ExplodingPin]</b></color> Detonated at {transform.position}");

        if (_renderer)     _renderer.enabled     = false;
        if (_meshCollider) _meshCollider.enabled  = false;
        if (_rb)           _rb.isKinematic        = true;

        explosionParticles?.Play();
    }

    public override void ResetPin()
    {
        if (_detonationCoroutine != null)
        {
            StopCoroutine(_detonationCoroutine);
            _detonationCoroutine = null;
        }

        _state = ExplodeState.Idle;

        if (_renderer)     _renderer.enabled    = true;
        if (_meshCollider) _meshCollider.enabled = true;

        StopParticles(explosionParticles);

        base.ResetPin();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawSphere(transform.position, explosionRadius);
    }

    public bool HasExploded
    {
        get
        {
            return _state == ExplodeState.Exploded;
        }
    }

    public bool IsKnockedDown
    {
        get
        {
            return _state == ExplodeState.KnockedDown || _state == ExplodeState.Detonating;
        }
    }
}