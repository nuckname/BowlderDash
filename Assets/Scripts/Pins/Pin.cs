using UnityEngine;
using System;

public class Pin : MonoBehaviour
{
    [Header("Scoring Values")]
    public int score = 10;
    public int mult = 0;
    
    [Header("Physics Rules")]
    public float fallAngleThreshold = 45f;

    // State Tracking
    private bool _isFallen = false;
    private Vector3 _startPos;
    private Quaternion _startRot;
    private Rigidbody _rb;

    // Global Event so we don't need a hard-reference to the Score Manager
    public static event Action<Pin> OnPinFallenGlobal;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _startPos = transform.position;
        _startRot = transform.rotation;
    }

    void Update()
    {
        if (BowlingModeSwitcher.isPlacementMode) return;
        if (!_isFallen)
        {
            if (Vector3.Angle(Vector3.up, transform.up) > fallAngleThreshold)
            {
                TriggerFall();
            }
        }
    }

    private void TriggerFall()
    {
        _isFallen = true;
        
        OnPinFallenGlobal?.Invoke(this);
    }
    
    public bool IsFallen()
    {
        return _isFallen;
    }

    public void ResetPin()
    {
        _isFallen = false;
        
        if (_rb != null)
        {
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        transform.position = _startPos;
        transform.rotation = _startRot;

        if(GetComponent<explodingPin>() != null)
        {
            GetComponent<explodingPin>().ResetPin();
        }
    }


    public void UpdateTransform()
    {
        _startPos = transform.position;
        _startRot = transform.rotation;
    }
}
