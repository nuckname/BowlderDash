using UnityEngine;

public class Tumbling : MonoBehaviour
{
    public GameObject objectToRotate;

    public Transform lookTowards;
    public float lessIntensity = 1f;

    private void Update()
    {
        if (objectToRotate == null || lookTowards == null)
        {
            return;
        }

        Vector3 targetPos = lookTowards.position + Vector3.up * lessIntensity;

        Vector3 direction = targetPos - objectToRotate.transform.position;

        if (direction.sqrMagnitude > 0.0001f)
        {
            objectToRotate.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
        }
        else
        {
            objectToRotate.transform.rotation = Quaternion.identity;
        }
    }

    public void setTumbling(bool enabled)
    {
        lookTowards.gameObject.GetComponent<Rigidbody>().isKinematic = !enabled;
        lookTowards.gameObject.GetComponent<Collider>().enabled = enabled;
        lookTowards.gameObject.GetComponent<MeshCollider>().enabled = enabled;
        Debug.Log($"<color=orange><b>[Tumbling]</b></color> Setting tumbling to {enabled}");
    }
}
