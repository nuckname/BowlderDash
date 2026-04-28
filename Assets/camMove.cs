using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class camMove : MonoBehaviour
{
    public Transform target;
    public Transform finalTransform;
    public Vector3 offset = new Vector3(0, 2, -5);
    public float smoothSpeed = 0.125f;
    public float transitionDistance = 10f;
    public float transitionSmoothness = 0.5f;
    public float returnSmoothness = 5f;

    private float currentTransitionT = 0f;
    private Quaternion initialRotation;

    void Start()
    {
        if (target != null && (offset == Vector3.zero || offset.magnitude < 0.1f))
        {
            offset = transform.position - target.position;
        }

        initialRotation = transform.rotation;
    }

    void Update()
    {
        if (target == null) return;

        Vector3 followPosition = new Vector3(offset.x, offset.y, target.position.z + offset.z);
        
        Vector3 desiredPosition = followPosition;
        Quaternion desiredRotation = initialRotation;

        if (finalTransform != null)
        {
            float distRemaining = finalTransform.position.z - followPosition.z;
            float targetT = 1f - Mathf.Clamp01(distRemaining / transitionDistance);
            
            bool isReturning = targetT < currentTransitionT;
            float speed = isReturning ? returnSmoothness : transitionSmoothness;
            currentTransitionT = Mathf.Lerp(currentTransitionT, targetT, speed * Time.deltaTime);
            
            float smoothT = isReturning ? currentTransitionT : currentTransitionT * currentTransitionT * (3f - 2f * currentTransitionT);
            
            desiredPosition = Vector3.Lerp(followPosition, finalTransform.position, smoothT);
            desiredRotation = Quaternion.Slerp(initialRotation, finalTransform.rotation, smoothT);
        }

        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, smoothSpeed);
    }
}
