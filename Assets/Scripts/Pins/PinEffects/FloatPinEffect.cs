using System;
using System.Collections.Generic;
using UnityEngine;

// https://share.gemini.google/yFDz2rQIO36j
[Serializable]
public class FloatPinEffect : PinEffect
{
    [SerializeField] private float floatSpeed = 1f;
    [SerializeField] private float floatDuration = 5f;

    [SerializeField] private List<Vector2Int> adjacentOffsets = new List<Vector2Int>
    {
        new Vector2Int(-1, 0) 
    };

    public override void Execute(PinBase pinBase, Collider target = null)
    {
        GameObject hexManagerObj = GameObject.FindGameObjectWithTag("HexManager");
        if (hexManagerObj == null) return;

        HexGrid gridManager = hexManagerObj.GetComponent<HexGrid>();
        if (gridManager == null) return;

        HexCell originCell = gridManager.GetNearestCell(pinBase.transform.position);
        if (originCell == null) return;

        foreach (Vector2Int offset in adjacentOffsets)
        {
            HexCell targetCell = gridManager.GetCellWithOffset(originCell, offset);
            
            if (targetCell != null && targetCell.occupant != null)
            {
                PinBase leftPin = targetCell.occupant.GetComponentInParent<PinBase>();
                
                if (leftPin != null && leftPin != pinBase)
                {
                    leftPin.AllowPinToFallOver = false;
                    
                    Rigidbody rb = leftPin.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.useGravity = false;
                        rb.isKinematic = true;
                    }
                    
                    if (leftPin.GetComponent<FloatUpBehavior>() == null)
                    {
                        FloatUpBehavior floatBehavior = leftPin.gameObject.AddComponent<FloatUpBehavior>();
                        floatBehavior.speed = floatSpeed;
                        floatBehavior.duration = floatDuration; 
                    }
                }
            }
        }
    }

    public override string GetEffectText()
    {
        return "Left Pin Floating!";
    }
}

public class FloatUpBehavior : MonoBehaviour
{
    public float speed = 1f;
    public float duration = 1f;
    
    private float _timer = 0f;
    private Vector3 _startPosition;
    private Quaternion _startRotation;

    private void Start()
    {
        _startPosition = transform.position;
        _startRotation = transform.rotation;
    }

    private void Update()
    {
        if (GameplayPause.IsPaused(this)) return;
        if (_timer < duration)
        {
            transform.Translate(Vector3.up * (speed * Time.deltaTime), Space.World);
            _timer += Time.deltaTime;
        }
        else
        {
            transform.position = _startPosition;
            transform.rotation = _startRotation;

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = true;
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            PinBase pinBase = GetComponent<PinBase>();
            if (pinBase != null)
            {
                pinBase.AllowPinToFallOver = true;
            }

            Destroy(this);
        }
    }
}
