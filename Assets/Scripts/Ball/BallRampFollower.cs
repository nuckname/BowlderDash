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
   
    [SerializeField] private Material[] randomMaterials;
    
    [Header("Material Settings")]
    [SerializeField] private Renderer ballRenderer;
    [SerializeField] private Vector2 hueChangeRange = new Vector2(0f, 1f);

    public Material getCurrentBallMaterial;
    
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

        if (ballRenderer != null && randomMaterials != null && randomMaterials.Length > 0)
        {
            int randomIndex = Random.Range(0, randomMaterials.Length);

            getCurrentBallMaterial = randomMaterials[randomIndex];
            ballRenderer.material = getCurrentBallMaterial;
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

private void FixedUpdate()
    {
        if (!_isFollowing || _path == null || _currentWaypointIndex >= _path.Count) return;

        Transform targetWaypoint = _path[_currentWaypointIndex];
        
        if (targetWaypoint == null) return;
        
        // Ignore Y axis when checking distance so balls don't get stuck orbiting a waypoint slightly above/below them
        Vector3 currentPos = transform.position;
        Vector3 targetPos = targetWaypoint.position;
        
        Vector3 flatTargetPos = targetPos;
        flatTargetPos.y = currentPos.y; 

        float distance = Vector3.Distance(currentPos, flatTargetPos);
        
        if (distance <= distanceToWaypoint)
        {
            _currentWaypointIndex++;
            
            if (_currentWaypointIndex >= _path.Count)
            {
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

    }
}