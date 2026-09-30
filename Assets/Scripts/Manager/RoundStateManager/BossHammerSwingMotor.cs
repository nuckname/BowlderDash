using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(HingeJoint))]
public sealed class BossHammerSwingMotor : MonoBehaviour
{
    [Header("Swing Motor")]
    [SerializeField, Min(0f), Tooltip("Motor speed in degrees per second at the bottom of the swing (0 degrees).")]
    private float targetSpeed = 90f;
    [SerializeField, Range(0.1f, 1f), Tooltip("Fraction of Target Speed near either hinge limit. Lower values make the swing slow more at the top.")]
    private float speedAtTopFraction = 0.25f;
    [SerializeField, Min(0f)] private float maximumForce = 1000f;
    [SerializeField, Min(0f)] private float reversalMargin = 5f;
    [SerializeField] private bool startTowardPositiveLimit = true;

    private HingeJoint hinge;
    private Rigidbody body;
    private int direction;
    private bool warnedAboutLimits;
    private bool warnedAboutInvalidAngle;
    private bool warnedAboutInvalidBody;
    private Quaternion restRelativeRotation;
    private Vector3 hingeAxisInAnchorSpace;
    private Vector3 angleReference;

    private void Awake()
    {
        hinge = GetComponent<HingeJoint>();
        body = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        if (hinge == null) hinge = GetComponent<HingeJoint>();
        if (body == null) body = GetComponent<Rigidbody>();
        hinge.useSpring = false;
        direction = startTowardPositiveLimit ? 1 : -1;
        warnedAboutLimits = false;
        warnedAboutInvalidAngle = false;
        warnedAboutInvalidBody = false;

        Quaternion anchorRotation = hinge.connectedBody != null
            ? hinge.connectedBody.rotation
            : Quaternion.identity;
        restRelativeRotation = Quaternion.Inverse(anchorRotation) * body.rotation;
        hingeAxisInAnchorSpace = (restRelativeRotation * hinge.axis).normalized;
        angleReference = Vector3.ProjectOnPlane(Vector3.up, hingeAxisInAnchorSpace).normalized;
        if (angleReference.sqrMagnitude < 0.001f)
            angleReference = Vector3.ProjectOnPlane(Vector3.right, hingeAxisInAnchorSpace).normalized;
    }

    private void FixedUpdate()
    {
        if (hinge == null) return;

        if (!IsFinite(body.position) || !IsFinite(body.rotation))
        {
            hinge.useMotor = false;
            if (!warnedAboutInvalidBody)
            {
                Debug.LogError("Hammer Rigidbody already has a non-finite pose before motor update; inspect collider overlaps and the hinge setup", this);
                warnedAboutInvalidBody = true;
            }
            return;
        }

        JointLimits limits = hinge.limits;
        if (!hinge.useLimits || !IsFinite(limits.min) || !IsFinite(limits.max) || limits.max <= limits.min)
        {
            hinge.useMotor = false;
            if (!warnedAboutLimits)
            {
                Debug.LogWarning("Hammer swing needs enabled hinge limits with min below max", this);
                warnedAboutLimits = true;
            }
            return;
        }

        if (!IsFinite(targetSpeed) || !IsFinite(maximumForce) ||
            !IsFinite(speedAtTopFraction) || !IsFinite(reversalMargin) ||
            targetSpeed <= 0f || maximumForce <= 0f)
        {
            hinge.useMotor = false;
            return;
        }

        float margin = Mathf.Min(reversalMargin, (limits.max - limits.min) * 0.49f);
        float angle = hinge.angle;
        if (!IsFinite(angle))
        {
            if (!warnedAboutInvalidAngle)
            {
                Debug.LogWarning("HingeJoint.angle is non-finite; using the Rigidbody rotations for hammer motor timing", this);
                warnedAboutInvalidAngle = true;
            }

            angle = GetAngleFromBodyRotations();
            if (!IsFinite(angle))
            {
                hinge.useMotor = false;
                return;
            }
        }

        bool reachedLimit = direction > 0
            ? angle >= limits.max - margin
            : angle <= limits.min + margin;

        if (reachedLimit)
            direction = -direction;

        float limitOnCurrentSide = angle >= 0f ? limits.max : -limits.min;
        float height = limitOnCurrentSide > 0f
            ? Mathf.Clamp01(Mathf.Abs(angle) / limitOnCurrentSide)
            : 0f;
        float speedFactor = Mathf.Lerp(1f, Mathf.Clamp(speedAtTopFraction, 0.1f, 1f),
            Mathf.SmoothStep(0f, 1f, height));
        float motorVelocity = direction * targetSpeed * speedFactor;
        if (!IsFinite(motorVelocity))
        {
            hinge.useMotor = false;
            return;
        }

        JointMotor motor = hinge.motor;
        motor.targetVelocity = motorVelocity;
        motor.force = maximumForce;
        motor.freeSpin = false;
        hinge.motor = motor;
        hinge.useMotor = true;
    }

    public void SetSwingDirection(bool towardPositiveLimit)
    {
        direction = towardPositiveLimit ? 1 : -1;
    }

    private float GetAngleFromBodyRotations()
    {
        Quaternion anchorRotation = hinge.connectedBody != null
            ? hinge.connectedBody.rotation
            : Quaternion.identity;
        Quaternion currentRelativeRotation = Quaternion.Inverse(anchorRotation) * body.rotation;
        Quaternion relativeSwing = currentRelativeRotation * Quaternion.Inverse(restRelativeRotation);
        return Vector3.SignedAngle(angleReference, relativeSwing * angleReference,
            hingeAxisInAnchorSpace);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(Quaternion value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
    }

    private void OnDisable()
    {
        if (hinge != null) hinge.useMotor = false;
    }
}
