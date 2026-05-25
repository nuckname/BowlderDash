using UnityEngine;

public class TicketPin : MonoBehaviour
{
    public string hitterTag = "Ball";
    public float minHitVelocity = 3f;
    public int ticketValue = 1;
    public float ballHitCooldown = 0f;
    public ParticleSystem particleSystem;

    private BoxCollider awardCollider;
    private float lastBallHitTime = -999f;

    private void Awake()
    {
        awardCollider = GetComponent<BoxCollider>();
    }

    public bool IsAwardCollider(Collider col)
    {
        return col == awardCollider;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!CanAward() || !IsBall(other))
        {
            return;
        }

        if (ballHitCooldown > 0f && Time.time - lastBallHitTime < ballHitCooldown && other.gameObject.GetComponent<Rigidbody>().velocity.magnitude < minHitVelocity)
        {
            Debug.Log($"<color=orange><b>[TicketPin]</b></color> Hit velocity too low: {other.gameObject.GetComponent<Rigidbody>().velocity.magnitude}");
            return;
        }

        lastBallHitTime = Time.time;
        Award();
    }

    public void TryAwardFromExplosion(Collider sourceCollider)
    {
        if (!CanAward() || !IsAwardCollider(sourceCollider))
        {
            return;
        }

        Award();
    }

    private bool CanAward()
    {
        GrandGameManager gm = GrandGameManager.Instance;
        return gm == null || gm.GetCurrentState() != GameState.PinPlacement;
    }

    private bool IsBall(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(hitterTag) && other.CompareTag(hitterTag))
        {
            return true;
        }

        GameObject ball = GrandGameManager.Instance != null ? GrandGameManager.Instance.ball : null;
        if (ball == null)
        {
            return false;
        }

        Transform otherTransform = other.transform;
        return otherTransform == ball.transform || otherTransform.IsChildOf(ball.transform);
    }

    private void Award()
    {
        GrandGameManager gm = GrandGameManager.Instance;
        if (gm != null)
        {
            gm.AddGridTicketsThisFrame(ticketValue);
        }

        if (particleSystem != null)
        {
            particleSystem.Play();
        }
    }
}
