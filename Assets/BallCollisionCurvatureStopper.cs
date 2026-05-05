using UnityEngine;

public class BallCollisionCurvatureStopper : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("The layer(s) that should trigger stopping the curvature force on collision.")]
    public LayerMask stopForceLayer;

    private DrawBowlManager bowlManager;

    private void Start()
    {
        bowlManager = Object.FindAnyObjectByType<DrawBowlManager>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (((1 << collision.gameObject.layer) & stopForceLayer) != 0)
        {
            if (bowlManager != null)
            {
                bowlManager.StopFollowingCurve();
                Debug.Log("<color=orange><b>[BallCollisionCurvatureStopper]</b></color> Pin hit");
            }
        }
    }
}
