using System;
using UnityEngine;

public enum ImpactAudioMaterial
{
    Unknown,
    Ball,
    Pin,
    Lane,
    Gutter,
    Pit,
    Obstacle
}

[Serializable]
public sealed class ImpactAudioResponse
{
    public ImpactAudioMaterial otherMaterial;
    public SoundCueSO cue;
    [Range(0f, 2f)] public float volumeMultiplier = 1f;
}

[CreateAssetMenu(menuName = "Audio/Impact Profile")]
public sealed class ImpactAudioProfileSO : ScriptableObject
{
    [SerializeField] private ImpactAudioMaterial material = ImpactAudioMaterial.Unknown;
    [SerializeField, Min(0f)] private float minimumImpulse = 0.6f;
    [SerializeField, Min(0.01f)] private float fullVolumeImpulse = 18f;
    [SerializeField, Range(0.25f, 3f)] private float strengthExponent = 0.65f;
    [SerializeField] private ImpactAudioResponse[] responses;

    public ImpactAudioMaterial Material => material;
    public float MinimumImpulse => minimumImpulse;

    public bool TryGetResponse(ImpactAudioMaterial otherMaterial, out ImpactAudioResponse response)
    {
        if (responses != null)
        {
            foreach (ImpactAudioResponse candidate in responses)
            {
                if (candidate != null && candidate.otherMaterial == otherMaterial && candidate.cue != null)
                {
                    response = candidate;
                    return true;
                }
            }
        }

        response = null;
        return false;
    }

    public float EvaluateVolume(float impulse, float multiplier)
    {
        float amount = Mathf.InverseLerp(minimumImpulse,
            Mathf.Max(minimumImpulse + 0.01f, fullVolumeImpulse), impulse);
        return Mathf.Clamp01(Mathf.Pow(amount, strengthExponent) * Mathf.Max(0f, multiplier));
    }
}
