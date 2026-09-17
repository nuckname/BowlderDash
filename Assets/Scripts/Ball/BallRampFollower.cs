using System.Collections.Generic;
using UnityEngine;
// ai https://share.gemini.google/iTGCk8kMEve2
[RequireComponent(typeof(Rigidbody))]
public class BallRampFollower : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float maxSpeed = 5f; // Limits speed so momentum doesn't launch it off corners
    [SerializeField] private float steerForce = 10f; // How strongly it corrects its path to turn
    [SerializeField] private float distanceToWaypoint = 1.0f;

    [Header("Material Settings")]
    [SerializeField] private Renderer ballRenderer;
    [SerializeField] private Vector2 hueChangeRange = new Vector2(0f, 1f);

    private List<Transform> _path;
    private int _currentWaypointIndex = 0;
    private Rigidbody _rb;
    private bool _isFollowing = false;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        
        if (ballRenderer == null)
        {
            ballRenderer = GetComponentInChildren<Renderer>();
        }

        if (ballRenderer != null)
        {
            Material uniqueMaterial = ballRenderer.material;
            float randomHue = Random.Range(hueChangeRange.x, hueChangeRange.y);
            uniqueMaterial.SetFloat("HueChange", randomHue);
        }
    }

    public void InitializePath(List<Transform> pathPoints)
    {
        if (pathPoints != null && pathPoints.Count > 0)
        {
            _path = new List<Transform>(pathPoints);
            _currentWaypointIndex = 0;
            _isFollowing = true;
        }
    }

    [Header("Debug Settings")]
    [SerializeField] private bool showDebugVisuals = true;

    private void FixedUpdate()
    {
        if (!_isFollowing || _path == null || _currentWaypointIndex >= _path.Count) return;

        Transform targetWaypoint = _path[_currentWaypointIndex];
        
        // Ignore Y axis when checking distance so balls don't get stuck orbiting a waypoint slightly above/below them
        Vector3 currentPos = transform.position;
        Vector3 targetPos = targetWaypoint.position;
        
        Vector3 flatTargetPos = targetPos;
        flatTargetPos.y = currentPos.y; 

        float distance = Vector3.Distance(currentPos, flatTargetPos);
        
        if (distance <= distanceToWaypoint)
        {
            if (showDebugVisuals) Debug.Log($"{gameObject.name} reached waypoint {_currentWaypointIndex}.");
            
            _currentWaypointIndex++;
            
            if (_currentWaypointIndex >= _path.Count)
            {
                if (showDebugVisuals) Debug.Log($"{gameObject.name} reached end of path.");
                _isFollowing = false;
            }
            return;
        }

        // Always push directly towards the waypoint to ensure it gets there
        Vector3 directionToWaypoint = (targetPos - currentPos).normalized;
        
        // Use Acceleration so the steering feels consistent regardless of the Rigidbody's mass
        _rb.AddForce(directionToWaypoint * steerForce, ForceMode.Acceleration);

        // Cap horizontal speed to stop it launching off corners, but let gravity and vertical momentum work naturally
        Vector3 currentVelocity = _rb.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0, currentVelocity.z);

        if (horizontalVelocity.magnitude > maxSpeed)
        {
            horizontalVelocity = horizontalVelocity.normalized * maxSpeed;
            _rb.linearVelocity = new Vector3(horizontalVelocity.x, currentVelocity.y, horizontalVelocity.z);
        }

        if (showDebugVisuals)
        {
            // Red line shows where the ball is trying to go
            Debug.DrawLine(transform.position, targetWaypoint.position, Color.red);
            // Blue line shows the ball's actual current momentum/velocity
            Debug.DrawRay(transform.position, _rb.linearVelocity, Color.blue);
        }
    }
    
    private void OnDrawGizmos()
    {
        if (!showDebugVisuals || _path == null || _currentWaypointIndex >= _path.Count) return;

        // Draws a yellow sphere around the target waypoint showing the exact size of 'distanceToWaypoint'
        Gizmos.color = Color.yellow;
        Transform targetWaypoint = _path[_currentWaypointIndex];
        Gizmos.DrawWireSphere(targetWaypoint.position, distanceToWaypoint);
    }
}