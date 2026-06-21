using System;
using System.Collections;
using UnityEngine;

[Serializable]
public class ScoreMultiplierEffect : PinEffect 
{
    public int scoreValue = 10;
    public float multValue = 0.5f;
    public ParticleSystem particleSystem;

    public override void Execute(PinBase context, Collider instigator = null) 
    {
        GameManager.Instance?.scoreCalculator?.QueueScoreModification(scoreValue, multValue, context.transform.position);
        particleSystem?.Play();
    }
}