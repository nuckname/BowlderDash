using System;
using System.Collections;
using UnityEngine;

[Serializable]
public class ExplosionEffect : PinEffect
{
    [Header("Explosion Settings")]
    public float explosionForce = 15f;
    public float explosionRadius = 5f;
    public float upwardModifier = 1.0f;
    public float detonationDelay = 1.0f;

    [Header("FX")]
    [Tooltip("Drag the child ParticleSystem here in the inspector")]
    public ParticleSystem explosionParticles;

    private bool _hasTriggered = false;
    private Coroutine _detonationCoroutine;

    // Triggered automatically by PinBase when the pin tips over
    public override void Execute(Pin context, Collider instigator = null)
    {
        if (_hasTriggered) return;
        
        _hasTriggered = true;

        // Ask the PinBase to run our delay timer
        _detonationCoroutine = context.StartCoroutine(DetonationSequence(context));
    }

    private IEnumerator DetonationSequence(Pin context)
    {
        // 1. Wait for the fuse
        yield return new WaitForSeconds(detonationDelay);

        // 2. BOOM
        Detonate(context);
    }

    private void Detonate(Pin context)
    {
        Vector3 pos = context.transform.position;
        Debug.Log($"<color=orange><b>[ExplosionEffect]</b></color> Detonated at {pos}");

        // 1. Apply visual feedback (Hide the pin, play particles)
        ApplyExplosionFeedback(context);

        // 2. Physics and Interactions
        Collider[] hits = Physics.OverlapSphere(pos, explosionRadius);
        foreach (Collider hit in hits)
        {
            // Check if we hit another pin
            // Encapsulation do we need this? Pin falling over handles itself?
            Pin hitPin = hit.GetComponentInParent<Pin>();
            if (hitPin != null && hitPin != context && !hitPin.IsFallen)
            {
                // This cleanly handles Vases, Tickets, and normal pins!
                //hitPin.TriggerFall(); 
            }

            // B. Apply physics blast
            Rigidbody rb = hit.attachedRigidbody;
            if (rb != null && rb != context.GetComponent<Rigidbody>())
            {
                // Unfreeze the pin so it can actually fly through the air
                if (hitPin != null) hitPin.AllowPinToFallOver = true; 

                rb.AddExplosionForce(explosionForce, pos, explosionRadius, upwardModifier, ForceMode.Impulse);
            }
        }

        // 3. Optional: Environment Exploder script integration
        // (If you still need this for breaking lane walls, etc.)
        GameObject exploderObj = GameObject.FindGameObjectWithTag("Exploder");
        if (exploderObj != null)
        {
            exploderObj.SendMessage("AddExplosionPoint", pos, SendMessageOptions.DontRequireReceiver);
        }
    }

    private void ApplyExplosionFeedback(Pin context)
    {
        // Hide the bomb pin so it looks like it was destroyed
        Renderer renderer = context.GetComponentInChildren<Renderer>();
        Collider collider = context.GetComponent<Collider>();
        Rigidbody rb = context.GetComponent<Rigidbody>();

        if (renderer) renderer.enabled = false;
        if (collider) collider.enabled = false;
        if (rb) rb.isKinematic = true;

        if (explosionParticles != null)
        {
            explosionParticles.Play();
        }
    }

    public override void ResetEffect()
    {
        _hasTriggered = false;

        if (explosionParticles != null)
        {
            explosionParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // Note: The visibility of the Renderer, Collider, and Rigidbody 
        // are automatically restored by the base.ResetPin() method inside PinBase.cs!
    }
}