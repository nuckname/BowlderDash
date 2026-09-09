using System;
using UnityEngine;

[Serializable]
public class GutterMultiEffect : PinEffect 
{
    public float multValue = 0.25f;
    public string gutterTag = "Gutter";
    public string pitTag = "ThePit";

    public override void Execute(PinBase context, Collider instigator = null) 
    {
        if (instigator != null && instigator.CompareTag(gutterTag) || context.hasEnterTheHoleAtTheBack)
        {
            context.FrameScoreCalculator.QueueScoreMulti(multValue, context.transform.position);
        }
    }
    
    public override string GetEffectText()
    {
        return "* " + multValue;
    }
}