using UnityEngine;

[DisallowMultipleComponent]
public class BossSpawnArea : MonoBehaviour
{
    [SerializeField] private Vector3 center = Vector3.zero;
    [SerializeField] private Vector3 size = Vector3.one;

    public Vector3 GetRandomPoint()
    {
        Vector3 halfSize = GetPositiveSize() * 0.5f;
        Vector3 localPoint = center + new Vector3(
            Random.Range(-halfSize.x, halfSize.x),
            Random.Range(-halfSize.y, halfSize.y),
            Random.Range(-halfSize.z, halfSize.z));

        return transform.TransformPoint(localPoint);
    }

    private Vector3 GetPositiveSize()
    {
        return new Vector3(
            Mathf.Abs(size.x),
            Mathf.Abs(size.y),
            Mathf.Abs(size.z));
    }

    private void OnDrawGizmos()
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.25f, 0.1f, 0.8f);
        Gizmos.DrawWireCube(center, GetPositiveSize());

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}
