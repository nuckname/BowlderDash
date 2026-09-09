using System;
using System.Collections;
using UnityEngine;

[Serializable]
public class ScoreMultiplierEffect : PinEffect 
{
    public int scoreValue = 10;
    //public ParticleSystem particleSystem;

    public override void Execute(PinBase context, Collider instigator = null) 
    {
        context.FrameScoreCalculator.QueueScoreModification(scoreValue, context.transform.position);
    }
    
    public override string GetEffectText()
    {
        return scoreValue.ToString();
    }
}