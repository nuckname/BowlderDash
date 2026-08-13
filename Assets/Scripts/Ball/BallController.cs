using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BallController : MonoBehaviour
{
    public ThrowTuningDataSO tuningData;
    
    private Rigidbody _ballRb;
    private Vector3 _startPos;
    private float _ballHeightOffset;
    
    private bool _isLaunched = false;
    private bool _isFollowingCurve = false;
    private List<CurvePoint> _currentPath;

    void Awake()
    {
        _ballRb = GetComponent<Rigidbody>();
        _startPos = transform.position;
        _ballHeightOffset = _startPos.y;
    }

    public void ResetBall()
    {
        StopAllCoroutines();
        _isLaunched = false;
        _isFollowingCurve = false;
        
        _ballRb.linearVelocity = Vector3.zero;
        _ballRb.angularVelocity = Vector3.zero;
        _ballRb.isKinematic = true;
        
        transform.position = _startPos;
    }

    public void ExecuteThrow(List<CurvePoint> path, float launchSpeed)
    {
        _currentPath = path;
        StartCoroutine(ThrowSequence(launchSpeed));
    }

    private IEnumerator ThrowSequence(float speed)
    {
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
        float elapsed = 0f;
        while (elapsed < windupDuration)
        {
            elapsed += Time.deltaTime;
            float targetT = elapsed / windupDuration;
            float curveVal = tuningData.swingCurve != null ? tuningData.swingCurve.Evaluate(targetT) : targetT;
            transform.position = Vector3.Lerp(firstLinePosition, pullbackPos, curveVal);
            yield return null;
        }

        float distanceToStart = Vector3.Distance(pullbackPos, firstLinePosition);
        float swingForwardDuration = distanceToStart / speed;
        elapsed = 0f;
        while (elapsed < swingForwardDuration)
        {
            elapsed += Time.deltaTime;
            float targetT = elapsed / swingForwardDuration;
            float easeIn = targetT * targetT; 
            transform.position = Vector3.Lerp(pullbackPos, firstLinePosition, easeIn);
            yield return null;
        }

        transform.position = firstLinePosition;
        _ballRb.isKinematic = false;
        _ballRb.linearVelocity = Vector3.forward * speed;
        _isLaunched = true;
        _isFollowingCurve = true;
    }

    void FixedUpdate()
    {
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