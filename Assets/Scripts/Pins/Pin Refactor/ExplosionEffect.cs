using System;
using UnityEngine;
using System.Collections;

[Serializable]
public class ExplosionEffect : PinEffect
{
    // Slightly more complex pin so going to have states
    private enum ExplodeState
    {
        Idle,
        KnockedDown,
        Detonating,
        Exploded
    }

    private ExplodeState _state = ExplodeState.Idle;

    [Header("Explosion Settings")] public float explosionForce = 15f;
    public float explosionRadius = 5f;
    public float upwardModifier = 1.0f;
    public float detonationDelay = 1.0f;
    public ParticleSystem explosionParticles;

    private Coroutine _detonationCoroutine;

    public override void Execute(Pin context, Collider instigator = null)
    {
        if (_state != ExplodeState.Idle) return;
        TransitionTo(ExplodeState.KnockedDown, context);
    }

    // Small State Machine 
    private void TransitionTo(ExplodeState next, Pin context)
    {
        _state = next;

        switch (next)
        {
            case ExplodeState.KnockedDown:
                _detonationCoroutine = context.StartCoroutine(DetonationSequence(context));
                break;

            case ExplodeState.Detonating:
                Detonate(context);
                break;
        }
    }

    private IEnumerator DetonationSequence(Pin context)
    {
        yield return new WaitForSeconds(detonationDelay);
        _detonationCoroutine = null;

        if (_state == ExplodeState.KnockedDown)
            TransitionTo(ExplodeState.Detonating, context);
    }

    private void Detonate(Pin context)
    {
        _state = ExplodeState.Exploded;
        Vector3 pos = context.transform.position;

        foreach (Collider hit in Physics.OverlapSphere(pos, explosionRadius))
        {
            Debug.LogError("Try shatter vase from explosion commented out");
           // hit.GetComponentInParent<VaseEffect>()?.TryShatterFromExplosion();
            hit.GetComponentInParent<TicketPin>()?.TryAwardFromExplosion(hit);

            Rigidbody rb = hit.GetComponent<Rigidbody>();
            //if (rb != null && rb != context.Rb)
                rb.AddExplosionForce(explosionForce, pos, explosionRadius, upwardModifier, ForceMode.Impulse);
        }

        explosionParticles?.Play();
        context.DisableVisualsAndPhysics();
    }

    public override void ResetEffect()
    {
        _state = ExplodeState.Idle;
        if (explosionParticles != null)
        {
            explosionParticles.Stop();
            explosionParticles.Clear();
        }
    }
}
