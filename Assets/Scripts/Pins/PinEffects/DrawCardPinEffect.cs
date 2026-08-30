using UnityEngine;

[System.Serializable]
public class DrawCardPinEffect : PinEffect
{
    [SerializeField] private int drawAmount = 2;

    public override void Execute(PinBase pin, Collider other = null)
    {
        DeckManager.Instance.Draw(drawAmount);
    }

    public override string GetEffectText()
    {
        // ?
        return $"+{drawAmount} Cards"; 
    }
}