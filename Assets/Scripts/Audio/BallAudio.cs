using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[DisallowMultipleComponent]
[RequireComponent(typeof(BallController))]
public sealed class BallAudio : MonoBehaviour
{
    private const int MaximumAudibleRollingLoops = 3;
    private const string RollingSourceObjectName = "Rolling Audio";
    private static readonly HashSet<BallAudio> rollingInstances = new HashSet<BallAudio>();
    private static readonly List<BallAudio> rankedRollingInstances = new List<BallAudio>();
    private static AudioListener cachedListener;

    [SerializeField] private SoundCueSO releaseCue;
    [SerializeField, Range(0f, 1f)] private float minimumReleaseVolume = 0.8f;
    [SerializeField, Range(-0.25f, 0.75f), Tooltip("Positive values start the clip before physics release; negative values delay it.")]
    private float releaseSoundLeadTime = 0.56f;

    [Header("Rolling")]
    [SerializeField] private SoundCueSO rollingCue;
    [SerializeField, Range(0f, 1f)] private float minimumSupportUpDot = 0.45f;
    [SerializeField, Min(0f)] private float rollingStartSpeed = 1f;
    [SerializeField, Min(0f)] private float rollingStopSpeed = 0.4f;
    [SerializeField, Min(0.01f)] private float fullRollingVolumeSpeed = 24f;
    [SerializeField, Min(0.01f)] private float rollingFadeInTime = 0.1f;
    [SerializeField, Min(0.01f)] private float rollingFadeOutTime = 0.12f;

    private BallController ballController;
    private Rigidbody ballRigidbody;
    private AudioSource rollingSource;
    private Coroutine scheduledPlayback;
    private bool isPrimaryBall;
    private bool releasePlayedForCurrentThrow;
    private bool rollingSessionActive;
    private bool hasRollingVoice;
    private float rollingTargetVolume;
    private double rollingSessionStartedAt;
    private int previousRollingVariant = -1;
    private readonly System.Random rollingRandom = new System.Random();
    private readonly Dictionary<Collider, SupportContact> supportContacts = new Dictionary<Collider, SupportContact>();
    private readonly List<Collider> staleSupports = new List<Collider>();

    private sealed class SupportContact
    {
        public Rigidbody body;
        public Vector3 normal;
        public double lastSeen;
    }

    private void Awake()
    {
        isPrimaryBall = true;
        ballController = GetComponent<BallController>();
        ballRigidbody = GetComponent<Rigidbody>();

        Transform sourceTransform = transform.Find(RollingSourceObjectName);
        if (sourceTransform == null)
        {
            var sourceObject = new GameObject(RollingSourceObjectName);
            sourceTransform = sourceObject.transform;
            sourceTransform.SetParent(transform, false);
        }

        rollingSource = sourceTransform.GetComponent<AudioSource>();
        if (rollingSource == null) rollingSource = sourceTransform.gameObject.AddComponent<AudioSource>();
        ResetRollingSource();
    }

    private void OnEnable()
    {
        if (ballController == null) ballController = GetComponent<BallController>();
        rollingInstances.Add(this);
        SubscribePrimaryEvents();
        ballController.ResetPerformed += InvalidateRollingState;
        if (GameAudio.Instance != null) GameAudio.Instance.WorldAudioStopped += InvalidateRollingState;
    }

    private void OnDisable()
    {
        if (ballController != null)
        {
            UnsubscribePrimaryEvents();
            ballController.ResetPerformed -= InvalidateRollingState;
        }

        if (GameAudio.Instance != null) GameAudio.Instance.WorldAudioStopped -= InvalidateRollingState;

        rollingInstances.Remove(this);
        CancelScheduledPlayback();
        InvalidateRollingState();
    }

