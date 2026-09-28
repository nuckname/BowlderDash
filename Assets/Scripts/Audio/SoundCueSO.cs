using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(menuName = "Audio/Sound Cue")]
public sealed class SoundCueSO : ScriptableObject
{
    public AudioClip[] variants;
    public AudioMixerGroup output;
    public bool isUI;
    [Range(0f, 1f)] public float gain = 0.5f;
    public Vector2 pitchRange = Vector2.one;
    [Range(0, 256)] public int priority = 128;
    [Min(1)] public int voiceLimit = 4;
    [Min(0f)] public float cooldown;
    [Range(0f, 1f)] public float spatialBlend = 1f;
    [Min(0.01f)] public float minDistance = 5f;
    [Min(0.01f)] public float maxDistance = 60f;

    public AudioClip PickVariant(System.Random random, int previous, out int selected)
    {
        selected = -1;
        if (variants == null) return null;
        int count = 0;
        for (int i = 0; i < variants.Length; i++)
            if (variants[i] != null && i != previous) count++;
        if (count == 0)
        {
            if (previous >= 0 && previous < variants.Length && variants[previous] != null)
                selected = previous;
        }
        else
        {
            int choice = random.Next(count);
            for (int i = 0; i < variants.Length; i++)
                if (variants[i] != null && i != previous && choice-- == 0) { selected = i; break; }
        }
        return selected < 0 ? null : variants[selected];
    }
}
