using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardVisualPresenter : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("Images")]
    [SerializeField] private Image artworkImage;
    [Tooltip("Optional image used for the card's presentation tint or accent colour.")]
    [SerializeField] private Graphic tintTarget;

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
        ApplyTint(cardData.cardTint);
    }

    public void ApplyTint(Color tint)
    {
        if (tintTarget != null)
        {
            tintTarget.color = tint;
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

        SetArtwork(null);
        ApplyTint(Color.white);
    }
}
