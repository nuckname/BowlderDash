using System;
using System.Collections;
using UnityEngine;

// ai https://share.gemini.google/JAjIhzZsHznq
// https://share.gemini.google/Y5123dc8Qtg0

[Serializable]
public class RocketPinEffect : PinEffect
{
    [Header("Rocket Launch Settings")]
    [SerializeField] private float thrustAcceleration = 35f;
    [SerializeField] private float initialImpulse = 8f;
    [SerializeField] [Range(0f, 85f)] private float launchAngle = 15f;
    [SerializeField] private float launchDuration = 1.2f;
    [SerializeField] private bool aimAtStandingPin = true;

    [SerializeField] private float holdYDuration = 0.2f;
    [SerializeField] [Range(0f, 1f)] private float yFreezeStrength = 0.5f;
    [SerializeField] private float rotationDuration = 0.15f;
    [SerializeField] private float launchSpeed = 25f;
    
    [Header("Particle Settings")]
    [Tooltip("Assign either a child ParticleSystem on the pin or a ParticleSystem prefab.")]
    [SerializeField] private ParticleSystem hitParticleEffect;
    [SerializeField] private int particlePlayCount = 3;
    [SerializeField] private float timeBetweenParticlePlays = 0.25f;

    private Coroutine _launchCoroutine;
    private Coroutine _particleCoroutine;
    private ParticleSystem _runtimeParticleInstance;

    public override void Execute(PinBase pinBase, Collider target = null)
    {
        if (pinBase == null) return;

        Rigidbody rb = pinBase.GetComponent<Rigidbody>();
        if (rb == null) return;

        // Ensure PinBase doesn't lock rotation upright during flight
        pinBase.AllowPinToFallOver = true;

        Vector3 launchDirection = CalculateLaunchDirection(pinBase, target);

        if (_launchCoroutine != null)
        {
            pinBase.StopCoroutine(_launchCoroutine);
        }
        _launchCoroutine = pinBase.StartCoroutine(RocketThrustRoutine(pinBase.transform, rb, launchDirection));

        if (hitParticleEffect != null && particlePlayCount > 0)
        {
            if (_particleCoroutine != null)
            {
                pinBase.StopCoroutine(_particleCoroutine);
            }
            _particleCoroutine = pinBase.StartCoroutine(PlayParticlesRoutine(pinBase));
        }
    }

    private Vector3 CalculateLaunchDirection(PinBase sourcePin, Collider target)
    {
        Transform pinTransform = sourcePin.transform;
        Vector3 horizontalDir;

        if (aimAtStandingPin && TryGetStandingPinDirection(sourcePin, out Vector3 standingPinDir))
        {
            horizontalDir = standingPinDir;
        }
        else if (target != null)
        {
            horizontalDir = (pinTransform.position - target.transform.position);
            horizontalDir.y = 0f;
            horizontalDir = horizontalDir.sqrMagnitude > 0.001f ? horizontalDir.normalized : pinTransform.forward;
        }
        else
        {
            horizontalDir = pinTransform.forward;
        }

        Vector3 tiltAxis = Vector3.Cross(Vector3.up, horizontalDir);
        return Quaternion.AngleAxis(launchAngle, tiltAxis) * Vector3.up;
    }

    private bool TryGetStandingPinDirection(PinBase sourcePin, out Vector3 direction)
    {
        direction = Vector3.zero;
        PinBase[] allPins = UnityEngine.Object.FindObjectsOfType<PinBase>();

        PinBase closestPin = null;
        float closestSqrDist = float.MaxValue;
        Vector3 sourcePos = sourcePin.transform.position;

        foreach (PinBase pin in allPins)
        {
            if (pin == null || pin == sourcePin || !pin.gameObject.activeInHierarchy) continue;
            if (pin.IsFallen || pin.hasEnterTheHoleAtTheBack) continue;

            Vector3 offset = pin.transform.position - sourcePos;
            offset.y = 0f;
            float sqrDist = offset.sqrMagnitude;

            if (sqrDist > 0.001f && sqrDist < closestSqrDist)
            {
                closestSqrDist = sqrDist;
                closestPin = pin;
                direction = offset.normalized;
            }
        }

        return closestPin != null;
    }

