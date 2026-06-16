using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct CurvePoint
{
    public Vector3 position;
    public int directionIndex; 
}

public interface IPathDrawListener
{
    void OnDrawCompleted(List<CurvePoint> path, float drawDuration);
    void OnDrawCancelled();
    void OnInstructionUpdated(string message);
}