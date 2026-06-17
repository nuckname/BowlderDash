using UnityEngine;
using System.Collections;

[System.Serializable]
public class ScoringState : RoundBaseState
{
    // A multiplier value previously stored in the manager, moved here where it's used
    private float strikeTicketMultiplier = 2f; 

    public override void EnterState(RoundStateManager manager)
    {
        Debug.Log("Current State: Calculating Score");
        manager.UpdateInstructionText("Waiting for pins to settle...");
        
        // Pass the coroutine to the manager to execute
        manager.StartCoroutine(ScoringSequence(manager));
    }

    private IEnumerator ScoringSequence(RoundStateManager manager)
    {
        // Have an array of every pin that has fallen over.
        // then we cycle though each pin giving them one second until we run out of pins.
        yield return new WaitForSeconds(7f);

        manager.UpdateInstructionText("Tallying Score...");

        int totalPinsActiveAtStart = 0;
        int fallenPinsThisThrow = 0;

        // can get this from inventory
        // can find all Game objects with a tag.
        Pin[] allPins = Object.FindObjectsOfType<Pin>(true);

        foreach (Pin p in allPins)
        {
            if (p.gameObject.scene.isLoaded && p.gameObject.activeInHierarchy && CountsTowardFrameClear(p))
            {
                Debug.LogWarning("how to check for if pin has fallen over");
                totalPinsActiveAtStart++;
                //if (p.IsFallen()) fallenPinsThisThrow++;
            }
        }

        AwardVaseIntactBonuses(manager.scoreCalculator);
        ResetVaseThrowState();

        // Wait for the external score calculator UI sequence
        if (manager.scoreCalculator != null)
        {
            yield return manager.StartCoroutine(manager.scoreCalculator.CalculateScoringSequence());
        }

        // Process Strikes & Final Score
        bool isStrike = (manager.gameState.throwsThisFrame == 1 && fallenPinsThisThrow == totalPinsActiveAtStart && totalPinsActiveAtStart > 0);

        if (isStrike && manager.scoreCalculator != null)
        {
            manager.scoreCalculator.ApplyStrikeBonus();
        }

        int frameScore = manager.scoreCalculator != null ? manager.scoreCalculator.GetFinalScore() : 0;
        int totalScoreDelta = frameScore - manager.gameState.scoreAppliedToTotalThisFrame;
        
        manager.gameState.totalScore += totalScoreDelta;
        manager.gameState.scoreAppliedToTotalThisFrame = frameScore;
        
        Debug.Log($"Tallying Frame! Added {totalScoreDelta} points to total. Frame Total: {frameScore}. Total: {manager.gameState.totalScore}");
        
        //if (manager.uiController != null) manager.uiController.UpdateStandardUI(manager);

        // Determine Next Phase (End Frame or Throw Again)
        if (manager.gameState.throwsThisFrame >= manager.gameState.maxThrowsPerFrame || isStrike)
        {
            bool clearedAllPlacedPins = manager.grid != null && manager.grid.AreAllPlacedPinsCleared();
            bool passedScoreTarget = frameScore >= manager.gameState.currentFrameScoreTarget;
            
            FinalizeFrameTickets(manager, clearedAllPlacedPins, isStrike, frameScore, passedScoreTarget);

            if (!passedScoreTarget)
            {
                // TODO: You may want to create a dedicated GameOverState later
                Debug.Log($"Game Over! Missed frame target. Scored {frameScore} / {manager.gameState.currentFrameScoreTarget}");
                manager.UpdateInstructionText($"Game Over! Missed the target: {frameScore} / {manager.gameState.currentFrameScoreTarget}");
            }
            else if (manager.gameState.currentFrame >= manager.gameState.maxFrames)
            {
                Debug.Log($"Game Over! Final Score: {manager.gameState.totalScore}");
                manager.UpdateInstructionText($"Game Over! Final Score: {manager.gameState.totalScore}");
            }
            else
            {
                manager.SwitchState(manager.Shop);
            }
        }
        else
        {
            // Set up for the next throw
            ClearFallenPins(allPins);
            manager.SwitchState(manager.ThrowingBall); // Assumes PlayerReady/Drawing are handled by ThrowingBall
        }
    }


