using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1000)]
public sealed class GameAudio : MonoBehaviour
{
    public static GameAudio Instance { get; private set; }
    [SerializeField, Range(1, 21)] private int transientCapacity = 21;
    [SerializeField] private AudioMixer mixer;

    private sealed class Voice
    {
        public AudioSource source;
        public SoundCueSO cue;
        public float remaining;
    }
    private sealed class CueState
    {
        public int previous = -1;
        public double nextPlay;
    }
    private Voice[] voices;
    private double nextHoverTime;
    private bool ownsLeaderboardPause;
    private bool listenerPauseBeforeLeaderboard;
    private MonoBehaviour leaderboardPauseOwner;
    private int leaderboardSceneHandle;
    public event System.Action WorldAudioStopped;
    // Audio variation must not consume the random sequence used by cards, grids or bosses.
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

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        voices = new Voice[Mathf.Clamp(transientCapacity, 1, 21)];
        for (int i = 0; i < voices.Length; i++)
        {
            var child = new GameObject($"Transient {i + 1:00}");
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            ResetSource(source);
            voices[i] = new Voice { source = source };
        }
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    // One source per transient: never stack unaccounted PlayOneShot voices.
    public bool Play(SoundCueSO cue, Vector3 position = default, float volumeScale = 1f)
    {
        if (Instance != this || voices == null || cue == null || cue.output == null) return false;
        if (!cue.isUI && ownsLeaderboardPause) return false;
        if (!cueStates.TryGetValue(cue, out var state)) state = new CueState();
        if (Time.unscaledTimeAsDouble < state.nextPlay) return false;
        float gain = Mathf.Clamp01(cue.gain * Mathf.Clamp01(volumeScale));
        if (gain <= 0f) return false;

        int active = 0;
        int uiActive = 0;
        Voice available = null;
        Voice weakest = null;
        Voice weakestUI = null;
        foreach (var voice in voices)
        {
            if (voice.cue == cue) active++;
            if (voice.cue == null) { if (available == null) available = voice; continue; }
            if (voice.cue.isUI)
            {
                uiActive++;
                if (voice.source.priority > Mathf.Clamp(cue.priority, 0, 256) &&
                    (weakestUI == null || voice.source.priority > weakestUI.source.priority)) weakestUI = voice;
            }
            // Lower numeric priority is more important. Equal-priority voices finish naturally.
            if (voice.source.priority > Mathf.Clamp(cue.priority, 0, 256) &&
                (weakest == null || voice.source.priority > weakest.source.priority ||
                 (voice.source.priority == weakest.source.priority && voice.source.volume < weakest.source.volume)))
                weakest = voice;
        }
        if (active >= Mathf.Max(1, cue.voiceLimit)) return false;
        // Hover and click share the two-voice UI budget; clicks may replace hover.
        var slot = cue.isUI && uiActive >= 2 ? weakestUI : available ?? weakest;
        if (slot == null) return false;
        var clip = cue.PickVariant(random, state.previous, out int selected);
        if (clip == null || clip.loadState != AudioDataLoadState.Loaded) return false;

        Release(slot);
        var source = slot.source;
        source.transform.position = position;
        source.clip = clip;
        source.outputAudioMixerGroup = cue.output;
        source.volume = gain;
        source.pitch = Mathf.Lerp(Mathf.Clamp(Mathf.Min(cue.pitchRange.x, cue.pitchRange.y), 0.1f, 3f),
                                  Mathf.Clamp(Mathf.Max(cue.pitchRange.x, cue.pitchRange.y), 0.1f, 3f),
                                  (float)random.NextDouble());
        source.priority = Mathf.Clamp(cue.priority, 0, 256);
        source.spatialBlend = cue.isUI ? 0f : Mathf.Clamp01(cue.spatialBlend);
        source.ignoreListenerPause = cue.isUI;
        source.minDistance = Mathf.Max(0.01f, cue.minDistance);
        source.maxDistance = Mathf.Max(source.minDistance, cue.maxDistance);
        slot.cue = cue;
        slot.remaining = clip.length / source.pitch + 0.1f;
        source.Play();
        state.previous = selected;
        state.nextPlay = Time.unscaledTimeAsDouble + Mathf.Max(0f, cue.cooldown);
        cueStates[cue] = state;
        return true;
    }

    private void Update()
    {
        if (voices == null) return;
        foreach (var voice in voices)
        {
            if (voice.cue == null || (AudioListener.pause && !voice.source.ignoreListenerPause)) continue;
            voice.remaining -= Time.unscaledDeltaTime;
            if (voice.remaining <= 0f) Release(voice);
        }
    }

    public void StopWorldVoices()
    {
        PhysicsImpactAudio.SetWorldEligible(false);
        if (voices != null)
            foreach (var voice in voices)
                if (voice.cue != null && !voice.cue.isUI) Release(voice);
        // Future ball-owned rolling sources subscribe here as they are introduced.
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
        listenerPauseBeforeLeaderboard = AudioListener.pause;
        leaderboardPauseOwner = owner;
        leaderboardSceneHandle = owner.gameObject.scene.handle;
        ownsLeaderboardPause = true;
        StopWorldVoices();
        AudioListener.pause = true;
    }

    public void RestoreLeaderboardPause(MonoBehaviour owner)
    {
        if (ownsLeaderboardPause && leaderboardPauseOwner == owner) RestoreOwnedPause();
    }

    private void RestoreOwnedPause()
    {
        if (!ownsLeaderboardPause) return;
        AudioListener.pause = listenerPauseBeforeLeaderboard;
        ownsLeaderboardPause = false;
        leaderboardPauseOwner = null;
        leaderboardSceneHandle = 0;
    }

    private void OnSceneUnloaded(Scene scene)
    {
        StopWorldVoices();
        if (ownsLeaderboardPause && scene.handle == leaderboardSceneHandle) RestoreOwnedPause();
        cueStates.Clear();
        nextHoverTime = 0;
    }

    public bool SetMasterVolume(float linear) => SetVolume("MasterVolume", linear);
    public bool SetGameplayVolume(float linear) => SetVolume("GameplayVolume", linear);
    public bool SetUIVolume(float linear) => SetVolume("UIVolume", linear);
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
        ResetSource(voice.source);
        voice.source.transform.localPosition = Vector3.zero;
        voice.cue = null;
        voice.remaining = 0f;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        RestoreOwnedPause();
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        Instance = null;
        cueStates.Clear();
    }
}
