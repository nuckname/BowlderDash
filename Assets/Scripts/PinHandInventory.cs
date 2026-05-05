using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum PinHandCardType
{
    Standard,
    Exploding,
    Mult
}

[System.Serializable]
public class PinTypeConfig
{
    public string editorLabel;
    [TextArea(2, 4)]
    public string cardDescription;
    public GameObject prefab;
}

public class PinHandInventory : MonoBehaviour
{
    public static PinHandInventory Instance { get; private set; }

    [Header("Starting Inventory")]
    public int startingExplodingPins = 2;
    public int startingMultPins = 2;

    [Header("Pin Type Prefabs")]
    public PinTypeConfig standardPinConfig = new PinTypeConfig
    {
        editorLabel = "Standard Pin",
        cardDescription = "Return field pins here"
    };
    public PinTypeConfig explodingPinConfig = new PinTypeConfig
    {
        editorLabel = "Exploding Pin",
        cardDescription = "Detonates nearby pins"
    };
    public PinTypeConfig multPinConfig = new PinTypeConfig
    {
        editorLabel = "Tumbler Pin",
        cardDescription = "Immovable, Adds score multiplier"
    };

    [Header("Hand Layout")]
    public Vector2 handSize = new Vector2(760f, 240f);
    public float cardSpacing = 170f;
    public float fanAngle = 14f;
    public float edgeDrop = 28f;
    public float hoverLift = 18f;
    public float selectedLift = 34f;
    public float bobAmplitude = 6f;
    public float bobSpeed = 2f;

    [Header("Card Style")]
    public Color handBackdropColor = new Color(0.05f, 0.08f, 0.12f, 0.72f);
    public Color cardFaceColor = new Color(0.12f, 0.16f, 0.22f, 0.96f);
    public Color cardTextColor = new Color(0.96f, 0.94f, 0.88f, 1f);
    public Color standardAccentColor = new Color(0.82f, 0.88f, 0.96f, 1f);
    public Color explodingAccentColor = new Color(1f, 0.47f, 0.21f, 1f);
    public Color multAccentColor = new Color(0.2f, 0.86f, 0.68f, 1f);

    private const float CardWidth = 140f;
    private const float CardHeight = 180f;

    private readonly Dictionary<PinHandCardType, int> availableCounts = new Dictionary<PinHandCardType, int>();
    private readonly Dictionary<PinHandCardType, PinHandCardView> cardViews = new Dictionary<PinHandCardType, PinHandCardView>();
    private readonly List<Pin> returnedStandardPins = new List<Pin>();
    private readonly List<Pin> runtimePowerupPins = new List<Pin>();
    private readonly List<PinHandCardType> orderedTypes = new List<PinHandCardType>
    {
        PinHandCardType.Standard,
        PinHandCardType.Exploding,
        PinHandCardType.Mult
    };

    private GameManager gameManager;
    private BowlingModeSwitcher modeSwitcher;
    private HexGridSnapper grid;

    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private RectTransform handRoot;
    private RectTransform cardContainer;
    private Font uiFont;

    private PinHandCardType? selectedType;
    private PinHandCardType? draggingType;
    private Pin reservedStandardPin;
    private GameObject previewPinObject;
    private bool handVisible;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (gameManager == null) gameManager = FindObjectOfType<GameManager>();
        if (modeSwitcher == null) modeSwitcher = FindObjectOfType<BowlingModeSwitcher>();
        if (grid == null) grid = FindObjectOfType<HexGridSnapper>();

