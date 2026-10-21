using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public sealed class RouletteTicketVisual : MonoBehaviour
{
    [SerializeField] private TMP_Text countLabel;
    private readonly HashSet<Collider> supports = new HashSet<Collider>();
    private Rigidbody body;
    private float settledTime;

    public Rigidbody Body => body != null ? body : body = GetComponent<Rigidbody>();
    public int Units { get; private set; }

    public bool HasAuthoredSetup(int layer)
    {
        if (layer < 0 || !gameObject.activeSelf || Body == null || Body.isKinematic || !Body.useGravity ||
            countLabel == null || countLabel.transform == transform || !countLabel.transform.IsChildOf(transform) ||
            GetComponentsInChildren<Rigidbody>(true).Length != 1) return false;
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
            if (child.gameObject.layer != layer || !child.CompareTag("Untagged")) return false;
        // The root body owns the child colliders before the prefab is spawned
        foreach (Collider collider in GetComponentsInChildren<Collider>(true))
            if (!collider.enabled || collider.isTrigger) return false;
        foreach (MonoBehaviour component in GetComponentsInChildren<MonoBehaviour>(true))
            if (!(component is RouletteTicketVisual) && !(component is TMP_Text) &&
                !(component is TMP_SubMesh) && !(component is TMP_SubMeshUI)) return false;
        return GetComponent<BoxCollider>().enabled && GetComponentsInChildren<AudioSource>(true).Length == 0;
    }

    public void SetUnits(int units)
    {
        Units = units;
        if (countLabel == null) return;
        countLabel.text = $"x{units}";
        countLabel.raycastTarget = false;
        countLabel.enabled = units > 1;
    }

    public bool CanConsolidate(float minimumSettledTime)
    {
        return Body != null && !Body.isKinematic && settledTime >= minimumSettledTime;
    }

    private void FixedUpdate()
    {
        supports.RemoveWhere(collider => collider == null);
        if (Body == null || Body.isKinematic || supports.Count == 0 ||
            Body.linearVelocity.sqrMagnitude > 0.01f || Body.angularVelocity.sqrMagnitude > 0.04f)
            settledTime = 0f;
        else settledTime += Time.fixedDeltaTime;
    }

    private void OnCollisionEnter(Collision collision) => UpdateSupport(collision);
    private void OnCollisionStay(Collision collision) => UpdateSupport(collision);

    private void UpdateSupport(Collision collision)
    {
        bool supported = false;
        for (int i = 0; i < collision.contactCount; i++)
            if (Vector3.Dot(collision.GetContact(i).normal, Vector3.up) >= 0.5f) supported = true;
        if (supported) supports.Add(collision.collider);
        else supports.Remove(collision.collider);
    }

    private void OnCollisionExit(Collision collision) => supports.Remove(collision.collider);

    private void OnDisable()
    {
        supports.Clear();
        settledTime = 0f;
    }
}
