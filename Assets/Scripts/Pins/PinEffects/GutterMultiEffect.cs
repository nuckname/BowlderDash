using System;
using UnityEngine;

[Serializable]
public class GutterMultiEffect : PinEffect 
{
    public float multValue = 0.75f;

    public override void Execute(PinBase context, Collider instigator = null) 
    {
        if (context.hasEnterGutter || context.hasEnterTheHoleAtTheBack)
        {
            context.FrameScoreCalculator.QueueScoreMulti(multValue, context.transform.position);
        }
    }
    
    public override string GetEffectText()
    {
        return "* " + multValue;
    }
}