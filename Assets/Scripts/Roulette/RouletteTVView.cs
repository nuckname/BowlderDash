using TMPro;
using UnityEngine;

public sealed class RouletteTVView : MonoBehaviour
{
    [SerializeField] private RouletteController controller;
    [SerializeField] private RouletteColour colour;

    [Header("Inner Panels")]
    [SerializeField] private GameObject selectionPanel;
    [SerializeField] private GameObject wagerPanel;

    [Header("Buttons")]
    [SerializeField] private UnityEngine.UI.Button selectButton;
    [SerializeField] private UnityEngine.UI.Button increaseButton;
    [SerializeField] private UnityEngine.UI.Button decreaseButton;
    [SerializeField] private UnityEngine.UI.Button goButton;
    [SerializeField] private UnityEngine.UI.Button backButton;

    [Header("Text")]
    [SerializeField] private TMP_Text selectionText;
    [SerializeField] private TMP_Text colourText;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private TMP_Text wagerText;
    [SerializeField] private TMP_Text balanceText;
    [SerializeField] private TMP_Text payoutRuleText;
    [SerializeField] private TMP_Text statusText;

    public RouletteColour Colour => colour;

    public bool IsConfiguredFor(RouletteController owner, RouletteColour expectedColour)
    {
        return owner != null && isActiveAndEnabled && controller == owner && colour == expectedColour &&
            selectionPanel != null && wagerPanel != null &&
            !selectionPanel.transform.IsChildOf(wagerPanel.transform) &&
            !wagerPanel.transform.IsChildOf(selectionPanel.transform) &&
            !transform.IsChildOf(selectionPanel.transform) && !transform.IsChildOf(wagerPanel.transform) &&
            !owner.HasControlsUnder(selectionPanel.transform) && !owner.HasControlsUnder(wagerPanel.transform) &&
            IsInside(selectButton, selectionPanel) && IsInside(selectionText, selectionPanel) &&
            IsInside(increaseButton, wagerPanel) && IsInside(decreaseButton, wagerPanel) &&
            IsInside(goButton, wagerPanel) && IsInside(backButton, wagerPanel) &&
            IsInside(colourText, wagerPanel) && IsInside(promptText, wagerPanel) &&
            IsInside(wagerText, wagerPanel) && IsInside(balanceText, wagerPanel) &&
            IsInside(payoutRuleText, wagerPanel) && IsInside(statusText, wagerPanel);
    }

    private static bool IsInside(Component component, GameObject panel)
    {
        return component != null && component.transform.IsChildOf(panel.transform);
    }

    public void SelectColour()
    {
        if (isActiveAndEnabled && controller != null) controller.SelectColour(this);
    }

    public void IncreaseWager()
    {
        if (isActiveAndEnabled && controller != null) controller.ChangeWager(this, 1);
    }

    public void DecreaseWager()
    {
        if (isActiveAndEnabled && controller != null) controller.ChangeWager(this, -1);
    }

    public void BackToTable()
    {
        if (isActiveAndEnabled && controller != null) controller.CancelDraft(this);
    }

    public void Go()
    {
        if (isActiveAndEnabled && controller != null) controller.CommitWager(this);
    }

    public void Render(bool selecting, bool editing, int stake, int wallet, int maximumStake,
        bool interactive = true, bool canGo = false, string unavailableReason = null)
    {
        if (selectionPanel != null) selectionPanel.SetActive(selecting);
        if (wagerPanel != null) wagerPanel.SetActive(editing);
        if (selectButton != null) selectButton.interactable = selecting && interactive;
        if (increaseButton != null) increaseButton.interactable = editing && interactive && stake < maximumStake;
        if (decreaseButton != null) decreaseButton.interactable = editing && interactive && stake > 1;
        if (backButton != null) backButton.interactable = editing && interactive;
        if (goButton != null) goButton.interactable = editing && interactive && canGo;

        if (selectionText != null) selectionText.text = $"Bet on {colour}";
        if (colourText != null) colourText.text = colour.ToString();
        if (promptText != null) promptText.text = "How many tickets to wager";
        if (wagerText != null) wagerText.text = (editing ? stake : 0).ToString();
        if (balanceText != null) balanceText.text = $"Your tickets: {wallet}";
        if (payoutRuleText != null)
            payoutRuleText.text = $"{RouletteWager.GetMultiplier(colour)}x total return on a win (stake included)";
        if (statusText != null)
            statusText.text = wallet <= 0 ? "No tickets available" :
                maximumStake == 0 ? "No safe wager available" :
                canGo ? "Ready to spin" : unavailableReason ?? "Waiting for the camera";
    }

    private void OnEnable()
    {
        if (goButton != null) goButton.interactable = false;
    }

    private void OnDisable()
    {
        if (selectButton != null) selectButton.interactable = false;
        if (increaseButton != null) increaseButton.interactable = false;
        if (decreaseButton != null) decreaseButton.interactable = false;
        if (goButton != null) goButton.interactable = false;
        if (backButton != null) backButton.interactable = false;
    }
}
