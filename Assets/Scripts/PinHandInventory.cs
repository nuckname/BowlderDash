using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum PinHandCardType
{
    Standard,
    Exploding,
    Mult,
    Vase,
    Small,
    Banana
}

/// <summary>
/// Inventory-only tag for pin types that do not yet have dedicated behaviour scripts.
/// </summary>
public class PinHandRuntimeCardMarker : MonoBehaviour
{
    public PinHandCardType cardType;
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
    private struct SavedLayoutEntry
    {
        public PinHandCardType type;
        public int col;
        public int row;
    }

    public static PinHandInventory Instance { get; private set; }

    [Header("Starting Inventory")]
    public int startingStandardPins = 3;
    public int startingExplodingPins = 2;
    public int startingMultPins = 2;
    public int startingVasePins = 0;
    public int startingSmallPins = 0;
    public int startingBananaPins = 0;

    [Header("Shop Limits")]
    public int maxStandardPins = 3;
    public int maxExplodingPins = 3;
    public int maxMultPins = 3;
    public int maxVasePins = 3;
    public int maxSmallPins = 3;
    public int maxBananaPins = 3;

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
    public PinTypeConfig vasePinConfig = new PinTypeConfig
    {
        editorLabel = "Vase Pin",
        cardDescription = "Survive the throw for bonus score; shatters when hit"
    };
    public PinTypeConfig smallPinConfig = new PinTypeConfig
    {
        editorLabel = "Small Pin",
        cardDescription = "Compact pin (behaviour coming soon)"
    };
    public PinTypeConfig bananaPinConfig = new PinTypeConfig
    {
        editorLabel = "Banana Pin",
        cardDescription = "Banana pin (behaviour coming soon)"
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
    public Color vaseAccentColor = new Color(0.78f, 0.55f, 0.95f, 1f);
    public Color smallAccentColor = new Color(0.95f, 0.82f, 0.38f, 1f);
    public Color bananaAccentColor = new Color(0.98f, 0.88f, 0.22f, 1f);

    private const float CardWidth = 140f;
    private const float CardHeight = 180f;
    private const float TopControlsHeight = 88f;
    private const float StatusMessageDuration = 3.5f;

    private readonly Dictionary<PinHandCardType, int> availableCounts = new Dictionary<PinHandCardType, int>();
    private readonly Dictionary<PinHandCardType, int> ownedCounts = new Dictionary<PinHandCardType, int>();
    private readonly Dictionary<PinHandCardType, PinHandCardView> cardViews = new Dictionary<PinHandCardType, PinHandCardView>();
    private readonly List<SavedLayoutEntry> savedLastLayout = new List<SavedLayoutEntry>();
    private readonly List<Pin> returnedStandardPins = new List<Pin>();
    private readonly List<Pin> runtimePowerupPins = new List<Pin>();
    private readonly List<Pin> standardPinPool = new List<Pin>();
    private readonly List<Pin> consumedVasePinsPendingCleanup = new List<Pin>();
    private readonly List<PinHandCardType> orderedTypes = new List<PinHandCardType>
    {
        PinHandCardType.Standard,
        PinHandCardType.Exploding,
        PinHandCardType.Mult,
        PinHandCardType.Vase,
        PinHandCardType.Small,
        PinHandCardType.Banana
    };

    private GrandGameManager gameManager;
    private HexGridSnapper grid;

    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private RectTransform handRoot;
    private RectTransform cardContainer;
    private Text statusText;
    private Font uiFont;
    private float statusMessageHideTime;

    private PinHandCardType? selectedType;
    private PinHandCardType? draggingType;
    private Pin reservedStandardPin;
    private GameObject previewPinObject;
    private bool handVisible;
    private bool ownedInventoryInitialized;
    private bool legacyScenePinsDisabled;

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
        if (gameManager == null) gameManager = FindObjectOfType<GrandGameManager>();
        if (grid == null) grid = FindObjectOfType<HexGridSnapper>();

        BuildRuntimeHand();
        EnsureEventSystemExists();
        CollectStandardPinPool();

        if (!ownedInventoryInitialized)
        {
            InitializeOwnedInventoryForNewGame();
        }

        ResetInventoryAndPins();
        PrepareForPlacementPhase();
    }

    private void Update()
    {
        if (gameManager == null) gameManager = FindObjectOfType<GrandGameManager>();
        if (grid == null) grid = FindObjectOfType<HexGridSnapper>();

        bool shouldShow = Application.isPlaying
            && (gameManager != null && gameManager.GetCurrentState() == GameState.PinPlacement)
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

        UpdateStatusMessageVisibility();
        UpdateCardLayout();
    }

