using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BallController))]
public sealed class BallAudio : MonoBehaviour
{
    [SerializeField] private SoundCueSO releaseCue;
    [SerializeField, Range(0f, 1f)] private float minimumReleaseVolume = 0.8f;

    private BallController ballController;

    private void Awake()
    {
        ballController = GetComponent<BallController>();
    }

    private void OnEnable()
    {
        if (ballController == null) ballController = GetComponent<BallController>();
        ballController.Released += OnReleased;
    }

    private void OnDisable()
    {
        if (ballController != null) ballController.Released -= OnReleased;
    }

    private void OnReleased(float launchSpeed)
    {
        float speedAmount = 1f;
        ThrowTuningDataSO tuning = ballController.tuningData;
        if (tuning != null)
        {
            speedAmount = Mathf.InverseLerp(tuning.forceMinSpeed, tuning.forceMaxSpeed, launchSpeed);
        }

        float volumeScale = Mathf.Lerp(minimumReleaseVolume, 1f, speedAmount);
        GameAudio.Instance?.Play(releaseCue, transform.position, volumeScale);
    }
}
