using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum PinLayoutType
{
    None,
    Triangle,
    DoubleVertical,
    DoubleHorizontal,
    Single,
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
    [SerializeField] private Vector2 zPositionRange;
    [SerializeField] private Vector2 zRotationRange;
    [SerializeField] private Vector2 sizeRangeX;
    [SerializeField] private Vector2 sizeRangeY;
    
    // ai https://share.gemini.google/eZpLzfcrjY4M
    // I want a slight variation in tint colour so all the card backgrounds don't look exactly the same.
    [Header("Tint Randomisation")]
    [Tooltip("Min and Max variation for Hue. Small values like -0.05 to 0.05 are best.")]
    [SerializeField] private Vector2 hueVariationRange = new Vector2(-0.02f, 0.02f);
    [SerializeField] private Vector2 saturationVariationRange = new Vector2(-0f, 0f);
    [SerializeField] private Vector2 valueVariationRange = new Vector2(-0f, 0f);
    
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
            Color.RGBToHSV(tint, out float h, out float s, out float v);

            h += Random.Range(hueVariationRange.x, hueVariationRange.y);
            s += Random.Range(saturationVariationRange.x, saturationVariationRange.y);
            v += Random.Range(valueVariationRange.x, valueVariationRange.y);

            h = Mathf.Repeat(h, 1f);
            s = Mathf.Clamp01(s);
            v = Mathf.Clamp01(v);

            Color variedColor = Color.HSVToRGB(h, s, v);
            variedColor.a = tint.a;

            tintTarget.color = variedColor;
        }
    }

    private void SetPinImages(CardDataSO cardData)
    {
        List<Sprite> pins = cardData?.pinImages ?? new List<Sprite>();
        PinLayoutType targetLayout = cardData?.desiredLayout ?? PinLayoutType.None;

        // Deactivate all layouts first
        foreach (PinLayout layout in pinLayouts)
        {
            if (layout?.layoutContainer != null)
            {
                layout.layoutContainer.SetActive(false);
            }
        }

        switch (targetLayout)
        {
            case PinLayoutType.Triangle:
                SetupLayout(PinLayoutType.Triangle, pins);
                break;
            
            case PinLayoutType.DoubleVertical:
                SetupLayout(PinLayoutType.DoubleVertical, pins);
                break;
            
            case PinLayoutType.DoubleHorizontal:
                SetupLayout(PinLayoutType.DoubleHorizontal, pins);
                break;
            
            case PinLayoutType.Single:
                SetupLayout(PinLayoutType.Single, pins);
                break;

            case PinLayoutType.None:
            default:
                // Do nothing, all layouts are already off
                break;
        }
    }
    
    private void SetupLayout(PinLayoutType type, List<Sprite> pins)
    {
        PinLayout activeLayout = pinLayouts.Find(l => l != null && l.layoutType == type);
        
        if (activeLayout == null)
        {
            return;
        }

        if (activeLayout.layoutContainer != null)
        {
            activeLayout.layoutContainer.SetActive(true);
        }

        for (int i = 0; i < activeLayout.pinImages.Count; i++)
        {
            if (activeLayout.pinImages[i] == null)
            {
                continue;
            }

            if (i < pins.Count && pins[i] != null)
            {
                activeLayout.pinImages[i].sprite = pins[i];
                activeLayout.pinImages[i].enabled = true;

                RectTransform pinRect = activeLayout.pinImages[i].rectTransform;

                // Rolling new random values individually for each pin in the loop
                float randomZPos = Random.Range(zPositionRange.x, zPositionRange.y);
                Vector3 pos = pinRect.localPosition;
                pos.z = randomZPos;
                pinRect.localPosition = pos;

                float randomZRot = Random.Range(zRotationRange.x, zRotationRange.y);
                pinRect.localEulerAngles = new Vector3(0f, 0f, randomZRot);

                float randomScaleX = Random.Range(sizeRangeX.x, sizeRangeX.y);
                float randomScaleY = Random.Range(sizeRangeY.x, sizeRangeY.y);
                pinRect.localScale = new Vector3(randomScaleX, randomScaleY, 1f);
            }
            else
            {
                activeLayout.pinImages[i].sprite = null;
                activeLayout.pinImages[i].enabled = false;
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