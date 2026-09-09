using System;
using UnityEngine;

[Serializable]
public class GutterMultiEffect : PinEffect 
{
    public float multValue = 0.25f;
    public string targetTag = "Gutter";

    public override void Execute(PinBase context, Collider instigator = null) 
    {
        if (instigator != null && instigator.CompareTag(targetTag))
        {
            context.FrameScoreCalculator.QueueScoreMulti(multValue, context.transform.position);
        }
    }
    
    public override string GetEffectText()
    {
        return "* " + multValue;
    }
}