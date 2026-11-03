using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1000)]
public sealed class GameAudio : MonoBehaviour
{
    private const int InteractionUIVoiceLimit = 2;
    private const int MaximumCardDrawVoices = 5;
    private const int MaximumGameplayLoops = 2;

    // Each handle controls one session
    // reused sources cannot revive an old handle
    public readonly struct LoopHandle
    {
        private readonly GameAudio service;
        private readonly int slot;
        private readonly ulong generation;

        internal LoopHandle(GameAudio service, int slot, ulong generation)
        {
            this.service = service;
            this.slot = slot;
            this.generation = generation;
        }

        public bool IsValid => service != null && service.TryGetLoop(slot, generation, out _);

        public bool SetParameters(float volumeScale, float pitch, float fadeTime = 0.05f) =>
            service != null && service.SetLoopParameters(slot, generation, volumeScale, pitch, fadeTime);

        public bool Stop(float fadeOutTime = 0.05f) =>
            service != null && service.StopLoop(slot, generation, fadeOutTime);
    }

    public static GameAudio Instance { get; private set; }
    [SerializeField, Range(1, 21)] private int transientCapacity = 21;
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private GameMusic music;

    private sealed class Voice
    {
        public AudioSource source;
        public SoundCueSO cue;
        public float remaining;
        public bool reservedForCardDraw;
        public bool reservedForPauseMenu;
        public bool pausedForGameplay;
        public ulong playOrder;
        public MonoBehaviour owner;
        public bool hasOwner;
    }
    private sealed class CueState
    {
        public int previous = -1;
        public double nextPlay;
    }
    // Loops keep their own ownership and fade state separate from transient lifetimes
    private sealed class LoopVoice
    {
        public AudioSource source;
        public SoundCueSO cue;
        public MonoBehaviour owner;
        public int sceneHandle;
        public ulong generation;
        public bool stopping;
        public float fadeStartVolume;
        public float targetVolume;
        public float fadeDuration;
        public float fadeElapsed;
    }
    private Voice[] voices;
    private LoopVoice[] loops;
    private ulong nextPlayOrder;
    private ulong nextLoopGeneration;
    private double nextHoverTime;
    private bool ownsLeaderboardPause;
    private bool listenerPauseBeforeOwnedPause;
    private bool ownsGameplayPause;
    private MonoBehaviour gameplayPauseOwner;
    private int gameplayPauseSceneHandle;
    private MonoBehaviour leaderboardPauseOwner;
    private int leaderboardSceneHandle;
    private int musicSceneHandle;
    public event System.Action WorldAudioStopped;
    public bool IsGameplaySuspended => ownsGameplayPause;
    // Audio variation must not consume the random sequence used by cards grids or bosses.
    private readonly System.Random random = new System.Random();
    private readonly Dictionary<SoundCueSO, CueState> cueStates = new Dictionary<SoundCueSO, CueState>();

    public int ActiveVoiceCount
    {
        get
        {
            int count = 0;
            if (voices != null) foreach (var voice in voices) if (voice.cue != null) count++;
            return count;
        }
    }

