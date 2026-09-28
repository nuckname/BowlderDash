using UnityEngine;
using UnityEngine.Audio;

// Owned by GameAudio. It is intentionally not a second persistent singleton.
public sealed class GameMusic : MonoBehaviour
{
    private const int PhaseCount = 4;
    [SerializeField] private MusicProfileSO profile;

    private readonly AudioSource[] sources = new AudioSource[PhaseCount];
    private readonly float[] gains = new float[PhaseCount];
    private readonly float[] fadeStarts = new float[PhaseCount];
    private int target = -1;
    private int pending = -1;
    private int warned = -1;
    private float elapsed;
    private float duration;
    private float pendingFadeOverride = -1f;
    private bool initialized;
    private AudioMixer mixer;

    public MusicPhase? TargetPhase => target < 0 ? (MusicPhase?)null : (MusicPhase)target;
    public MusicPhase? PendingPhase => pending < 0 ? (MusicPhase?)null : (MusicPhase)pending;
    public float GetFadeGain(MusicPhase phase) => IsValid(phase) ? gains[(int)phase] : 0f;

    internal void Initialize(GameAudio owner, AudioMixer ownerMixer)
    {
        if (initialized || owner == null || GameAudio.Instance != owner) return;
        mixer = ownerMixer;
        initialized = true;
        for (int i = 0; i < PhaseCount; i++)
        {
            if (sources[i] != null) continue;
            var child = new GameObject($"Music {(MusicPhase)i}");
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.spatialize = false;
            source.dopplerLevel = 0f;
            source.reverbZoneMix = 0f;
            source.pitch = 1f;
            source.volume = 0f;
            source.ignoreListenerPause = true;
            sources[i] = source;
        }
    }

    public bool RequestPhase(MusicPhase phase) => RequestPhase(phase, -1f);

    // A nonnegative fade override is useful for previewing instant transitions.
    public bool RequestPhase(MusicPhase phase, float fadeOverride)
    {
        if (!initialized || GameAudio.Instance == null || GameAudio.Instance.Music != this || !IsValid(phase)) return false;
        int next = (int)phase;
        if (next == target)
        {
            pending = -1;
            return true;
        }
        if (next == pending) return false;
        if (!TryPrepare(next))
        {
            pending = next;
            pendingFadeOverride = fadeOverride;
            return false;
        }

        pending = -1;
        warned = -1;
        target = next;
        elapsed = 0f;
        duration = fadeOverride >= 0f ? fadeOverride : Mathf.Max(0f, profile.fadeDuration);
        for (int i = 0; i < PhaseCount; i++) fadeStarts[i] = gains[i];
        if (duration <= 0f) ApplyFade(1f);
        return true;
    }

    private bool TryPrepare(int index)
    {
        if (profile == null || profile.output == null || mixer == null || profile.output.audioMixer != mixer)
        {
            WarnOnce(index, "Music profile or Music mixer routing is missing or invalid.");
            return false;
        }
        var track = profile.GetTrack((MusicPhase)index);
        if (track.clip == null)
        {
            WarnOnce(index, $"Music clip is missing for {(MusicPhase)index}.");
            return false;
        }
        if (track.clip.loadState == AudioDataLoadState.Unloaded && !track.clip.LoadAudioData())
        {
            WarnOnce(index, $"Music clip could not start loading for {(MusicPhase)index}.");
            return false;
        }
        if (track.clip.loadState == AudioDataLoadState.Failed)
        {
            WarnOnce(index, $"Music clip failed to load for {(MusicPhase)index}.");
            return false;
        }
        if (track.clip.loadState != AudioDataLoadState.Loaded) return false;

        var source = sources[index];
        if (source.clip != track.clip || !source.isPlaying)
        {
            source.Stop();
            source.clip = track.clip;
            source.volume = 0f;
            gains[index] = 0f;
            source.Play();
        }
        source.outputAudioMixerGroup = profile.output;
        source.ignoreListenerPause = true;
        return true;
    }

    private void Update()
    {
        if (!initialized || GameAudio.Instance == null || GameAudio.Instance.Music != this) return;
        if (pending >= 0)
        {
            int next = pending;
            if (TryPrepare(next))
            {
                float fadeOverride = pendingFadeOverride;
                pending = -1;
                RequestPhase((MusicPhase)next, fadeOverride);
            }
        }
        if (target < 0 || duration <= 0f) return;
        elapsed = Mathf.Min(duration, elapsed + Time.unscaledDeltaTime);
        ApplyFade(elapsed / duration);
    }

    private void ApplyFade(float progress)
    {
        for (int i = 0; i < PhaseCount; i++)
        {
            float end = i == target ? 1f : 0f;
            gains[i] = Mathf.Lerp(fadeStarts[i], end, progress);
            var source = sources[i];
            if (source == null) continue;
            source.volume = gains[i] * (profile != null ? Mathf.Clamp01(profile.GetTrack((MusicPhase)i).gain) : 0f);
            if (gains[i] <= 0f && i != target && source.isPlaying)
            {
                source.Stop();
                source.clip = null;
            }
        }
    }

    private void WarnOnce(int index, string message)
    {
        if (warned == index) return;
        warned = index;
        Debug.LogWarning(message, this);
    }

    private static bool IsValid(MusicPhase phase) => (int)phase >= 0 && (int)phase < PhaseCount;

    internal void Shutdown()
    {
        initialized = false;
        target = pending = -1;
        for (int i = 0; i < PhaseCount; i++)
        {
            if (sources[i] != null)
            {
                sources[i].Stop();
                sources[i].clip = null;
            }
            gains[i] = fadeStarts[i] = 0f;
        }
    }

    private void OnEnable()
    {
        // Supports re-enabling the accepted service component in Play Mode.
        if (!initialized && GameAudio.Instance != null && GameAudio.Instance.gameObject == gameObject)
            Initialize(GameAudio.Instance, GameAudio.Instance.Mixer);
    }

    private void OnDisable() => Shutdown();
}
