using System;
using UnityEngine;
// https://share.gemini.google/oyy5yFMYdH97
// Currently not working
[Serializable]
public class TeleportBallPinEffect : PinEffect
{
    public override void Execute(PinBase pinBase, Collider target = null)
    {
        if (pinBase == null) return;

        GameObject ball = GameObject.FindGameObjectWithTag("Ball");
        if (ball == null) return;

        PinBase[] allPins = UnityEngine.Object.FindObjectsOfType<PinBase>();
        PinBase targetPin = null;

        foreach (PinBase pin in allPins)
        {
            if (pin != pinBase && !pin.IsFallen)
            {
                targetPin = pin;
                break;
            }
        }

        if (targetPin != null)
        {
            Vector3 newPos = new Vector3(26.1599998f, 0.69299984f, targetPin.transform.position.z);
            ball.GetComponent<BallAudio>()?.InvalidateForMovement();
            ball.transform.position = newPos;
        }
    }
}
