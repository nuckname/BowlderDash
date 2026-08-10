using System;
using UnityEngine;

[Serializable]
public class PinataEffect : PinEffect
{
    public override void Execute(PinBase pin, Collider target)
    {
        // On Collision, Add a ticket
    }
    
    public override void ResetEffect()
    {
    }
}
