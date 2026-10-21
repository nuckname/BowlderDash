using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
public sealed class RoulettePocket : MonoBehaviour
{
    [SerializeField, Range(0, 31)] private int pocketId;
    [SerializeField] private RouletteColour colour;

    private BoxCollider seat;
    public int PocketId => pocketId;
    public RouletteColour Colour => colour;
    public BoxCollider Seat => seat != null ? seat : seat = GetComponent<BoxCollider>();

    public bool HasAuthoredSetup(Rigidbody wheel, int pocketLayer)
    {
        return isActiveAndEnabled && pocketId >= 0 && pocketId < 32 &&
            (colour == RouletteColour.Red || colour == RouletteColour.Black || colour == RouletteColour.Green) &&
            wheel != null && transform != wheel.transform && transform.IsChildOf(wheel.transform) &&
            gameObject.layer == pocketLayer && gameObject.CompareTag("Untagged") &&
            Seat != null && Seat.enabled && Seat.isTrigger && Seat.attachedRigidbody == wheel &&
            GetComponentsInChildren<Collider>(true).Length == 1 &&
            RouletteWheelMotor.IsFinite(transform.lossyScale) &&
            transform.lossyScale.x > 0f && transform.lossyScale.y > 0f && transform.lossyScale.z > 0f &&
            RouletteWheelMotor.IsFinite(Seat.center) && RouletteWheelMotor.IsFinite(Seat.size) &&
            Seat.size.x > 0f && Seat.size.y > 0f && Seat.size.z > 0f;
    }

    public bool ContainsBallCentre(Rigidbody registeredBall)
    {
        if (!isActiveAndEnabled || registeredBall == null || !registeredBall.gameObject.activeInHierarchy ||
            registeredBall.isKinematic || Seat == null || !Seat.enabled || !Seat.isTrigger ||
            !RouletteWheelMotor.IsFinite(registeredBall.worldCenterOfMass)) return false;

        // Check the seat directly in case a trigger exit is missed
        Vector3 local = Seat.transform.InverseTransformPoint(registeredBall.worldCenterOfMass) - Seat.center;
        Vector3 half = Seat.size * 0.5f;
        return RouletteWheelMotor.IsFinite(local) && Mathf.Abs(local.x) <= half.x &&
            Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
    }

    private void OnDrawGizmosSelected()
    {
        if (Seat == null) return;
        Gizmos.color = colour == RouletteColour.Red ? Color.red :
            colour == RouletteColour.Black ? Color.gray : Color.green;
        Gizmos.matrix = Seat.transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Seat.center, Seat.size);
    }
}
