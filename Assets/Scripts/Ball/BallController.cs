using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BallController : MonoBehaviour
{
    private static readonly HashSet<BallController> activeBalls = new HashSet<BallController>();
    private bool _isSecondaryBall;
    private bool _flightEnded;
    private bool _attemptFinalized;

    private void OnEnable() => activeBalls.Add(this);
    private void OnDisable() => activeBalls.Remove(this);

    public void InitializeAsSecondaryBall()
    {
        _isSecondaryBall = true;
        _flightEnded = false;
        _attemptFinalized = false;
    }

    public void MarkFlightEnded() => _flightEnded = true;

    public static bool HasUnfinishedSecondaryBalls(RoundStateManager manager)
    {
        foreach (BallController ball in activeBalls)
            if (ball != null && ball.gameObject.scene == manager.gameObject.scene &&
                ball._isSecondaryBall && !ball._flightEnded) return true;
        return false;
    }

    public static void FinishAttemptBalls(RoundStateManager manager)
    {
        foreach (BallController ball in new List<BallController>(activeBalls))
        {
            if (ball == null || ball.gameObject.scene != manager.gameObject.scene ||
                (ball != manager.ballController && !ball._isSecondaryBall)) continue;
            if (ball._attemptFinalized) continue;
            ball._attemptFinalized = true;
            ball.StopAllCoroutines();
            ball._hammerHitRoutine = null;
            ball._isLaunched = false;
            ball._isFollowingCurve = false;
            ball._homingTarget = null;
            ball._flightEnded = true;
            ball.ReleaseSchedulingCancelled?.Invoke();
            ball.ResetPerformed?.Invoke();
            if (ball._ballRb != null)
            {
                if (!ball._ballRb.isKinematic)
                {
                    ball._ballRb.linearVelocity = Vector3.zero;
                    ball._ballRb.angularVelocity = Vector3.zero;
                }
                ball._ballRb.isKinematic = true;
            }
            if (ball._isSecondaryBall)
            {
                ball.gameObject.SetActive(false);
                Destroy(ball.gameObject);
            }
        }
    }
    public ThrowTuningDataSO tuningData;
    
    private Rigidbody _ballRb;
    private Vector3 _startPos;
    private float _ballHeightOffset;
    
    public Vector3 StartPos => _startPos;
    
    private bool _isLaunched = false;
    private bool _isFollowingCurve = false;
    private List<CurvePoint> _currentPath;

    private Transform _homingTarget;
    private float _homingSpeed;
    private Coroutine _hammerHitRoutine;

    public bool IsLaunched => _isLaunched;
    public float LinearSpeedSquared => _ballRb != null ? _ballRb.linearVelocity.sqrMagnitude : 0f;
    public event Action<float, float> ReleaseScheduled;
    public event Action ReleaseSchedulingCancelled;
    public event Action ResetPerformed;
    public event Action<float> Released;
    
    void Awake()
    {
        _ballRb = GetComponent<Rigidbody>();
        _startPos = transform.position;
        _ballHeightOffset = _startPos.y;
    }

    public void ResetBall()
    {
        _flightEnded = false;
        _attemptFinalized = false;
        StopAllCoroutines();
        _hammerHitRoutine = null;
        ReleaseSchedulingCancelled?.Invoke();
        ResetPerformed?.Invoke();
        _isLaunched = false;
        _isFollowingCurve = false;
        _homingTarget = null;
        _homingSpeed = 0f;
        _currentPath = null;
        
        _ballRb.linearVelocity = Vector3.zero;
        _ballRb.angularVelocity = Vector3.zero;
        _ballRb.isKinematic = true;
        
        _ballRb.constraints = RigidbodyConstraints.None;
        
        transform.position = _startPos;
    }

    public void ExecuteThrow(List<CurvePoint> path, float launchSpeed)
    {
        if (GameplayPause.IsPaused(this)) return;
        _currentPath = path;
        StartCoroutine(ThrowSequence(launchSpeed));
    }
    
    // Add these two new methods anywhere in the class:
    public void SetHomingTarget(Transform targetPin, float speed)
    {
        _isFollowingCurve = false;
        _homingTarget = targetPin;
        _homingSpeed = speed;
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.GetComponentInParent<BossHammerSwingMotor>() != null)
        {
            _isFollowingCurve = false;
            _homingTarget = null;

            if (_hammerHitRoutine == null)
            {
                _hammerHitRoutine = StartCoroutine(SwitchToRoundOverAfterDelay(2f));
            }
            return;
        }

        // Stop homing once we actually hit the pin we were tracking, 
        // letting physics take over and go straight/bounce
        if (_homingTarget != null && collision.transform == _homingTarget)
        {
            _homingTarget = null;
        }
    }

    private IEnumerator SwitchToRoundOverAfterDelay(float delay)
    {
        GameObject managerObj = GameObject.FindGameObjectWithTag("RoundManager");
        RoundStateManager manager = managerObj != null ? managerObj.GetComponent<RoundStateManager>() : null;
        if (manager == null) yield break;
        int launchVersion = manager.throwingBallState.LaunchVersion;
        while (GameplayPause.IsPaused(this)) yield return null;
        yield return new WaitForSeconds(delay);
        while (GameplayPause.IsPaused(this)) yield return null;
        _hammerHitRoutine = null;

        if (manager != null && manager.currentState == manager.throwingBallState &&
            manager.throwingBallState.LaunchVersion == launchVersion)
        {
            MarkFlightEnded();
            manager.throwingBallState.RequestCompletion(manager, "hammer completion delay elapsed", this);
        }
    }

    private IEnumerator ThrowSequence(float speed)
    {
        while (GameplayPause.IsPaused(this)) yield return null;
        _ballRb.isKinematic = true;

        Vector3 firstPointRaw = _currentPath[0].position;
        Vector3 firstLinePosition = new Vector3(firstPointRaw.x, _ballHeightOffset, firstPointRaw.z);
        transform.position = firstLinePosition;

        float deltaXAverage = 0;
        int sampleCount = Mathf.Min(_currentPath.Count, 5);
        if (sampleCount > 1)
        {
            deltaXAverage = (_currentPath[sampleCount - 1].position.x - _currentPath[0].position.x);
        }
        
        float pullbackXOffset = -deltaXAverage * tuningData.oppositeXScale;
        float pullbackY = speed * tuningData.pullbackYScale;
        float pullbackZ = speed * tuningData.pullbackZScale;

        Vector3 pullbackPos = new Vector3(
            firstLinePosition.x + pullbackXOffset,
            firstLinePosition.y + pullbackY,
            firstLinePosition.z - pullbackZ
        );

        float windupDuration = 0.5f;
        float distanceToStart = Vector3.Distance(pullbackPos, firstLinePosition);
        float swingForwardDuration = distanceToStart / speed;
        ReleaseScheduled?.Invoke(speed, windupDuration + swingForwardDuration);

        float elapsed = 0f;
        while (elapsed < windupDuration)
        {
            elapsed += Time.deltaTime;
            float targetT = elapsed / windupDuration;
            float curveVal = tuningData.swingCurve != null ? tuningData.swingCurve.Evaluate(targetT) : targetT;
            transform.position = Vector3.Lerp(firstLinePosition, pullbackPos, curveVal);
            yield return null;
            while (GameplayPause.IsPaused(this)) yield return null;
        }

        elapsed = 0f;
        while (elapsed < swingForwardDuration)
        {
            elapsed += Time.deltaTime;
            float targetT = elapsed / swingForwardDuration;
            float easeIn = targetT * targetT; 
            transform.position = Vector3.Lerp(pullbackPos, firstLinePosition, easeIn);
            yield return null;
            while (GameplayPause.IsPaused(this)) yield return null;
        }

        transform.position = firstLinePosition;
        _ballRb.isKinematic = false;
        _ballRb.linearVelocity = Vector3.forward * speed;
        _isLaunched = true;
        _isFollowingCurve = true;
        Released?.Invoke(speed);
    }
    
    public void ExecuteAutoThrow(float launchSpeed, float zOffset)
    {
        if (GameplayPause.IsPaused(this)) return;
        _currentPath = null;
        StartCoroutine(AutoThrowSequence(launchSpeed, zOffset));
    }
    
    public void Apply45DegreeDeflection()
    {
        _isFollowingCurve = false;
        _homingTarget = null;

        float currentSpeed = _ballRb.linearVelocity.magnitude;
        
        Vector3 newDirection = Quaternion.Euler(0, 45f, 0) * _ballRb.linearVelocity.normalized;
        
        _ballRb.linearVelocity = newDirection * currentSpeed;
    }

    // ai https://share.gemini.google/xeTGREPt9NkP
    private IEnumerator AutoThrowSequence(float speed, float zOffset)
    {
        while (GameplayPause.IsPaused(this)) yield return null;
        _ballRb.isKinematic = true;

        Vector3 firstLinePosition = new Vector3(_startPos.x + zOffset, _ballHeightOffset, _startPos.z);
        transform.position = firstLinePosition;

        float pullbackY = speed * tuningData.pullbackYScale;
        float pullbackZ = speed * tuningData.pullbackZScale;

        Vector3 pullbackPos = new Vector3(
            firstLinePosition.x,
            firstLinePosition.y + pullbackY,
            firstLinePosition.z - pullbackZ
        );

        float windupDuration = 0.5f;
        float distanceToStart = Vector3.Distance(pullbackPos, firstLinePosition);
        float swingForwardDuration = distanceToStart / speed;
        ReleaseScheduled?.Invoke(speed, windupDuration + swingForwardDuration);

        float elapsed = 0f;
        while (elapsed < windupDuration)
        {
            elapsed += Time.deltaTime;
            float targetT = elapsed / windupDuration;
            float curveVal = tuningData.swingCurve != null ? tuningData.swingCurve.Evaluate(targetT) : targetT;
            transform.position = Vector3.Lerp(firstLinePosition, pullbackPos, curveVal);
            yield return null;
            while (GameplayPause.IsPaused(this)) yield return null;
        }

        elapsed = 0f;
        while (elapsed < swingForwardDuration)
        {
            elapsed += Time.deltaTime;
            float targetT = elapsed / swingForwardDuration;
            float easeIn = targetT * targetT;
            transform.position = Vector3.Lerp(pullbackPos, firstLinePosition, easeIn);
            yield return null;
            while (GameplayPause.IsPaused(this)) yield return null;
        }

        transform.position = firstLinePosition;
        _ballRb.isKinematic = false;
        _ballRb.linearVelocity = Vector3.forward * speed;
        _isLaunched = true;
        _isFollowingCurve = false;
        Released?.Invoke(speed);
    }
    
    void FixedUpdate()
    {
        // Continuously apply velocity towards the target pin until we hit it
        if (_homingTarget != null)
        {
            Vector3 directionToTarget = (_homingTarget.position - transform.position).normalized;
            _ballRb.linearVelocity = directionToTarget * _homingSpeed;
            return;
        }
        
        if (!_isLaunched || !_isFollowingCurve) return;
        
        Vector3 currentPos = transform.position;
        float targetX = currentPos.x;
        bool found = false;
        int currentDirectionIndex = 0;

        for (int i = 0; i < _currentPath.Count - 1; i++)
        {
            if (currentPos.z >= _currentPath[i].position.z && currentPos.z <= _currentPath[i+1].position.z)
            {
                float t = Mathf.InverseLerp(_currentPath[i].position.z, _currentPath[i+1].position.z, currentPos.z);
                targetX = Mathf.Lerp(_currentPath[i].position.x, _currentPath[i+1].position.x, t);
                currentDirectionIndex = _currentPath[i].directionIndex;
                found = true;
                break;
            }
        }

        if (currentDirectionIndex >= 2)
        {
            _isFollowingCurve = false;
            return;
        }

        if (found)
        {
            float xDiff = targetX - currentPos.x;
            float forceStrength = 60f; 
            _ballRb.AddForce(new Vector3(xDiff * forceStrength, 0, 0), ForceMode.Acceleration);
            Vector3 vel = _ballRb.linearVelocity;
            vel.x *= 0.85f;
            _ballRb.linearVelocity = vel;
        }
        else if (currentPos.z > tuningData.maxZ)
        {
            _isFollowingCurve = false;
        }
    }
}
