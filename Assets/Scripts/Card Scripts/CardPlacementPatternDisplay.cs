using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardPlacementPatternDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform dotContainer;
    [SerializeField] private Sprite placementDotSprite;

    [Header("Layout")]
    [Min(0f)]
    [SerializeField] private float cellSpacing = 11f;
    [Min(0f)]
    [SerializeField] private float dotSize = 5f;
    [SerializeField] private Color dotColor = Color.black;

    private readonly List<GameObject> spawnedDots = new List<GameObject>();

    public void Bind(CardDataSO cardData)
    {
        Clear();

        if (cardData == null || dotContainer == null || placementDotSprite == null)
        {
            return;
        }

        List<Vector2Int> offsets = GetPatternOffsets(cardData);
        List<Vector2> positions = CalculateCenteredPositions(offsets);

        for (int i = 0; i < positions.Count; i++)
        {
            CreateDot(i, positions[i]);
        }
    }

    public void Clear()
    {
        for (int i = 0; i < spawnedDots.Count; i++)
        {
            if (spawnedDots[i] != null)
            {
                Destroy(spawnedDots[i]);
            }
        }

        spawnedDots.Clear();
    }

    private static List<Vector2Int> GetPatternOffsets(CardDataSO cardData)
    {
        List<Vector2Int> offsets = new List<Vector2Int>();

        if (cardData.spawnPattern != null)
        {
            for (int i = 0; i < cardData.spawnPattern.Count; i++)
            {
                PatternPiece piece = cardData.spawnPattern[i];
                if (piece != null)
                {
                    offsets.Add(piece.offset);
                }
            }
        }

        if (offsets.Count == 0)
        {
            offsets.Add(Vector2Int.zero);
        }

        return offsets;
    }

    private List<Vector2> CalculateCenteredPositions(List<Vector2Int> offsets)
    {
        List<Vector2> positions = new List<Vector2>(offsets.Count);
        Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        for (int i = 0; i < offsets.Count; i++)
        {
            Vector2Int offset = offsets[i];
            Vector2 position = new Vector2(
                cellSpacing * (offset.x + (offset.y * 0.5f)),
                -cellSpacing * 0.8660254f * offset.y);

            positions.Add(position);
            minimum = Vector2.Min(minimum, position);
            maximum = Vector2.Max(maximum, position);
        }

        Vector2 geometricCenter = (minimum + maximum) * 0.5f;
        Vector2 anchorPosition = FindClosestPosition(positions, geometricCenter);
        Vector2 halfAvailableSize = Vector2.Max(
            Vector2.zero,
            (dotContainer.rect.size - Vector2.one * dotSize) * 0.5f);

        Vector2 maximumExtent = Vector2.zero;
        for (int i = 0; i < positions.Count; i++)
        {
            positions[i] -= anchorPosition;
            maximumExtent = Vector2.Max(maximumExtent, new Vector2(
                Mathf.Abs(positions[i].x),
                Mathf.Abs(positions[i].y)));
        }

        float scale = 1f;
        if (maximumExtent.x > halfAvailableSize.x && maximumExtent.x > 0f)
        {
            scale = Mathf.Min(scale, halfAvailableSize.x / maximumExtent.x);
        }

        if (maximumExtent.y > halfAvailableSize.y && maximumExtent.y > 0f)
        {
            scale = Mathf.Min(scale, halfAvailableSize.y / maximumExtent.y);
        }

        for (int i = 0; i < positions.Count; i++)
        {
            positions[i] *= scale;
        }

        return positions;
    }

    private static Vector2 FindClosestPosition(List<Vector2> positions, Vector2 target)
    {
        Vector2 closestPosition = positions[0];
        float closestDistanceSquared = (closestPosition - target).sqrMagnitude;

        for (int i = 1; i < positions.Count; i++)
        {
            float distanceSquared = (positions[i] - target).sqrMagnitude;
            if (distanceSquared < closestDistanceSquared)
            {
                closestPosition = positions[i];
                closestDistanceSquared = distanceSquared;
            }
        }

        return closestPosition;
    }

    private void CreateDot(int index, Vector2 anchoredPosition)
    {
        GameObject dotObject = new GameObject(
            $"PlacementDot_{index}",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        dotObject.layer = dotContainer.gameObject.layer;

        RectTransform dotTransform = dotObject.GetComponent<RectTransform>();
        dotTransform.SetParent(dotContainer, false);
        dotTransform.anchorMin = new Vector2(0.5f, 0.5f);
        dotTransform.anchorMax = new Vector2(0.5f, 0.5f);
        dotTransform.pivot = new Vector2(0.5f, 0.5f);
        dotTransform.anchoredPosition = anchoredPosition;
        dotTransform.sizeDelta = Vector2.one * dotSize;

        Image dotImage = dotObject.GetComponent<Image>();
        dotImage.sprite = placementDotSprite;
        dotImage.color = dotColor;
        dotImage.preserveAspect = true;
        dotImage.raycastTarget = false;

        spawnedDots.Add(dotObject);
    }
}
