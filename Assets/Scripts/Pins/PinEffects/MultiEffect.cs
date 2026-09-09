using System;
using UnityEngine;

[Serializable]
public class MultiEffect : PinEffect 
{
    public float multValue = 0.5f;
    
    public ParticleSystem particleSystem;

    public override void Execute(PinBase context, Collider instigator = null) 
    {
        context.FrameScoreCalculator.QueueScoreMulti(multValue, context.transform.position);
        //particleSystem.Play();
    }

    public override void ResetEffect() 
    {
        // particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
    
    public override string GetEffectText()
    {
        return "* " + multValue;
    }
}