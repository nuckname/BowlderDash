using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BallPhysics : MonoBehaviour
{
    private Rigidbody rb;
    
    [Header("Physics Settings")]
    [Tooltip("How much lateral force the spin generates for the hook.")]
    public float hookMultiplier = 2.5f; 
    [Tooltip("Cap on the sideways hook force.")]
    public float maxHookSpeed = 8f;   

    private float currentSpin = 0f; 
    private bool isRolling = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        // Increase default max angular velocity so visual spinning isn't capped prematurely
        rb.maxAngularVelocity = 50f; 
    }

    
    // Throws the ball with a forward force + spin.
    public void ThrowBall(Vector3 throwForce, float spinAmount)
    {
        currentSpin = spinAmount;
        isRolling = true;
        
        // Unfreeze constraints in case they were locked during the aiming phase
        rb.constraints = RigidbodyConstraints.None;

        // Apply sudden forward throw force
        rb.AddForce(throwForce, ForceMode.Impulse);

        // Apply physical torque for visual forward-roll and curve
        // Right curve applies opposite visual rotation
        Vector3 visualTorque = new Vector3(throwForce.z, spinAmount * 50f, -throwForce.x) * rb.mass * 2f;
        rb.AddTorque(visualTorque, ForceMode.Impulse); 
    }

    void FixedUpdate()
    {
        if (!isRolling) return;

        if (Mathf.Abs(currentSpin) > 0.01f)
        {
   
            Vector3 forwardVel = new Vector3(rb.velocity.x, 0, rb.velocity.z);
            
            if (forwardVel.magnitude > 0.1f)
            {
                // Calculate a perpendicular lateral vector based on travel direction
                Vector3 lateralDir = Vector3.Cross(Vector3.up, forwardVel.normalized);
                
                // Apply a lateral force (currentSpin > 0 is right curve, < 0 is left curve)
                // Multiplying by forward velocity magnitude makes the hook 'bite' harder when moving fast
                Vector3 hookForce = lateralDir * (currentSpin * hookMultiplier);
                
                // Cap the force
                hookForce = Vector3.ClampMagnitude(hookForce, maxHookSpeed);
                
                rb.AddForce(hookForce, ForceMode.Force);
            }
        }
    }
}