    private void FixedUpdate()
    {
        PruneStaleSupports();

        bool hasMovementRole = isPrimaryBall ? ballController.IsLaunched : true;
        bool canRoll = hasMovementRole && !ballRigidbody.isKinematic &&
                       ballRigidbody.constraints != RigidbodyConstraints.FreezeAll &&
                       !ballRigidbody.IsSleeping() && supportContacts.Count > 0;
        float tangentialSpeed = canRoll ? GetMaximumTangentialSpeed() : 0f;

        if (!rollingSessionActive && tangentialSpeed >= rollingStartSpeed)
        {
            rollingSessionActive = BeginRollingSession();
        }
        else if (rollingSessionActive && (!canRoll || tangentialSpeed <= rollingStopSpeed))
        {
            rollingSessionActive = false;
        }

        rollingTargetVolume = GetRollingTargetVolume(tangentialSpeed);
        RebalanceRollingVoices();
        UpdateRollingSource(tangentialSpeed);
    }

    public void InitializeAsSecondaryBall()
    {
        isPrimaryBall = false;
        UnsubscribePrimaryEvents();
        CancelScheduledPlayback();
        InvalidateRollingState();
    }

    public void InvalidateForMovement()
    {
        InvalidateRollingState();
    }

    private void OnCollisionEnter(Collision collision)
    {
        RefreshSupport(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        RefreshSupport(collision);
    }

    private void OnCollisionExit(Collision collision)
    {
        supportContacts.Remove(collision.collider);
    }

    private void OnReleaseScheduled(float launchSpeed, float timeUntilRelease)
    {
        CancelScheduledPlayback();
        releasePlayedForCurrentThrow = false;

        float delay = Mathf.Max(0f, timeUntilRelease - releaseSoundLeadTime);
        if (delay <= 0f)
        {
            releasePlayedForCurrentThrow = PlayRelease(launchSpeed);
        }
        else
        {
            scheduledPlayback = StartCoroutine(PlayReleaseAfterDelay(delay, launchSpeed));
        }
    }

    private void OnReleased(float launchSpeed)
    {
        // Direct physics launches that did not run the presentation schedule still get audio.
        if (!releasePlayedForCurrentThrow && scheduledPlayback == null) PlayRelease(launchSpeed);
    }

    private IEnumerator PlayReleaseAfterDelay(float delay, float launchSpeed)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        scheduledPlayback = null;
        releasePlayedForCurrentThrow = PlayRelease(launchSpeed);
    }

    private bool PlayRelease(float launchSpeed)
    {
        float speedAmount = 1f;
        ThrowTuningDataSO tuning = ballController.tuningData;
        if (tuning != null)
        {
            speedAmount = Mathf.InverseLerp(tuning.forceMinSpeed, tuning.forceMaxSpeed, launchSpeed);
        }

        float volumeScale = Mathf.Lerp(minimumReleaseVolume, 1f, speedAmount);
        return GameAudio.Instance != null && GameAudio.Instance.Play(releaseCue, transform.position, volumeScale);
    }

    private void RefreshSupport(Collision collision)
    {
        Collider surfaceCollider = collision.collider;
        if (surfaceCollider == null) return;

        AudioSurface surface = surfaceCollider.GetComponentInParent<AudioSurface>();
        if (surface == null || !surface.SupportsRolling) return;

        Vector3 up = Physics.gravity.sqrMagnitude > 0f ? -Physics.gravity.normalized : Vector3.up;
        float strongestUpDot = -1f;
        Vector3 supportNormal = up;
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;
            float upDot = Vector3.Dot(normal, up);
            if (upDot >= strongestUpDot)
            {
                strongestUpDot = upDot;
                supportNormal = normal;
            }
        }

        if (strongestUpDot < minimumSupportUpDot) return;

        if (!supportContacts.TryGetValue(surfaceCollider, out SupportContact contact))
        {
            contact = new SupportContact();
            supportContacts.Add(surfaceCollider, contact);
        }

