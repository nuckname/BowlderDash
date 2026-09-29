using UnityEngine;
using UnityEngine.Audio;

// Owned by GameAudio. It is intentionally not a second persistent singleton.
public sealed class GameMusic : MonoBehaviour
{
    private const int PhaseCount = 4;
    private const int GameplayIndex = (int)MusicPhase.Gameplay;
    private const int ShopIndex = (int)MusicPhase.Shop;
    private const double PairScheduleLeadSeconds = 0.2d;
    private const double PairStartGraceSeconds = 0.5d;

    private enum PairState { None, Loading, Scheduled, Playing, Invalid }

    [SerializeField] private MusicProfileSO profile;

    private readonly AudioSource[] sources = new AudioSource[PhaseCount];
    private readonly float[] gains = new float[PhaseCount];
    private readonly float[] fadeStarts = new float[PhaseCount];
    private int target = -1;
    private int pending = -1;
    private int warnedPhases;
    private float elapsed;
    private float duration;
    private float pendingFadeOverride = -1f;
    private bool initialized;
    private bool pairWarned;
    private PairState pairState;
    private double pairStartDspTime;
    private AudioClip gameplayPairClip;
    private AudioClip shopPairClip;
    private AudioMixer mixer;

    public MusicPhase? TargetPhase => target < 0 ? (MusicPhase?)null : (MusicPhase)target;
    public MusicPhase? PendingPhase => pending < 0 ? (MusicPhase?)null : (MusicPhase)pending;
    public float GetFadeGain(MusicPhase phase) => IsValid(phase) ? gains[(int)phase] : 0f;
    public string GameplayShopPlaybackState => pairState.ToString();
    public bool IsPlaying(MusicPhase phase) => IsValid(phase) && sources[(int)phase] != null && sources[(int)phase].isPlaying;
    public float GetPlaybackSeconds(MusicPhase phase)
    {
        if (!IsValid(phase)) return 0f;
        var source = sources[(int)phase];
        return source != null && source.clip != null && source.clip.frequency > 0
            ? (float)source.timeSamples / source.clip.frequency : 0f;
    }

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
        warnedPhases &= ~(1 << next);
        target = next;
        elapsed = 0f;
        duration = fadeOverride >= 0f ? fadeOverride : Mathf.Max(0f, profile.fadeDuration);
        for (int i = 0; i < PhaseCount; i++) fadeStarts[i] = gains[i];
        if (duration <= 0f) ApplyFade(1f);
        return true;
    }

    private bool TryPrepare(int index)
    {
        if (IsPaired(index))
        {
            if (pairState == PairState.None) pairState = PairState.Loading;
            if (pairState == PairState.Loading || pairState == PairState.Scheduled)
            {
                AdvancePair();
                if (pairState != PairState.Playing && pairState != PairState.Invalid) return false;
            }
            if (pairState == PairState.Playing) return true;
        }

        return TryPrepareSolo(index);
    }

    private bool TryPrepareSolo(int index)
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
        source.outputAudioMixerGroup = profile.output;
        source.ignoreListenerPause = true;
        if (source.clip != track.clip || !source.isPlaying)
        {
            source.Stop();
            source.clip = track.clip;
            source.volume = gains[index] * Mathf.Clamp01(track.gain);
            source.Play();
        }
        return true;
    }

    private void AdvancePair()
    {
        if (pairState == PairState.Loading)
        {
            if (profile == null || profile.output == null || mixer == null || profile.output.audioMixer != mixer)
            {
                InvalidatePair("Music profile or Music mixer routing is missing or invalid.");
                return;
            }

            var gameplay = profile.gameplay.clip;
            var shop = profile.shop.clip;
            if (gameplay == null || shop == null)
            {
                InvalidatePair("Gameplay and Shop both need clips for synchronized playback.");
                return;
            }

            double gameplayPeriod = gameplay.frequency > 0 ? (double)gameplay.samples / gameplay.frequency : 0d;
            double shopPeriod = shop.frequency > 0 ? (double)shop.samples / shop.frequency : 0d;
            double periodTolerance = gameplay.frequency > 0 && shop.frequency > 0
                ? System.Math.Max(1d / gameplay.frequency, 1d / shop.frequency) : 0d;
            if (gameplayPeriod <= 0d || shopPeriod <= 0d ||
                System.Math.Abs(gameplayPeriod - shopPeriod) > periodTolerance)
            {
                InvalidatePair($"Gameplay and Shop imported loop periods differ ({gameplayPeriod:F6}s / {shopPeriod:F6}s).");
                return;
            }

            bool gameplayLoaded = LoadPairClip(gameplay, MusicPhase.Gameplay);
            if (pairState == PairState.Invalid) return;
            bool shopLoaded = LoadPairClip(shop, MusicPhase.Shop);
            if (pairState == PairState.Invalid || !gameplayLoaded || !shopLoaded) return;

            gameplayPairClip = gameplay;
            shopPairClip = shop;
            PreparePairSource(GameplayIndex, gameplay);
            PreparePairSource(ShopIndex, shop);
            pairStartDspTime = AudioSettings.dspTime + PairScheduleLeadSeconds;
            sources[GameplayIndex].PlayScheduled(pairStartDspTime);
            sources[ShopIndex].PlayScheduled(pairStartDspTime);
            pairState = PairState.Scheduled;
        }

        if (pairState != PairState.Scheduled && pairState != PairState.Playing) return;
        if (profile == null || profile.output == null || profile.output.audioMixer != mixer ||
            profile.gameplay.clip != gameplayPairClip || profile.shop.clip != shopPairClip ||
            sources[GameplayIndex].clip != gameplayPairClip || sources[ShopIndex].clip != shopPairClip ||
            sources[GameplayIndex].outputAudioMixerGroup != profile.output ||
            sources[ShopIndex].outputAudioMixerGroup != profile.output)
        {
            InvalidatePair("Gameplay or Shop clip/routing changed during synchronized playback.");
            return;
        }

        if (pairState == PairState.Scheduled)
        {
            if (AudioSettings.dspTime < pairStartDspTime) return;
            if (sources[GameplayIndex].isPlaying && sources[ShopIndex].isPlaying)
                pairState = PairState.Playing;
            else if (AudioSettings.dspTime > pairStartDspTime + PairStartGraceSeconds)
                InvalidatePair("Gameplay and Shop could not begin together at the scheduled audio time.");
        }
        else if (!sources[GameplayIndex].isPlaying || !sources[ShopIndex].isPlaying)
        {
            InvalidatePair("Gameplay or Shop stopped during synchronized playback.");
        }
    }

    private bool LoadPairClip(AudioClip clip, MusicPhase phase)
    {
        if (clip.loadState == AudioDataLoadState.Unloaded && !clip.LoadAudioData())
        {
            InvalidatePair($"Music clip could not start loading for {phase}.");
            return false;
        }
        if (clip.loadState == AudioDataLoadState.Failed)
        {
            InvalidatePair($"Music clip failed to load for {phase}.");
            return false;
        }
        return clip.loadState == AudioDataLoadState.Loaded;
    }

    private void PreparePairSource(int index, AudioClip clip)
    {
        var source = sources[index];
        source.Stop();
        source.clip = clip;
        source.outputAudioMixerGroup = profile.output;
        source.ignoreListenerPause = true;
        source.loop = true;
        source.pitch = 1f;
        source.volume = 0f;
        gains[index] = 0f;
    }

    private void InvalidatePair(string reason)
    {
        if (pairState == PairState.Invalid) return;
        pairState = PairState.Invalid;
        pairStartDspTime = 0d;
        gameplayPairClip = shopPairClip = null;
        foreach (int index in new[] { GameplayIndex, ShopIndex })
        {
            var source = sources[index];
            if (source == null || gains[index] > 0f) continue;
            source.Stop();
            source.clip = null;
        }
        if (!pairWarned)
        {
            pairWarned = true;
            Debug.LogWarning($"Synchronized Gameplay/Shop music unavailable: {reason} Falling back to individual tracks.", this);
        }
    }

    private void Update()
    {
        if (!initialized || GameAudio.Instance == null || GameAudio.Instance.Music != this) return;
        if (pairState == PairState.Loading || pairState == PairState.Scheduled || pairState == PairState.Playing)
            AdvancePair();
        // A failed pair can still keep its current audible phase alive while a solo clip loads.
        if (pairState == PairState.Invalid && IsPaired(target)) TryPrepareSolo(target);
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
            // The two treatments must advance even while their gain is zero.
            if (gains[i] <= 0f && i != target && source.isPlaying &&
                !(IsPaired(i) && (pairState == PairState.Scheduled || pairState == PairState.Playing)))
            {
                source.Stop();
                source.clip = null;
            }
        }
    }

    private void WarnOnce(int index, string message)
    {
        int bit = 1 << index;
        if ((warnedPhases & bit) != 0) return;
        warnedPhases |= bit;
        Debug.LogWarning(message, this);
    }

    private static bool IsValid(MusicPhase phase) => (int)phase >= 0 && (int)phase < PhaseCount;
    private static bool IsPaired(int index) => index == GameplayIndex || index == ShopIndex;

    internal void Shutdown()
    {
        initialized = false;
        target = pending = -1;
        pairState = PairState.None;
        pairWarned = false;
        pairStartDspTime = 0d;
        gameplayPairClip = shopPairClip = null;
        warnedPhases = 0;
        pendingFadeOverride = -1f;
        elapsed = duration = 0f;
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
