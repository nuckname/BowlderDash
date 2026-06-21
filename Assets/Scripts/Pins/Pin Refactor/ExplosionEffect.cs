using System;
using UnityEngine;
using System.Collections;

[Serializable]
public class ExplosionEffect : PinEffect
{
    [Header("Explosion Settings")] 
    public float explosionForce = 15f;
    public float explosionRadius = 5f;
    public float upwardModifier = 1.0f;
    public float detonationDelay = 1.0f;
    public ParticleSystem explosionParticles;

    private Coroutine _detonationCoroutine;
    private bool _hasTriggered = false;
    private bool _hasExploded = false;

    // Updated parameter to PinBase
    public override void Execute(PinBase context, Collider instigator = null)
    {
        if (_hasTriggered || _hasExploded) return;
        
        _hasTriggered = true;
        _detonationCoroutine = context.StartCoroutine(DetonationSequence(context));
    }

    private IEnumerator DetonationSequence(PinBase context)
    {
        yield return new WaitForSeconds(detonationDelay);
        _detonationCoroutine = null;

        if (!_hasExploded)
        {
            Detonate(context);
        }
    }

    private void Detonate(PinBase context)
    {
        _hasExploded = true;
        Vector3 pos = context.transform.position;

        foreach (Collider hit in Physics.OverlapSphere(pos, explosionRadius))
        {
            // Since VaseEffect is now a serialized class in a list on PinBase, 
            // GetComponent won't find it. You'll need to grab the PinBase and check its effects list instead.
            Debug.LogError("Try shatter vase from explosion commented out");
            // PinBase hitPin = hit.GetComponentInParent<PinBase>();
            // if (hitPin != null) ... loop through hitPin.onHitEffects to find VaseEffect ...
            
            // hit.GetComponentInParent<TicketPin>()?.TryAwardFromExplosion(hit);

            // attachedRigidbody is more efficient than GetComponent<Rigidbody>()
            Rigidbody rb = hit.attachedRigidbody; 
            
            // To ensure we don't blow up the pin that is causing the explosion
            // (Assuming you add a public getter for _rb to PinBase, or you check the GameObject)
            if (rb != null && hit.gameObject != context.gameObject) 
            {
                rb.AddExplosionForce(explosionForce, pos, explosionRadius, upwardModifier, ForceMode.Impulse);
            }
        }

        if (explosionParticles != null)
        {
            explosionParticles.Play();
        }
    }

    public override void ResetEffect()
    {
        _hasTriggered = false;
        _hasExploded = false;
        
        if (explosionParticles != null)
        {
            explosionParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}