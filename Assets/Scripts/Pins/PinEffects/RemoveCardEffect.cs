using System;
using UnityEngine;

[Serializable]
public class RemoveCardEffect : PinEffect
{
    public string cardIdToRemove;

    public override void Execute(PinBase pinBase, Collider target = null)
    {
        DeckManager.Instance.RemoveCardByID(cardIdToRemove);
    }
}