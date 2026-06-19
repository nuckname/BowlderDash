using UnityEngine;
using UnityEngine.Events;

public class MultPin : PinBase
{
    public ParticleSystem particleSystem;

    [Header("Hit Settings")]
    public string hitterTag  = "Ball";
    public int scoreValue = 10;
    public float multValue  = 0.5f;

    [Header("Overrides")]
    public GameManager gameManager;
    public ScoreCalculator scoreCalculatorOverride;

    [Header("Events")]
    public UnityEvent onHit;
    public UnityEvent onReset;

    private bool _hasAwardedThisFrame;

    protected override void Awake()
    {
        base.Awake();
        particleSystem = GetComponent<ParticleSystem>();
    }

    private void OnEnable() => ResetFrameState();

    private void Update()
    {
       // if (!IsInPlacementMode()) return;
        ResetFrameState();
        onReset?.Invoke();
    }

    private void OnCollisionEnter(Collision collision)
    {
        //if (_hasAwardedThisFrame || IsInPlacementMode()) return;
        if (!IsValidHitter(collision.gameObject)) return;

        _hasAwardedThisFrame = true;
        ResolveScoreCalculator()?.QueueScoreModification(scoreValue, multValue, transform.position);
        onHit?.Invoke();
        particleSystem?.Play();
    }

    private bool IsValidHitter(GameObject hitter)
    {
        if (hitter == null) return false;
        if (!string.IsNullOrEmpty(hitterTag) && hitter.CompareTag(hitterTag)) return true;

        GameManager gm = gameManager ?? GameManager.Instance;
        return gm != null && gm.ball == hitter;
    }

    private ScoreCalculator ResolveScoreCalculator()
    {
        if (scoreCalculatorOverride != null) return scoreCalculatorOverride;
        GameManager gm = gameManager ?? GameManager.Instance;
        return gm?.scoreCalculator;
    }

    private void ResetFrameState()
    {
        _hasAwardedThisFrame = false;
        StopParticles(particleSystem);
    }

    public override void ResetPin()
    {
        base.ResetPin();
        ResetFrameState();
        onReset?.Invoke();
    }
}