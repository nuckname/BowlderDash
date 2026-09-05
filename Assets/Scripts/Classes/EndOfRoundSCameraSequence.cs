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
                    currentPhase = TransitionPhase.ShowBallsThrown;
                    break;

                case TransitionPhase.ShowBallsThrown:
                    manager.MoveCamera(CameraLocation.BallsThrowUi);
                    yield return WaitOrSkip(waitBeforeUiCamera);
                    manager.AddBallThrownToSession();
                    yield return WaitOrSkip(waitBeforeUiCamera);
                    currentPhase = TransitionPhase.ShowScoreUpdate;
                    break;

                case TransitionPhase.ShowScoreUpdate:
                    manager.MoveCamera(CameraLocation.ScoreToBeatUi);
                    yield return WaitOrSkip(waitBeforeUiCamera);
                    
                    // we want to only incremental the UI here and only here.
                    Coroutine scoreCountUpRoutine = manager.inGameScoreUi.UpdateScoreText(manager.sessionData.currentScore);
                    if (scoreCountUpRoutine != null) yield return scoreCountUpRoutine;
                    
                    if (wonRound)
                    {
                        manager.inGameScoreUi.TriggerWinRoundText();
                    }
                    else if (lostGame)
                    {
                        manager.inGameScoreUi.TriggerGameLostText();
                    }
                    
                    yield return WaitOrSkip(scoreDisplayHoldTime);
                    currentPhase = TransitionPhase.ShowTicketsEarned;
                    break;

                case TransitionPhase.ShowTicketsEarned:
                    // Move to the Ticket Camera and animate tickets
                    manager.MoveCamera(CameraLocation.TicketCameraUi);
                    yield return WaitOrSkip(waitBeforeUiCamera);

                    Coroutine ticketCountUpRoutine = manager.inGameScoreUi.UpdateTicketTextAnimated(manager.sessionData.tickets);
                    if (ticketCountUpRoutine != null) yield return ticketCountUpRoutine;

                    yield return WaitOrSkip(scoreDisplayHoldTime);
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

    private IEnumerator WaitOrSkip(float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                break;
            }
            
            timer += Time.deltaTime;
            yield return null;
        }
    }
}