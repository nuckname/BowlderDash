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

    private IEnumerator SkippableWait(float waitTime)
    {
        float timer = 0f;
        
        // Wait one frame to avoid consuming the same click that skipped the text animation
        yield return null; 

        while (timer < waitTime)
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
        TransitionPhase currentPhase = TransitionPhase.InitializeUI;

        while (currentPhase != TransitionPhase.FinishTransition)
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
                    yield return SkippableWait(waitBeforeUiCamera);

                    // we want to only incremental the UI here and only here.
                    Coroutine scoreCountUpRoutine = manager.inGameScoreUi.UpdateScoreText(manager.sessionData.currentScore);
                    if (scoreCountUpRoutine != null) yield return scoreCountUpRoutine;

                    if (wonRound)
                    {
                        manager.inGameScoreUi.TriggerWinRoundText();
                        yield return SkippableWait(2);
                        // Final victory keeps the completed round and score for the end screen.
                        currentPhase = prepareNextRound
                            ? TransitionPhase.ShowIncreaseRound
                            : TransitionPhase.ShowTicketsEarned;
                        break;
                    }
                    else if (lostGame)
                    {
                        manager.inGameScoreUi.TriggerGameLostText();

                        // Wait a bit longer to let the player see the game lost text
                        // and then break so we dont show tickets earned
                        yield return SkippableWait(scoreDisplayHoldTime + 3);
                        currentPhase = TransitionPhase.FinishTransition;
                        break;
                    }
                    
                    // If we didnt meet quota
                    yield return SkippableWait(scoreDisplayHoldTime);
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
                    
                    if (manager.sessionData.ballsThrown != 0)
                    {
                        yield return SkippableWait(waitBeforeUiCamera);
                        manager.inGameScoreUi.ResetBallsTextAnimated(SessionData.Instance.ballsThrown, manager.GetMaxBallsForCurrentRound());
                    }
 
                    yield return SkippableWait(waitBeforeUiCamera);
                 
                    manager.sessionData.AddRound();
                    manager.inGameScoreUi.UpdateRoundText(manager.sessionData.currentRound, manager.roundData.GetMaxRoundNumber());
                    yield return SkippableWait(waitBeforeUiCamera);
                    currentPhase = TransitionPhase.ShowTicketsEarned;
                    break;

                case TransitionPhase.ShowTicketsEarned:
                    manager.inGameScoreUi.StopAllFlashing();
                    
                    // Move to the Ticket Camera and animate tickets
                    manager.MoveCamera(CameraLocation.TicketCameraUi);
                    yield return SkippableWait(waitBeforeUiCamera);

                    Coroutine ticketCountUpRoutine = manager.inGameScoreUi.UpdateTicketTextAnimated(manager.sessionData.tickets);
                    if (ticketCountUpRoutine != null) yield return ticketCountUpRoutine;

                    yield return SkippableWait(scoreDisplayHoldTime);
                    currentPhase = TransitionPhase.FinishTransition;
                    break;
            }
        }

        if (prepareNextRound)
        {
            manager.ResetUiForNewRound();
        }

        manager.SwitchState(nextState);
    }
}
