using UnityEngine;
using UnityEngine.UI;

public class GameUIController : MonoBehaviour
{
    private string placementWarningMessage;
    private Vector2 gameplayHudVisibleAnchoredPosition;
    private Vector3 gameplayHudVisibleScale = Vector3.one;
    private float gameplayHudVisibility = 1f;
    private bool gameplayHudPoseCached;
    private bool gameplayHudTransitionInitialized;

    [Header("HUD Components")]
    public Text frameText;
    public Text ballText;
    public Text scoreToBeatText;
    public Text totalScoreText;
    public Text ticketText;
    
    [Header("Legacy / Optional")]
    public Text currentMathText;

    [Header("Shop Phase Transition")]
    [SerializeField] private RectTransform gameplayHudRoot;
    [SerializeField] private CanvasGroup gameplayHudCanvasGroup;
    [SerializeField] private Vector2 flyPastOffset = new Vector2(0f, -140f);
    [SerializeField] private float flyPastScale = 3.2f;
    [SerializeField] [Range(0f, 1f)] private float hiddenHudAlpha = 0f;

    public void UpdateStandardUI(GrandGameManager gm)
    {
        EnsureGameplayHudReferences();

        int throwDisplay = gm.GetCurrentThrowDisplayNumber();

        if (frameText) frameText.text = $"{gm.currentFrame}/{gm.maxFrames}";
        if (ballText) ballText.text = $"{throwDisplay}/{Mathf.Max(1, gm.maxThrowsPerFrame)}";
        if (scoreToBeatText) scoreToBeatText.text = Mathf.Max(0, gm.currentFrameScoreTarget).ToString();
        if (ticketText) ticketText.text = gm.currentTickets.ToString();

        if (currentMathText)
        {
            currentMathText.text = string.Empty;
        }
    }

    public void SetPlacementWarning(string warningMessage)
    {
        placementWarningMessage = warningMessage;
    }

    void Update()
    {
        GrandGameManager gm = GrandGameManager.Instance;
        if (gm != null && gm.scoreCalculator != null)
        {
            int finalScore = gm.scoreCalculator.GetFinalScore();
            if (totalScoreText) totalScoreText.text = finalScore.ToString();
        }

        UpdateGameplayHudTransition(gm);
    }

    private void UpdateGameplayHudTransition(GrandGameManager gm)
    {
        EnsureGameplayHudReferences();
        if (gameplayHudRoot == null)
        {
            return;
        }

        bool shouldShowGameplayHud = gm == null || gm.GetCurrentState() != GameState.Shop;
        float targetVisibility = shouldShowGameplayHud ? 1f : 0f;

        if (!gameplayHudTransitionInitialized)
        {
            gameplayHudVisibility = targetVisibility;
            ApplyGameplayHudPose(1f - gameplayHudVisibility);
            gameplayHudTransitionInitialized = true;
            return;
        }

        float transitionDuration = GetHudTransitionDuration(gm, shouldShowGameplayHud);
        gameplayHudVisibility = Mathf.MoveTowards(
            gameplayHudVisibility,
            targetVisibility,
            Time.deltaTime / Mathf.Max(0.0001f, transitionDuration));

        ApplyGameplayHudPose(1f - gameplayHudVisibility);
    }

    private float GetHudTransitionDuration(GrandGameManager gm, bool shouldShowGameplayHud)
    {
        if (gm == null)
        {
            return 1f;
        }

        return shouldShowGameplayHud
            ? Mathf.Max(0.01f, gm.cameraPlacementTransitionTime)
            : Mathf.Max(0.01f, gm.cameraShopTransitionTime);
    }

    private void ApplyGameplayHudPose(float hiddenBlend)
    {
        if (gameplayHudRoot == null)
        {
            return;
        }

        float easedBlend = hiddenBlend * hiddenBlend * (3f - (2f * hiddenBlend));
        float scaleMultiplier = Mathf.Lerp(1f, Mathf.Max(1f, flyPastScale), easedBlend);

        gameplayHudRoot.anchoredPosition = gameplayHudVisibleAnchoredPosition + (flyPastOffset * easedBlend);
        gameplayHudRoot.localScale = Vector3.Scale(gameplayHudVisibleScale, new Vector3(scaleMultiplier, scaleMultiplier, scaleMultiplier));

        if (gameplayHudCanvasGroup != null)
        {
            gameplayHudCanvasGroup.alpha = Mathf.Lerp(1f, hiddenHudAlpha, easedBlend);
            bool hudActive = gameplayHudVisibility > 0.001f;
            gameplayHudCanvasGroup.interactable = hudActive;
            gameplayHudCanvasGroup.blocksRaycasts = hudActive;
        }
    }

    private void EnsureGameplayHudReferences()
    {
        if (gameplayHudRoot == null)
        {
            gameplayHudRoot = ResolveGameplayHudRoot();
            gameplayHudPoseCached = false;
        }

        if (gameplayHudRoot == null)
        {
            return;
        }

        if (gameplayHudCanvasGroup == null)
        {
            gameplayHudCanvasGroup = gameplayHudRoot.GetComponent<CanvasGroup>();
            if (gameplayHudCanvasGroup == null)
            {
                gameplayHudCanvasGroup = gameplayHudRoot.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (!gameplayHudPoseCached)
        {
            gameplayHudVisibleAnchoredPosition = gameplayHudRoot.anchoredPosition;
            gameplayHudVisibleScale = gameplayHudRoot.localScale;
            gameplayHudPoseCached = true;
        }
    }

    private RectTransform ResolveGameplayHudRoot()
    {
        Text[] candidateTexts =
        {
            frameText,
            ballText,
            scoreToBeatText,
            totalScoreText,
            ticketText,
            currentMathText
        };

        for (int i = 0; i < candidateTexts.Length; i++)
        {
            Text candidate = candidateTexts[i];
            if (candidate == null)
            {
                continue;
            }

            Canvas rootCanvas = candidate.canvas != null
                ? candidate.canvas.rootCanvas
                : candidate.GetComponentInParent<Canvas>();

            RectTransform current = candidate.rectTransform;
            if (current == null || rootCanvas == null)
            {
                continue;
            }

            RectTransform parentRect = current.parent as RectTransform;
            while (parentRect != null && parentRect != rootCanvas.transform)
            {
                current = parentRect;
                parentRect = current.parent as RectTransform;
            }

            return current;
        }

        return null;
    }
}
