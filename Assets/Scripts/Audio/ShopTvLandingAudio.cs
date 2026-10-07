using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
public sealed class ShopTvLandingAudio : MonoBehaviour
{
    private SoundCueSO cue;
    private ShopUIManager owner;
    private LayerMask landingSurfaces;
    private float minimumSupportDot;
    private float minimumBounceSpeed;
    private float bounceCooldown;
    private double nextImpactTime;
    private bool hasLanded;
    private bool armed;

    public void Arm(SoundCueSO landingCue, ShopUIManager shopOwner, LayerMask surfaces, float supportDot,
        float bounceSpeed = 0.1f, float impactCooldown = 0.12f)
    {
        cue = landingCue;
        owner = shopOwner;
        landingSurfaces = surfaces;
        minimumSupportDot = Mathf.Clamp01(supportDot);
        minimumBounceSpeed = Mathf.Max(0f, bounceSpeed);
        bounceCooldown = Mathf.Max(0f, impactCooldown);
        nextImpactTime = 0;
        hasLanded = false;
        armed = isActiveAndEnabled && owner != null && owner.IsHexGridExpansionActive;
    }

    public void Disarm()
    {
        armed = false;
        cue = null;
        owner = null;
    }

    private void OnCollisionEnter(Collision collision) => ReportLanding(collision);

    // Stay can supply the first landing; resting contact must not become a repeating thud.
    private void OnCollisionStay(Collision collision)
    {
        if (!hasLanded) ReportLanding(collision);
    }

    // Shop landings have their own eligibility; the throwing impact gate stays closed.
    private void ReportLanding(Collision collision)
    {
        if (!armed || !isActiveAndEnabled || owner == null || !owner.IsHexGridExpansionActive || collision == null) return;
        var surface = collision.collider;
        if (surface == null || surface.isTrigger ||
            (landingSurfaces.value & (1 << surface.gameObject.layer)) == 0) return;

        // A TV or other moving body is not a ground landing.
        if (surface.GetComponentInParent<ShopTvLandingAudio>() != null ||
            (surface.attachedRigidbody != null && !surface.attachedRigidbody.isKinematic)) return;

        Vector3 up = Physics.gravity.sqrMagnitude > 0f ? -Physics.gravity.normalized : Vector3.up;
        for (int i = 0; i < collision.contactCount; i++)
        {
            var contact = collision.GetContact(i);
            if (!(Vector3.Dot(contact.normal, up) >= minimumSupportDot)) continue;
            float impactSpeed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
            if (hasLanded && (!(impactSpeed >= minimumBounceSpeed) || Time.unscaledTimeAsDouble < nextImpactTime)) return;
            float volumeScale = hasLanded ? Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(impactSpeed / 3f)) : 1f;
            // Consume each impact even when audio is unavailable; there is no queued replay.
            hasLanded = true;
            nextImpactTime = Time.unscaledTimeAsDouble + bounceCooldown;
            if (cue != null && !cue.isUI) GameAudio.Instance?.Play(cue, contact.point, volumeScale);
            return;
        }
    }

    private void OnDisable() => Disarm();
}