    public void Configure(GrandGameManager gm, HexGridSnapper activeGrid, MonoBehaviour activeModeSwitcher = null)
    {
        gameManager = gm;
        grid = activeGrid;
        CollectStandardPinPool();
    }

    public void PrepareForPlacementPhase()
    {
        SyncScenePinsForDragging();
        if (grid != null)
        {
            grid.SyncPinsToGrid(GetPlacedPins());
        }

        RefreshCards();
    }

    public void SaveCurrentLayoutAsLastUsed()
    {
        if (grid == null) grid = FindObjectOfType<HexGridSnapper>();

        savedLastLayout.Clear();
        if (grid == null)
        {
            return;
        }

        foreach (Pin pin in GetPlacedPins())
        {
            if (pin == null || !pin.gameObject.activeInHierarchy)
            {
                continue;
            }

            HexGridSnapper.Cell cell = grid.GetCellByOccupant(pin.transform);
            if (cell == null)
            {
                cell = grid.GetNearestCell(pin.transform.position);
            }

            if (cell == null)
            {
                continue;
            }

            savedLastLayout.Add(new SavedLayoutEntry
            {
                type = DetectCardType(pin),
                col = cell.col,
                row = cell.row
            });
        }
    }

    public void ClearSavedLayout()
    {
        savedLastLayout.Clear();
    }

    public void InitializeOwnedInventoryForNewGame()
    {
        CollectStandardPinPool();
        ownedCounts[PinHandCardType.Standard] = Mathf.Clamp(startingStandardPins, 0, Mathf.Max(0, maxStandardPins));
        ownedCounts[PinHandCardType.Exploding] = Mathf.Clamp(startingExplodingPins, 0, Mathf.Max(0, maxExplodingPins));
        ownedCounts[PinHandCardType.Mult] = Mathf.Clamp(startingMultPins, 0, Mathf.Max(0, maxMultPins));
        ownedCounts[PinHandCardType.Vase] = Mathf.Clamp(startingVasePins, 0, Mathf.Max(0, maxVasePins));
        ownedCounts[PinHandCardType.Small] = Mathf.Clamp(startingSmallPins, 0, Mathf.Max(0, maxSmallPins));
        ownedCounts[PinHandCardType.Banana] = Mathf.Clamp(startingBananaPins, 0, Mathf.Max(0, maxBananaPins));
        ownedInventoryInitialized = true;
    }

