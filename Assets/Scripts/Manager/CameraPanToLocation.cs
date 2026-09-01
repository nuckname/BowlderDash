using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum CameraLocation 
{ 
    PinCameraLocation, 
    BallThrowingCameraLocation, 
    ShopCameraLocation,
    SpawnBossObjectsCameraPosition,
    ScoringCameraLocation,
    RoundOverCameraLocation,
    ShowInGameUiLocation,
    
    ScoreToBeat,
    BallsThrow
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

    [Header("Transform Targets")]
    public List<CameraLocationMapping> cameraMappings = new List<CameraLocationMapping>();

    private Coroutine activeLerp;

    public void MoveCameraTo(CameraLocation location)
    {
        Transform target = mainCamera.transform; // Fallback

        foreach (var mapping in cameraMappings)
        {
            if (mapping.location == location)
            {
                target = mapping.target;
                break;
            }
        }

        if (activeLerp != null) StopCoroutine(activeLerp);
        activeLerp = StartCoroutine(LerpCamera(target));
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
    }
}