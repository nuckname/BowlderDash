using System;
using UnityEngine;

[Serializable]
public class TicketEffect : PinEffect
{
    public int ticketValue = 1;
    
    public ParticleSystem particleSystem;

    // We use a local bool so the pin doesn't spam tickets if it gets hit multiple times
    private bool _hasAwarded = false; 

    public override void Execute(PinBase context, Collider instigator = null)
    {
        if (_hasAwarded) return;

        _hasAwarded = true;
        
        PlayerSheet.Instance.AddTickets(ticketValue);

        //particleSystem.Play();
    }

    public override void ResetEffect()
    {
        _hasAwarded = false;

        //particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}