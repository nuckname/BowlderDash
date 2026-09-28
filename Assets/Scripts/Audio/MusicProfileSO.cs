using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(fileName = "MusicProfile", menuName = "Audio/Music Profile")]
public sealed class MusicProfileSO : ScriptableObject
{
    [System.Serializable]
    public struct Track
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float gain;
    }

    public AudioMixerGroup output;
    [Min(0f)] public float fadeDuration = 1.5f;
    public Track mainMenu;
    public Track gameplay;
    public Track activeBowling;
    public Track shop;

    public Track GetTrack(MusicPhase phase)
    {
        switch (phase)
        {
            case MusicPhase.MainMenu: return mainMenu;
            case MusicPhase.Gameplay: return gameplay;
            case MusicPhase.ActiveBowling: return activeBowling;
            case MusicPhase.Shop: return shop;
            default: return default;
        }
    }
}
