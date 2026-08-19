using UnityEngine;

public class ForcePinToStandUpForPhysicalDeck : MonoBehaviour
{

    // This is only used for the pins that dont interact with the gameplay.
    // This is for the pins that are on the left and right.
    void Update()
    {
        Vector3 currentEuler = transform.rotation.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, currentEuler.y, 0f);
    }
}
