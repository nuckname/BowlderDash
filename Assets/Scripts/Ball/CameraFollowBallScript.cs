using UnityEngine;

public class CameraFollowBallScript : MonoBehaviour
{
    [Header("Camera & Tracking Targets")]
    public Camera targetCamera;
    public BallController ballController;
    public Transform cameraFinalTransform; 

    [Header("Camera Following Settings")]
    public float cameraSmoothSpeed = 0.125f;
    [Tooltip("How many Z-units before the final transform should the camera start blending into its final position/rotation?")]
    public float smoothBlendDistance = 5f;

    // Internal Camera Tracking State
    private bool isTrackingBall = false;
    private Vector3 cameraOffset;
    private Quaternion cameraInitialRotation;

    public void StartTracking()
    {
        if (targetCamera != null && ballController != null)
        {
            cameraOffset = targetCamera.transform.position - ballController.transform.position;
            cameraInitialRotation = targetCamera.transform.rotation;
            isTrackingBall = true;
        }
    }

    public void StopTracking()
    {
        isTrackingBall = false;
    }

    void LateUpdate()
    {
        if (!isTrackingBall) return;

        // 1. Calculate where the camera normally wants to be to follow the ball
        float rawFollowZ = ballController.transform.position.z + cameraOffset.z;
        Vector3 followPosition = new Vector3(targetCamera.transform.position.x, cameraOffset.y, rawFollowZ);
        
        Vector3 desiredPosition = followPosition;
        Quaternion desiredRotation = cameraInitialRotation;

        if (cameraFinalTransform != null)
        {
            float distanceToEnd = cameraFinalTransform.position.z - rawFollowZ;
            
            float blendFactor = 1f - Mathf.Clamp01(distanceToEnd / smoothBlendDistance);

            // Blend the position and rotation based on that percentage
            desiredPosition = Vector3.Lerp(followPosition, cameraFinalTransform.position, blendFactor);
            desiredRotation = Quaternion.Slerp(cameraInitialRotation, cameraFinalTransform.rotation, blendFactor);
        }

        // Apply the final smoothed movement
        targetCamera.transform.position = Vector3.Lerp(targetCamera.transform.position, desiredPosition, cameraSmoothSpeed);
        targetCamera.transform.rotation = Quaternion.Slerp(targetCamera.transform.rotation, desiredRotation, cameraSmoothSpeed);
    }
}