using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;

public enum CameraLocation 
{ 
    PinCameraLocation, 
    BallThrowingCameraLocation, 
    ShopCameraLocation,
    SpawnBossObjectsCameraPosition,
    ScoringCameraLocation,
    
    TicketCameraUi,
    ScoreToBeatUi,
    BallsThrowUi,
    ShowIncreaseRound
}

[System.Serializable]
public struct CameraLocationMapping
{
    public CameraLocation location;
    public Transform target;
}

public class CameraPanToLocation : MonoBehaviour
{
    public Camera mainCamera;
    public float lerpSpeed = 3f;

    [SerializeField] private bool showDebugMenu = true;
    
    [Header("Transform Targets")]
    public List<CameraLocationMapping> cameraMappings = new List<CameraLocationMapping>();

    private Coroutine activeLerp;

    public void MoveCameraTo(CameraLocation location)
    {
        CancelActiveMove();

        if (!TryGetCameraTarget(location, out Transform target))
        {
            Debug.LogWarning($"No camera target is assigned for {location}.");
            return;
        }

        activeLerp = StartCoroutine(LerpCamera(target));
    }

    public bool TryGetCameraTarget(CameraLocation location, out Transform target)
    {
        foreach (CameraLocationMapping mapping in cameraMappings)
        {
            if (mapping.location == location && mapping.target != null)
            {
                target = mapping.target;
                return true;
            }
        }

        target = null;
        return false;
    }

    public void CancelActiveMove()
    {
        if (activeLerp == null) return;

        StopCoroutine(activeLerp);
        activeLerp = null;
    }

    private IEnumerator LerpCamera(Transform target)
    {
        while (Vector3.Distance(mainCamera.transform.position, target.position) > 0.01f || 
               Quaternion.Angle(mainCamera.transform.rotation, target.rotation) > 0.01f)
        {
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, target.position, Time.deltaTime * lerpSpeed);
            mainCamera.transform.rotation = Quaternion.Lerp(mainCamera.transform.rotation, target.rotation, Time.deltaTime * lerpSpeed);
            yield return null;
        }

        mainCamera.transform.SetPositionAndRotation(target.position, target.rotation);
        activeLerp = null;
    }
    
    // Debug GUI for testing camera locations, turn on showDebugMenu and look top right, just ai slop
    private void OnGUI()
    {
        // This will hide the entire GUI when set to false in the Inspector or by another script
        if (!showDebugMenu) return;

        GUILayout.BeginArea(new Rect(10, 10, 250, 400));
        
        foreach (CameraLocation location in System.Enum.GetValues(typeof(CameraLocation)))
        {
            if (GUILayout.Button(location.ToString(), GUILayout.Height(30)))
            {
                MoveCameraTo(location);
            }
        }
        
        GUILayout.EndArea();
    }
}
