using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BallRampFollower : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveForce = 10f;
    [SerializeField] private float distanceToWaypoint = 1.0f;

    private List<Transform> _path;
    private int _currentWaypointIndex = 0;
    private Rigidbody _rb;
    private bool _isFollowing = false;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
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
        
        Vector3 directionToWaypoint = (targetWaypoint.position - transform.position).normalized;
        
        _rb.AddForce(directionToWaypoint * moveForce, ForceMode.Force);

        float distance = Vector3.Distance(transform.position, targetWaypoint.position);
        
        if (distance <= distanceToWaypoint)
        {
            _currentWaypointIndex++;
            
            if (_currentWaypointIndex >= _path.Count)
            {
                _isFollowing = false;
            }
        }
    }
}