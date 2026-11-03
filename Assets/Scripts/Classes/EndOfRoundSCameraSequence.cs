using UnityEngine;
using System.Collections;
using Unity.VisualScripting;

public enum TransitionPhase
{
    InitializeUI,
    ShowBallsThrown,
    ShowIncreaseRound,
    ShowScoreUpdate,
    ShowTicketsEarned,
    FinishTransition
}
public class EndOfRoundSCameraSequence
{
    // Moved camera settings here
    private float waitBeforeUiCamera = 2f;
    private float scoreDisplayHoldTime = 1.5f;

    private IEnumerator SkippableWait(float waitTime, System.Func<bool> isCurrent)
    {
        float timer = 0f;
        
        // Wait one frame to avoid consuming the same click that skipped the text animation
        yield return null; 

        while (timer < waitTime && isCurrent())
        {
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                yield break;
            }
            timer += Time.deltaTime;
            yield return null;
        }
    }

    public IEnumerator Execute(RoundStateManager manager, RoundBaseState nextState, bool prepareNextRound, bool wonRound = false, bool lostGame = false, RoundScore roundScore = null)
    {
        if (manager == null || manager.inGameScoreUi == null) yield break;
        InGameScoreUi scoreUi = manager.inGameScoreUi;
        RoundBaseState ownerState = manager.currentState;
        ulong runVersion = manager.sessionData.RunVersion;
        uint presentationVersion = scoreUi.CountPresentationVersion;
        // An interrupted camera sequence must never start the next count in a newer presentation.
        System.Func<bool> isCurrent = () => manager != null && manager.isActiveAndEnabled &&
            manager.currentState == ownerState && manager.sessionData.RunVersion == runVersion &&
            manager.inGameScoreUi == scoreUi && scoreUi != null && scoreUi.isActiveAndEnabled &&
            scoreUi.CountPresentationVersion == presentationVersion;
        if (!isCurrent()) yield break;

        TransitionPhase currentPhase = TransitionPhase.InitializeUI;

        while (currentPhase != TransitionPhase.FinishTransition && isCurrent())
        {
            switch (currentPhase)
            {
                case TransitionPhase.InitializeUI:
                    // lets just update this before so the player doesnt see it
                    manager.inGameScoreUi.UpdateRoundText(manager.sessionData.currentRound, manager.roundData.GetMaxRoundNumber());
                    currentPhase = TransitionPhase.ShowScoreUpdate;
                    break;
                
                case TransitionPhase.ShowScoreUpdate:
                    manager.inGameScoreUi.StopAllFlashing();
                    
                    manager.MoveCamera(CameraLocation.ScoreToBeatUi);
                    yield return SkippableWait(waitBeforeUiCamera, isCurrent);
                    if (!isCurrent()) yield break;

                    // we want to only incremental the UI here and only here.
                    Coroutine scoreCountUpRoutine = manager.inGameScoreUi.UpdateScoreText(manager.sessionData.currentScore);
                    presentationVersion = scoreUi.CountPresentationVersion;
                    if (scoreCountUpRoutine != null) yield return scoreCountUpRoutine;
                    if (!isCurrent()) yield break;

                    if (wonRound)
                    {
                        manager.inGameScoreUi.TriggerWinRoundText();
                        yield return SkippableWait(2, isCurrent);
                        if (!isCurrent()) yield break;
                        // Final victory keeps the completed round and score for the end screen.
                        currentPhase = TransitionPhase.ShowTicketsEarned;
                        break;
                    }
                    else if (lostGame)
                    {
                        manager.inGameScoreUi.TriggerGameLostText();

                        // Wait a bit longer to let the player see the game lost text
                        // and then break so we dont show tickets earned
                        yield return SkippableWait(scoreDisplayHoldTime + 3, isCurrent);
                        if (!isCurrent()) yield break;
                        currentPhase = TransitionPhase.FinishTransition;
                        break;
                    }
                    
                    // If we didnt meet quota
                    yield return SkippableWait(scoreDisplayHoldTime, isCurrent);
                    if (!isCurrent()) yield break;
                    currentPhase = TransitionPhase.ShowTicketsEarned;
                    break;
                

                // We pick one of these TransitionPhase.ShowBallsThrown or TransitionPhase.ShowIncreaseRound
                /*
                case TransitionPhase.ShowBallsThrown:
                    manager.inGameScoreUi.StopAllFlashing();
                    
                    manager.MoveCamera(CameraLocation.BallsThrowUi);
                    yield return SkippableWait(waitBeforeUiCamera);
                    
                    manager.AddBallThrownToSession();
                    
                    if (manager.sessionData.ballsThrown == manager.GetMaxBallsForCurrentRound() - 1)
                    {
                        yield return SkippableWait(waitBeforeUiCamera);
                        manager.inGameScoreUi.TriggerLastThrowText();
                        yield return SkippableWait(waitBeforeUiCamera + 1);
                        manager.inGameScoreUi.StopAllFlashing();
                        
                        // Update the text back to OG.
                        manager.inGameScoreUi.UpdateBallsText(manager.sessionData.ballsThrown, 
                            roundScore.maxBallsAllowedToThrow);
                    }
                    
                    
                    yield return SkippableWait(waitBeforeUiCamera);
                    currentPhase = TransitionPhase.ShowTicketsEarned;
                    break;
                */
                // We pick one of these TransitionPhase.ShowBallsThrown or TransitionPhase.ShowIncreaseRound
                case TransitionPhase.ShowIncreaseRound:
                    manager.inGameScoreUi.StopAllFlashing();
                    
                    manager.MoveCamera(CameraLocation.ShowIncreaseRound);
                    yield return SkippableWait(waitBeforeUiCamera, isCurrent);
                    if (!isCurrent()) yield break;
                 
                    manager.sessionData.AddRound();
                    manager.inGameScoreUi.UpdateRoundText(manager.sessionData.currentRound, manager.roundData.GetMaxRoundNumber());
                    yield return SkippableWait(waitBeforeUiCamera, isCurrent);
                    if (!isCurrent()) yield break;
                    currentPhase = TransitionPhase.FinishTransition;
                    break;
                
                case TransitionPhase.ShowTicketsEarned:
                    manager.inGameScoreUi.StopAllFlashing();
                    
                    // Move to the Ticket Camera and animate tickets
                    manager.MoveCamera(CameraLocation.TicketCameraUi);
                    yield return SkippableWait(waitBeforeUiCamera, isCurrent);
                    if (!isCurrent()) yield break;

                    Coroutine ticketCountUpRoutine = manager.inGameScoreUi.UpdateTicketTextAnimated(manager.sessionData.tickets);
                    presentationVersion = scoreUi.CountPresentationVersion;
                    if (ticketCountUpRoutine != null) yield return ticketCountUpRoutine;
                    if (!isCurrent()) yield break;

                    yield return SkippableWait(scoreDisplayHoldTime, isCurrent);

                    if (!isCurrent()) yield break;
                    if (wonRound)
                    {
                        currentPhase = TransitionPhase.ShowIncreaseRound;
                    }
                    else
                    {
                        currentPhase = TransitionPhase.FinishTransition;
                    }
                    break;
            }
        }

        if (!isCurrent()) yield break;

        if (prepareNextRound)
        {
            manager.ResetUiForNewRound();
        }

        manager.SwitchState(nextState);
    }
}
