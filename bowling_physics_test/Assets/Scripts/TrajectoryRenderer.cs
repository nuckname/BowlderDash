using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class TrajectoryRenderer : MonoBehaviour
{
    private LineRenderer lineRenderer;
    
    [Header("Trajectory Settings")]
    [Tooltip("Maximum length the aiming line will draw down the lane.")]
    public float maxTrajectoryDistance = 15f; 
    public int segments = 30; 
    
    private BallPhysics physicsData;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = segments + 1;
        
        // Ensure the line scales appropriately in world space
        lineRenderer.useWorldSpace = true;
    }

    public void Initialize(BallPhysics ballPhys)
    {
        physicsData = ballPhys;
    }

    
    // Calculates a visual prediction of the ball's path.
    
    public void DrawTrajectory(Vector3 startPos, float launchAngleY, float spinValue)
    {
        if (physicsData == null) return;
        
        // Convert Aim angle into forward/right local directions
        Vector3 forwardDir = Quaternion.Euler(0, launchAngleY, 0) * Vector3.forward;
        Vector3 rightDir = Quaternion.Euler(0, launchAngleY, 0) * Vector3.right;
        
        float stepSize = maxTrajectoryDistance / segments;
        
        for (int i = 0; i <= segments; i++)
        {
            float currentDist = i * stepSize;
            
            // We use 'currentDist' as a literal mock "Time" parameter (t).
            float mockTime = currentDist * 0.1f; 
            
            float hookOffset = (spinValue * physicsData.hookMultiplier) * 0.5f * (mockTime * mockTime);
            
            // Cap the visual arc proportionally, just as maxHookSpeed caps it in reality
            float maxVisualOffset = physicsData.maxHookSpeed * mockTime;
            if (Mathf.Abs(hookOffset) > maxVisualOffset) 
            {
                hookOffset = Mathf.Sign(hookOffset) * maxVisualOffset;
            }

            Vector3 pointPos = startPos + (forwardDir * currentDist) + (rightDir * hookOffset);
            
            // Keep the line flush with the floor but slightly elevated so it doesn't clip
            pointPos.y = 0.1f; 
            
            lineRenderer.SetPosition(i, pointPos);
        }
    }

    public void Hide()
    {
        lineRenderer.positionCount = 0;
    }

    public void Show()
    {
        lineRenderer.positionCount = segments + 1;
    }
}
