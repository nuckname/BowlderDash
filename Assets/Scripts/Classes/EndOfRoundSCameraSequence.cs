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

    public IEnumerator Execute(RoundStateManager manager, RoundBaseState nextState, bool prepareNextRound, bool wonRound = false, bool lostGame = false)
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
                    manager.MoveCamera(CameraLocation.ScoreToBeatUi);
                    yield return new WaitForSeconds(waitBeforeUiCamera);

                    // we want to only incremental the UI here and only here.
                    Coroutine scoreCountUpRoutine = manager.inGameScoreUi.UpdateScoreText(manager.sessionData.currentScore);
                    if (scoreCountUpRoutine != null) yield return scoreCountUpRoutine;

                    if (wonRound)
                    {
                        manager.inGameScoreUi.TriggerWinRoundText();
                        yield return new WaitForSeconds(2);
                        currentPhase = TransitionPhase.ShowIncreaseRound;
                        break;
                    }
                    else if (lostGame)
                    {
                        manager.inGameScoreUi.TriggerGameLostText();

                        // Wait a bit longer to let the player see the game lost text
                        // and then break so we dont show tickets earned
                        yield return new WaitForSeconds(scoreDisplayHoldTime + 3);
                        currentPhase = TransitionPhase.FinishTransition;
                        break;
                    }
                    
                    // If we didnt meet quota
                    yield return new WaitForSeconds(scoreDisplayHoldTime);
                    currentPhase = TransitionPhase.ShowBallsThrown;
                    break;
                

                // We pick one of these TransitionPhase.ShowBallsThrown or TransitionPhase.ShowIncreaseRound
                case TransitionPhase.ShowBallsThrown:
                    manager.MoveCamera(CameraLocation.BallsThrowUi);
                    yield return new WaitForSeconds(waitBeforeUiCamera);
                    manager.AddBallThrownToSession();
                    yield return new WaitForSeconds(waitBeforeUiCamera);
                    currentPhase = TransitionPhase.ShowTicketsEarned;
                    break;
                
                // We pick one of these TransitionPhase.ShowBallsThrown or TransitionPhase.ShowIncreaseRound
                case TransitionPhase.ShowIncreaseRound:
                    manager.MoveCamera(CameraLocation.ShowIncreaseRound);
                    
                    if (manager.sessionData.ballsThrown != 0)
                    {
                        yield return new WaitForSeconds(waitBeforeUiCamera);
                        manager.inGameScoreUi.ResetBallsTextAnimated(SessionData.Instance.ballsThrown, manager.GetMaxBallsForCurrentRound());
                    }
 
                    yield return new WaitForSeconds(waitBeforeUiCamera);
                 
                    manager.sessionData.AddRound();
                    manager.inGameScoreUi.UpdateRoundText(manager.sessionData.currentRound, manager.roundData.GetMaxRoundNumber());
                    yield return new WaitForSeconds(waitBeforeUiCamera);
                    currentPhase = TransitionPhase.ShowTicketsEarned;
                    break;

                case TransitionPhase.ShowTicketsEarned:
                    // Move to the Ticket Camera and animate tickets
                    manager.MoveCamera(CameraLocation.TicketCameraUi);
                    yield return new WaitForSeconds(waitBeforeUiCamera);

                    Coroutine ticketCountUpRoutine = manager.inGameScoreUi.UpdateTicketTextAnimated(manager.sessionData.tickets);
                    if (ticketCountUpRoutine != null) yield return ticketCountUpRoutine;

                    yield return new WaitForSeconds(scoreDisplayHoldTime);
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