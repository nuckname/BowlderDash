using System;
using System.Collections;
using UnityEngine;

// ai https://share.gemini.google/JAjIhzZsHznq

[Serializable]
public class RocketPinEffect : PinEffect
{
    [Header("Rocket Launch Settings")]
    [SerializeField] private float thrustAcceleration = 35f;
    [SerializeField] private float initialImpulse = 8f;
    [SerializeField] [Range(0f, 85f)] private float launchAngle = 15f;
    [SerializeField] private float launchDuration = 1.2f;
    [SerializeField] private bool randomizeAngleDirection = true;

    [SerializeField] private float holdYDuration = 0.2f;
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

        Vector3 launchDirection = CalculateLaunchDirection(pinBase.transform, target);

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

    private Vector3 CalculateLaunchDirection(Transform pinTransform, Collider target)
    {
        Vector3 horizontalDir;

        if (randomizeAngleDirection)
        {
            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle.normalized;
            if (randomCircle == Vector2.zero) randomCircle = Vector2.up;
            horizontalDir = new Vector3(randomCircle.x, 0f, randomCircle.y);
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

    private IEnumerator RocketThrustRoutine(Transform pinTransform, Rigidbody rb, Vector3 launchDirection)
    {
        rb.isKinematic = false;
        rb.WakeUp();

        RigidbodyConstraints originalConstraints = rb.constraints;
        float lockedY = pinTransform.position.y;

        // Stop ball impact from knocking or spinning the pin off course
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;

        // Hold the Y position briefly on hit
        float holdElapsed = 0f;
        WaitForFixedUpdate fixedWait = new WaitForFixedUpdate();

        while (holdElapsed < holdYDuration && rb != null)
        {
            Vector3 pos = rb.position;
            pos.y = lockedY;
            rb.position = pos;
            rb.linearVelocity = Vector3.zero;

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

            Vector3 pos = rb.position;
            pos.y = lockedY;
            rb.position = pos;
            rb.linearVelocity = Vector3.zero;
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