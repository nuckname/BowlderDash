using UnityEngine;

[CreateAssetMenu(fileName = "ShopCost", menuName = "Shop Cost")]
public class ShopCostSO : ScriptableObject
{
    [System.Serializable]
    public struct RoundCost
    {
        public string roundName;
        public int costModifier;
    }

    public RoundCost[] roundCostModifiers = new RoundCost[]
    {
        new RoundCost { roundName = "Round 0", costModifier = 1 },
        new RoundCost { roundName = "Round 1", costModifier = 1 },
        new RoundCost { roundName = "Round 2", costModifier = 1 },
        new RoundCost { roundName = "Round 3", costModifier = 1 },
        new RoundCost { roundName = "Round 4", costModifier = 2 },
        new RoundCost { roundName = "Round 5", costModifier = 2 },
        new RoundCost { roundName = "Round 6", costModifier = 2 },
        new RoundCost { roundName = "Round 7", costModifier = 4 },
        new RoundCost { roundName = "Round 8", costModifier = 4 }
    };
}
