using UnityEngine;
using UnityEngine.Events;

public class MultPin : MonoBehaviour
{
    [Header("Hit Settings")]
    public string hitterTag = "Ball";
    public GrandGameManager gameManager;
    public ScoreCalculator scoreCalculatorOverride;
    public int scoreValue = 10;
    public float multValue = 0.5f;

    [Header("Events")]
    public UnityEvent onHit;

    private bool hasAwardedThisFrame;

    private void OnEnable()
    {
        ResetFrameState();
    }

    private void Update()
    {
        if (GrandGameManager.Instance != null && GrandGameManager.Instance.GetCurrentState() == GameState.PinPlacement)
        {
            ResetFrameState();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasAwardedThisFrame)
        {
            return;
        }

        if (GrandGameManager.Instance != null && GrandGameManager.Instance.GetCurrentState() == GameState.PinPlacement)
        {
            return;
        }

        if (!IsValidHitter(collision.gameObject))
        {
            return;
        }

        HandleHit(collision.gameObject);
    }

    private bool IsValidHitter(GameObject hitter)
    {
        if (hitter == null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(hitterTag) && hitter.CompareTag(hitterTag))
        {
            return true;
        }

        GrandGameManager resolvedGameManager = ResolveGameManager();
        return resolvedGameManager != null && resolvedGameManager.ball == hitter;
    }

    private void HandleHit(GameObject hitter)
    {
        hasAwardedThisFrame = true;
        AddScore();
        OnHit(hitter);
    }

    private void AddScore()
    {
        ScoreCalculator resolvedScoreCalculator = ResolveScoreCalculator();
        if (resolvedScoreCalculator != null)
        {
            resolvedScoreCalculator.QueueScoreModification(scoreValue, multValue, transform.position);
        }
    }

    private GrandGameManager ResolveGameManager()
    {
        if (gameManager != null)
        {
            return gameManager;
        }

        gameManager = GrandGameManager.Instance;
        return gameManager;
    }

    private ScoreCalculator ResolveScoreCalculator()
    {
        if (scoreCalculatorOverride != null)
        {
            return scoreCalculatorOverride;
        }

        GrandGameManager resolvedGameManager = ResolveGameManager();
        if (resolvedGameManager != null)
        {
            return resolvedGameManager.scoreCalculator;
        }

        return GrandGameManager.Instance != null ? GrandGameManager.Instance.scoreCalculator : null;
    }

    private void ResetFrameState()
    {
        hasAwardedThisFrame = false;
    }

    protected virtual void OnHit(GameObject hitter)
    {
        if (onHit != null)
        {
            onHit.Invoke();
        }
    }
}
