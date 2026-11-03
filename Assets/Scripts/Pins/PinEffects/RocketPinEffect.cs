using System;
using System.Collections;
using UnityEngine;

// https://claude.ai/share/10512fce-aaac-43e0-a3ba-99b3b9321a45

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
    [Tooltip("Fallback speed, used when auto-landing can't be solved or is disabled.")]
    [SerializeField] private float launchSpeed = 25f;

    [Header("Landing Accuracy")]
    [Tooltip("Solve the launch speed so the pin lands on the target instead of overshooting.")]
    [SerializeField] private bool autoCalculateSpeed = true;
    [Tooltip("1 = land on the target. <1 lands short, >1 lands past it.")]
    [SerializeField] private float landingDistanceMultiplier = 1f;
    [Tooltip("Height relative to the target's position that counts as 'landed'.")]
    [SerializeField] private float landingHeightOffset = 0f;
    [SerializeField] private float maxSolveSpeed = 150f;

    [Header("Particle Settings")]
    [Tooltip("Assign either a child ParticleSystem on the pin or a ParticleSystem prefab.")]
    [SerializeField] private ParticleSystem hitParticleEffect;
    [SerializeField] private int particlePlayCount = 3;
    [SerializeField] private float timeBetweenParticlePlays = 0.25f;

    private Coroutine _launchCoroutine;
    private Coroutine _particleCoroutine;
    private ParticleSystem _runtimeParticleInstance;
    private PinBase _launchOwner;
    private bool _launchPending;
    private Rigidbody _launchBody;
    private RigidbodyConstraints _launchConstraints;
    private bool _launchUseGravity;
    public override bool HasPendingGameplay => _launchPending && _launchOwner != null;

    public override void Execute(PinBase pinBase, Collider target = null)
    {
        if (pinBase == null) return;

        Rigidbody rb = pinBase.GetComponent<Rigidbody>();
        if (rb == null) return;

        // Ensure PinBase doesn't lock rotation upright during flight
        pinBase.AllowPinToFallOver = true;

        Transform aimTransform = ResolveAimTransform(pinBase, target);
        Vector3 fallbackForward = pinBase.transform.forward;

        if (_launchPending)
        {
            ResetEffect();
        }
        _launchOwner = pinBase;
        _launchBody = rb;
        _launchConstraints = rb.constraints;
        _launchUseGravity = rb.useGravity;
        _launchPending = true;
        _launchCoroutine = pinBase.StartCoroutine(
            RocketThrustRoutine(pinBase.transform, rb, aimTransform, fallbackForward));

        if (hitParticleEffect != null && particlePlayCount > 0)
        {
            if (_particleCoroutine != null)
            {
                pinBase.StopCoroutine(_particleCoroutine);
            }
            _particleCoroutine = pinBase.StartCoroutine(PlayParticlesRoutine(pinBase));
        }
    }

    // ---------------------------------------------------------------- Aiming

    private Transform ResolveAimTransform(PinBase sourcePin, Collider target)
    {
        if (aimAtStandingPin && TryGetClosestStandingPin(sourcePin, out PinBase standingPin))
        {
            return standingPin.transform;
        }

        if (target != null)
        {
            return target.transform;
        }

        return null;
    }

    private bool TryGetClosestStandingPin(PinBase sourcePin, out PinBase closestPin)
    {
        closestPin = null;
        PinBase[] allPins = UnityEngine.Object.FindObjectsOfType<PinBase>();

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
            }
        }

        return closestPin != null;
    }

    private Vector3 GetHorizontalDirection(Vector3 fromPos, Transform aim, Vector3 fallbackForward)
    {
        Vector3 horizontalDir;

        if (aim != null)
        {
            horizontalDir = aim.position - fromPos;
            horizontalDir.y = 0f;
            if (horizontalDir.sqrMagnitude > 0.001f) return horizontalDir.normalized;
        }

        horizontalDir = fallbackForward;
        horizontalDir.y = 0f;
        return horizontalDir.sqrMagnitude > 0.001f ? horizontalDir.normalized : Vector3.forward;
    }

    private Vector3 BuildLaunchDirection(Vector3 horizontalDir)
    {
        Vector3 tiltAxis = Vector3.Cross(Vector3.up, horizontalDir);
        return Quaternion.AngleAxis(launchAngle, tiltAxis) * Vector3.up;
    }

    // ------------------------------------------------------- Landing solver

    /// <summary>
    /// Finds the thrust speed that makes the pin land on the aim point, accounting for
    /// the constant-velocity thrust phase, gravity afterwards, and the rigidbody's drag.
    /// </summary>
    private float SolveLaunchSpeed(Vector3 startPos, Vector3 launchDir, Transform aim, float drag)
    {
        if (!autoCalculateSpeed || aim == null) return launchSpeed;

        Vector3 flat = aim.position - startPos;
        flat.y = 0f;
        float targetDist = flat.magnitude * landingDistanceMultiplier;
        if (targetDist < 0.01f) return launchSpeed;

        Vector3 hDir = flat.normalized;
        float landY = aim.position.y + landingHeightOffset;

        // Needs both an upward and a forward component to be solvable
        if (launchDir.y <= 0.01f || Vector3.Dot(launchDir, hDir) <= 0.01f) return launchSpeed;

        float hi = Mathf.Max(maxSolveSpeed, launchSpeed);
        if (SimulateRange(hi, startPos, launchDir, hDir, landY, drag) < targetDist)
        {
            return hi; // can't reach, go as far as allowed
        }

        float lo = 0f;
        for (int i = 0; i < 30; i++)
        {
            float mid = (lo + hi) * 0.5f;
            if (SimulateRange(mid, startPos, launchDir, hDir, landY, drag) < targetDist) lo = mid;
            else hi = mid;
        }

        return Mathf.Max((lo + hi) * 0.5f, 0.01f);
    }

    private float SimulateRange(float speed, Vector3 startPos, Vector3 launchDir, Vector3 hDir, float landY, float drag)
    {
        float dt = Time.fixedDeltaTime;
        float damp = 1f / (1f + drag * dt);
        float gravityY = Physics.gravity.y;

        Vector3 pos = startPos;
        Vector3 vel = launchDir * speed;

        // Thrust phase: velocity is re-set every fixed step, physics then applies damping + integration
        int thrustSteps = Mathf.CeilToInt(launchDuration / dt);
        for (int i = 0; i < thrustSteps; i++)
        {
            vel = launchDir * speed * damp;
            pos += vel * dt;
        }

        // Ballistic phase: gravity restored
        for (int i = 0; i < 3000; i++)
        {
            vel.y += gravityY * dt;
            vel *= damp;
            pos += vel * dt;

            if (vel.y < 0f && pos.y <= landY) break;
        }

        return Vector3.Dot(pos - startPos, hDir);
    }

    // --------------------------------------------------------------- Launch

    private IEnumerator RocketThrustRoutine(Transform pinTransform, Rigidbody rb, Transform aim, Vector3 fallbackForward)
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

        if (rb == null) { _launchPending = false; yield break; }

        // Re-aim from where the pin actually is now (the hold phase moves it)
        Vector3 horizontalDir = GetHorizontalDirection(rb.position, aim, fallbackForward);
        Vector3 launchDirection = BuildLaunchDirection(horizontalDir);

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

        if (rb == null) { _launchPending = false; yield break; }

        pinTransform.rotation = targetRot;

        // Final aim + speed solve using the position at the moment thrust begins
        horizontalDir = GetHorizontalDirection(rb.position, aim, fallbackForward);
        launchDirection = BuildLaunchDirection(horizontalDir);
        float solvedSpeed = SolveLaunchSpeed(rb.position, launchDirection, aim, rb.linearDamping);

        // Release Y position but keep rotation frozen so the pin flies straight
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        float launchElapsed = 0f;
        while (launchElapsed < launchDuration && rb != null)
        {
            rb.linearVelocity = launchDirection * solvedSpeed;
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
        _launchPending = false;
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

    // ------------------------------------------------------------ Particles

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
        if (_launchOwner != null && _launchCoroutine != null) _launchOwner.StopCoroutine(_launchCoroutine);
        if (_launchPending && _launchBody != null)
        {
            _launchBody.constraints = _launchConstraints;
            _launchBody.useGravity = _launchUseGravity;
        }
        _launchCoroutine = null;
        _launchPending = false;
        _launchOwner = null;
        _launchBody = null;
        if (_runtimeParticleInstance != null)
        {
            _runtimeParticleInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