    public void ResetInventoryAndPins()
    {
        CancelPreviewDrag();
        ClearRuntimePowerups();

        selectedType = null;
        if (!ownedInventoryInitialized)
        {
            InitializeOwnedInventoryForNewGame();
        }

        CollectStandardPinPool();
        PrepareStandardPinsForFrame();
        availableCounts[PinHandCardType.Exploding] = GetOwnedCount(PinHandCardType.Exploding);
        availableCounts[PinHandCardType.Mult] = GetOwnedCount(PinHandCardType.Mult);
        availableCounts[PinHandCardType.Vase] = GetOwnedCount(PinHandCardType.Vase);
        availableCounts[PinHandCardType.Small] = GetOwnedCount(PinHandCardType.Small);
        availableCounts[PinHandCardType.Banana] = GetOwnedCount(PinHandCardType.Banana);
        returnedStandardPins.Clear();
        for (int i = 0; i < Mathf.Min(GetOwnedCount(PinHandCardType.Standard), standardPinPool.Count); i++)
        {
            returnedStandardPins.Add(standardPinPool[i]);
        }

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
                return "Pinata";
            case PinHandCardType.Vase:
                return "Vase";
            case PinHandCardType.Small:
                return "Small";
            case PinHandCardType.Banana:
                return "Banana";
            default:
                return "Standard";
        }
    }

    public int GetOwnedCount(PinHandCardType type)
    {
        return ownedCounts.TryGetValue(type, out int count) ? count : 0;
    }

    public int GetMaxOwnedCount(PinHandCardType type)
    {
        switch (type)
        {
            case PinHandCardType.Exploding:
                return Mathf.Max(0, maxExplodingPins);
            case PinHandCardType.Mult:
                return Mathf.Max(0, maxMultPins);
            case PinHandCardType.Vase:
                return Mathf.Max(0, maxVasePins);
            case PinHandCardType.Small:
                return Mathf.Max(0, maxSmallPins);
            case PinHandCardType.Banana:
                return Mathf.Max(0, maxBananaPins);
            default:
                return Mathf.Max(0, maxStandardPins);
        }
    }

    public bool CanBuyPin(PinHandCardType type)
    {
        return GetOwnedCount(type) < GetMaxOwnedCount(type);
    }

    public bool CanSellPin(PinHandCardType type)
    {
        return GetOwnedCount(type) > 0;
    }

    public bool TryBuyPin(PinHandCardType type)
    {
        if (!CanBuyPin(type))
        {
            return false;
        }

        ownedCounts[type] = GetOwnedCount(type) + 1;
        if (type != PinHandCardType.Standard)
        {
            availableCounts[type] = GetOwnedCount(type);
        }

        RefreshCards();
        return true;
    }

    public bool TrySellPin(PinHandCardType type)
    {
        if (!CanSellPin(type))
        {
            return false;
        }

        ownedCounts[type] = Mathf.Max(0, GetOwnedCount(type) - 1);
        RefreshCards();
        return true;
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
            case PinHandCardType.Vase: return "Bonus if untouched; lost forever when shattered";
            case PinHandCardType.Small: return "Compact pin (behaviour coming soon)";
            case PinHandCardType.Banana: return "Banana pin (behaviour coming soon)";
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
            case PinHandCardType.Vase:
                return vaseAccentColor;
            case PinHandCardType.Small:
                return smallAccentColor;
            case PinHandCardType.Banana:
                return bananaAccentColor;
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
        else if (pin.GetComponent<vasePin>() != null)
        {
            vasePin vaseComponent = pin.GetComponent<vasePin>();
            if (vaseComponent != null && vaseComponent.IsRemovedFromInventory())
            {
                return false;
            }

            availableCounts[PinHandCardType.Vase] = GetCount(PinHandCardType.Vase) + 1;
            runtimePowerupPins.Remove(pin);
            Destroy(pin.gameObject);
        }
        else if (!TryReturnMarkedPowerupToInventory(pin))
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

        GameObject handCanvasObject = new GameObject("PinHandCanvas");
        handCanvasObject.transform.SetParent(null, false);

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
        backdropRect.sizeDelta = new Vector2(handSize.x, handSize.y + TopControlsHeight);
        backdropRect.anchoredPosition = new Vector2(0f, 24f);

        handRoot = backdropRect;

        BuildTopControls(handBackdrop.transform);

        GameObject titleObject = CreateTextObject("PinHandTitle", "PIN INVENTORY", 20, FontStyle.Bold, TextAnchor.UpperCenter);
        titleObject.transform.SetParent(handBackdrop.transform, false);
        Text titleText = titleObject.GetComponent<Text>();
        titleText.color = new Color(cardTextColor.r, cardTextColor.g, cardTextColor.b, 0.82f);
        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(240f, 30f);
        titleRect.anchoredPosition = new Vector2(0f, -72f);

        GameObject cardContainerObject = new GameObject("Cards", typeof(RectTransform));
        cardContainerObject.transform.SetParent(handBackdrop.transform, false);
        cardContainer = cardContainerObject.GetComponent<RectTransform>();
        cardContainer.anchorMin = new Vector2(0.5f, 0f);
        cardContainer.anchorMax = new Vector2(0.5f, 0f);
        cardContainer.pivot = new Vector2(0.5f, 0.5f);
        cardContainer.sizeDelta = new Vector2(handSize.x - 40f, handSize.y - 56f);
        cardContainer.anchoredPosition = new Vector2(0f, 108f);

        foreach (PinHandCardType type in orderedTypes)
        {
            cardViews[type] = CreateCard(type);
        }

        SetHandVisible(false);
    }

    private void BuildTopControls(Transform parent)
    {
        GameObject controlsRowObject = new GameObject("TopControls", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        controlsRowObject.transform.SetParent(parent, false);

        RectTransform controlsRect = controlsRowObject.GetComponent<RectTransform>();
        controlsRect.anchorMin = new Vector2(0.5f, 1f);
        controlsRect.anchorMax = new Vector2(0.5f, 1f);
        controlsRect.pivot = new Vector2(0.5f, 1f);
        controlsRect.sizeDelta = new Vector2(handSize.x - 32f, 34f);
        controlsRect.anchoredPosition = new Vector2(0f, -12f);

        HorizontalLayoutGroup controlsLayout = controlsRowObject.GetComponent<HorizontalLayoutGroup>();
        controlsLayout.spacing = 12f;
        controlsLayout.childAlignment = TextAnchor.UpperCenter;
        controlsLayout.childControlWidth = false;
        controlsLayout.childForceExpandWidth = false;
        controlsLayout.childControlHeight = true;
        controlsLayout.childForceExpandHeight = true;

        CreateActionButton("PlaceLastLayoutButton", controlsRowObject.transform, "PLACE LAST LAYOUT", new Vector2(220f, 32f), OnPlaceLastLayoutPressed);
        CreateActionButton("RandomiseButton", controlsRowObject.transform, "RANDOMISE", new Vector2(150f, 32f), OnRandomisePressed);
        CreateActionButton("ReturnAllButton", controlsRowObject.transform, "RETURN ALL", new Vector2(160f, 32f), OnReturnAllPinsPressed);

        GameObject statusObject = CreateTextObject("PinHandStatus", string.Empty, 13, FontStyle.Italic, TextAnchor.UpperCenter);
        statusObject.transform.SetParent(parent, false);
        statusText = statusObject.GetComponent<Text>();
        statusText.color = new Color(1f, 0.78f, 0.34f, 0f);

        RectTransform statusRect = statusObject.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.5f, 1f);
        statusRect.anchorMax = new Vector2(0.5f, 1f);
        statusRect.pivot = new Vector2(0.5f, 1f);
        statusRect.sizeDelta = new Vector2(handSize.x - 48f, 26f);
        statusRect.anchoredPosition = new Vector2(0f, -46f);
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

    private Button CreateActionButton(string objectName, Transform parent, string label, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);

        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredWidth = size.x;
        layout.preferredHeight = size.y;

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.92f, 0.73f, 0.2f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(onClick);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text labelText = labelObject.GetComponent<Text>();
        labelText.font = GetUIFont();
        labelText.text = label;
        labelText.fontSize = 13;
        labelText.fontStyle = FontStyle.Bold;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = new Color(0.08f, 0.1f, 0.14f, 1f);
        labelText.raycastTarget = false;
        return button;
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

    private void OnPlaceLastLayoutPressed()
    {
        if (!handVisible)
        {
            return;
        }

        if (savedLastLayout.Count == 0)
        {
            ShowStatusMessage("No last layout is saved yet.", false);
            return;
        }

        if (grid == null) grid = FindObjectOfType<HexGridSnapper>();
        if (grid == null)
        {
            ShowStatusMessage("The hex grid is unavailable right now.", true);
            return;
        }

        ResetInventoryAndPins();
        grid.ClearAllOccupants();

        int placedCount = 0;
        int missingCount = 0;

        for (int i = 0; i < savedLastLayout.Count; i++)
        {
            SavedLayoutEntry entry = savedLastLayout[i];
            HexGridSnapper.Cell targetCell = grid.GetCell(entry.col, entry.row);
            if (targetCell == null)
            {
                continue;
            }

            if (GetCount(entry.type) <= 0)
            {
                missingCount++;
                continue;
            }

            if (TryPlacePinOnCell(entry.type, targetCell))
            {
                placedCount++;
            }
        }

        FinalizeBulkPlacement();

        if (missingCount > 0)
        {
            ShowStatusMessage($"Restored {placedCount} pin(s). {missingCount} pin(s) from the last layout are missing from inventory.", true);
        }
        else
        {
            ShowStatusMessage($"Restored {placedCount} pin(s) from the last layout.", false);
        }
    }

    private void OnRandomisePressed()
    {
        if (!handVisible)
        {
            return;
        }

        if (grid == null) grid = FindObjectOfType<HexGridSnapper>();
        if (grid == null)
        {
            ShowStatusMessage("The hex grid is unavailable right now.", true);
            return;
        }

        ResetInventoryAndPins();
        grid.ClearAllOccupants();

        List<HexGridSnapper.Cell> cells = grid.GetCellsSnapshot();
        if (cells.Count == 0)
        {
            ShowStatusMessage("The hex grid has no cells to place pins on.", true);
            return;
        }

        ShuffleCells(cells);

        List<PinHandCardType> pinsToPlace = BuildRandomPinSelection(cells.Count, out int overflowCount);
        int placedCount = 0;

        for (int i = 0; i < pinsToPlace.Count && i < cells.Count; i++)
        {
            if (TryPlacePinOnCell(pinsToPlace[i], cells[i]))
            {
                placedCount++;
            }
        }

        FinalizeBulkPlacement();

        if (overflowCount > 0)
        {
            ShowStatusMessage($"Randomised {placedCount} pin(s). {overflowCount} pin(s) stayed in inventory because the grid is full.", false);
        }
        else
        {
            ShowStatusMessage($"Randomised {placedCount} pin(s) on the grid.", false);
        }
    }

    private void OnReturnAllPinsPressed()
    {
        if (!handVisible)
        {
            return;
        }

        int placedCount = 0;
        foreach (Pin pin in GetPlacedPins())
        {
            if (pin != null && pin.gameObject.activeInHierarchy)
            {
                placedCount++;
            }
        }

        ResetInventoryAndPins();
        if (grid != null)
        {
            grid.ClearAllOccupants();
        }

        PrepareForPlacementPhase();
        ShowStatusMessage(placedCount > 0
            ? $"Returned {placedCount} pin(s) to inventory."
            : "All pins are already in inventory.", false);
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

    private void FinalizeBulkPlacement()
    {
        selectedType = null;
        SyncScenePinsForDragging();
        if (grid != null)
        {
            grid.SyncPinsToGrid(GetPlacedPins());
        }

        RefreshCards();
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

            if (grid != null)
            {
                grid.ReleaseCell(runtimePowerupPins[i].transform);
            }

            Destroy(runtimePowerupPins[i].gameObject);
        }

        runtimePowerupPins.Clear();

        for (int i = consumedVasePinsPendingCleanup.Count - 1; i >= 0; i--)
        {
            Pin consumedVase = consumedVasePinsPendingCleanup[i];
            if (consumedVase == null)
            {
                consumedVasePinsPendingCleanup.RemoveAt(i);
                continue;
            }

            if (grid != null)
            {
                grid.ReleaseCell(consumedVase.transform);
            }

            Destroy(consumedVase.gameObject);
        }

        consumedVasePinsPendingCleanup.Clear();
    }

    private GameObject CreatePreviewPin(PinHandCardType type)
    {
        GameObject pinObject = TakePinInstance(type, out Pin standardReservation);
        if (pinObject == null) return null;

        reservedStandardPin = standardReservation;
        PreparePinForPreview(pinObject);
        return pinObject;
    }

    private void PreparePinForPreview(GameObject pinObject)
    {
        if (pinObject == null) return;

        EnablePinColliders(pinObject);
        SyncPinPhysics(pinObject);
    }

    private void PreparePinForPlacement(GameObject pinObject)
    {
        if (pinObject == null) return;

        EnablePinColliders(pinObject);

        DraggableSnap draggable = pinObject.GetComponent<DraggableSnap>();
        if (draggable == null)
        {
            draggable = pinObject.AddComponent<DraggableSnap>();
        }

        draggable.enabled = true;
        SyncPinPhysics(pinObject);
    }

    private static void EnablePinColliders(GameObject pinObject)
    {
        Collider[] colliders = pinObject.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = true;
            }
        }
    }

    private static void SyncPinPhysics(GameObject pinObject)
    {
        if (pinObject == null) return;

        Pin pin = pinObject.GetComponent<Pin>();
        if (pin != null)
        {
            pin.SyncPhysicsToGameState();
        }
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

    private bool TryPlacePinOnCell(PinHandCardType type, HexGridSnapper.Cell targetCell)
    {
        if (grid == null || targetCell == null)
        {
            return false;
        }

        GameObject pinObject = TakePinInstance(type, out Pin standardReservation);
        if (pinObject == null)
        {
            return false;
        }

        if (!grid.TryReserveCell(targetCell, pinObject.transform))
        {
            ReturnPinInstance(type, pinObject, standardReservation);
            return false;
        }

        pinObject.transform.position = targetCell.center;
        PreparePinForPlacement(pinObject);
        ConsumePinnedCard(type);
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
        else if (type == PinHandCardType.Vase)
        {
            availableCounts[PinHandCardType.Vase] = Mathf.Max(0, GetCount(PinHandCardType.Vase) - 1);
        }
        else if (UsesAvailableCountInventory(type))
        {
            availableCounts[type] = Mathf.Max(0, GetCount(type) - 1);
        }
    }

    public void ConsumePlacedVaseInstance(Pin pin)
    {
        if (pin == null)
        {
            return;
        }

        vasePin vaseComponent = pin.GetComponent<vasePin>();
        if (vaseComponent == null || vaseComponent.IsRemovedFromInventory())
        {
            return;
        }

        vaseComponent.MarkRemovedFromInventory();
        runtimePowerupPins.Remove(pin);

        ownedCounts[PinHandCardType.Vase] = Mathf.Max(0, GetOwnedCount(PinHandCardType.Vase) - 1);

        if (grid != null)
        {
            grid.ReleaseCell(pin.transform);
        }

        DraggableSnap draggable = pin.GetComponent<DraggableSnap>();
        if (draggable != null)
        {
            draggable.enabled = false;
        }

        if (!consumedVasePinsPendingCleanup.Contains(pin))
        {
            consumedVasePinsPendingCleanup.Add(pin);
        }

        RefreshCards();
    }

    private void CancelPreviewDrag()
    {
        if (draggingType.HasValue && previewPinObject != null)
        {
            ReturnPinInstance(draggingType.Value, previewPinObject, reservedStandardPin);
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

    private void CollectStandardPinPool()
    {
        DisableLegacyScenePinsOnce();
        standardPinPool.RemoveAll(pin => pin == null);
        EnsureStandardPinPoolSize(GetMaxOwnedCount(PinHandCardType.Standard));
    }

    private GameObject TakePinInstance(PinHandCardType type, out Pin standardReservation)
    {
        standardReservation = null;

        Pin pinComponent = null;
        GameObject pinObject = null;

        if (type == PinHandCardType.Standard)
        {
            for (int i = returnedStandardPins.Count - 1; i >= 0; i--)
            {
                Pin candidate = returnedStandardPins[i];
                returnedStandardPins.RemoveAt(i);

                if (candidate == null)
                {
                    continue;
                }

                standardReservation = candidate;
                pinComponent = candidate;
                pinObject = candidate.gameObject;
                pinObject.SetActive(true);
                break;
            }
        }
        else
        {
            pinObject = CreateConfiguredPinInstance(type);
            if (pinObject == null)
            {
                return null;
            }

            pinObject.name = GetRuntimePinObjectName(type);
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

                multPin.gameManager = gameManager;
                multPin.scoreCalculatorOverride = gameManager != null ? gameManager.scoreCalculator : null;
            }
            else if (type == PinHandCardType.Vase)
            {
                if (pinObject.GetComponent<vasePin>() == null)
                {
                    pinObject.AddComponent<vasePin>();
                }
            }
            else if (type == PinHandCardType.Small || type == PinHandCardType.Banana)
            {
                EnsureRuntimeCardMarker(pinObject, type);
            }

            if (pinComponent != null && !runtimePowerupPins.Contains(pinComponent))
            {
                runtimePowerupPins.Add(pinComponent);
            }
        }

        if (pinComponent != null)
        {
            pinComponent.ResetPin();
        }

        return pinObject;
    }

    private void ReturnPinInstance(PinHandCardType type, GameObject pinObject, Pin standardReservation)
    {
        if (type == PinHandCardType.Standard)
        {
            if (standardReservation == null)
            {
                return;
            }

            standardReservation.ResetPin();
            standardReservation.gameObject.SetActive(false);
            if (!returnedStandardPins.Contains(standardReservation))
            {
                returnedStandardPins.Add(standardReservation);
            }

            return;
        }

        Pin runtimePin = pinObject != null ? pinObject.GetComponent<Pin>() : null;
        if (runtimePin != null)
        {
            runtimePowerupPins.Remove(runtimePin);
        }

        if (pinObject != null)
        {
            Destroy(pinObject);
        }
    }

    private void EnsureStandardPinPoolSize(int requiredCount)
    {
        requiredCount = Mathf.Max(0, requiredCount);

        while (standardPinPool.Count < requiredCount)
        {
            GameObject instance = CreateConfiguredPinInstance(PinHandCardType.Standard);
            if (instance == null)
            {
                break;
            }

            Pin pin = instance.GetComponent<Pin>();
            if (pin == null)
            {
                Destroy(instance);
                break;
            }

            instance.name = "StandardPin_RuntimePool";
            instance.SetActive(false);
            standardPinPool.Add(pin);
        }
    }

    private void DisableLegacyScenePinsOnce()
    {
        if (legacyScenePinsDisabled)
        {
            return;
        }

        legacyScenePinsDisabled = true;

        Pin[] pins = FindObjectsOfType<Pin>(true);
        for (int i = 0; i < pins.Length; i++)
        {
            Pin pin = pins[i];
            if (pin == null)
            {
                continue;
            }

            if (grid != null)
            {
                grid.ReleaseCell(pin.transform);
            }

            pin.ResetPin();
            pin.gameObject.SetActive(false);
        }
    }

    private void PrepareStandardPinsForFrame()
    {
        for (int i = 0; i < standardPinPool.Count; i++)
        {
            Pin pin = standardPinPool[i];
            if (pin == null)
            {
                continue;
            }

            if (grid != null)
            {
                grid.ReleaseCell(pin.transform);
            }

            pin.ResetPin();
            pin.gameObject.SetActive(false);
        }
    }

    private List<PinHandCardType> BuildRandomPinSelection(int maxPinsToPlace, out int overflowCount)
    {
        List<PinHandCardType> pinTypes = new List<PinHandCardType>();
        foreach (PinHandCardType type in orderedTypes)
        {
            int count = GetCount(type);
            for (int i = 0; i < count; i++)
            {
                pinTypes.Add(type);
            }
        }

        ShuffleTypes(pinTypes);

        overflowCount = Mathf.Max(0, pinTypes.Count - Mathf.Max(0, maxPinsToPlace));
        if (pinTypes.Count > maxPinsToPlace)
        {
            pinTypes.RemoveRange(maxPinsToPlace, pinTypes.Count - maxPinsToPlace);
        }

        return pinTypes;
    }

    private IEnumerable<Pin> GetPlacedPins()
    {
        for (int i = 0; i < standardPinPool.Count; i++)
        {
            Pin pin = standardPinPool[i];
            if (pin != null && pin.gameObject.activeInHierarchy)
            {
                yield return pin;
            }
        }

        for (int i = 0; i < runtimePowerupPins.Count; i++)
        {
            Pin pin = runtimePowerupPins[i];
            if (pin != null && pin.gameObject.activeInHierarchy)
            {
                yield return pin;
            }
        }
    }

    private void ShuffleCells(List<HexGridSnapper.Cell> cells)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            int swapIndex = Random.Range(i, cells.Count);
            HexGridSnapper.Cell temp = cells[i];
            cells[i] = cells[swapIndex];
            cells[swapIndex] = temp;
        }
    }

    private void ShuffleTypes(List<PinHandCardType> pinTypes)
    {
        for (int i = 0; i < pinTypes.Count; i++)
        {
            int swapIndex = Random.Range(i, pinTypes.Count);
            PinHandCardType temp = pinTypes[i];
            pinTypes[i] = pinTypes[swapIndex];
            pinTypes[swapIndex] = temp;
        }
    }

    private GameObject CreateConfiguredPinInstance(PinHandCardType type)
    {
        GameObject configuredPrefab = GetConfiguredPrefabAsset(type);
        if (configuredPrefab == null)
        {
            return null;
        }

        GameObject instance = Instantiate(configuredPrefab);
        EnsureTypeComponents(instance, type);
        return instance;
    }

    private GameObject GetConfiguredPrefabAsset(PinHandCardType type)
    {
        PinTypeConfig config = GetTypeConfig(type);
        GameObject configuredPrefab = config != null ? config.prefab : null;
        if (configuredPrefab == null)
        {
            Debug.LogWarning($"[PinHandInventory] Missing prefab for {GetDisplayName(type)} pins. Assign a prefab asset in PinHandInventory.");
            return null;
        }

        if (configuredPrefab.scene.IsValid())
        {
            Debug.LogWarning($"[PinHandInventory] {GetDisplayName(type)} pin source must be a prefab asset, not a scene object.");
            return null;
        }

        return configuredPrefab;
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
        vasePin vaseComponent = pinObject.GetComponent<vasePin>();

        if (type == PinHandCardType.Exploding)
        {
            if (explodingComponent == null)
            {
                pinObject.AddComponent<explodingPin>();
            }

            ClearConflictingPowerupComponents(pinObject, explodingComponent, multComponent, vaseComponent, type);
        }
        else if (type == PinHandCardType.Mult)
        {
            if (multComponent == null)
            {
                multComponent = pinObject.AddComponent<MultPin>();
            }

            multComponent.gameManager = gameManager;
            multComponent.scoreCalculatorOverride = gameManager != null ? gameManager.scoreCalculator : null;

            ClearConflictingPowerupComponents(pinObject, explodingComponent, multComponent, vaseComponent, type);
        }
        else if (type == PinHandCardType.Vase)
        {
            if (vaseComponent == null)
            {
                vaseComponent = pinObject.AddComponent<vasePin>();
            }

            ClearConflictingPowerupComponents(pinObject, explodingComponent, multComponent, vaseComponent, type);
        }
        else if (type == PinHandCardType.Small || type == PinHandCardType.Banana)
        {
            EnsureRuntimeCardMarker(pinObject, type);
            ClearConflictingPowerupComponents(pinObject, explodingComponent, multComponent, vaseComponent, type);
        }
        else
        {
            ClearConflictingPowerupComponents(pinObject, explodingComponent, multComponent, vaseComponent, type);
        }
    }

    private static bool UsesAvailableCountInventory(PinHandCardType type)
    {
        return type == PinHandCardType.Small || type == PinHandCardType.Banana;
    }

    private static void EnsureRuntimeCardMarker(GameObject pinObject, PinHandCardType type)
    {
        if (pinObject == null)
        {
            return;
        }

        PinHandRuntimeCardMarker marker = pinObject.GetComponent<PinHandRuntimeCardMarker>();
        if (marker == null)
        {
            marker = pinObject.AddComponent<PinHandRuntimeCardMarker>();
        }

        marker.cardType = type;
    }

    private bool TryReturnMarkedPowerupToInventory(Pin pin)
    {
        PinHandRuntimeCardMarker marker = pin.GetComponent<PinHandRuntimeCardMarker>();
        if (marker == null || !UsesAvailableCountInventory(marker.cardType))
        {
            return false;
        }

        availableCounts[marker.cardType] = GetCount(marker.cardType) + 1;
        runtimePowerupPins.Remove(pin);
        Destroy(pin.gameObject);
        return true;
    }

    private void ClearConflictingPowerupComponents(
        GameObject pinObject,
        explodingPin explodingComponent,
        MultPin multComponent,
        vasePin vaseComponent,
        PinHandCardType type)
    {
        if (explodingComponent != null && type != PinHandCardType.Exploding)
        {
            Destroy(explodingComponent);
        }

        if (multComponent != null && type != PinHandCardType.Mult)
        {
            Destroy(multComponent);
        }

        if (vaseComponent != null && type != PinHandCardType.Vase)
        {
            Destroy(vaseComponent);
        }

        PinHandRuntimeCardMarker marker = pinObject.GetComponent<PinHandRuntimeCardMarker>();
        if (marker != null && type != PinHandCardType.Small && type != PinHandCardType.Banana)
        {
            Destroy(marker);
        }
    }

    private static string GetRuntimePinObjectName(PinHandCardType type)
    {
        switch (type)
        {
            case PinHandCardType.Exploding:
                return "ExplodingPin_Runtime";
            case PinHandCardType.Mult:
                return "MultPin_Runtime";
            case PinHandCardType.Vase:
                return "VasePin_Runtime";
            case PinHandCardType.Small:
                return "SmallPin_Runtime";
            case PinHandCardType.Banana:
                return "BananaPin_Runtime";
            default:
                return "Pin_Runtime";
        }
    }

    private PinHandCardType DetectCardType(Pin pin)
    {
        if (pin == null) return PinHandCardType.Standard;

        PinHandRuntimeCardMarker marker = pin.GetComponent<PinHandRuntimeCardMarker>();
        if (marker != null)
        {
            return marker.cardType;
        }

        if (pin.GetComponent<vasePin>() != null) return PinHandCardType.Vase;
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
            case PinHandCardType.Vase:
                return vasePinConfig;
            case PinHandCardType.Small:
                return smallPinConfig;
            case PinHandCardType.Banana:
                return bananaPinConfig;
            default:
                return standardPinConfig;
        }
    }

    private void ShowStatusMessage(string message, bool warning)
    {
        if (statusText == null)
        {
            return;
        }

        statusText.text = message;
        statusText.color = warning
            ? new Color(1f, 0.76f, 0.34f, 1f)
            : new Color(0.72f, 0.94f, 0.86f, 1f);
        statusMessageHideTime = Time.unscaledTime + StatusMessageDuration;
    }

    private void UpdateStatusMessageVisibility()
    {
        if (statusText == null || string.IsNullOrEmpty(statusText.text))
        {
            return;
        }

        if (Time.unscaledTime < statusMessageHideTime)
        {
            return;
        }

        statusText.text = string.Empty;
        statusText.color = new Color(statusText.color.r, statusText.color.g, statusText.color.b, 0f);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
