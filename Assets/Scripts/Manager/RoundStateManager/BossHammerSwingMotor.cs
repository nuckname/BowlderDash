using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(HingeJoint))]
public sealed class BossHammerSwingMotor : MonoBehaviour
{
    [Header("Swing Motor")]
    [SerializeField, Min(0f)] private float targetSpeed = 90f;
    [SerializeField, Min(0f)] private float maximumForce = 1000f;
    [SerializeField, Min(0f)] private float reversalMargin = 5f;
    [SerializeField, Min(0f)] private float stallReversalDelay = 0.4f;
    [SerializeField, Min(0f)] private float minimumProgressPerStep = 0.05f;
    [SerializeField] private bool startTowardPositiveLimit = true;

    private HingeJoint hinge;
    private int direction;
    private bool warnedAboutLimits;
    private float previousAngle;
    private float stalledFor;
    private bool hasPreviousAngle;

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
        stalledFor = 0f;
        hasPreviousAngle = false;
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
        float angle = hinge.angle;
        bool reachedLimit = direction > 0
            ? angle >= limits.max - margin
            : angle <= limits.min + margin;

        if (reachedLimit)
        {
            ReverseDirection();
        }
        else if (stallReversalDelay > 0f && hasPreviousAngle)
        {
            float progress = (angle - previousAngle) * direction;
            stalledFor = progress < minimumProgressPerStep
                ? stalledFor + Time.fixedDeltaTime
                : 0f;

            if (stalledFor >= stallReversalDelay)
                ReverseDirection();
        }

        previousAngle = angle;
        hasPreviousAngle = true;

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
        stalledFor = 0f;
        hasPreviousAngle = false;
    }

    private void ReverseDirection()
    {
        direction = -direction;
        stalledFor = 0f;
        hasPreviousAngle = false;
    }

    private void OnDisable()
    {
        if (hinge != null) hinge.useMotor = false;
    }
}
