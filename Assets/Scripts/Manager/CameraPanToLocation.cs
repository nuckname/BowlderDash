using UnityEngine;
using System.Collections;

public enum CameraLocation 
{ 
    PinCameraLocation, 
    BallThrowingCameraLocation, 
    ShopCameraLocation 
}

public class CameraPanToLocation : MonoBehaviour
{
    public Camera mainCamera;
    public float lerpSpeed = 5f;

    [Header("Transform Targets")]
    public Transform pinCameraTarget;
    public Transform ballThrowingTarget;
    public Transform shopCameraTarget;

    private Coroutine activeLerp;

    public void MoveCameraTo(CameraLocation location)
    {
        Transform target = location switch
        {
            CameraLocation.PinCameraLocation => pinCameraTarget,
            CameraLocation.BallThrowingCameraLocation => ballThrowingTarget,
            CameraLocation.ShopCameraLocation => shopCameraTarget,
            _ => mainCamera.transform
        };

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