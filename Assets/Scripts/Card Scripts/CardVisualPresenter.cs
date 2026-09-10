using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum PinLayoutType
{
    None,
    Triangle,
    Vertical
}

[System.Serializable]
public class PinLayout
{
    public PinLayoutType layoutType;   
    public GameObject layoutContainer;
    public List<Image> pinImages = new List<Image>();
    public bool doNotRandomiseValues = false;
}

public class CardVisualPresenter : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("Images")]
    [SerializeField] private Image artworkImage;
    [SerializeField] private List<PinLayout> pinLayouts = new List<PinLayout>();
    
    [Tooltip("Optional image used for the card's presentation tint or accent colour.")]
    [SerializeField] private Graphic tintTarget;

    [Header("Placement Pattern")]
    [SerializeField] private CardPlacementPatternDisplay placementPatternDisplay;

    [Header("Pin Randomisation")]
    [SerializeField] private Vector2 zPositionRange = new Vector2(-3f, -30f);
    [SerializeField] private Vector2 zRotationRange = new Vector2(-10f, 10f);
    [SerializeField] private Vector2 sizeRange = new Vector2(0.75f, 0.8f);
    
    public void Bind(CardDataSO cardData)
    {
        if (cardData == null)
        {
            Clear();
            return;
        }

        if (titleText != null)
        {
            titleText.text = string.IsNullOrWhiteSpace(cardData.displayName)
                ? cardData.cardID
                : cardData.displayName;
        }

        if (descriptionText != null)
        {
            descriptionText.text = cardData.description ?? string.Empty;
        }

        SetArtwork(cardData.cardArtwork);
        ApplyTint(cardData.cardTintCardRarity);
        SetPinImages(cardData);

        if (placementPatternDisplay != null)
        {
            placementPatternDisplay.Bind(cardData);
        }
    }

    public void ApplyTint(Color tint)
    {
        if (tintTarget != null)
        {
            tintTarget.color = tint;
        }
    }

    private void SetPinImages(CardDataSO cardData)
    {
        List<Sprite> pins = cardData?.pinImages ?? new List<Sprite>();
        PinLayoutType targetLayout = cardData?.desiredLayout ?? PinLayoutType.None;

        foreach (PinLayout layout in pinLayouts)
        {
            if (layout == null)
            {
                continue;
            }

            bool isMatchingLayout = layout.layoutType == targetLayout;
            
            if (layout.layoutContainer != null)
            {
                layout.layoutContainer.SetActive(isMatchingLayout && targetLayout != PinLayoutType.None);
            }

            if (isMatchingLayout)
            {
                for (int i = 0; i < layout.pinImages.Count; i++)
                {
                    if (i < pins.Count && pins[i] != null)
                    {
                        layout.pinImages[i].sprite = pins[i];
                        layout.pinImages[i].enabled = true;

                        if (!layout.doNotRandomiseValues)
                        {
                            RectTransform pinRect = layout.pinImages[i].rectTransform;
                        
                            // Rolling new random values individually for each pin in the loop
                            float randomZPos = Random.Range(zPositionRange.x, zPositionRange.y);
                            Vector3 pos = pinRect.localPosition;
                            pos.z = randomZPos;
                            pinRect.localPosition = pos;

                            float randomZRot = Random.Range(zRotationRange.x, zRotationRange.y);
                            pinRect.localEulerAngles = new Vector3(0f, 0f, randomZRot);

                            float randomScale = Random.Range(sizeRange.x, sizeRange.y);
                            pinRect.localScale = new Vector3(randomScale, randomScale, 1f);
                        }
                    }
                }
            }
        }
    }
 
    private void SetArtwork(Sprite artwork)
    {
        if (artworkImage == null)
        {
            return;
        }

        artworkImage.sprite = artwork;
        artworkImage.enabled = artwork != null;
    }

    private void Clear()
    {
        if (titleText != null)
        {
            titleText.text = string.Empty;
        }

        if (descriptionText != null)
        {
            descriptionText.text = string.Empty;
        }

        SetPinImages(null);
        SetArtwork(null);
        ApplyTint(Color.white);

        if (placementPatternDisplay != null)
        {
            placementPatternDisplay.Clear();
        }
    }
}
