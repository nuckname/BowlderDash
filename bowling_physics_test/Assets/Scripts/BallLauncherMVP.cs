using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallLauncherMVP : MonoBehaviour
{
    public float launchForce = 500f;
    private Rigidbody rb;
    private bool launched = false;
    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        if (!launched && Input.GetKeyDown(KeyCode.Space))
        {
            Launch();
        }
    }
    void Launch()
    {
        rb.AddForce(Vector3.forward * launchForce);
        launched = true;
    }
}
