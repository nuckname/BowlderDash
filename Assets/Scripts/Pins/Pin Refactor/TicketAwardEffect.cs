using System;
using System.Collections;
using UnityEngine;

[Serializable]
public class TicketAwardEffect : PinEffect 
{
    public int ticketValue = 1;
    public ParticleSystem particleSystem;

    public override void Execute(PinBase context, Collider instigator = null) 
    {
        //PlayerSheet.Instance?.AddGridTicketsThisFrame(ticketValue);
        particleSystem?.Play();
    }

    public override void ResetEffect() 
    {
        if (particleSystem != null) 
        {
            particleSystem.Stop();
            particleSystem.Clear();
        }
    }
}