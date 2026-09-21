using UnityEngine;

public enum AudioSurfaceType
{
    Lane,
    Gutter,
    Pit,
    Obstacle
}

[DisallowMultipleComponent]
public sealed class AudioSurface : MonoBehaviour
{
    [SerializeField] private AudioSurfaceType surfaceType = AudioSurfaceType.Lane;
    [SerializeField] private bool supportsRolling = true;

    public AudioSurfaceType SurfaceType => surfaceType;
    public bool SupportsRolling => supportsRolling;
}
