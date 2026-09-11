using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardScreen : MonoBehaviour
{
    public TMP_Text outcomeText;
    public TMP_Text resultText;
    public TMP_Text emptyText;
    public TMP_Text statusText;
    public TMP_InputField nameInput;
    public Button saveButton;
    public Button skipButton;
    public Button playAgainButton;
    public ScrollRect scrollRect;
    public Transform rowContainer;
    public LeaderboardRow rowPrefab;

    private readonly List<LeaderboardRow> rows = new List<LeaderboardRow>();
    private LeaderboardStorage storage;
    private LeaderboardEntry result;
    private Action onSkip;
    private Action onPlayAgain;
    private bool submitted;
    private bool finished;
    private bool isSaving;

    private void Awake()
    {
        nameInput.characterLimit = LeaderboardStorage.MaxNameLength;
        nameInput.lineType = TMP_InputField.LineType.SingleLine;
        nameInput.richText = false;
        nameInput.textComponent.richText = false;
        saveButton.onClick.AddListener(SaveScore);
        skipButton.onClick.AddListener(Skip);
        playAgainButton.onClick.AddListener(PlayAgain);
    }

    // Capture a copy: the displayed result cannot change if the session later resets.
    public void Show(LeaderboardEntry gameResult, bool victory, Action skip, Action playAgain,
        LeaderboardStorage leaderboardStorage = null)
    {
        if (gameResult == null) throw new ArgumentNullException(nameof(gameResult));
        gameObject.SetActive(true);
        storage = leaderboardStorage ?? new LeaderboardStorage();
        result = new LeaderboardEntry
        {
            totalScore = gameResult.totalScore,
            highestRoundScore = gameResult.highestRoundScore,
            highestRoundReached = gameResult.highestRoundReached
        };
        onSkip = skip;
        onPlayAgain = playAgain;
        submitted = false;
        finished = false;
        isSaving = false;
        outcomeText.text = victory ? "VICTORY" : "GAME OVER";
        resultText.text = $"TOTAL SCORE  {result.totalScore:N0}     •     HIGHEST ROUND SCORE  {result.highestRoundScore:N0}     •     HIGHEST ROUND  {result.highestRoundReached}";
        nameInput.SetTextWithoutNotify(string.Empty);
        nameInput.interactable = true;
        saveButton.gameObject.SetActive(true);
        saveButton.interactable = true;
        skipButton.gameObject.SetActive(true);
        skipButton.interactable = true;
        playAgainButton.gameObject.SetActive(false);
        bool loaded = storage.TryLoad(out var entries, out string error);
        Render(entries, loaded);
        statusText.text = loaded ? "Enter your name to save this game, or skip." : error;
        nameInput.Select();
        nameInput.ActivateInputField();
    }

    public void SaveScore()
    {
        if (result == null || submitted || finished || isSaving) return;
        isSaving = true;
        saveButton.interactable = false;
        result.playerName = nameInput.text;
        if (!storage.TryAddEntry(result, out var entries, out string error))
        {
            statusText.text = error;
            isSaving = false;
            saveButton.interactable = true;
            return;
        }

        submitted = true;
        isSaving = false;
        nameInput.SetTextWithoutNotify(result.playerName.Trim());
        nameInput.interactable = false;
        saveButton.gameObject.SetActive(false);
        skipButton.gameObject.SetActive(false);
        playAgainButton.gameObject.SetActive(true);
        Render(entries, true);
        statusText.text = "Score saved!";
        // Leave keyboard focus cleared so an extra Enter cannot immediately restart.
        nameInput.DeactivateInputField();
        if (UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
    }

    public void Skip()
    {
        if (result == null || submitted || finished || isSaving) return;
        Finish(onSkip);
    }

    public void PlayAgain()
    {
        if (!submitted || finished || isSaving) return;
        Finish(onPlayAgain);
    }

    private void Finish(Action callback)
    {
        finished = true;
        gameObject.SetActive(false);
        callback?.Invoke();
    }

    private void Render(List<LeaderboardEntry> entries, bool loaded)
    {
        foreach (var row in rows)
        {
            row.gameObject.SetActive(false);
            Destroy(row.gameObject);
        }
        rows.Clear();
        for (int i = 0; i < entries.Count; i++)
        {
            var row = Instantiate(rowPrefab, rowContainer);
            row.Bind(i + 1, entries[i]);
            rows.Add(row);
        }
        emptyText.gameObject.SetActive(entries.Count == 0);
        emptyText.text = loaded ? "No scores yet. Be the first!" : "Leaderboard unavailable.";
        Canvas.ForceUpdateCanvases();
        scrollRect.StopMovement();
        scrollRect.verticalNormalizedPosition = 1f;
    }
}
