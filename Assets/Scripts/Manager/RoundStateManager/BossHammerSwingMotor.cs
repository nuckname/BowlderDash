using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(HingeJoint))]
public sealed class BossHammerSwingMotor : MonoBehaviour
{
    [Header("Swing Motor")]
    [SerializeField, Min(0f)] private float targetSpeed = 90f;
    [SerializeField, Min(0f)] private float maximumForce = 1000f;
    [SerializeField, Min(0f)] private float reversalMargin = 5f;
    [SerializeField] private bool startTowardPositiveLimit = true;

    private HingeJoint hinge;
    private int direction;
    private bool warnedAboutLimits;

    private void Awake()
    {
        hinge = GetComponent<HingeJoint>();
    }

    private void OnEnable()
    {
        if (hinge == null) hinge = GetComponent<HingeJoint>();
        hinge.useSpring = false;
        direction = startTowardPositiveLimit ? 1 : -1;
        warnedAboutLimits = false;
    }

    private void FixedUpdate()
    {
        if (hinge == null) return;

        JointLimits limits = hinge.limits;
        if (!hinge.useLimits || limits.max <= limits.min)
        {
            hinge.useMotor = false;
            if (!warnedAboutLimits)
            {
                Debug.LogWarning("Hammer swing needs enabled hinge limits with min below max", this);
                warnedAboutLimits = true;
            }
            return;
        }

        if (targetSpeed <= 0f || maximumForce <= 0f)
        {
            hinge.useMotor = false;
            return;
        }

        float margin = Mathf.Min(reversalMargin, (limits.max - limits.min) * 0.49f);
        if (direction > 0 && hinge.angle >= limits.max - margin)
            direction = -1;
        else if (direction < 0 && hinge.angle <= limits.min + margin)
            direction = 1;

        JointMotor motor = hinge.motor;
        motor.targetVelocity = direction * targetSpeed;
        motor.force = maximumForce;
        motor.freeSpin = false;
        hinge.motor = motor;
        hinge.useMotor = true;
    }

    public void SetSwingDirection(bool towardPositiveLimit)
    {
        direction = towardPositiveLimit ? 1 : -1;
    }

    private void OnDisable()
    {
        if (hinge != null) hinge.useMotor = false;
    }
}
