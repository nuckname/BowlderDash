using System;
using UnityEngine;

[Serializable]
public class PinataEffect : PinEffect
{
    public override void Execute(PinBase pin, Collider target)
    {
        SessionData.Instance.AddTickets(1);
    }
    
    public override void ResetEffect()
    {
    }
    
    public override string GetEffectText()
    {
        return "+1 Ticket";
    }
}