        contact.body = surfaceCollider.attachedRigidbody;
        contact.normal = supportNormal.normalized;
        contact.lastSeen = Time.fixedTimeAsDouble;
    }

    private void PruneStaleSupports()
    {
        double oldestAllowed = Time.fixedTimeAsDouble - Time.fixedDeltaTime * 2.5f;
        staleSupports.Clear();
        foreach (KeyValuePair<Collider, SupportContact> pair in supportContacts)
        {
            if (pair.Key == null || pair.Value.lastSeen < oldestAllowed) staleSupports.Add(pair.Key);
        }

        foreach (Collider collider in staleSupports) supportContacts.Remove(collider);
    }

    private float GetMaximumTangentialSpeed()
    {
        float maximumSpeed = 0f;
        Vector3 point = ballRigidbody.worldCenterOfMass;
        foreach (SupportContact contact in supportContacts.Values)
        {
            Vector3 surfaceVelocity = contact.body != null ? contact.body.GetPointVelocity(point) : Vector3.zero;
            Vector3 relativeVelocity = ballRigidbody.GetPointVelocity(point) - surfaceVelocity;
            float speed = Vector3.ProjectOnPlane(relativeVelocity, contact.normal).magnitude;
            maximumSpeed = Mathf.Max(maximumSpeed, speed);
        }

        return maximumSpeed;
    }

    private bool BeginRollingSession()
    {
        if (rollingCue == null || rollingCue.output == null || rollingCue.isUI) return false;

        AudioClip clip = rollingCue.PickVariant(rollingRandom, previousRollingVariant, out int selected);
        if (clip == null || clip.loadState != AudioDataLoadState.Loaded) return false;

        ConfigureRollingSource(rollingCue.output, clip);
        previousRollingVariant = selected;
        rollingSessionStartedAt = Time.unscaledTimeAsDouble;
        return true;
    }

    private void UpdateRollingSource(float tangentialSpeed)
    {
        float targetVolume = hasRollingVoice ? rollingTargetVolume : 0f;
        if (hasRollingVoice && rollingSessionActive && !rollingSource.isPlaying) StartRollingSourceAtSessionPhase();

        float fadeTime = targetVolume > rollingSource.volume ? rollingFadeInTime : rollingFadeOutTime;
        rollingSource.volume = Mathf.MoveTowards(rollingSource.volume, targetVolume, Time.fixedDeltaTime / fadeTime);

        if (rollingCue != null)
        {
            float speedAmount = GetRollingSpeedAmount(tangentialSpeed);
            float lowPitch = Mathf.Clamp(Mathf.Min(rollingCue.pitchRange.x, rollingCue.pitchRange.y), 0.1f, 3f);
            float highPitch = Mathf.Clamp(Mathf.Max(rollingCue.pitchRange.x, rollingCue.pitchRange.y), 0.1f, 3f);
            float targetPitch = Mathf.Lerp(lowPitch, highPitch, speedAmount);
            rollingSource.pitch = Mathf.MoveTowards(rollingSource.pitch, targetPitch, Time.fixedDeltaTime * 2f);
        }

        if ((!rollingSessionActive || !hasRollingVoice) && rollingSource.isPlaying && rollingSource.volume <= 0f)
        {
            rollingSource.Stop();
            if (!rollingSessionActive) rollingSource.clip = null;
        }
    }

    private float GetRollingTargetVolume(float tangentialSpeed)
    {
        return rollingSessionActive && rollingCue != null
            ? rollingCue.gain * GetRollingSpeedAmount(tangentialSpeed)
            : 0f;
    }

    private float GetRollingSpeedAmount(float tangentialSpeed)
    {
        return Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(rollingStopSpeed, fullRollingVolumeSpeed, tangentialSpeed));
    }

    private void StartRollingSourceAtSessionPhase()
    {
        AudioClip clip = rollingSource.clip;
        if (clip == null) return;

        rollingSource.Play();
        if (clip.length > 0f)
        {
            rollingSource.time = (float)((Time.unscaledTimeAsDouble - rollingSessionStartedAt) % clip.length);
        }
    }

    private void ConfigureRollingSource(AudioMixerGroup output, AudioClip clip)
    {
        rollingSource.Stop();
        rollingSource.clip = clip;
        rollingSource.outputAudioMixerGroup = output;
        rollingSource.loop = true;
        rollingSource.playOnAwake = false;
        rollingSource.volume = 0f;
        rollingSource.pitch = 1f;
        rollingSource.priority = Mathf.Clamp(rollingCue.priority, 0, 256);
        rollingSource.spatialBlend = Mathf.Clamp01(rollingCue.spatialBlend);
        rollingSource.dopplerLevel = 0f;
        rollingSource.rolloffMode = AudioRolloffMode.Logarithmic;
        rollingSource.minDistance = Mathf.Max(0.01f, rollingCue.minDistance);
        rollingSource.maxDistance = Mathf.Max(rollingSource.minDistance, rollingCue.maxDistance);
        rollingSource.ignoreListenerPause = false;
    }

    private void InvalidateRollingState()
    {
        supportContacts.Clear();
        rollingSessionActive = false;
        rollingTargetVolume = 0f;
        hasRollingVoice = false;
        StopRollingImmediate();
        RebalanceRollingVoices();
    }

    private void StopRollingImmediate()
    {
        if (rollingSource == null) return;
        rollingSource.Stop();
        ResetRollingSource();
    }

    private void ResetRollingSource()
    {
        rollingSource.clip = null;
        rollingSource.outputAudioMixerGroup = null;
        rollingSource.playOnAwake = false;
        rollingSource.loop = true;
        rollingSource.volume = 0f;
        rollingSource.pitch = 1f;
        rollingSource.priority = 128;
        rollingSource.spatialBlend = 1f;
        rollingSource.dopplerLevel = 0f;
        rollingSource.minDistance = 1f;
        rollingSource.maxDistance = 500f;
        rollingSource.ignoreListenerPause = false;
    }

    private void SubscribePrimaryEvents()
    {
        if (!isPrimaryBall || ballController == null) return;
        ballController.ReleaseScheduled -= OnReleaseScheduled;
        ballController.ReleaseSchedulingCancelled -= CancelScheduledPlayback;
        ballController.Released -= OnReleased;
        ballController.ReleaseScheduled += OnReleaseScheduled;
        ballController.ReleaseSchedulingCancelled += CancelScheduledPlayback;
        ballController.Released += OnReleased;
    }

    private void UnsubscribePrimaryEvents()
    {
        if (ballController == null) return;
        ballController.ReleaseScheduled -= OnReleaseScheduled;
        ballController.ReleaseSchedulingCancelled -= CancelScheduledPlayback;
        ballController.Released -= OnReleased;
    }

    private static void RebalanceRollingVoices()
    {
        rankedRollingInstances.Clear();
        foreach (BallAudio instance in rollingInstances)
        {
            if (instance != null && instance.isActiveAndEnabled && instance.RollingAudibility > 0f)
                rankedRollingInstances.Add(instance);
        }

        rankedRollingInstances.Sort((a, b) =>
        {
            int scoreComparison = b.RollingAudibility.CompareTo(a.RollingAudibility);
            return scoreComparison != 0 ? scoreComparison : a.GetInstanceID().CompareTo(b.GetInstanceID());
        });

        int audibleCount = Mathf.Min(MaximumAudibleRollingLoops, rankedRollingInstances.Count);
        foreach (BallAudio instance in rollingInstances)
        {
            if (instance == null) continue;
            int rank = rankedRollingInstances.IndexOf(instance);
            instance.SetRollingVoice(rank >= 0 && rank < audibleCount);
        }
    }

    private float RollingAudibility
    {
        get
        {
            float candidateVolume = rollingSessionActive ? rollingTargetVolume : rollingSource.volume;
            if (candidateVolume <= 0f) return 0f;

            if (cachedListener == null) cachedListener = FindFirstObjectByType<AudioListener>();
            if (cachedListener == null) return candidateVolume;

            float minimumDistance = rollingCue != null ? Mathf.Max(0.01f, rollingCue.minDistance) : 1f;
            float distanceRatio = Mathf.Max(1f,
                Vector3.Distance(transform.position, cachedListener.transform.position) / minimumDistance);
            return candidateVolume / (distanceRatio * distanceRatio);
        }
    }

    private void SetRollingVoice(bool permitted)
    {
        if (hasRollingVoice == permitted) return;
        hasRollingVoice = permitted;
        if (!permitted && rollingSource != null && rollingSource.isPlaying)
        {
            rollingSource.Stop();
            rollingSource.volume = 0f;
        }
    }

    private void CancelScheduledPlayback()
    {
        if (scheduledPlayback != null)
        {
            StopCoroutine(scheduledPlayback);
            scheduledPlayback = null;
        }

        releasePlayedForCurrentThrow = false;
    }
}