    // A source finishing its stop fade still occupies a loop slot.
    public int ActiveLoopCount
    {
        get
        {
            int count = 0;
            if (loops != null) foreach (var loop in loops) if (loop.generation != 0) count++;
            return count;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        if (music == null) music = GetComponent<GameMusic>();
        if (music != null) music.Initialize(this, mixer);
        int gameplayCapacity = Mathf.Clamp(transientCapacity, 1, 21);
        voices = new Voice[gameplayCapacity + InteractionUIVoiceLimit];
        // Five draw voices cover the current clip/pitch/lifetime at 0.08 second intervals
        // Keep at least two ordinary slots when a smaller pool is configured
        int drawCapacity = Mathf.Min(MaximumCardDrawVoices, Mathf.Max(0, gameplayCapacity - InteractionUIVoiceLimit));
        for (int i = 0; i < gameplayCapacity; i++)
        {
            var child = new GameObject($"Transient {i + 1:00}");
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            ResetSource(source);
            voices[i] = new Voice { source = source, reservedForCardDraw = i >= gameplayCapacity - drawCapacity };
        }
        // Menu feedback cannot steal suspended gameplay voices from a full pool
        for (int i = 0; i < InteractionUIVoiceLimit; i++)
        {
            var child = new GameObject($"Pause Menu {i + 1:00}");
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            ResetSource(source);
            voices[gameplayCapacity + i] = new Voice { source = source, reservedForPauseMenu = true };
        }
        // Sustained feedback gets its own fixed pool so it cannot starve one-shot cues
        loops = new LoopVoice[MaximumGameplayLoops];
        for (int i = 0; i < loops.Length; i++)
        {
            var child = new GameObject($"Gameplay Loop {i + 1:00}");
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            ResetSource(source);
            loops[i] = new LoopVoice { source = source };
        }
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    // One source per transient: never stack unaccounted PlayOneShot voices.
    public bool Play(SoundCueSO cue, Vector3 position = default, float volumeScale = 1f) =>
        PlayTransient(cue, position, volumeScale, null);

    public bool PlayOwned(SoundCueSO cue, MonoBehaviour owner, Vector3 position = default) =>
        !ownsGameplayPause && !ownsLeaderboardPause && owner != null && owner.isActiveAndEnabled &&
        PlayTransient(cue, position, 1f, null, owner);

    public void StopOwnedTransients(MonoBehaviour owner)
    {
        if (voices == null) return;
        foreach (var voice in voices)
            if (voice.hasOwner && ReferenceEquals(voice.owner, owner)) Release(voice);
    }

    // Override pitch for ordered feedback without changing the shared cue's variation
    public bool PlayPitched(SoundCueSO cue, float pitch, Vector3 position = default, float volumeScale = 1f) =>
        IsFinite(pitch) && PlayTransient(cue, position, volumeScale, Mathf.Clamp(pitch, 0.1f, 3f));

    // Random and explicit-pitch playback share the same voice limits and priority rules
    private bool PlayTransient(SoundCueSO cue, Vector3 position, float volumeScale, float? pitch,
        MonoBehaviour owner = null)
    {
        if (!TryPrepareCue(cue, volumeScale, out var state, out float gain)) return false;

        int active = 0;
        int uiActive = 0;
        Voice available = null;
        Voice weakest = null;
        Voice weakestUI = null;
        foreach (var voice in voices)
        {
            if (voice.reservedForCardDraw) continue;
            bool suspendedGameplay = ownsGameplayPause && IsGameplayVoice(voice);
            if (voice.cue == cue && !suspendedGameplay) active++;
            bool canUseSlot = !voice.reservedForPauseMenu || (cue.isUI && ownsGameplayPause);
            if (voice.cue == null)
            {
                if (canUseSlot && available == null) available = voice;
                continue;
            }
            if (voice.cue.isUI && !suspendedGameplay) uiActive++;
            if (!canUseSlot || suspendedGameplay) continue;
            if (voice.cue.isUI)
            {
                if (voice.source.priority > Mathf.Clamp(cue.priority, 0, 256) &&
                    (weakestUI == null || voice.source.priority > weakestUI.source.priority)) weakestUI = voice;
            }
            if (voice.source.priority > Mathf.Clamp(cue.priority, 0, 256) &&
                (weakest == null || voice.source.priority > weakest.source.priority ||
                 (voice.source.priority == weakest.source.priority && voice.source.volume < weakest.source.volume)))
                weakest = voice;
        }
        if (active >= Mathf.Max(1, cue.voiceLimit)) return false;
        // Hover and click share the two-voice UI budget thus clicks may replace hover.
        var slot = cue.isUI && uiActive >= InteractionUIVoiceLimit ? weakestUI : available ?? weakest;
        if (slot == null) return false;
        if (!PlayVoice(slot, cue, state, position, gain, pitch)) return false;
        slot.owner = owner;
        slot.hasOwner = owner != null;
        return true;
    }

    public bool PlayUICardDraw(SoundCueSO cue)
    {
        // Draw feedback belongs to "gameplay", even though it uses the 2D UI mixer route
        if (cue == null || !cue.isUI || ownsLeaderboardPause || ownsGameplayPause ||
            !TryPrepareCue(cue, 1f, out var state, out float gain)) return false;

        int active = 0;
        Voice available = null;
        Voice oldest = null;
        Voice oldestForCue = null;
        foreach (var voice in voices)
        {
            if (!voice.reservedForCardDraw) continue;
            if (voice.cue == null) { if (available == null) available = voice; continue; }
            if (oldest == null || voice.playOrder < oldest.playOrder) oldest = voice;
            if (voice.cue == cue)
            {
                active++;
                if (oldestForCue == null || voice.playOrder < oldestForCue.playOrder) oldestForCue = voice;
            }
        }
        Voice slot = active >= Mathf.Max(1, cue.voiceLimit) ? oldestForCue : available ?? oldest;
        return slot != null && PlayVoice(slot, cue, state, default, gain);
    }

    // Keep one loop per owner, repeat requests reuse playback instead of restarting it
    public LoopHandle BeginLoop(SoundCueSO cue, MonoBehaviour owner, float volumeScale = 1f,
        float pitch = 1f, float fadeInTime = 0.05f)
    {
        if (Instance != this || !isActiveAndEnabled || loops == null || ownsLeaderboardPause || ownsGameplayPause ||
            AudioListener.pause || owner == null || !owner.isActiveAndEnabled ||
            cue == null || cue.isUI || cue.output == null || !IsFinite(cue.gain) ||
            !IsFinite(volumeScale) || !IsFinite(pitch) || !IsFinite(fadeInTime)) return default;

        Scene scene = owner.gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded) return default;
        float gain = Mathf.Clamp01(cue.gain * Mathf.Clamp01(volumeScale));
        if (gain <= 0f) return default;

        int slot = -1;
        int fadingSlot = -1;
        int activeForCue = 0;
        for (int i = 0; i < loops.Length; i++)
        {
            var loop = loops[i];
            if (loop.generation != 0 && !HasLiveLoopOwner(loop)) ReleaseLoop(loop);
            if (loop.generation == 0)
            {
                if (slot < 0) slot = i;
                continue;
            }
            if (loop.owner == owner)
            {
                if (!loop.stopping && loop.cue == cue)
                {
                    SetLoopParameters(i, loop.generation, volumeScale, pitch, fadeInTime);
                    return new LoopHandle(this, i, loop.generation);
                }
                slot = i;
            }
            else if (!loop.stopping && loop.cue == cue) activeForCue++;
            if (loop.stopping && fadingSlot < 0) fadingSlot = i;
        }

        if (slot < 0) slot = fadingSlot;
        if (slot < 0 || activeForCue >= Mathf.Max(1, cue.voiceLimit) ||
            !TryPrepareCue(cue, volumeScale, out var state, out gain)) return default;
        var clip = cue.PickVariant(random, state.previous, out int selected);
        if (clip == null || clip.loadState != AudioDataLoadState.Loaded) return default;

        var voice = loops[slot];
        ReleaseLoop(voice);
        ConfigureSource(voice.source, cue, clip, owner.transform.position, 0f, Mathf.Clamp(pitch, 0.1f, 3f));
        voice.source.loop = true;
        voice.owner = owner;
        voice.sceneHandle = scene.handle;
        voice.cue = cue;
        if (++nextLoopGeneration == 0) ++nextLoopGeneration;
        voice.generation = nextLoopGeneration;
        FadeLoop(voice, gain, fadeInTime);
        voice.source.Play();
        state.previous = selected;
        state.nextPlay = Time.unscaledTimeAsDouble + Mathf.Max(0f, cue.cooldown);
        cueStates[cue] = state;
        return new LoopHandle(this, slot, voice.generation);
    }

    // A caller must still own this exact session before touching its pooled source.
    private bool TryGetLoop(int slot, ulong generation, out LoopVoice voice)
    {
        voice = null;
        if (Instance != this || !isActiveAndEnabled || loops == null ||
            slot < 0 || slot >= loops.Length || generation == 0) return false;
        var candidate = loops[slot];
        if (candidate.generation != generation || candidate.stopping) return false;
        if (!HasLiveLoopOwner(candidate))
        {
            ReleaseLoop(candidate);
            return false;
        }
        voice = candidate;
        return true;
    }

    // Adjust playback in place, preserving an existing fade when its target is unchanged
    private bool SetLoopParameters(int slot, ulong generation, float volumeScale, float pitch, float fadeTime)
    {
        if (!IsFinite(volumeScale) || !IsFinite(pitch) || !IsFinite(fadeTime) ||
            !TryGetLoop(slot, generation, out var voice) || !IsFinite(voice.cue.gain)) return false;
        voice.source.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
        float targetVolume = Mathf.Clamp01(voice.cue.gain * Mathf.Clamp01(volumeScale));
        if (targetVolume != voice.targetVolume || (fadeTime <= 0f && voice.source.volume != targetVolume))
            FadeLoop(voice, targetVolume, fadeTime);
        return true;
    }

    private bool StopLoop(int slot, ulong generation, float fadeOutTime)
    {
        if (!IsFinite(fadeOutTime) || !TryGetLoop(slot, generation, out var voice)) return false;
        // Invalidate the handle immediately even while the source finishes fading
        voice.stopping = true;
        if (fadeOutTime <= 0f || AudioListener.pause) ReleaseLoop(voice);
        else FadeLoop(voice, 0f, fadeOutTime);
        return true;
    }

    // A loop belongs to its owner's original scene and active gameplay lifetime
    private static bool HasLiveLoopOwner(LoopVoice voice) => voice.owner != null &&
        voice.owner.isActiveAndEnabled && voice.cue != null && voice.source != null &&
        voice.owner.gameObject.scene.IsValid() && voice.owner.gameObject.scene.isLoaded &&
        voice.owner.gameObject.scene.handle == voice.sceneHandle;

    // Start from the current level so an interrupted fade does not jump in volume
    private static void FadeLoop(LoopVoice voice, float volume, float duration)
    {
        voice.fadeStartVolume = voice.source.volume;
        voice.targetVolume = volume;
        voice.fadeDuration = Mathf.Max(0f, duration);
        voice.fadeElapsed = 0f;
        if (voice.fadeDuration == 0f) voice.source.volume = volume;
    }

    // Follow owners and advance fades on unscaled time, freezing fades with listener pause
    private void UpdateLoops()
    {
        if (loops == null) return;
        foreach (var voice in loops)
        {
            if (voice.generation == 0) continue;
            if (!HasLiveLoopOwner(voice)) { ReleaseLoop(voice); continue; }
            voice.source.transform.position = voice.owner.transform.position;
            if (AudioListener.pause || ownsGameplayPause) continue;
            voice.fadeElapsed += Time.unscaledDeltaTime;
            float t = voice.fadeDuration > 0f ? Mathf.Clamp01(voice.fadeElapsed / voice.fadeDuration) : 1f;
            voice.source.volume = Mathf.Lerp(voice.fadeStartVolume, voice.targetVolume, t);
            if (voice.stopping && t >= 1f) ReleaseLoop(voice);
        }
    }

    // Lifecycle cleanup overrides fades and frees every gameplay loop immediately
    private void StopGameplayLoops()
    {
        if (loops == null) return;
        foreach (var loop in loops) if (loop.generation != 0) ReleaseLoop(loop);
    }

    // Clear playback and ownership before this slot can serve another session
    private static void ReleaseLoop(LoopVoice voice)
    {
        if (voice.source != null)
        {
            ResetSource(voice.source);
            voice.source.transform.localPosition = Vector3.zero;
        }
        voice.cue = null;
        voice.owner = null;
        voice.sceneHandle = 0;
        voice.generation = 0;
        voice.stopping = false;
        voice.fadeStartVolume = 0f;
        voice.targetVolume = 0f;
        voice.fadeDuration = 0f;
        voice.fadeElapsed = 0f;
    }

    private bool TryPrepareCue(SoundCueSO cue, float volumeScale, out CueState state, out float gain)
    {
        state = null;
        gain = 0f;
        if (Instance != this || !isActiveAndEnabled || voices == null || cue == null || cue.output == null) return false;
        if (!cue.isUI && (ownsLeaderboardPause || ownsGameplayPause)) return false;
        if (!IsFinite(cue.gain) || !IsFinite(volumeScale)) return false;
        if (!cueStates.TryGetValue(cue, out state)) state = new CueState();
        if (Time.unscaledTimeAsDouble < state.nextPlay) return false;
        gain = Mathf.Clamp01(cue.gain * Mathf.Clamp01(volumeScale));
        return gain > 0f;
    }

    private bool PlayVoice(Voice slot, SoundCueSO cue, CueState state, Vector3 position, float gain, float? pitch = null)
    {
        var clip = cue.PickVariant(random, state.previous, out int selected);
        if (clip == null || clip.loadState != AudioDataLoadState.Loaded) return false;

        // Pooled lifetime must use the final pitch, whether requested or randomised
        float finalPitch = pitch ?? Mathf.Lerp(Mathf.Clamp(Mathf.Min(cue.pitchRange.x, cue.pitchRange.y), 0.1f, 3f),
                                  Mathf.Clamp(Mathf.Max(cue.pitchRange.x, cue.pitchRange.y), 0.1f, 3f),
                                  (float)random.NextDouble());
        if (!IsFinite(finalPitch)) return false;

        Release(slot);
        var source = slot.source;
        ConfigureSource(source, cue, clip, position, gain, finalPitch);
        slot.cue = cue;
        slot.remaining = clip.length / source.pitch + 0.1f;
        slot.playOrder = ++nextPlayOrder;
        source.Play();
        state.previous = selected;
        state.nextPlay = Time.unscaledTimeAsDouble + Mathf.Max(0f, cue.cooldown);
        cueStates[cue] = state;
        return true;
    }

    // Transients and loops use the same cue routing spatial settings and pause policy
    private static void ConfigureSource(AudioSource source, SoundCueSO cue, AudioClip clip,
        Vector3 position, float gain, float pitch)
    {
        source.transform.position = position;
        source.clip = clip;
        source.outputAudioMixerGroup = cue.output;
        source.volume = gain;
        source.pitch = pitch;
        source.priority = Mathf.Clamp(cue.priority, 0, 256);
        source.spatialBlend = cue.isUI ? 0f : Mathf.Clamp01(cue.spatialBlend);
        source.ignoreListenerPause = cue.isUI;
        source.minDistance = Mathf.Max(0.01f, cue.minDistance);
        source.maxDistance = Mathf.Max(source.minDistance, cue.maxDistance);
    }

    // Invalid numbers must not reach source pitch gain or fade timing.
    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private void Update()
    {
        if (ownsGameplayPause && !IsLivePauseOwner(gameplayPauseOwner, gameplayPauseSceneHandle))
            ReleaseGameplaySuspension();
        if (ownsLeaderboardPause && !IsLivePauseOwner(leaderboardPauseOwner, leaderboardSceneHandle))
            RestoreOwnedPause();
        UpdateLoops();
        if (voices == null) return;
        foreach (var voice in voices)
        {
            if (voice.hasOwner && (voice.owner == null || !voice.owner.isActiveAndEnabled))
            {
                Release(voice);
                continue;
            }
            if (voice.cue == null || voice.pausedForGameplay ||
                (ownsGameplayPause && IsGameplayVoice(voice)) ||
                (AudioListener.pause && !voice.source.ignoreListenerPause)) continue;
            voice.remaining -= Time.unscaledDeltaTime;
            if (voice.remaining <= 0f) Release(voice);
        }
    }

    public void StopWorldVoices()
    {
        PhysicsImpactAudio.SetWorldEligible(false);
        StopGameplayLoops();
        if (voices != null)
            foreach (var voice in voices)
                if (voice.cue != null && (!voice.cue.isUI || voice.hasOwner)) Release(voice);
        WorldAudioStopped?.Invoke();
    }

    public bool PlayUIHover(SoundCueSO cue)
    {
        if (cue == null || !cue.isUI || Time.unscaledTimeAsDouble < nextHoverTime) return false;
        if (!Play(cue)) return false;
        nextHoverTime = Time.unscaledTimeAsDouble + 0.08;
        return true;
    }

    public void PauseForLeaderboard(MonoBehaviour owner)
    {
        if (owner == null || ownsLeaderboardPause) return;
        if (!ownsGameplayPause) listenerPauseBeforeOwnedPause = AudioListener.pause;
        leaderboardPauseOwner = owner;
        leaderboardSceneHandle = owner.gameObject.scene.handle;
        ownsLeaderboardPause = true;
        StopWorldVoices();
        StopCardDrawVoices();
        AudioListener.pause = true;
    }

    public bool TrySuspendGameplay(MonoBehaviour owner)
    {
        if (Instance != this || !isActiveAndEnabled || ownsLeaderboardPause || owner == null ||
            !owner.isActiveAndEnabled || !owner.gameObject.scene.IsValid() ||
            !owner.gameObject.scene.isLoaded) return false;
        if (ownsGameplayPause) return ReferenceEquals(gameplayPauseOwner, owner);

        listenerPauseBeforeOwnedPause = AudioListener.pause;
        gameplayPauseOwner = owner;
        gameplayPauseSceneHandle = owner.gameObject.scene.handle;
        ownsGameplayPause = true;
        if (voices != null)
            foreach (var voice in voices)
            {
                if (!IsGameplayVoice(voice) || !voice.source.ignoreListenerPause) continue;
                voice.pausedForGameplay = true;
                voice.source.Pause();
            }
        AudioListener.pause = true;
        return true;
    }

    public void RestoreGameplaySuspension(MonoBehaviour owner)
    {
        if (ownsGameplayPause && ReferenceEquals(gameplayPauseOwner, owner)) ReleaseGameplaySuspension();
    }

    private void ReleaseGameplaySuspension()
    {
        if (!ownsGameplayPause) return;
        ownsGameplayPause = false;
        gameplayPauseOwner = null;
        gameplayPauseSceneHandle = 0;
        if (voices != null)
            foreach (var voice in voices)
            {
                if (!voice.pausedForGameplay) continue;
                voice.pausedForGameplay = false;
                if (!ownsLeaderboardPause && voice.cue != null) voice.source.UnPause();
            }
        RestoreListenerPauseIfUnowned();
    }

    private static bool IsLivePauseOwner(MonoBehaviour owner, int sceneHandle) => owner != null &&
        owner.isActiveAndEnabled && owner.gameObject.scene.IsValid() && owner.gameObject.scene.isLoaded &&
        owner.gameObject.scene.handle == sceneHandle;

    private static bool IsGameplayVoice(Voice voice) => voice.cue != null &&
        (!voice.cue.isUI || voice.reservedForCardDraw || voice.hasOwner);

    private void RestoreListenerPauseIfUnowned()
    {
        if (!ownsGameplayPause && !ownsLeaderboardPause && AudioListener.pause)
            AudioListener.pause = listenerPauseBeforeOwnedPause;
    }

    public void RestoreLeaderboardPause(MonoBehaviour owner)
    {
        if (ownsLeaderboardPause && leaderboardPauseOwner == owner) RestoreOwnedPause();
    }

    // The service persists, while each MainScene owns only its own music session
    internal void BeginMusicSession(Scene scene)
    {
        if (Instance != this || !scene.IsValid() || !scene.isLoaded || musicSceneHandle == scene.handle) return;
        music?.ResetSession();
        musicSceneHandle = scene.handle;
    }

    private void RestoreOwnedPause()
    {
        if (!ownsLeaderboardPause) return;
        ownsLeaderboardPause = false;
        leaderboardPauseOwner = null;
        leaderboardSceneHandle = 0;
        RestoreListenerPauseIfUnowned();
    }

    private void OnSceneUnloaded(Scene scene)
    {
        if (scene.handle == musicSceneHandle)
        {
            music?.ResetSession();
            musicSceneHandle = 0;
        }
        StopWorldVoices();
        StopCardDrawVoices();
        if (ownsGameplayPause && scene.handle == gameplayPauseSceneHandle) ReleaseGameplaySuspension();
        if (ownsLeaderboardPause && scene.handle == leaderboardSceneHandle) RestoreOwnedPause();
        cueStates.Clear();
        nextHoverTime = 0;
    }

    private void StopCardDrawVoices()
    {
        if (voices == null) return;
        foreach (var voice in voices)
            if (voice.reservedForCardDraw && voice.cue != null) Release(voice);
    }

    public bool SetMasterVolume(float linear) => SetVolume("MasterVolume", linear);
    public bool SetGameplayVolume(float linear) => SetVolume("GameplayVolume", linear);
    public bool SetUIVolume(float linear) => SetVolume("UIVolume", linear);
    public bool SetMusicVolume(float linear) => SetVolume("MusicVolume", linear);
    public float MusicVolume
    {
        get
        {
            if (mixer == null || !mixer.GetFloat("MusicVolume", out float decibels)) return 1f;
            return decibels <= -80f ? 0f : Mathf.Clamp01(Mathf.Pow(10f, decibels / 20f));
        }
    }
    public GameMusic Music => Instance == this ? music : null;
    internal AudioMixer Mixer => mixer;
    private bool SetVolume(string parameter, float linear)
    {
        return mixer != null && mixer.SetFloat(parameter, linear <= 0.0001f ? -80f : 20f * Mathf.Log10(Mathf.Clamp01(linear)));
    }

    private static void ResetSource(AudioSource source)
    {
        source.Stop();
        source.clip = null;
        source.outputAudioMixerGroup = null;
        source.playOnAwake = false;
        source.loop = false;
        source.mute = false;
        source.volume = 1f;
        source.pitch = 1f;
        source.priority = 128;
        source.panStereo = 0f;
        source.spatialBlend = 0f;
        source.spatialize = false;
        source.spatializePostEffects = false;
        source.dopplerLevel = 0f;
        source.spread = 0f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = 1f;
        source.maxDistance = 500f;
        source.reverbZoneMix = 0f;
        source.bypassEffects = false;
        source.bypassListenerEffects = false;
        source.bypassReverbZones = false;
        source.ignoreListenerPause = false;
        source.ignoreListenerVolume = false;
        source.velocityUpdateMode = AudioVelocityUpdateMode.Dynamic;
    }

    private static void Release(Voice voice)
    {
        voice.owner = null;
        voice.hasOwner = false;
        voice.pausedForGameplay = false;
        ResetSource(voice.source);
        voice.source.transform.localPosition = Vector3.zero;
        voice.cue = null;
        voice.remaining = 0f;
        voice.playOrder = 0;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        OnDisable();
        if (music != null) music.Shutdown();
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        Instance = null;
        musicSceneHandle = 0;
        cueStates.Clear();
    }
    private void OnDisable()
    {
        if (Instance != this) return;
        StopGameplayLoops();
        if (voices != null) foreach (var voice in voices) Release(voice);
        ReleaseGameplaySuspension();
        RestoreOwnedPause();
    }
}
