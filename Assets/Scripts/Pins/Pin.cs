using UnityEngine;

public class Pin : PinBase
{
    [Header("Scoring Values")]
    public int score = 10;
    public int mult  = 0;

    [Header("Physics Rules")]
    public float fallAngleThreshold = 45f;

    private bool _isFallen = false;

    public bool allowPinToFallOver = false;

    void Update()
    {
        if (!allowPinToFallOver) return;
        if (_isFallen) return;

        if (Vector3.Angle(Vector3.up, transform.up) > fallAngleThreshold)
        {
            TriggerFall();
        }
    }

    private void TriggerFall()
    {
        _isFallen = true;
        NotifyFallen();
    }

    public bool IsFallen() => _isFallen;

    public override void ResetPin()
    {
        _isFallen = false;

        base.ResetPin();

//        GetComponent<explodingPin>()?.ResetPin();
  //      GetComponent<vasePin>()?.ResetPin();
    }
}