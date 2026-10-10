using System;
using System.Collections;
using UnityEngine;

//Legacy Code

[Serializable]
public class ExplosionEffect : PinEffect
{
    [Header("Explosion Settings")]
    public float explosionForce = 15f;
    public float explosionRadius = 5f;
    public float upwardModifier = 1.0f;
    public float detonationDelay;

    [Header("FX")]
    [Tooltip("Drag the child ParticleSystem here in the inspector")]
    public ParticleSystem explosionParticles;

    [Header("Audio")]
    public SoundCueSO explosionCue;

    private bool _hasTriggered = false;
    private Coroutine _detonationCoroutine;
    private PinBase _detonationOwner;
    private int _detonationVersion;

    public override void Execute(PinBase context, Collider instigator = null)
    {
        if (_hasTriggered || !CanDetonate(context)) return;
        
        _hasTriggered = true;

        _detonationOwner = context;
        int version = ++_detonationVersion;
        _detonationCoroutine = context.StartCoroutine(DetonationSequence(context, version));
        
    }

    // Watch the gameplay role during the scaled delay so old throws cannot leave a live timer.
    private IEnumerator DetonationSequence(PinBase context, int version)
    {
        float detonateAt = Time.time + Mathf.Max(0f, detonationDelay);
        do
        {
            yield return null;
            if (version != _detonationVersion) yield break;
            if (!CanDetonate(context))
            {
                _detonationCoroutine = null;
                _detonationOwner = null;
                _hasTriggered = false;
                yield break;
            }
        } while (Time.time < detonateAt);

        _detonationCoroutine = null;
        _detonationOwner = null;
        Detonate(context);
    }

    private static bool CanDetonate(PinBase context) => context != null &&
        context.IsActiveGameplayPin && context.AllowCollisionEffects;

    private void Detonate(PinBase context)
    {
        Vector3 pos = context.transform.position;
        Debug.Log($"<color=orange><b>[ExplosionEffect]</b></color> Detonated at {pos}");

        // The pooled voice survives the pin being hidden or cleared after detonation.
        if (explosionCue != null && !explosionCue.isUI) GameAudio.Instance?.Play(explosionCue, pos);

        // Apply visual feedback (Hide the pin, play particles)
        ApplyExplosionFeedback(context);

        // Physics and Interactions
        Collider[] hits = Physics.OverlapSphere(pos, explosionRadius);
        foreach (Collider hit in hits)
        {
            // Check if we hit another pin
            // Encapsulation do we need this? Pin falling over handles itself?
            PinBase hitPin = hit.GetComponentInParent<PinBase>();
            if (hitPin != null && hitPin != context && !hitPin.IsFallen)
            {
                // This cleanly handles Vases, Tickets, and normal pins!
                //hitPin.TriggerFall(); 
            }

            // Apply physics blast
            Rigidbody rb = hit.attachedRigidbody;
            if (rb != null && rb != context.GetComponent<Rigidbody>())
            {
                // Unfreeze the pin so it can actually fly through the air
                if (hitPin != null) hitPin.AllowPinToFallOver = true; 

                rb.AddExplosionForce(explosionForce, pos, explosionRadius, upwardModifier, ForceMode.Impulse);
            }
        }

        // Optional: Environment Exploder script integration
        // (If you still need this for breaking lane walls, etc.)
        GameObject exploderObj = GameObject.FindGameObjectWithTag("Exploder");
        if (exploderObj != null)
        {
            exploderObj.SendMessage("AddExplosionPoint", pos, SendMessageOptions.DontRequireReceiver);
        }
    }

    private void ApplyExplosionFeedback(PinBase context)
    {
        context.TriggerFall(); 

        // Get ALL renderers (including children) and turn them off, but IGNORE the particle renderer
        Renderer[] renderers = context.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (r == null || r is ParticleSystemRenderer) continue;
            r.enabled = false;
        }

        // Get ALL colliders (including children) and disable them so the invisible pin doesn't block the lane
        Collider[] colliders = context.GetComponentsInChildren<Collider>();
        foreach (var c in colliders)
        {
            if (c) c.enabled = false;
        }
        
        Rigidbody rb = context.GetComponent<Rigidbody>();
        if (rb) rb.isKinematic = true;

        if (explosionParticles != null)
        {
            explosionParticles.Play();
        }
    }

    public override void ResetEffect()
    {
        // Invalidate the old sequence before a reset pin can schedule its next explosion.
        ++_detonationVersion;
        if (_detonationOwner != null && _detonationCoroutine != null)
            _detonationOwner.StopCoroutine(_detonationCoroutine);
        _detonationCoroutine = null;
        _detonationOwner = null;
        _hasTriggered = false;

        if (explosionParticles != null)
        {
            explosionParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

    }
}
