using UnityEngine;

public class TicketPin : PinBase
{
    public string hitterTag = "Ball";
    public float minHitVelocity = 3f;
    public int ticketValue = 1;
    public float ballHitCooldown = 0f;

    public ParticleSystem particleSystem;

    private BoxCollider _awardCollider;
    private float _lastBallHitTime = -999f;

    protected override void Awake()
    {
        base.Awake();
        _awardCollider = GetComponent<BoxCollider>();
    }

    public bool IsAwardCollider(Collider col) => col == _awardCollider;

    private void OnTriggerEnter(Collider other)
    {
        if (IsInPlacementMode() || !IsBall(other)) return;

        Rigidbody otherRb = other.GetComponent<Rigidbody>();
        if (ballHitCooldown > 0f
            && Time.time - _lastBallHitTime < ballHitCooldown
            && otherRb != null
            && otherRb.velocity.magnitude < minHitVelocity) return;

        _lastBallHitTime = Time.time;
        Award();
    }

    public void TryAwardFromExplosion(Collider sourceCollider)
    {
        if (IsInPlacementMode() || !IsAwardCollider(sourceCollider)) return;
        Award();
        
    }

    private bool IsBall(Collider other)
    {
        if (other == null) return false;
        if (!string.IsNullOrEmpty(hitterTag) && other.CompareTag(hitterTag)) return true;

        GameObject ball = GameManager.Instance?.ball;
        if (ball == null) return false;

        return other.transform == ball.transform
               || other.transform.IsChildOf(ball.transform);
    }

    private void Award()
    {
        GameManager.Instance?.AddGridTicketsThisFrame(ticketValue);
        particleSystem?.Play();
    }

    public override void ResetPin()
    {
        base.ResetPin();
        StopParticles(particleSystem);
    }
}