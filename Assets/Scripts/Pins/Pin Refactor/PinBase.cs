using UnityEngine;
using System;

/// <summary>
/// Abstract base for all pin types. Handles shared reset logic,
/// physics toggling, and placement-mode guard.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public abstract class PinBase : MonoBehaviour
{
    protected Rigidbody _rb;
    protected Vector3 _startPos;
    protected Quaternion _startRot;

    // Refactor event system
    public static event Action<PinBase> OnPinFallenGlobal;

    protected virtual void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _startPos = transform.position;
        _startRot = transform.rotation;
    }

    // Make State machine later
    protected bool IsInPlacementMode()
    {
        return GameManager.Instance != null
            && GameManager.Instance.GetCurrentState() == GameState.PinPlacement;
    }

    protected void NotifyFallen()
    {
        OnPinFallenGlobal?.Invoke(this);
    }

    public void UpdateTransform()
    {
        _startPos = transform.position;
        _startRot = transform.rotation;
    }

    public void SyncPhysicsToGameState()
    {
        bool active = true;
        if (GameManager.Instance != null)
        {
            GameState s = GameManager.Instance.GetCurrentState();
            active = s != GameState.PinPlacement && s != GameState.Shop;
        }
        SetPhysicsEnabled(active);
    }

    public void SetPhysicsEnabled(bool enabled)
    {
        if (_rb != null) _rb.isKinematic = !enabled;

        foreach (Tumbling t in GetComponentsInChildren<Tumbling>(true))
            if (t != null) t.setTumbling(enabled);
    }

    protected static void StopParticles(ParticleSystem ps)
    {
        if (ps != null)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public virtual void ResetPin()
    {
        // Reset physics state
        if (_rb != null)
        {
            _rb.velocity        = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        transform.position = _startPos;
        transform.rotation = _startRot;

        SyncPhysicsToGameState();
    }
}