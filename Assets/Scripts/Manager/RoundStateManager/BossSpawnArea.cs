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

    public bool TryGetRandomPair(float minimumHorizontalSpacing, int maxAttempts,
        out Vector3 firstPoint, out Vector3 secondPoint)
    {
        firstPoint = default;
        secondPoint = default;

        float minimumSpacingSquared = Mathf.Max(0f, minimumHorizontalSpacing);
        minimumSpacingSquared *= minimumSpacingSquared;

        for (int attempt = 0; attempt < Mathf.Max(1, maxAttempts); attempt++)
        {
            Vector3 firstCandidate = GetRandomPoint();
            Vector3 secondCandidate = GetRandomPoint();
            Vector2 horizontalOffset = new Vector2(
                firstCandidate.x - secondCandidate.x,
                firstCandidate.z - secondCandidate.z);

            if (horizontalOffset.sqrMagnitude < minimumSpacingSquared)
                continue;

            firstPoint = firstCandidate;
            secondPoint = secondCandidate;
            return true;
        }

        return false;
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
