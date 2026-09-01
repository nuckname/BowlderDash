using UnityEngine;
using System.Collections;

public enum TransitionPhase
{
    InitializeUI,
    ShowBallsThrown,
    ShowScoreUpdate,
    ShowTicketsEarned,
    FinishTransition
}

public class EndOfRoundSequence
{
    // Moved camera settings here
    private float waitBeforeUiCamera = 2f;
    private float scoreDisplayHoldTime = 1.5f;

    public IEnumerator Execute(RoundStateManager manager, RoundBaseState nextState, bool prepareNextRound)
    {
        TransitionPhase currentPhase = TransitionPhase.InitializeUI;

        while (currentPhase != TransitionPhase.FinishTransition)
        {
            switch (currentPhase)
            {
                case TransitionPhase.InitializeUI:
                    // lets just update this before so the player doesnt see it
                    manager.inGameScoreUi.UpdateRoundText(manager.sessionData.currentRound, manager.roundData.GetMaxRoundNumber());
                    currentPhase = TransitionPhase.ShowBallsThrown;
                    break;

                case TransitionPhase.ShowBallsThrown:
                    manager.MoveCamera(CameraLocation.BallsThrowUi);
                    yield return new WaitForSeconds(waitBeforeUiCamera);
                    manager.AddBallThrownToSession();
                    yield return new WaitForSeconds(waitBeforeUiCamera);
                    currentPhase = TransitionPhase.ShowScoreUpdate;
                    break;

                case TransitionPhase.ShowScoreUpdate:
                    manager.MoveCamera(CameraLocation.ScoreToBeatUi);
                    yield return new WaitForSeconds(waitBeforeUiCamera);
                    
                    // we want to only incremental the UI here and only here.
                    Coroutine scoreCountUpRoutine = manager.inGameScoreUi.UpdateScoreText(manager.sessionData.currentScore);
                    if (scoreCountUpRoutine != null) yield return scoreCountUpRoutine;
                    
                    yield return new WaitForSeconds(scoreDisplayHoldTime);
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