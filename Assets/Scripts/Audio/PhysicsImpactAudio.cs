using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PhysicsImpactAudio : MonoBehaviour
{
    private const double PairCooldownSeconds = 0.075;
    private const double PairStateLifetimeSeconds = 3.0;

    [SerializeField] private ImpactAudioProfileSO profile;
    [SerializeField] private bool requireInstanceEligibility;

    private bool instanceEligible;

    private static readonly Dictionary<long, Dictionary<PairKey, ImpactReport>> pendingSteps =
        new Dictionary<long, Dictionary<PairKey, ImpactReport>>();
    private static readonly Dictionary<PairKey, PairState> pairStates =
        new Dictionary<PairKey, PairState>();
    private static readonly List<long> orderedSteps = new List<long>();
    private static readonly List<ImpactReport> orderedReports = new List<ImpactReport>();
    private static readonly List<PairKey> stalePairs = new List<PairKey>();

    private static bool worldEligible;
    private static int lastFlushFrame = -1;

    public ImpactAudioProfileSO Profile => profile;
    public bool RequiresInstanceEligibility => requireInstanceEligibility;
    public bool IsInstanceEligible => !requireInstanceEligibility || instanceEligible;

    private enum ImpactCategory
    {
        BallPin,
        BallSurface,
        PinPin,
        PinSurface,
        Other
    }

    private readonly struct PairKey : IEquatable<PairKey>
    {
        public readonly int first;
        public readonly int second;

        public PairKey(UnityEngine.Object firstObject, UnityEngine.Object secondObject)
        {
            int firstId = firstObject != null ? firstObject.GetInstanceID() : 0;
            int secondId = secondObject != null ? secondObject.GetInstanceID() : 0;
            first = Mathf.Min(firstId, secondId);
            second = Mathf.Max(firstId, secondId);
        }

        public bool Equals(PairKey other) => first == other.first && second == other.second;
        public override bool Equals(object obj) => obj is PairKey other && Equals(other);
        public override int GetHashCode() => (first * 397) ^ second;
    }

    private sealed class ImpactReport
    {
        public PairKey pair;
        public UnityEngine.Object firstObject;
        public UnityEngine.Object secondObject;
        public SoundCueSO cue;
        public ImpactAudioProfileSO strengthProfile;
        public ImpactCategory category;
        public Vector3 position;
        public float impulse;
        public float volumeMultiplier;
    }

    private sealed class PairState
    {
        public UnityEngine.Object firstObject;
        public UnityEngine.Object secondObject;
        public double nextAllowedTime;
        public double lastSeenTime;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        pendingSteps.Clear();
        pairStates.Clear();
        orderedSteps.Clear();
        orderedReports.Clear();
        stalePairs.Clear();
        worldEligible = false;
        lastFlushFrame = -1;
    }

    public static void SetWorldEligible(bool eligible)
    {
        if (worldEligible == eligible) return;

        worldEligible = eligible;
        if (!eligible)
        {
            pendingSteps.Clear();
            pairStates.Clear();
        }
    }

    public void SetInstanceEligible(bool eligible)
    {
        instanceEligible = eligible;
    }

    private void OnDisable()
    {
        if (requireInstanceEligibility) instanceEligible = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!worldEligible || !IsInstanceEligible || profile == null || collision == null) return;

        bool hasContact = collision.contactCount > 0;
        ContactPoint firstContact = hasContact ? collision.GetContact(0) : default;
        Collider ownCollider = hasContact ? firstContact.thisCollider : GetComponent<Collider>();
        Collider otherCollider = hasContact ? firstContact.otherCollider : collision.collider;
        if (ownCollider == null || otherCollider == null || ownCollider.isTrigger || otherCollider.isTrigger) return;
        if (!IsColliderInstanceEligible(otherCollider)) return;

        UnityEngine.Object ownIdentity = GetCollisionIdentity(ownCollider);
        UnityEngine.Object otherIdentity = GetCollisionIdentity(otherCollider);
        if (ownIdentity == null || otherIdentity == null || ownIdentity == otherIdentity) return;

        ImpactAudioProfileSO otherProfile = FindProfile(otherCollider);
        ImpactAudioMaterial otherMaterial = GetMaterial(otherCollider, otherProfile);
        if (!TryResolveResponse(profile, otherProfile, otherMaterial,
                out SoundCueSO cue, out ImpactAudioProfileSO strengthProfile, out float volumeMultiplier)) return;

        float impulse = collision.impulse.magnitude;
        if (impulse < strengthProfile.MinimumImpulse) return;

        PairKey pair = new PairKey(ownIdentity, otherIdentity);
        long step = BitConverter.DoubleToInt64Bits(Time.fixedTimeAsDouble);
        if (!pendingSteps.TryGetValue(step, out Dictionary<PairKey, ImpactReport> reports))
        {
            reports = new Dictionary<PairKey, ImpactReport>();
            pendingSteps.Add(step, reports);
        }

        if (reports.TryGetValue(pair, out ImpactReport existing) && existing.impulse >= impulse) return;

        reports[pair] = new ImpactReport
        {
            pair = pair,
            firstObject = ownIdentity,
            secondObject = otherIdentity,
            cue = cue,
            strengthProfile = strengthProfile,
            category = GetCategory(profile.Material, otherMaterial),
            position = hasContact ? firstContact.point : transform.position,
            impulse = impulse,
            volumeMultiplier = volumeMultiplier
        };
    }

    private void Update()
    {
        if (GameplayPause.IsPaused(this)) return;
        // Unity completes every FixedUpdate/physics step scheduled for this rendered frame before Update.
        // Step-keyed buckets therefore deduplicate complete steps without merging low-frame-rate catch-up steps.
        if (lastFlushFrame == Time.frameCount) return;
        lastFlushFrame = Time.frameCount;
        FlushPendingSteps();
        CleanupPairStates();
    }

    private static void FlushPendingSteps()
    {
        if (!worldEligible || pendingSteps.Count == 0)
        {
            pendingSteps.Clear();
            return;
        }

        orderedSteps.Clear();
        foreach (long step in pendingSteps.Keys) orderedSteps.Add(step);
        orderedSteps.Sort();

        foreach (long step in orderedSteps)
        {
            Dictionary<PairKey, ImpactReport> reports = pendingSteps[step];
            orderedReports.Clear();
            foreach (ImpactReport report in reports.Values) orderedReports.Add(report);
            orderedReports.Sort((a, b) => b.impulse.CompareTo(a.impulse));

            int ballPinCount = 0;
            int ballSurfaceCount = 0;
            int pinPinCount = 0;
            int pinSurfaceCount = 0;
            int otherCount = 0;

            foreach (ImpactReport report in orderedReports)
            {
                if (!HasCategoryCapacity(report.category, ballPinCount, ballSurfaceCount,
                        pinPinCount, pinSurfaceCount, otherCount)) continue;

                double now = Time.unscaledTimeAsDouble;
                if (pairStates.TryGetValue(report.pair, out PairState state) && now < state.nextAllowedTime) continue;

                float volume = report.strengthProfile.EvaluateVolume(report.impulse, report.volumeMultiplier);
                if (volume <= 0f || GameAudio.Instance == null ||
                    !GameAudio.Instance.Play(report.cue, report.position, volume)) continue;

                if (state == null)
                {
                    state = new PairState();
                    pairStates.Add(report.pair, state);
                }

                state.firstObject = report.firstObject;
                state.secondObject = report.secondObject;
                state.nextAllowedTime = now + PairCooldownSeconds;
                state.lastSeenTime = now;
                IncrementCategory(report.category, ref ballPinCount, ref ballSurfaceCount,
                    ref pinPinCount, ref pinSurfaceCount, ref otherCount);
            }
        }

        pendingSteps.Clear();
    }

    private static bool HasCategoryCapacity(ImpactCategory category, int ballPinCount,
        int ballSurfaceCount, int pinPinCount, int pinSurfaceCount, int otherCount)
    {
        switch (category)
        {
            case ImpactCategory.BallPin: return ballPinCount < 4;
            case ImpactCategory.BallSurface: return ballSurfaceCount < 2;
            case ImpactCategory.PinPin: return pinPinCount < 4;
            case ImpactCategory.PinSurface: return pinSurfaceCount < 4;
            default: return otherCount < 2;
        }
    }

    private static void IncrementCategory(ImpactCategory category, ref int ballPinCount,
        ref int ballSurfaceCount, ref int pinPinCount, ref int pinSurfaceCount, ref int otherCount)
    {
        switch (category)
        {
            case ImpactCategory.BallPin: ballPinCount++; break;
            case ImpactCategory.BallSurface: ballSurfaceCount++; break;
            case ImpactCategory.PinPin: pinPinCount++; break;
            case ImpactCategory.PinSurface: pinSurfaceCount++; break;
            default: otherCount++; break;
        }
    }

    private static void CleanupPairStates()
    {
        if (pairStates.Count == 0) return;

        double oldestAllowed = Time.unscaledTimeAsDouble - PairStateLifetimeSeconds;
        stalePairs.Clear();
        foreach (KeyValuePair<PairKey, PairState> pair in pairStates)
        {
            PairState state = pair.Value;
            if (state.firstObject == null || state.secondObject == null || state.lastSeenTime < oldestAllowed)
                stalePairs.Add(pair.Key);
        }

        foreach (PairKey pair in stalePairs) pairStates.Remove(pair);
    }

    private static bool TryResolveResponse(ImpactAudioProfileSO ownProfile,
        ImpactAudioProfileSO otherProfile, ImpactAudioMaterial otherMaterial, out SoundCueSO cue,
        out ImpactAudioProfileSO strengthProfile, out float volumeMultiplier)
    {
        if (ownProfile.TryGetResponse(otherMaterial, out ImpactAudioResponse ownResponse))
        {
            cue = ownResponse.cue;
            strengthProfile = ownProfile;
            volumeMultiplier = ownResponse.volumeMultiplier;
            return true;
        }

        if (otherProfile != null && otherProfile.TryGetResponse(ownProfile.Material, out ImpactAudioResponse otherResponse))
        {
            cue = otherResponse.cue;
            strengthProfile = otherProfile;
            volumeMultiplier = otherResponse.volumeMultiplier;
            return true;
        }

        cue = null;
        strengthProfile = null;
        volumeMultiplier = 0f;
        return false;
    }

    private static UnityEngine.Object GetCollisionIdentity(Collider collider)
    {
        return collider.attachedRigidbody != null ? collider.attachedRigidbody : collider;
    }

    private static ImpactAudioProfileSO FindProfile(Collider collider)
    {
        PhysicsImpactAudio reporter = collider.GetComponentInParent<PhysicsImpactAudio>();
        if (reporter != null && reporter.profile != null) return reporter.profile;

        AudioSurface surface = collider.GetComponentInParent<AudioSurface>();
        return surface != null ? surface.ImpactProfile : null;
    }

    private static bool IsColliderInstanceEligible(Collider collider)
    {
        PhysicsImpactAudio reporter = collider.GetComponentInParent<PhysicsImpactAudio>();
        return reporter == null || reporter.IsInstanceEligible;
    }

    private static ImpactAudioMaterial GetMaterial(Collider collider, ImpactAudioProfileSO resolvedProfile)
    {
        if (resolvedProfile != null) return resolvedProfile.Material;
        if (collider.GetComponentInParent<PinBase>() != null) return ImpactAudioMaterial.Pin;
        if (collider.CompareTag("Ball")) return ImpactAudioMaterial.Ball;
        return ImpactAudioMaterial.Unknown;
    }

    private static ImpactCategory GetCategory(ImpactAudioMaterial first, ImpactAudioMaterial second)
    {
        bool hasBall = first == ImpactAudioMaterial.Ball || second == ImpactAudioMaterial.Ball;
        bool hasPin = first == ImpactAudioMaterial.Pin || second == ImpactAudioMaterial.Pin;
        bool firstSurface = IsSurface(first);
        bool secondSurface = IsSurface(second);

        if (hasBall && hasPin) return ImpactCategory.BallPin;
        if (first == ImpactAudioMaterial.Pin && second == ImpactAudioMaterial.Pin) return ImpactCategory.PinPin;
        if (hasBall && (firstSurface || secondSurface)) return ImpactCategory.BallSurface;
        if (hasPin && (firstSurface || secondSurface)) return ImpactCategory.PinSurface;
        return ImpactCategory.Other;
    }

    private static bool IsSurface(ImpactAudioMaterial material)
    {
        return material == ImpactAudioMaterial.Lane || material == ImpactAudioMaterial.Gutter ||
               material == ImpactAudioMaterial.Pit || material == ImpactAudioMaterial.Obstacle;
    }
}
