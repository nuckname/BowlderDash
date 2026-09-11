using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Creates editable Unity assets once, without changing the user's open scene.
public static class LeaderboardScreenBuilder
{
    public const string Folder = "Assets/Prefab/Leaderboard";
    public const string ScreenPath = Folder + "/LeaderboardScreen.prefab";
    public const string RowPath = Folder + "/LeaderboardRow.prefab";
    public const string PreviewPath = "Assets/Scenes/LeaderboardPreview.unity";
    private static readonly Color Ink = new Color(0.045f, 0.075f, 0.10f);
    private static readonly Color Surface = new Color(0.085f, 0.13f, 0.16f);
    private static readonly Color Gold = new Color(1f, 0.78f, 0.36f);

    [MenuItem("Tools/Leaderboard/Create Missing Screen Assets")]
    public static void CreateMissingAssets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        bool missingRow = AssetDatabase.LoadAssetAtPath<GameObject>(RowPath) == null;
        bool missingScreen = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath) == null;
        bool missingPreview = AssetDatabase.LoadAssetAtPath<SceneAsset>(PreviewPath) == null;
        if (!missingRow && !missingScreen && !missingPreview) return;
        if (TMP_Settings.defaultFontAsset == null)
        {
            Debug.LogWarning("Leaderboard assets need the project's default TMP font. Run Create Missing Screen Assets after the font imports.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefab", "Leaderboard");
        Scene previous = SceneManager.GetActiveScene();
        Scene staging = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(staging);
        try
        {
            if (missingRow)
            {
                var row = BuildRow();
                PrefabUtility.SaveAsPrefabAsset(row.gameObject, RowPath);
                Object.DestroyImmediate(row.gameObject);
            }
            if (missingScreen)
            {
                var screen = BuildScreen();
                screen.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(screen.gameObject, ScreenPath);
                Object.DestroyImmediate(screen.gameObject);
            }
            if (missingPreview)
            {
                var screenObject = (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath), staging);
                var preview = new GameObject("Sample Result - edit values here").AddComponent<LeaderboardScreenPreview>();
                preview.screen = screenObject.GetComponent<LeaderboardScreen>();
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                var camera = new GameObject("Preview Camera").AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Ink;
                camera.cullingMask = 0;
                EditorSceneManager.SaveScene(staging, PreviewPath);
            }
            Debug.Log("Leaderboard prefabs and preview scene are ready. Open Assets/Scenes/LeaderboardPreview.unity to test.");
        }
        finally
        {
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(staging, true);
        }
    }

    private static LeaderboardRow BuildRow()
    {
        var root = Rect("LeaderboardRow", null);
        root.sizeDelta = new Vector2(1000, 52);
        root.gameObject.AddComponent<Image>().color = Surface;
        var layout = root.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 52;
        layout.minHeight = 52;
        var row = root.gameObject.AddComponent<LeaderboardRow>();
        row.rankText = Cell(root, "Rank", "1", 0, 0.08f, Gold);
        row.nameText = Cell(root, "Player", "Player", 0.08f, 0.36f, Color.white);
        row.totalScoreText = Cell(root, "Total", "0", 0.36f, 0.57f, Gold);
        row.highestRoundScoreText = Cell(root, "Best Round Score", "0", 0.57f, 0.81f, Color.white);
        row.highestRoundText = Cell(root, "Highest Round", "0", 0.81f, 1, Color.white);
        return row;
    }

    private static LeaderboardScreen BuildScreen()
    {
        var root = Rect("LeaderboardScreen", null);
        var canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = root.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 800);
        scaler.matchWidthOrHeight = 0.5f;
        root.gameObject.AddComponent<GraphicRaycaster>();
        root.gameObject.AddComponent<Image>().color = Ink;
        var screen = root.gameObject.AddComponent<LeaderboardScreen>();
        var panel = Rect("Panel", root);
        Place(panel, 0.06f, 0.06f, 0.94f, 0.94f);

        screen.outcomeText = Label(panel, "Outcome", "VICTORY", 22, Gold, 0, 0.935f, 1, 1);
        Label(panel, "Title", "LEADERBOARD", 42, Color.white, 0, 0.84f, 1, 0.94f);
        screen.resultText = Label(panel, "Game Result", "", 19, Color.white, 0, 0.735f, 1, 0.84f);
        var header = Rect("Column Headers", panel);
        Place(header, 0, 0.65f, 0.97f, 0.72f);
        Cell(header, "Rank", "#", 0, 0.08f, Gold, 17);
        Cell(header, "Player", "PLAYER", 0.08f, 0.36f, Gold, 17);
        Cell(header, "Total", "TOTAL SCORE", 0.36f, 0.57f, Gold, 17);
        Cell(header, "Best", "HIGHEST ROUND\nSCORE", 0.57f, 0.81f, Gold, 17);
        Cell(header, "Round", "HIGHEST\nROUND", 0.81f, 1, Gold, 17);

        var scroll = Rect("Leaderboard List", panel);
        Place(scroll, 0, 0.29f, 1, 0.645f);
        scroll.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.15f);
        screen.scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
        screen.scrollRect.horizontal = false;
        screen.scrollRect.movementType = ScrollRect.MovementType.Clamped;
        screen.scrollRect.scrollSensitivity = 30;
        var viewport = Rect("Viewport", scroll);
        Place(viewport, 0, 0, 0.97f, 1);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = Rect("Rows", viewport);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1);
        content.sizeDelta = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        screen.rowContainer = content;
        screen.scrollRect.content = content;
        screen.scrollRect.viewport = viewport;
        var track = Rect("Scrollbar", scroll);
        Place(track, 0.982f, 0, 1, 1);
        track.gameObject.AddComponent<Image>().color = Surface;
        var handle = Rect("Handle", track);
        Place(handle, 0, 0, 1, 1);
        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = Gold;
        var scrollbar = track.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        screen.scrollRect.verticalScrollbar = scrollbar;
        screen.scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        screen.emptyText = Label(viewport, "Empty List", "No scores yet. Be the first!", 24,
            Color.white, 0, 0, 1, 1);

        Label(panel, "Name Label", "PLAYER NAME  /  10 CHARACTERS MAX", 16, Gold, 0, 0.205f, 0.52f, 0.28f);
        var input = Rect("Player Name", panel);
        Place(input, 0, 0.115f, 0.49f, 0.205f);
        var inputBackground = input.gameObject.AddComponent<Image>();
        inputBackground.color = Surface;
        screen.nameInput = input.gameObject.AddComponent<TMP_InputField>();
        screen.nameInput.targetGraphic = inputBackground;
        screen.nameInput.characterLimit = LeaderboardStorage.MaxNameLength;
        screen.nameInput.lineType = TMP_InputField.LineType.SingleLine;
        var textArea = Rect("Text Area", input);
        Place(textArea, 0.035f, 0.08f, 0.965f, 0.92f);
        textArea.gameObject.AddComponent<RectMask2D>();
        var placeholder = Label(textArea, "Placeholder", "Enter your name", 24,
            new Color(0.6f, 0.68f, 0.72f), 0, 0, 1, 1);
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        var nameText = Label(textArea, "Text", "", 24, Color.white, 0, 0, 1, 1);
        nameText.alignment = TextAlignmentOptions.MidlineLeft;
        nameText.richText = false;
        screen.nameInput.textViewport = textArea;
        screen.nameInput.textComponent = nameText;
        screen.nameInput.placeholder = placeholder;
        screen.nameInput.customCaretColor = true;
        screen.nameInput.caretColor = Gold;
        screen.saveButton = Button(panel, "Save Score", 0.53f, 0.115f, 0.80f, 0.205f, Gold, Ink);
        screen.skipButton = Button(panel, "Skip", 0.83f, 0.115f, 1, 0.205f, Surface, Color.white);
        screen.playAgainButton = Button(panel, "Play Again", 0.53f, 0.115f, 1, 0.205f, Gold, Ink);
        screen.playAgainButton.gameObject.SetActive(false);
        screen.statusText = Label(panel, "Status", "", 18, Color.white, 0, 0, 1, 0.105f);
        screen.rowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RowPath).GetComponent<LeaderboardRow>();
        return screen;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        if (parent != null) rect.SetParent(parent, false);
        return rect;
    }

    private static void Place(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = new Vector2(left, bottom);
        rect.anchorMax = new Vector2(right, top);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static TMP_Text Label(Transform parent, string name, string text, float size, Color color,
        float left, float bottom, float right, float top)
    {
        var rect = Rect(name, parent);
        Place(rect, left, bottom, right, top);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = size * 0.75f;
        label.fontSizeMax = size;
        label.raycastTarget = false;
        return label;
    }

    private static TMP_Text Cell(Transform parent, string name, string text, float left, float right, Color color, float size = 23)
        => Label(parent, name, text, size, color, left + 0.008f, 0, right - 0.008f, 1);

    private static Button Button(Transform parent, string name, float left, float bottom, float right, float top, Color background, Color foreground)
    {
        var rect = Rect(name, parent);
        Place(rect, left, bottom, right, top);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = background;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        Label(rect, "Label", name, 24, foreground, 0.04f, 0, 0.96f, 1);
        return button;
    }
}