        BuildRuntimeHand();
        EnsureEventSystemExists();
        ResetInventoryAndPins();
        PrepareForPlacementPhase();
    }

    private void Update()
    {
        if (modeSwitcher == null) modeSwitcher = FindObjectOfType<BowlingModeSwitcher>();
        if (grid == null) grid = FindObjectOfType<HexGridSnapper>();

        bool shouldShow = Application.isPlaying
            && BowlingModeSwitcher.isPlacementMode
            && grid != null
            && grid.isActiveAndEnabled
            && grid.gameObject.activeInHierarchy;

        if (handVisible != shouldShow)
        {
            SetHandVisible(shouldShow);
        }

        if (!handVisible)
        {
            if (draggingType.HasValue)
            {
                CancelPreviewDrag();
            }

            return;
        }

        UpdateCardLayout();
    }

    public void Configure(GameManager gm, HexGridSnapper activeGrid, BowlingModeSwitcher activeModeSwitcher)
    {
        gameManager = gm;
        grid = activeGrid;
        modeSwitcher = activeModeSwitcher;
    }

    public void PrepareForPlacementPhase()
    {
        SyncScenePinsForDragging();
        RefreshCards();
    }

    public void ResetInventoryAndPins()
    {
        CancelPreviewDrag();
        ClearRuntimePowerups();

        selectedType = null;
        availableCounts[PinHandCardType.Exploding] = startingExplodingPins;
        availableCounts[PinHandCardType.Mult] = startingMultPins;
        returnedStandardPins.Clear();

        RefreshCards();
    }

    public int GetCount(PinHandCardType type)
    {
        if (type == PinHandCardType.Standard)
        {
            int validPins = 0;
            for (int i = returnedStandardPins.Count - 1; i >= 0; i--)
            {
                if (returnedStandardPins[i] == null)
                {
                    returnedStandardPins.RemoveAt(i);
                    continue;
                }

                validPins++;
            }

            return validPins;
        }

        return availableCounts.TryGetValue(type, out int count) ? count : 0;
    }

    public string GetDisplayName(PinHandCardType type)
    {
        switch (type)
        {
            case PinHandCardType.Exploding:
                return "Exploding";
            case PinHandCardType.Mult:
                return "Tumbler";
            default:
                return "Standard";
        }
    }

    public string GetCardSubtitle(PinHandCardType type)
    {
        PinTypeConfig config = GetTypeConfig(type);
        if (config != null && !string.IsNullOrWhiteSpace(config.cardDescription))
        {
            return config.cardDescription;
        }

        switch (type)
        {
            case PinHandCardType.Exploding: return "Detonates nearby pins";
            case PinHandCardType.Mult: return "Adds score multiplier";
            default: return "Return field pins here";
        }
    }

    public Color GetAccentColor(PinHandCardType type)
    {
        switch (type)
        {
            case PinHandCardType.Exploding:
                return explodingAccentColor;
            case PinHandCardType.Mult:
                return multAccentColor;
            default:
                return standardAccentColor;
        }
    }

    public bool IsSelected(PinHandCardType type)
    {
        return selectedType.HasValue && selectedType.Value == type;
    }

    public void ToggleSelection(PinHandCardType type)
    {
        if (selectedType.HasValue && selectedType.Value == type)
        {
            selectedType = null;
        }
        else
        {
            selectedType = type;
        }
    }

    public bool BeginPreviewDrag(PinHandCardType type, PointerEventData eventData)
    {
        if (!handVisible || draggingType.HasValue || GetCount(type) <= 0)
        {
            return false;
        }

        previewPinObject = CreatePreviewPin(type);
        if (previewPinObject == null)
        {
            return false;
        }

        draggingType = type;
        selectedType = type;
        UpdatePreviewPosition(eventData.position);
        RefreshCards();
        return true;
    }

    public void ContinuePreviewDrag(PointerEventData eventData)
    {
        if (!draggingType.HasValue || previewPinObject == null) return;
        UpdatePreviewPosition(eventData.position);
    }

    public void EndPreviewDrag(PointerEventData eventData)
    {
        if (!draggingType.HasValue || previewPinObject == null)
        {
            CancelPreviewDrag();
            return;
        }

        UpdatePreviewPosition(eventData.position);

        if (TryPlacePreviewPin(previewPinObject))
        {
            ConsumePinnedCard(draggingType.Value);
            previewPinObject = null;
            draggingType = null;
            reservedStandardPin = null;
            RefreshCards();
            return;
        }

        CancelPreviewDrag();
    }

    public bool TryReturnPinToInventory(Pin pin, Vector2 screenPosition)
    {
        if (pin == null || !handVisible || !IsScreenPointOverHand(screenPosition))
        {
            return false;
        }

        if (grid != null)
        {
            grid.ReleaseCell(pin.transform);
        }

        if (pin.GetComponent<explodingPin>() != null)
        {
            availableCounts[PinHandCardType.Exploding] = GetCount(PinHandCardType.Exploding) + 1;
            runtimePowerupPins.Remove(pin);
            Destroy(pin.gameObject);
        }
        else if (pin.GetComponent<MultPin>() != null)
        {
            availableCounts[PinHandCardType.Mult] = GetCount(PinHandCardType.Mult) + 1;
            runtimePowerupPins.Remove(pin);
            Destroy(pin.gameObject);
        }
        else
        {
            if (!returnedStandardPins.Contains(pin))
            {
                pin.ResetPin();
                pin.gameObject.SetActive(false);
                returnedStandardPins.Add(pin);
            }
        }

        RefreshCards();
        return true;
    }

    public bool IsScreenPointOverHand(Vector2 screenPosition)
    {
        if (handRoot == null || !handVisible) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(handRoot, screenPosition, null);
    }

    private void SetHandVisible(bool visible)
    {
        handVisible = visible;

        if (canvasGroup == null) return;

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    private void BuildRuntimeHand()
    {
        if (canvasGroup != null) return;

        Canvas hostCanvas = FindObjectOfType<Canvas>();
        Transform hostParent = hostCanvas != null ? hostCanvas.transform : null;

        GameObject handCanvasObject = new GameObject("PinHandCanvas");
        handCanvasObject.transform.SetParent(hostParent, false);

        canvas = handCanvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        handCanvasObject.AddComponent<GraphicRaycaster>();
        canvasGroup = handCanvasObject.AddComponent<CanvasGroup>();

        RectTransform canvasRect = handCanvasObject.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        GameObject handBackdrop = new GameObject("PinHandBackdrop", typeof(Image));
        handBackdrop.transform.SetParent(handCanvasObject.transform, false);
        Image backdropImage = handBackdrop.GetComponent<Image>();
        backdropImage.color = handBackdropColor;
        backdropImage.raycastTarget = false;

        RectTransform backdropRect = handBackdrop.GetComponent<RectTransform>();
        backdropRect.anchorMin = new Vector2(0.5f, 0f);
        backdropRect.anchorMax = new Vector2(0.5f, 0f);
        backdropRect.pivot = new Vector2(0.5f, 0f);
        backdropRect.sizeDelta = handSize;
        backdropRect.anchoredPosition = new Vector2(0f, 24f);

        handRoot = backdropRect;

        GameObject titleObject = CreateTextObject("PinHandTitle", "PIN HAND", 20, FontStyle.Bold, TextAnchor.UpperCenter);
        titleObject.transform.SetParent(handBackdrop.transform, false);
        Text titleText = titleObject.GetComponent<Text>();
        titleText.color = new Color(cardTextColor.r, cardTextColor.g, cardTextColor.b, 0.82f);
        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(240f, 30f);
        titleRect.anchoredPosition = new Vector2(0f, -14f);

        GameObject cardContainerObject = new GameObject("Cards", typeof(RectTransform));
        cardContainerObject.transform.SetParent(handBackdrop.transform, false);
        cardContainer = cardContainerObject.GetComponent<RectTransform>();
        cardContainer.anchorMin = new Vector2(0.5f, 0.5f);
        cardContainer.anchorMax = new Vector2(0.5f, 0.5f);
        cardContainer.pivot = new Vector2(0.5f, 0.5f);
        cardContainer.sizeDelta = new Vector2(handSize.x - 40f, handSize.y - 56f);
        cardContainer.anchoredPosition = new Vector2(0f, -6f);

        foreach (PinHandCardType type in orderedTypes)
        {
            cardViews[type] = CreateCard(type);
        }

        SetHandVisible(false);
    }

    private PinHandCardView CreateCard(PinHandCardType type)
    {
        GameObject cardObject = new GameObject($"{type}Card", typeof(Image), typeof(CanvasGroup), typeof(Outline), typeof(PinHandCardView));
        cardObject.transform.SetParent(cardContainer, false);

        Image faceImage = cardObject.GetComponent<Image>();
        faceImage.color = cardFaceColor;
        faceImage.raycastTarget = true;

        Outline outline = cardObject.GetComponent<Outline>();
        outline.effectColor = new Color(GetAccentColor(type).r, GetAccentColor(type).g, GetAccentColor(type).b, 0.92f);
        outline.effectDistance = new Vector2(3f, -3f);

        RectTransform cardRect = cardObject.GetComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(CardWidth, CardHeight);

        GameObject accentObject = new GameObject("Accent", typeof(Image));
        accentObject.transform.SetParent(cardObject.transform, false);
        Image accentImage = accentObject.GetComponent<Image>();
        accentImage.color = GetAccentColor(type);
        accentImage.raycastTarget = false;
        RectTransform accentRect = accentObject.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 1f);
        accentRect.anchorMax = new Vector2(1f, 1f);
        accentRect.pivot = new Vector2(0.5f, 1f);
        accentRect.sizeDelta = new Vector2(0f, 14f);
        accentRect.anchoredPosition = Vector2.zero;

        GameObject nameObject = CreateTextObject("Title", GetDisplayName(type).ToUpperInvariant(), 18, FontStyle.Bold, TextAnchor.UpperLeft);
        nameObject.transform.SetParent(cardObject.transform, false);
        Text nameText = nameObject.GetComponent<Text>();
        nameText.color = cardTextColor;
        RectTransform nameRect = nameObject.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0f, 1f);
        nameRect.offsetMin = new Vector2(14f, -54f);
        nameRect.offsetMax = new Vector2(-14f, -20f);

        GameObject subtitleObject = CreateTextObject("Subtitle", GetCardSubtitle(type), 13, FontStyle.Normal, TextAnchor.UpperLeft);
        subtitleObject.transform.SetParent(cardObject.transform, false);
        Text subtitleText = subtitleObject.GetComponent<Text>();
        subtitleText.color = new Color(cardTextColor.r, cardTextColor.g, cardTextColor.b, 0.82f);
        RectTransform subtitleRect = subtitleObject.GetComponent<RectTransform>();
        subtitleRect.anchorMin = new Vector2(0f, 1f);
        subtitleRect.anchorMax = new Vector2(1f, 1f);
        subtitleRect.pivot = new Vector2(0f, 1f);
        subtitleRect.offsetMin = new Vector2(14f, -108f);
        subtitleRect.offsetMax = new Vector2(-14f, -56f);

        GameObject countObject = CreateTextObject("Count", "x0", 30, FontStyle.Bold, TextAnchor.LowerRight);
        countObject.transform.SetParent(cardObject.transform, false);
        Text countText = countObject.GetComponent<Text>();
        countText.color = GetAccentColor(type);
        RectTransform countRect = countObject.GetComponent<RectTransform>();
        countRect.anchorMin = new Vector2(0f, 0f);
        countRect.anchorMax = new Vector2(1f, 0f);
        countRect.pivot = new Vector2(1f, 0f);
        countRect.offsetMin = new Vector2(14f, 12f);
        countRect.offsetMax = new Vector2(-12f, 58f);

        PinHandCardView cardView = cardObject.GetComponent<PinHandCardView>();
        cardView.Initialize(this, type, cardRect, faceImage, nameText, subtitleText, countText);
        return cardView;
    }

    private GameObject CreateTextObject(string objectName, string content, int fontSize, FontStyle fontStyle, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(Text));
        Text text = textObject.GetComponent<Text>();
        text.font = GetUIFont();
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return textObject;
    }

    private Font GetUIFont()
    {
        if (uiFont != null) return uiFont;

        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (uiFont == null)
        {
            uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return uiFont;
    }

    private void EnsureEventSystemExists()
    {
        if (FindObjectOfType<EventSystem>() != null) return;

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(eventSystemObject);
    }

    private void UpdateCardLayout()
    {
        List<PinHandCardView> visibleCards = new List<PinHandCardView>();
        foreach (PinHandCardType type in orderedTypes)
        {
            PinHandCardView view = cardViews[type];
            bool showCard = GetCount(type) > 0 || (draggingType.HasValue && draggingType.Value == type);
            view.SetVisible(showCard);
            view.SetCount(GetCount(type));
            view.SetSelected(IsSelected(type));

            if (showCard)
            {
                visibleCards.Add(view);
            }
        }

        int visibleCount = visibleCards.Count;
        if (visibleCount == 0) return;

        for (int i = 0; i < visibleCount; i++)
        {
            PinHandCardView view = visibleCards[i];
            float centerOffset = i - (visibleCount - 1) * 0.5f;
            float x = centerOffset * cardSpacing;
            float normalized = visibleCount <= 1 ? 0f : centerOffset / Mathf.Max(1f, (visibleCount - 1) * 0.5f);
            float fanStrength = Mathf.Clamp01(Mathf.Abs(fanAngle) / 14f);
            float y = -Mathf.Abs(normalized) * edgeDrop * fanStrength;
            float bob = Mathf.Sin(Time.unscaledTime * bobSpeed + i * 0.75f) * bobAmplitude;
            float rotation = -normalized * fanAngle;
            float lift = 0f;
            float scale = 1f;

            if (view.IsHovered)
            {
                lift += hoverLift;
                scale += 0.03f;
            }

            if (view.IsSelected)
            {
                lift += selectedLift;
                scale += 0.06f;
            }

            if (draggingType.HasValue && draggingType.Value == view.CardType)
            {
                lift += selectedLift + 12f;
                scale += 0.06f;
            }

            Vector2 targetPosition = new Vector2(x, -8f + y + bob + lift);
            view.ApplyLayout(targetPosition, rotation, scale);
            int siblingIndex = view.IsSelected || view.IsHovered
                ? Mathf.Max(0, cardContainer.childCount - 1)
                : Mathf.Clamp(i, 0, Mathf.Max(0, cardContainer.childCount - 1));
            view.RectTransform.SetSiblingIndex(siblingIndex);
        }
    }

    private void RefreshCards()
    {
        if (cardViews.Count == 0) return;

        foreach (PinHandCardType type in orderedTypes)
        {
            if (!cardViews.TryGetValue(type, out PinHandCardView view)) continue;
            view.UpdateVisuals(GetDisplayName(type), GetCardSubtitle(type), GetCount(type), GetAccentColor(type));
            view.SetSelected(IsSelected(type));
        }
    }

    private void SyncScenePinsForDragging()
    {
        Pin[] pins = FindObjectsOfType<Pin>(true);
        for (int i = 0; i < pins.Length; i++)
        {
            Pin pin = pins[i];
            if (pin == null) continue;

            if (pin.GetComponent<DraggableSnap>() == null)
            {
                pin.gameObject.AddComponent<DraggableSnap>();
            }
        }
    }

    private void ClearRuntimePowerups()
    {
        for (int i = runtimePowerupPins.Count - 1; i >= 0; i--)
        {
            if (runtimePowerupPins[i] == null)
            {
                runtimePowerupPins.RemoveAt(i);
                continue;
            }

            Destroy(runtimePowerupPins[i].gameObject);
        }

        runtimePowerupPins.Clear();
    }

    private GameObject CreatePreviewPin(PinHandCardType type)
    {
        Pin pinComponent = null;
        GameObject pinObject = null;

        if (type == PinHandCardType.Standard)
        {
            for (int i = returnedStandardPins.Count - 1; i >= 0; i--)
            {
                Pin candidate = returnedStandardPins[i];
                returnedStandardPins.RemoveAt(i);

                if (candidate == null) continue;

                reservedStandardPin = candidate;
                pinComponent = candidate;
                pinObject = candidate.gameObject;
                pinObject.SetActive(true);
                break;
            }
        }
        else
        {
            pinObject = CreateConfiguredPinInstance(type);
            if (pinObject == null) return null;

            pinObject.name = type == PinHandCardType.Exploding ? "ExplodingPin_Runtime" : "MultPin_Runtime";
            pinComponent = pinObject.GetComponent<Pin>();

            DraggableSnap existingDrag = pinObject.GetComponent<DraggableSnap>();
            if (existingDrag == null)
            {
                existingDrag = pinObject.AddComponent<DraggableSnap>();
            }
            existingDrag.enabled = false;

            if (type == PinHandCardType.Exploding)
            {
                if (pinObject.GetComponent<explodingPin>() == null)
                {
                    pinObject.AddComponent<explodingPin>();
                }
            }
            else if (type == PinHandCardType.Mult)
            {
                MultPin multPin = pinObject.GetComponent<MultPin>();
                if (multPin == null)
                {
                    multPin = pinObject.AddComponent<MultPin>();
                }

                multPin.scoreCalculator = gameManager != null ? gameManager.scoreCalculator : null;
            }

            if (pinComponent != null && !runtimePowerupPins.Contains(pinComponent))
            {
                runtimePowerupPins.Add(pinComponent);
            }
        }

        if (pinObject == null) return null;

        if (pinComponent != null)
        {
            pinComponent.ResetPin();
        }

        PreparePinForPreview(pinObject);
        return pinObject;
    }

    private void PreparePinForPreview(GameObject pinObject)
    {
        if (pinObject == null) return;

        Collider[] colliders = pinObject.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = true;
               // colliders[i].isTrigger = true;
            }
        }
    }

    private void PreparePinForPlacement(GameObject pinObject)
    {
        if (pinObject == null) return;

        Collider[] colliders = pinObject.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = true;
                //colliders[i].isTrigger = false; 
            }
        }

        DraggableSnap draggable = pinObject.GetComponent<DraggableSnap>();
        if (draggable == null)
        {
            draggable = pinObject.AddComponent<DraggableSnap>();
        }

        draggable.enabled = true;
    }

    private bool TryPlacePreviewPin(GameObject pinObject)
    {
        if (grid == null || pinObject == null) return false;

        HexGridSnapper.Cell nearestCell = grid.GetNearestCell(pinObject.transform.position);
        if (nearestCell == null) return false;

        if (!grid.TryReserveCell(nearestCell, pinObject.transform))
        {
            return false;
        }

        pinObject.transform.position = nearestCell.center;
        PreparePinForPlacement(pinObject);
        return true;
    }

    private void ConsumePinnedCard(PinHandCardType type)
    {
        if (type == PinHandCardType.Exploding)
        {
            availableCounts[PinHandCardType.Exploding] = Mathf.Max(0, GetCount(PinHandCardType.Exploding) - 1);
        }
        else if (type == PinHandCardType.Mult)
        {
            availableCounts[PinHandCardType.Mult] = Mathf.Max(0, GetCount(PinHandCardType.Mult) - 1);
        }
    }

    private void CancelPreviewDrag()
    {
        if (draggingType.HasValue && previewPinObject != null)
        {
            if (draggingType.Value == PinHandCardType.Standard)
            {
                if (reservedStandardPin != null)
                {
                    reservedStandardPin.ResetPin();
                    reservedStandardPin.gameObject.SetActive(false);
                    if (!returnedStandardPins.Contains(reservedStandardPin))
                    {
                        returnedStandardPins.Add(reservedStandardPin);
                    }
                }
            }
            else
            {
                Pin previewPin = previewPinObject.GetComponent<Pin>();
                if (previewPin != null)
                {
                    runtimePowerupPins.Remove(previewPin);
                }

                Destroy(previewPinObject);
            }
        }

        previewPinObject = null;
        draggingType = null;
        reservedStandardPin = null;
        RefreshCards();
    }

    private void UpdatePreviewPosition(Vector2 screenPosition)
    {
        if (previewPinObject == null) return;

        Camera activeCamera = Camera.main;
        if (activeCamera == null)
        {
            previewPinObject.transform.position = new Vector3(0f, 0.5f, 0f);
            return;
        }

        float planeHeight = grid != null ? grid.transform.position.y : 0f;
        Plane dragPlane = new Plane(Vector3.up, new Vector3(0f, planeHeight, 0f));
        Ray ray = activeCamera.ScreenPointToRay(screenPosition);
        if (dragPlane.Raycast(ray, out float enter))
        {
            previewPinObject.transform.position = ray.GetPoint(enter);
        }
    }

    private Pin FindStandardTemplatePin()
    {
        Pin[] pins = FindObjectsOfType<Pin>(true);
        for (int i = 0; i < pins.Length; i++)
        {
            Pin pin = pins[i];
            if (pin == null) continue;
            if (pin.GetComponent<explodingPin>() != null) continue;
            if (pin.GetComponent<MultPin>() != null) continue;
            return pin;
        }

        return null;
    }

    private GameObject CreateConfiguredPinInstance(PinHandCardType type)
    {
        PinTypeConfig config = GetTypeConfig(type);
        GameObject configuredPrefab = config != null ? config.prefab : null;

        if (configuredPrefab != null)
        {
            GameObject instance = Instantiate(configuredPrefab);
            EnsureTypeComponents(instance, type);
            return instance;
        }

        Pin template = FindStandardTemplatePin();
        if (template == null) return null;

        GameObject fallback = Instantiate(template.gameObject);
        EnsureTypeComponents(fallback, type);
        return fallback;
    }

    private void EnsureTypeComponents(GameObject pinObject, PinHandCardType type)
    {
        if (pinObject == null) return;

        if (pinObject.GetComponent<Pin>() == null)
        {
            pinObject.AddComponent<Pin>();
        }

        explodingPin explodingComponent = pinObject.GetComponent<explodingPin>();
        MultPin multComponent = pinObject.GetComponent<MultPin>();

        if (type == PinHandCardType.Exploding)
        {
            if (explodingComponent == null)
            {
                pinObject.AddComponent<explodingPin>();
            }

            if (multComponent != null)
            {
                Destroy(multComponent);
            }
        }
        else if (type == PinHandCardType.Mult)
        {
            if (multComponent == null)
            {
                multComponent = pinObject.AddComponent<MultPin>();
            }

            multComponent.scoreCalculator = gameManager != null ? gameManager.scoreCalculator : null;

            if (explodingComponent != null)
            {
                Destroy(explodingComponent);
            }
        }
        else
        {
            if (explodingComponent != null)
            {
                Destroy(explodingComponent);
            }

            if (multComponent != null)
            {
                Destroy(multComponent);
            }
        }
    }

    private PinHandCardType DetectCardType(Pin pin)
    {
        if (pin == null) return PinHandCardType.Standard;
        if (pin.GetComponent<explodingPin>() != null) return PinHandCardType.Exploding;
        if (pin.GetComponent<MultPin>() != null) return PinHandCardType.Mult;
        return PinHandCardType.Standard;
    }

    private PinTypeConfig GetTypeConfig(PinHandCardType type)
    {
        switch (type)
        {
            case PinHandCardType.Exploding:
                return explodingPinConfig;
            case PinHandCardType.Mult:
                return multPinConfig;
            default:
                return standardPinConfig;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
