using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PinShopController : MonoBehaviour
{
    private enum ShopPage
    {
        Buy,
        Sell,
        Upgrades
    }

    private class ShopRow
    {
        public Text ownedText;
        public Text priceText;
        public Button actionButton;
        public Text actionButtonText;
    }

    [Header("Scene Shop References")]
    [SerializeField] private Canvas shopCanvas;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private CanvasScaler canvasScaler;
    [SerializeField] private Text titleText;
    [SerializeField] private Text summaryText;
    [SerializeField] private Text pageTitleText;
    [SerializeField] private Text footerText;
    [SerializeField] private Button prevPageButton;
    [SerializeField] private Button nextPageButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private RectTransform buyPageRoot;
    [SerializeField] private RectTransform sellPageRoot;
    [SerializeField] private RectTransform upgradesPageRoot;
    [SerializeField] private ScrollRect buyScrollRect;
    [SerializeField] private ScrollRect sellScrollRect;

    [Header("TV Shop Anchors")]
    public Transform worldSpaceScreenAnchor;
    public Vector2 canvasSize = new Vector2(760f, 460f);
    public Vector3 canvasLocalPosition = Vector3.zero;
    public Vector3 canvasLocalEulerAngles = Vector3.zero;
    public Vector3 canvasLocalScale = new Vector3(0.002f, 0.002f, 0.002f);

    [Header("Prices")]
    public int standardBuyPrice = 2;
    public int explodingBuyPrice = 5;
    public int multBuyPrice = 4;

    [Header("Style")]
    public Color backdropColor = new Color(0.03f, 0.05f, 0.08f, 0.92f);
    public Color panelColor = new Color(0.12f, 0.15f, 0.2f, 0.98f);
    public Color highlightColor = new Color(1f, 0.82f, 0.18f, 1f);
    public Color textColor = new Color(0.95f, 0.95f, 0.92f, 1f);
    public Color disabledTextColor = new Color(0.55f, 0.58f, 0.62f, 1f);

    private readonly Dictionary<PinHandCardType, ShopRow> buyRows = new Dictionary<PinHandCardType, ShopRow>();
    private readonly Dictionary<PinHandCardType, ShopRow> sellRows = new Dictionary<PinHandCardType, ShopRow>();

    private GrandGameManager gameManager;
    private PinHandInventory inventory;

    private RectTransform pageHost;
    private RectTransform buyPageContent;
    private RectTransform sellPageContent;
    private Font uiFont;
    private ShopPage currentPage;
    private bool createdRuntimeCanvas;

    public void Configure(GrandGameManager owner, PinHandInventory pinInventory)
    {
        gameManager = owner;
        inventory = pinInventory;

        EnsureEventSystemExists();
        EnsureShopReferences();
        RefreshShop();
        HideShop();
    }

    public void ShowShop()
    {
        EnsureShopReferences();
        RefreshCanvasPlacement();
        currentPage = ShopPage.Buy;
        SetVisible(true);
        RefreshShop();
    }

    public void HideShop()
    {
        if (canvasGroup == null)
        {
            return;
        }

        SetVisible(false);
    }

    public void RefreshShop()
    {
        if (gameManager == null || inventory == null)
        {
            return;
        }

        EnsureShopReferences();
        if (titleText == null)
        {
            return;
        }

        titleText.text = "PIN SHOP";

        string frameSummary = $"Tickets: {gameManager.currentTickets}";
        if (gameManager.ticketsEarnedLastFrame > 0)
        {
            frameSummary += $"\nLast frame payout: +{gameManager.ticketsEarnedLastFrame}{gameManager.GetLastFrameTicketBonusSummary()}";
        }

        if (summaryText != null) summaryText.text = frameSummary;
        if (pageTitleText != null) pageTitleText.text = GetPageTitle(currentPage);
        if (footerText != null) footerText.text = GetFooterCopy(currentPage);

        RefreshRows(buyRows, isBuyPage: true);
        RefreshRows(sellRows, isBuyPage: false);
        RefreshPageVisibility();

        if (prevPageButton != null)
        {
            prevPageButton.interactable = true;
        }

        if (nextPageButton != null)
        {
            nextPageButton.interactable = true;
        }

        if (continueButton != null)
        {
            continueButton.interactable = gameManager.GetCurrentState() == GameState.Shop;
        }
    }

    private void EnsureShopReferences()
    {
        if (!TryBindSceneShop())
        {
            BuildRuntimeShop();
        }

        RegisterButtonListeners();
    }

    private bool TryBindSceneShop()
    {
        if (shopCanvas == null)
        {
            if (worldSpaceScreenAnchor != null)
            {
                shopCanvas = worldSpaceScreenAnchor.GetComponentInChildren<Canvas>(true);
            }

            if (shopCanvas == null)
            {
                shopCanvas = FindNamedComponentInChildren<Canvas>("PinShopCanvas");
            }
        }

        if (shopCanvas == null)
        {
            return false;
        }

        canvasGroup = canvasGroup != null ? canvasGroup : shopCanvas.GetComponent<CanvasGroup>();
        canvasScaler = canvasScaler != null ? canvasScaler : shopCanvas.GetComponent<CanvasScaler>();

        titleText = titleText != null ? titleText : FindNamedComponentInChildren<Text>("Title", shopCanvas.transform);
        summaryText = summaryText != null ? summaryText : FindNamedComponentInChildren<Text>("Summary", shopCanvas.transform);
        pageTitleText = pageTitleText != null ? pageTitleText : FindNamedComponentInChildren<Text>("PageTitle", shopCanvas.transform);
        footerText = footerText != null ? footerText : FindNamedComponentInChildren<Text>("Footer", shopCanvas.transform);
        prevPageButton = prevPageButton != null ? prevPageButton : FindNamedComponentInChildren<Button>("PrevPageButton", shopCanvas.transform);
        nextPageButton = nextPageButton != null ? nextPageButton : FindNamedComponentInChildren<Button>("NextPageButton", shopCanvas.transform);
        continueButton = continueButton != null ? continueButton : FindNamedComponentInChildren<Button>("ContinueButton", shopCanvas.transform);
        buyPageRoot = buyPageRoot != null ? buyPageRoot : FindNamedComponentInChildren<RectTransform>("BuyPage", shopCanvas.transform);
        sellPageRoot = sellPageRoot != null ? sellPageRoot : FindNamedComponentInChildren<RectTransform>("SellPage", shopCanvas.transform);
        upgradesPageRoot = upgradesPageRoot != null ? upgradesPageRoot : FindNamedComponentInChildren<RectTransform>("UpgradesPage", shopCanvas.transform);
        buyScrollRect = buyScrollRect != null ? buyScrollRect : FindNamedComponentInChildren<ScrollRect>("BuyPage", shopCanvas.transform);
        sellScrollRect = sellScrollRect != null ? sellScrollRect : FindNamedComponentInChildren<ScrollRect>("SellPage", shopCanvas.transform);

        BindSceneRows();
        return canvasGroup != null;
    }

    private void RegisterButtonListeners()
    {
        RegisterButton(prevPageButton, ShowPreviousPage);
        RegisterButton(nextPageButton, ShowNextPage);
        RegisterButton(continueButton, OnContinuePressed);
    }

    private void RegisterButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void BindSceneRows()
    {
        if (shopCanvas == null)
        {
            return;
        }

        buyRows.Clear();
        sellRows.Clear();

        BindRow(buyPageRoot, buyRows, PinHandCardType.Standard, true, "Standard_BuyRow");
        BindRow(buyPageRoot, buyRows, PinHandCardType.Exploding, true, "Exploding_BuyRow");
        BindRow(buyPageRoot, buyRows, PinHandCardType.Mult, true, "Mult_BuyRow");

        BindRow(sellPageRoot, sellRows, PinHandCardType.Standard, false, "Standard_SellRow");
        BindRow(sellPageRoot, sellRows, PinHandCardType.Exploding, false, "Exploding_SellRow");
        BindRow(sellPageRoot, sellRows, PinHandCardType.Mult, false, "Mult_SellRow");
    }

    private void BindRow(RectTransform pageRoot, Dictionary<PinHandCardType, ShopRow> targetRows, PinHandCardType type, bool isBuyPage, string rowObjectName)
    {
        if (pageRoot == null)
        {
            return;
        }

        RectTransform rowRoot = FindNamedComponentInChildren<RectTransform>(rowObjectName, pageRoot);
        if (rowRoot == null)
        {
            return;
        }

        ShopRow row = new ShopRow
        {
            ownedText = FindNamedComponentInChildren<Text>("Owned", rowRoot),
            priceText = FindNamedComponentInChildren<Text>("Price", rowRoot),
            actionButton = FindNamedComponentInChildren<Button>(isBuyPage ? "BuyButton" : "SellButton", rowRoot)
        };

        if (row.actionButton != null)
        {
            row.actionButtonText = row.actionButton.GetComponentInChildren<Text>(true);
            RegisterButton(row.actionButton, isBuyPage ? (() => OnBuyPressed(type)) : (() => OnSellPressed(type)));
        }

        if (row.ownedText != null && row.priceText != null && row.actionButton != null)
        {
            targetRows[type] = row;
        }
    }

    private void BuildRuntimeShop()
    {
        if (shopCanvas != null)
        {
            RefreshCanvasPlacement();
            return;
        }

        GameObject canvasObject = new GameObject("PinShopCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        shopCanvas = canvasObject.GetComponent<Canvas>();
        canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        canvasGroup = canvasObject.GetComponent<CanvasGroup>();
        createdRuntimeCanvas = true;
        shopCanvas.sortingOrder = 80;
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = canvasSize;
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = canvasSize;

        RefreshCanvasPlacement();

        GameObject backdrop = CreateImageObject("Backdrop", backdropColor, shopCanvas.transform);
        StretchToParent(backdrop.GetComponent<RectTransform>());

        GameObject panel = CreateImageObject("Panel", panelColor, backdrop.transform);
        RectTransform panelRoot = panel.GetComponent<RectTransform>();
        panelRoot.anchorMin = Vector2.zero;
        panelRoot.anchorMax = Vector2.one;
        panelRoot.offsetMin = new Vector2(18f, 18f);
        panelRoot.offsetMax = new Vector2(-18f, -18f);

        VerticalLayoutGroup panelLayout = panel.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(22, 22, 18, 18);
        panelLayout.spacing = 10f;
        panelLayout.childAlignment = TextAnchor.UpperCenter;
        panelLayout.childForceExpandHeight = false;
        panelLayout.childControlHeight = true;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childControlWidth = true;

        titleText = CreateText("Title", panel.transform, 24, FontStyle.Bold, TextAnchor.MiddleCenter, textColor, 42f);
        summaryText = CreateText("Summary", panel.transform, 15, FontStyle.Normal, TextAnchor.MiddleCenter, highlightColor, 48f);

        BuildPageNavigator(panel.transform);
        BuildPages(panel.transform);

        footerText = CreateText("Footer", panel.transform, 13, FontStyle.Italic, TextAnchor.MiddleCenter, disabledTextColor, 28f);
        continueButton = CreateButton("ContinueButton", panel.transform, "CONTINUE", new Vector2(210f, 38f), OnContinuePressed);

        SetVisible(false);
    }

    private void BuildPageNavigator(Transform parent)
    {
        GameObject navRow = new GameObject("PageNav", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        navRow.transform.SetParent(parent, false);

        LayoutElement navLayout = navRow.GetComponent<LayoutElement>();
        navLayout.preferredHeight = 36f;

        HorizontalLayoutGroup navGroup = navRow.GetComponent<HorizontalLayoutGroup>();
        navGroup.spacing = 8f;
        navGroup.childAlignment = TextAnchor.MiddleCenter;
        navGroup.childControlWidth = false;
        navGroup.childForceExpandWidth = false;
        navGroup.childControlHeight = true;
        navGroup.childForceExpandHeight = true;

        prevPageButton = CreateButton("PrevPageButton", navRow.transform, "<", new Vector2(60f, 24f), ShowPreviousPage);
        pageTitleText = CreateFixedWidthText("PageTitle", navRow.transform, 17, FontStyle.Bold, TextAnchor.MiddleCenter, textColor, 240f);
        nextPageButton = CreateButton("NextPageButton", navRow.transform, ">", new Vector2(60f, 24f), ShowNextPage);
    }

    private void BuildPages(Transform parent)
    {
        GameObject pageHostObject = new GameObject("PageHost", typeof(RectTransform), typeof(LayoutElement));
        pageHostObject.transform.SetParent(parent, false);
        pageHost = pageHostObject.GetComponent<RectTransform>();
        LayoutElement hostLayout = pageHostObject.GetComponent<LayoutElement>();
        hostLayout.minHeight = 120f;
        hostLayout.flexibleHeight = 1f;

        CreateScrollPageRoot("BuyPage", pageHostObject.transform, out buyPageRoot, out buyPageContent, out buyScrollRect);
        CreateScrollPageRoot("SellPage", pageHostObject.transform, out sellPageRoot, out sellPageContent, out sellScrollRect);
        upgradesPageRoot = CreatePageRoot("UpgradesPage", pageHostObject.transform);

        CreateBuySellRows(buyPageContent, buyRows, true);
        CreateBuySellRows(sellPageContent, sellRows, false);
        CreateUpgradePage(upgradesPageRoot);
    }

    private RectTransform CreatePageRoot(string objectName, Transform parent)
    {
        GameObject pageObject = new GameObject(objectName, typeof(RectTransform), typeof(VerticalLayoutGroup));
        pageObject.transform.SetParent(parent, false);

        RectTransform rect = pageObject.GetComponent<RectTransform>();
        StretchToParent(rect);

        VerticalLayoutGroup layout = pageObject.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;

        return rect;
    }

    private void CreateScrollPageRoot(string objectName, Transform parent, out RectTransform rootRect, out RectTransform contentRect, out ScrollRect scrollRect)
    {
        GameObject pageObject = new GameObject(objectName, typeof(RectTransform), typeof(ScrollRect));
        pageObject.transform.SetParent(parent, false);

        rootRect = pageObject.GetComponent<RectTransform>();
        StretchToParent(rootRect);

        GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportObject.transform.SetParent(pageObject.transform, false);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(0f, 0f);
        viewportRect.offsetMax = new Vector2(-18f, 0f);

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.02f);
        viewportImage.raycastTarget = true;

        Mask viewportMask = viewportObject.GetComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewportObject.transform, false);
        contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup contentLayout = contentObject.GetComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 8f;
        contentLayout.childAlignment = TextAnchor.UpperCenter;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.childControlWidth = true;
        contentLayout.childForceExpandWidth = true;

        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Scrollbar scrollbar = CreateVerticalScrollbar(pageObject.transform);

        scrollRect = pageObject.GetComponent<ScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
    }

    private void CreateBuySellRows(Transform parent, Dictionary<PinHandCardType, ShopRow> targetRows, bool isBuyPage)
    {
        CreateRow(parent, targetRows, PinHandCardType.Standard, isBuyPage);
        CreateRow(parent, targetRows, PinHandCardType.Exploding, isBuyPage);
        CreateRow(parent, targetRows, PinHandCardType.Mult, isBuyPage);
    }

    private void CreateUpgradePage(Transform parent)
    {
        GameObject card = CreateImageObject("UpgradeCard", new Color(0.08f, 0.1f, 0.14f, 0.95f), parent);
        LayoutElement cardLayout = card.AddComponent<LayoutElement>();
        cardLayout.preferredHeight = 138f;

        VerticalLayoutGroup group = card.AddComponent<VerticalLayoutGroup>();
        group.padding = new RectOffset(16, 16, 14, 14);
        group.spacing = 6f;
        group.childAlignment = TextAnchor.UpperLeft;
        group.childControlWidth = true;
        group.childControlHeight = false;
        group.childForceExpandWidth = true;
        group.childForceExpandHeight = false;

        CreateText("UpgradeTitle", card.transform, 18, FontStyle.Bold, TextAnchor.UpperLeft, textColor, 24f).text = "UPGRADES";
        CreateText("UpgradeBody", card.transform, 14, FontStyle.Normal, TextAnchor.UpperLeft, disabledTextColor, 68f).text =
            "This page is reserved for future pin stat upgrades. The page flow is now separated, so we can drop upgrade cards here later without crowding buy and sell.";
        CreateText("UpgradeHint", card.transform, 13, FontStyle.Italic, TextAnchor.UpperLeft, highlightColor, 20f).text =
            "Use < and > to flip pages on the TV.";
    }

    private void CreateRow(Transform parent, Dictionary<PinHandCardType, ShopRow> targetRows, PinHandCardType type, bool isBuyPage)
    {
        GameObject rowObject = CreateImageObject($"{type}_{(isBuyPage ? "Buy" : "Sell")}Row", new Color(0.09f, 0.12f, 0.17f, 0.96f), parent);
        LayoutElement rowLayout = rowObject.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = 46f;

        HorizontalLayoutGroup layoutGroup = rowObject.AddComponent<HorizontalLayoutGroup>();
        layoutGroup.padding = new RectOffset(10, 10, 6, 6);
        layoutGroup.spacing = 8f;
        layoutGroup.childAlignment = TextAnchor.MiddleCenter;
        layoutGroup.childControlWidth = false;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childControlHeight = true;
        layoutGroup.childForceExpandHeight = true;

        ShopRow row = new ShopRow();
        row.ownedText = CreateFixedWidthText("Owned", rowObject.transform, 15, FontStyle.Bold, TextAnchor.MiddleLeft, textColor, 190f);
        row.priceText = CreateFixedWidthText("Price", rowObject.transform, 13, FontStyle.Normal, TextAnchor.MiddleCenter, highlightColor, 100f);
        row.actionButton = CreateButton(isBuyPage ? "BuyButton" : "SellButton", rowObject.transform, isBuyPage ? "BUY" : "SELL", new Vector2(86f, 28f),
            isBuyPage ? (() => OnBuyPressed(type)) : (() => OnSellPressed(type)));
        row.actionButtonText = row.actionButton.GetComponentInChildren<Text>();

        targetRows[type] = row;
    }

    private void RefreshRows(Dictionary<PinHandCardType, ShopRow> sourceRows, bool isBuyPage)
    {
        foreach (KeyValuePair<PinHandCardType, ShopRow> kvp in sourceRows)
        {
            PinHandCardType type = kvp.Key;
            ShopRow row = kvp.Value;
            int owned = inventory.GetOwnedCount(type);
            int maxOwned = inventory.GetMaxOwnedCount(type);

            row.ownedText.text = $"{inventory.GetDisplayName(type)}  {owned}/{maxOwned}";

            if (isBuyPage)
            {
                int buyPrice = GetBuyPrice(type);
                bool canBuy = inventory.CanBuyPin(type) && gameManager.currentTickets >= buyPrice;
                row.priceText.text = $"Cost {buyPrice}";
                row.actionButton.interactable = canBuy;
                row.actionButtonText.color = canBuy ? textColor : disabledTextColor;
            }
            else
            {
                int sellPrice = GetSellPrice(type);
                bool canSell = inventory.CanSellPin(type);
                row.priceText.text = $"Return {sellPrice}";
                row.actionButton.interactable = canSell;
                row.actionButtonText.color = canSell ? textColor : disabledTextColor;
            }
        }

        Canvas.ForceUpdateCanvases();
    }

    private void ShowPreviousPage()
    {
        currentPage = (ShopPage)(((int)currentPage + 2) % 3);
        RefreshShop();
    }

    private void ShowNextPage()
    {
        currentPage = (ShopPage)(((int)currentPage + 1) % 3);
        RefreshShop();
    }

    private void RefreshPageVisibility()
    {
        if (buyPageRoot != null) buyPageRoot.gameObject.SetActive(currentPage == ShopPage.Buy);
        if (sellPageRoot != null) sellPageRoot.gameObject.SetActive(currentPage == ShopPage.Sell);
        if (upgradesPageRoot != null) upgradesPageRoot.gameObject.SetActive(currentPage == ShopPage.Upgrades);

        if (buyScrollRect != null && currentPage == ShopPage.Buy)
        {
            buyScrollRect.verticalNormalizedPosition = 1f;
        }

        if (sellScrollRect != null && currentPage == ShopPage.Sell)
        {
            sellScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private void OnBuyPressed(PinHandCardType type)
    {
        if (gameManager == null || inventory == null)
        {
            return;
        }

        int buyPrice = GetBuyPrice(type);
        if (!inventory.CanBuyPin(type))
        {
            return;
        }

        if (!gameManager.TrySpendTickets(buyPrice))
        {
            return;
        }

        if (!inventory.TryBuyPin(type))
        {
            gameManager.AddTickets(buyPrice);
        }

        RefreshShop();
    }

    private void OnSellPressed(PinHandCardType type)
    {
        if (gameManager == null || inventory == null)
        {
            return;
        }

        if (!inventory.TrySellPin(type))
        {
            return;
        }

        gameManager.AddTickets(GetSellPrice(type));
        RefreshShop();
    }

    private void OnContinuePressed()
    {
        if (gameManager == null)
        {
            return;
        }

        gameManager.CompleteShopPhase();
    }

    private void RefreshCanvasPlacement()
    {
        if (shopCanvas == null)
        {
            return;
        }

        if (!createdRuntimeCanvas)
        {
            return;
        }

        RectTransform canvasRect = shopCanvas.GetComponent<RectTransform>();
        shopCanvas.worldCamera = gameManager != null ? gameManager.targetCamera : Camera.main;

        if (worldSpaceScreenAnchor != null)
        {
            shopCanvas.renderMode = RenderMode.WorldSpace;
            canvasRect.SetParent(worldSpaceScreenAnchor, false);
            canvasRect.sizeDelta = canvasSize;
            canvasRect.localPosition = canvasLocalPosition;
            canvasRect.localRotation = Quaternion.Euler(canvasLocalEulerAngles);
            canvasRect.localScale = canvasLocalScale;
        }
        else
        {
            shopCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasRect.SetParent(transform, false);
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;
        }
    }

    private GameObject CreateImageObject(string objectName, Color color, Transform parent)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return imageObject;
    }

    private Text CreateText(string objectName, Transform parent, int fontSize, FontStyle fontStyle, TextAnchor anchor, Color color, float preferredHeight)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);

        LayoutElement layout = textObject.GetComponent<LayoutElement>();
        layout.preferredHeight = preferredHeight;

        Text text = textObject.GetComponent<Text>();
        text.font = GetUIFont();
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = anchor;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private Text CreateFixedWidthText(string objectName, Transform parent, int fontSize, FontStyle fontStyle, TextAnchor anchor, Color color, float preferredWidth)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);

        LayoutElement layout = textObject.GetComponent<LayoutElement>();
        layout.preferredWidth = preferredWidth;

        Text text = textObject.GetComponent<Text>();
        text.font = GetUIFont();
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = anchor;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private Button CreateButton(string objectName, Transform parent, string label, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);

        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredWidth = size.x;
        layout.preferredHeight = size.y;

        Image image = buttonObject.GetComponent<Image>();
        image.color = highlightColor;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        StretchToParent(labelRect);

        Text text = labelObject.GetComponent<Text>();
        text.font = GetUIFont();
        text.text = label;
        text.fontSize = 14;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = textColor;
        text.raycastTarget = false;

        return button;
    }

    private Scrollbar CreateVerticalScrollbar(Transform parent)
    {
        GameObject scrollbarObject = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        scrollbarObject.transform.SetParent(parent, false);

        RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 0.5f);
        scrollbarRect.sizeDelta = new Vector2(14f, 0f);
        scrollbarRect.anchoredPosition = Vector2.zero;

        Image background = scrollbarObject.GetComponent<Image>();
        background.color = new Color(1f, 1f, 1f, 0.12f);

        GameObject slidingArea = new GameObject("Sliding Area", typeof(RectTransform));
        slidingArea.transform.SetParent(scrollbarObject.transform, false);
        RectTransform slidingAreaRect = slidingArea.GetComponent<RectTransform>();
        slidingAreaRect.anchorMin = Vector2.zero;
        slidingAreaRect.anchorMax = Vector2.one;
        slidingAreaRect.offsetMin = new Vector2(2f, 2f);
        slidingAreaRect.offsetMax = new Vector2(-2f, -2f);

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(slidingArea.transform, false);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 1f);
        handleRect.anchorMax = new Vector2(1f, 1f);
        handleRect.pivot = new Vector2(0.5f, 1f);
        handleRect.sizeDelta = new Vector2(0f, 32f);

        Image handleImage = handle.GetComponent<Image>();
        handleImage.color = highlightColor;

        Scrollbar scrollbar = scrollbarObject.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;
        scrollbar.value = 1f;

        return scrollbar;
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.blocksRaycasts = visible;
        canvasGroup.interactable = visible;
    }

    private Font GetUIFont()
    {
        if (uiFont != null)
        {
            return uiFont;
        }

        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (uiFont == null)
        {
            uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return uiFont;
    }

    private int GetBuyPrice(PinHandCardType type)
    {
        switch (type)
        {
            case PinHandCardType.Exploding:
                return Mathf.Max(1, explodingBuyPrice);
            case PinHandCardType.Mult:
                return Mathf.Max(1, multBuyPrice);
            default:
                return Mathf.Max(1, standardBuyPrice);
        }
    }

    private int GetSellPrice(PinHandCardType type)
    {
        return Mathf.Max(1, Mathf.FloorToInt(GetBuyPrice(type) * 0.5f));
    }

    private string GetPageTitle(ShopPage page)
    {
        switch (page)
        {
            case ShopPage.Sell:
                return "SELL PINS";
            case ShopPage.Upgrades:
                return "UPGRADES";
            default:
                return "BUY PINS";
        }
    }

    private string GetFooterCopy(ShopPage page)
    {
        switch (page)
        {
            case ShopPage.Sell:
                return "Sell owned pins here for half their purchase price.";
            case ShopPage.Upgrades:
                return "Upgrade space is reserved for the next pass.";
            default:
                return "Spend tickets here to add more pins to your stock.";
        }
    }

    private void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private void EnsureEventSystemExists()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(eventSystemObject);
    }

    private T FindNamedComponentInChildren<T>(string objectName) where T : Component
    {
        return FindNamedComponentInChildren<T>(objectName, transform);
    }

    private T FindNamedComponentInChildren<T>(string objectName, Transform root) where T : Component
    {
        if (root == null)
        {
            return null;
        }

        Transform target = FindChildRecursive(root, objectName);
        if (target == null)
        {
            return null;
        }

        return target.GetComponent<T>();
    }

    private Transform FindChildRecursive(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildRecursive(root.GetChild(i), objectName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
