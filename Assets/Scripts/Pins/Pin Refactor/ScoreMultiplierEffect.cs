using System;
using System.Collections;
using UnityEngine;

[Serializable]
public class ScoreMultiplierEffect : PinEffect 
{
    public int scoreValue = 10;
    public float multValue = 0.5f;
    //public ParticleSystem particleSystem;

    public override void Execute(PinBase context, Collider instigator = null) 
    {
        context.FrameScoreCalculator.QueueScoreModification(scoreValue, multValue, context.transform.position);
    }
    
    public override string GetEffectText()
    {
        return scoreValue.ToString();
    }
}