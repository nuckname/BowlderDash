using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BallController))]
public sealed class BallAudio : MonoBehaviour
{
    [SerializeField] private SoundCueSO releaseCue;
    [SerializeField, Range(0f, 1f)] private float minimumReleaseVolume = 0.8f;
    [SerializeField, Range(-0.25f, 0.75f), Tooltip("Positive values start the clip before physics release; negative values delay it.")]
    private float releaseSoundLeadTime = 0.56f;

    private BallController ballController;
    private Coroutine scheduledPlayback;
    private bool releasePlayedForCurrentThrow;

    private void Awake()
    {
        ballController = GetComponent<BallController>();
    }

    private void OnEnable()
    {
        if (ballController == null) ballController = GetComponent<BallController>();
        ballController.ReleaseScheduled += OnReleaseScheduled;
        ballController.ReleaseSchedulingCancelled += CancelScheduledPlayback;
        ballController.Released += OnReleased;
    }

    private void OnDisable()
    {
        if (ballController != null)
        {
            ballController.ReleaseScheduled -= OnReleaseScheduled;
            ballController.ReleaseSchedulingCancelled -= CancelScheduledPlayback;
            ballController.Released -= OnReleased;
        }

        CancelScheduledPlayback();
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
