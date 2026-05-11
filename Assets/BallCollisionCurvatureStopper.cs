using UnityEngine;

public class BallCollisionCurvatureStopper : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("The layer(s) that should trigger stopping the curvature force on collision.")]
    public LayerMask stopForceLayer;



    private void OnCollisionEnter(Collision collision)
    {
        if (((1 << collision.gameObject.layer) & stopForceLayer) != 0)
        {
            if (GrandGameManager.Instance != null)
            {
                GrandGameManager.Instance.StopFollowingCurve();
                Debug.Log("<color=orange><b>[BallCollisionCurvatureStopper]</b></color> Pin hit");
            }
        }
    }
}
