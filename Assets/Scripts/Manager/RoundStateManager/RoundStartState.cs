using UnityEngine;

// Called when we start a new round / frame
[System.Serializable]
public class RoundStartState : RoundBaseState
{
    public override void EnterState(RoundStateManager manager)
    {
        manager.sessionData.RecordRoundReached();

        manager.scoreCalculator.ResetFrameScore();

        manager.pinsToScore.Clear();

        UpdateTicketAmountPerRound(manager);
        
        manager.ResetHandDrawState();
        
        HandleMap(manager);

        HandleGrid(manager);

        // At a certain round number lets stop clicking the ball
        // feels ok to click the ball every time we throw it
        if (!manager.CheckKeepClickingTheBall())
        {
            //manager.TurnOnBallMesh(true);
        }

        // Boss State Check
        bool isBossRound = CheckForBossState(manager);
        
        if (isBossRound)
        {
            manager.SwitchState(manager.spawnBossObjectsState);
        }
        else
        {
            manager.SwitchState(manager.pinPlacementState);
            
        }
    }

    private bool CheckForBossState(RoundStateManager manager)
    {
        int currentRoundNumber = manager.sessionData.currentRound;
        bool isLastRound = manager.roundData.IsLastRound(currentRoundNumber);

        if (isLastRound || currentRoundNumber == 4)
        {
            return true;
        }
        else
        {
            return false;            
        }

        //bool bossPhaseHasRun = manager.spawnBossObjectsState.HasRunForRound(currentRoundNumber);
    }

    private void HandleGrid(RoundStateManager manager)
    {
        manager.gridManager?.SpawnTicketsForFrame();
        
        manager.onLaneSquareGrid.GenerateGridForRound(manager);
    }

    private void HandleMap(RoundStateManager manager)
    {
        manager.ResetLaneExploders();
    }
    
    private static void UpdateTicketAmountPerRound(RoundStateManager manager)
    {
        switch (manager.sessionData.currentRound)
        {
            case 0:
                manager.gridManager.gridTicketManager.ticketsPerFrame = 1;
                break;
            case 1:
                manager.gridManager.gridTicketManager.ticketsPerFrame = 1;
                break;
            case 2:
                manager.gridManager.gridTicketManager.ticketsPerFrame = 1;
                break;
            case 3:
                manager.gridManager.gridTicketManager.ticketsPerFrame = 2;
                break;
            case 4:
                manager.gridManager.gridTicketManager.ticketsPerFrame = 2;
                break;
            case 5:
                manager.gridManager.gridTicketManager.ticketsPerFrame = 3;
                break;
            case 6:
                manager.gridManager.gridTicketManager.ticketsPerFrame = 3;
                break;
            case 7:
                manager.gridManager.gridTicketManager.ticketsPerFrame = 3;
                break;
            case 8:
                manager.gridManager.gridTicketManager.ticketsPerFrame = 3;
                break;
        }
    }

    public override void UpdateState(RoundStateManager manager) { }
    public override void OnCollisionEnter(RoundStateManager manager, Collision collision) { }
    public override void FixedUpdateState(RoundStateManager manager) { }
    public override void OnCollisionExit(RoundStateManager manager, Collision collision) { }
    public override void OnTriggerEnter(RoundStateManager manager, Collider other) { }
    public override void OnTriggerExit(RoundStateManager manager, Collider other) { }
    public override void ExitState(RoundStateManager manager) { }
}