    private bool CountsTowardFrameClear(Pin pin)
    {
        // Assuming MultPin, TicketPin, and vasePin components still exist
        return pin != null
            && pin.GetComponent<MultPin>() == null
            && pin.GetComponent<TicketPin>() == null
            && pin.GetComponent<VaseEffect>() == null;
    }

    private void AwardVaseIntactBonuses(ScoreCalculator calculator)
    {
        if (calculator == null) return;

        /*
        //VaseEffect[] vasePins = Object.FindObjectsOfType<VaseEffect>(true);
        foreach (var vase in vasePins)
        {
            if (vase != null && vase.gameObject.scene.isLoaded && vase.gameObject.activeInHierarchy)
            {
                vase.TryAwardIntactBonus(calculator);
            }
        }
        */
    }

    private void ResetVaseThrowState()
    {
        // some sort of vase effect
        /*
        VaseEffect[] vasePins = Object.FindObjectsOfType<VaseEffect>(true);
        foreach (var vase in vasePins)
        {
            if (vase != null) vase.ResetThrowState();
        }
        */
    }

    private void FinalizeFrameTickets(RoundStateManager manager, bool clearedAllPlacedPins, bool isStrike, int frameScore, bool passedScoreTarget)
    {
        int targetTickets = 0;
        if (passedScoreTarget && manager.gameState.currentFrameScoreTarget > 0)
        {
            targetTickets = Mathf.Max(1, frameScore / manager.gameState.currentFrameScoreTarget);
        }

        manager.ticketEconomy.lastFrameUsedStrikeTicketBonus = passedScoreTarget && isStrike && targetTickets > 0;
        if (manager.ticketEconomy.lastFrameUsedStrikeTicketBonus)
        {
            targetTickets = Mathf.Max(1, Mathf.RoundToInt(targetTickets * Mathf.Max(1f, strikeTicketMultiplier)));
        }

        manager.ticketEconomy.scoreTargetTicketsEarnedThisFrame = targetTickets;

        int gridTickets = Mathf.Max(0, manager.ticketEconomy.gridTicketsEarnedThisFrame);
        manager.ticketEconomy.lastFrameUsedTicketBonus = clearedAllPlacedPins && gridTickets > 0;
        if (manager.ticketEconomy.lastFrameUsedTicketBonus)
        {
            gridTickets *= 2;
        }

        manager.ticketEconomy.ticketsEarnedLastFrame = gridTickets + manager.ticketEconomy.scoreTargetTicketsEarnedThisFrame;
        manager.ticketEconomy.currentTickets += manager.ticketEconomy.ticketsEarnedLastFrame;

       // if (manager.uiController != null) manager.uiController.UpdateStandardUI(manager);
        if (manager.shopController != null) manager.shopController.RefreshShop();

        // Update Text
        string bonusText = "";
        if (manager.ticketEconomy.lastFrameUsedTicketBonus) bonusText += " Clear bonus x2 applied!";
        if (manager.ticketEconomy.lastFrameUsedStrikeTicketBonus) bonusText += " Strike ticket bonus x2 applied!";

        string passText = passedScoreTarget
            ? $"Target cleared: {frameScore} / {manager.gameState.currentFrameScoreTarget}."
            : $"Target missed: {frameScore} / {manager.gameState.currentFrameScoreTarget}.";
            
        manager.UpdateInstructionText($"Frame complete. {passText} Earned {manager.ticketEconomy.ticketsEarnedLastFrame} tickets.{bonusText}");
    }

    // Refactor to somewhere else
    private void ClearFallenPins(Pin[] allPins)
    {
        foreach(var p in allPins)
        {
            Debug.unityLogger.Log("Disable pins");
            /*
            if (p.gameObject.scene.isLoaded && p.IsFallen()) 
            {
                p.gameObject.SetActive(false);
            }
            */
        }
    }

    public override void UpdateState(RoundStateManager manager) { }
    public override void FixedUpdateState(RoundStateManager manager) { }
    public override void OnCollisionEnter(RoundStateManager manager, Collision collision) { }
    public override void OnCollisionExit(RoundStateManager manager, Collision collision) { }
    public override void OnTriggerEnter(RoundStateManager manager, Collider other) { }
    public override void OnTriggerExit(RoundStateManager manager, Collider other) { }
    public override void ExitState(RoundStateManager manager) { }
}