using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PinHandCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public PinHandCardType CardType { get; private set; }
    public RectTransform RectTransform { get; private set; }
    public bool IsHovered { get; private set; }
    public bool IsSelected { get; private set; }

    private PinHandInventory owner;
    private Image faceImage;
    private Text titleText;
    private Text subtitleText;
    private Text countText;
    private CanvasGroup canvasGroup;
    private bool dragging;

    public void Initialize(
        PinHandInventory handOwner,
        PinHandCardType type,
        RectTransform rectTransform,
        Image backgroundImage,
        Text title,
        Text subtitle,
        Text count)
    {
        owner = handOwner;
        CardType = type;
        RectTransform = rectTransform;
        faceImage = backgroundImage;
        titleText = title;
        subtitleText = subtitle;
        countText = count;
        canvasGroup = GetComponent<CanvasGroup>();
    }

    public void UpdateVisuals(string title, string subtitle, int count, Color accentColor)
    {
        if (titleText != null) titleText.text = title.ToUpperInvariant();
        if (subtitleText != null) subtitleText.text = subtitle;
        if (countText != null) countText.text = $"x{count}";
        if (faceImage != null)
        {
            Color baseColor = owner != null ? owner.cardFaceColor : Color.white;
            faceImage.color = owner != null && owner.GetCount(CardType) > 0
                ? baseColor
                : new Color(baseColor.r, baseColor.g, baseColor.b, 0.35f);
        }
        if (countText != null) countText.color = accentColor;
    }

    public void SetCount(int count)
    {
        if (countText != null)
        {
            countText.text = $"x{count}";
        }
    }

    public void SetVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible;
        }
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
    }

    public void ApplyLayout(Vector2 anchoredPosition, float zRotation, float scale)
    {
        if (RectTransform == null) return;

        RectTransform.anchoredPosition = Vector2.Lerp(RectTransform.anchoredPosition, anchoredPosition, 0.22f);
        RectTransform.localRotation = Quaternion.Lerp(RectTransform.localRotation, Quaternion.Euler(0f, 0f, zRotation), 0.22f);
        RectTransform.localScale = Vector3.Lerp(RectTransform.localScale, Vector3.one * scale, 0.22f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        IsHovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        IsHovered = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner == null) return;
        owner.ToggleSelection(CardType);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (owner == null) return;
        dragging = owner.BeginPreviewDrag(CardType, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging || owner == null) return;
        owner.ContinuePreviewDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragging || owner == null) return;
        dragging = false;
        owner.EndPreviewDrag(eventData);
    }
}
