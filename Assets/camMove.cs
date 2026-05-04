using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class camMove : MonoBehaviour
{
    public Transform target;
    public Transform finalTransform;
    public Transform placementTransform;
    private Vector3 offset = new Vector3(0, 2, -5);
    public float smoothSpeed = 0.125f;
    public float transitionDistance = 10f;
    public float transitionSmoothness = 0.5f;
    public float returnSmoothness = 5f;

    private float currentTransitionT = 0f;
    private float placementBlend = 0f;
    private float placementTransitionTime = 1f;
    private bool placementModeActive = false;
    private Quaternion initialRotation;

    void Start()
    {

        offset = transform.position - target.position;

        initialRotation = transform.rotation;
    }

    void Update()
    {
        float targetPlacementBlend = (placementModeActive && placementTransform != null) ? 1f : 0f;
        if (!Mathf.Approximately(placementBlend, targetPlacementBlend))
        {
            float duration = Mathf.Max(0.0001f, placementTransitionTime);
            placementBlend = Mathf.MoveTowards(placementBlend, targetPlacementBlend, Time.deltaTime / duration);
        }

        bool canFollowBall = target != null;
        bool canUsePlacement = placementTransform != null && placementBlend > 0f;
        if (!canFollowBall && !canUsePlacement) return;

        Vector3 followPosition = transform.position;
        Quaternion followRotation = transform.rotation;

        if (canFollowBall)
        {
            GetFollowPose(out followPosition, out followRotation);
        }

        Vector3 desiredPosition = followPosition;
        Quaternion desiredRotation = followRotation;

        if (canUsePlacement)
        {
            float smoothPlacementBlend = placementBlend * placementBlend * (3f - 2f * placementBlend);
            desiredPosition = Vector3.Lerp(followPosition, placementTransform.position, smoothPlacementBlend);
            desiredRotation = Quaternion.Slerp(followRotation, placementTransform.rotation, smoothPlacementBlend);
        }

        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, smoothSpeed);
    }

    public void SetPlacementMode(bool placementMode, Transform placementTarget, float transitionTime)
    {
        placementModeActive = placementMode;
        placementTransform = placementTarget;
        placementTransitionTime = Mathf.Max(0.0001f, transitionTime);

        if (transitionTime <= 0f)
        {
            placementBlend = placementModeActive && placementTransform != null ? 1f : 0f;
        }
    }

    private void GetFollowPose(out Vector3 position, out Quaternion rotation)
    {
        Vector3 followPosition = new Vector3(transform.position.x, offset.y, target.position.z + offset.z);
        position = followPosition;
        rotation = initialRotation;

        if (finalTransform != null)
        {
            float distRemaining = finalTransform.position.z - followPosition.z;
            float targetT = 1f - Mathf.Clamp01(distRemaining / transitionDistance);

            bool isReturning = targetT < currentTransitionT;
            float speed = isReturning ? returnSmoothness : transitionSmoothness;
            currentTransitionT = Mathf.Lerp(currentTransitionT, targetT, speed * Time.deltaTime);

            float smoothT = isReturning ? currentTransitionT : currentTransitionT * currentTransitionT * (3f - 2f * currentTransitionT);
            position = Vector3.Lerp(followPosition, finalTransform.position, smoothT);
            rotation = Quaternion.Slerp(initialRotation, finalTransform.rotation, smoothT);
        }
    }
}
