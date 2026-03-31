using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BowlingControllerV1 : MonoBehaviour
{
    // define variables
    public float maxPower = 1200f;
    public float sideForceMult = 4f;
    public float spinMult = 8f;

    private Rigidbody rb;

    private Vector2 startMouse;
    private Vector2 currentMouse;

    private bool dragging = false;
    private bool launched = false;

    private float power;
    private float aimOffset;
    private float spin;
    private float maxPull;

    private float mousePos;

    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        
       
        if (launched) return;

        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("Mouse Clicked");
            startMouse = Input.mousePosition;
            dragging = true;
            maxPull = 0;
        }
        if (dragging)
        {
            Debug.Log("Dragging");
            currentMouse = Input.mousePosition;

            float verticalDrag = startMouse.y - currentMouse.y;
            maxPull = Mathf.Max(maxPull, verticalDrag);

            power = Mathf.Clamp(maxPull * 5f, 0, maxPower);

            aimOffset = (currentMouse.x - startMouse.x) * 0.01f;

            spin = (currentMouse.x - startMouse.x) * spinMult;

            // release the ball when past the start point

            if (currentMouse.y > startMouse.y)
            {
                Launch();
                dragging = false;
            }
        }
        if (Input.GetMouseButtonUp(0))
        {
            dragging = false;
        }
    }
        void Launch()
        {
            if (power < 50f) return;

            Vector3 force = new Vector3(aimOffset * sideForceMult, 0, power);
            rb.AddForce(force);

            // rolling
            rb.AddTorque(Vector3.right * power * 0.3f);

        //hook spin
        if (mousePos > 0)
        {
            rb.AddTorque(Vector3.forward * spin);
        }
        if (mousePos < 0)
        {
            rb.AddTorque(Vector3.back * spin);
        }
            launched = true;
        }
    }