    private IEnumerator RocketThrustRoutine(Transform pinTransform, Rigidbody rb, Vector3 launchDirection)
    {
        rb.isKinematic = false;
        rb.WakeUp();

        RigidbodyConstraints originalConstraints = rb.constraints;
        float lockedY = pinTransform.position.y;

        // Stop ball impact from knocking or spinning the pin off course
        rb.linearVelocity *= (1f - yFreezeStrength);
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        // Hold the Y position briefly on hit
        float holdElapsed = 0f;
        WaitForFixedUpdate fixedWait = new WaitForFixedUpdate();

        while (holdElapsed < holdYDuration && rb != null)
        {
            ApplySoftYFreeze(rb, lockedY);

            holdElapsed += Time.fixedDeltaTime;
            yield return fixedWait;
        }

        // Rotate smoothly toward the launch angle while keeping Y locked
        Quaternion startRot = pinTransform.rotation;
        Quaternion targetRot = Quaternion.FromToRotation(Vector3.up, launchDirection);
        float rotElapsed = 0f;

        while (rotElapsed < rotationDuration && rb != null)
        {
            rotElapsed += Time.fixedDeltaTime;
            float t = rotationDuration > 0f ? Mathf.Clamp01(rotElapsed / rotationDuration) : 1f;

            ApplySoftYFreeze(rb, lockedY);
            rb.MoveRotation(Quaternion.Slerp(startRot, targetRot, t));

            yield return fixedWait;
        }

        if (rb == null) yield break;

        pinTransform.rotation = targetRot;

        // Release Y position but keep rotation frozen so the pin flies straight
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        float launchElapsed = 0f;
        while (launchElapsed < launchDuration && rb != null)
        {
            rb.linearVelocity = launchDirection * launchSpeed;
            launchElapsed += Time.fixedDeltaTime;
            yield return fixedWait;
        }

        // Restore gravity and rotation after the straight flight finishes
        if (rb != null)
        {
            rb.useGravity = true;
            rb.constraints = originalConstraints;
        }

        _launchCoroutine = null;
    }

    private void ApplySoftYFreeze(Rigidbody rb, float minLockedY)
    {
        Vector3 velocity = rb.linearVelocity;
        velocity.x = Mathf.Lerp(velocity.x, 0f, yFreezeStrength);
        velocity.z = Mathf.Lerp(velocity.z, 0f, yFreezeStrength);

        if (rb.position.y < minLockedY)
        {
            // Gently keep the pin from sinking below its starting height
            float recoveryVelocityY = (minLockedY - rb.position.y) / Time.fixedDeltaTime;
            velocity.y = Mathf.Lerp(velocity.y, recoveryVelocityY, yFreezeStrength);
        }
        else
        {
            // Dampen upward velocity without snapping the pin back down onto the ball
            velocity.y = Mathf.Lerp(velocity.y, 0f, yFreezeStrength);
        }

        rb.linearVelocity = velocity;
    }
    
    private IEnumerator PlayParticlesRoutine(PinBase pinBase)
    {
        ParticleSystem psToPlay = GetOrCreateParticleSystem(pinBase.transform);
        if (psToPlay == null) yield break;

        WaitForSeconds wait = new WaitForSeconds(timeBetweenParticlePlays);

        for (int i = 0; i < particlePlayCount; i++)
        {
            psToPlay.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            psToPlay.Play(true);

            if (i < particlePlayCount - 1)
            {
                yield return wait;
            }
        }

        _particleCoroutine = null;
    }

    private ParticleSystem GetOrCreateParticleSystem(Transform pinTransform)
    {
        if (hitParticleEffect == null) return null;

        // If a child ParticleSystem in the scene was assigned directly, use it
        if (hitParticleEffect.gameObject.scene.IsValid())
        {
            return hitParticleEffect;
        }

        // Otherwise, instantiate the assigned prefab onto the pin once
        if (_runtimeParticleInstance == null)
        {
            _runtimeParticleInstance = UnityEngine.Object.Instantiate(
                hitParticleEffect, 
                pinTransform.position, 
                pinTransform.rotation, 
                pinTransform
            );
        }

        return _runtimeParticleInstance;
    }

    public override void ResetEffect()
    {
        if (_runtimeParticleInstance != null)
        {
            _runtimeParticleInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}