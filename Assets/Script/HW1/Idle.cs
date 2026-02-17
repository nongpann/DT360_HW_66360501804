using UnityEngine;

public class Idle : BaseState
{
    private float _horizontalInput;
    private FSM _sm;

    public Idle (FSM stateMachine) : base("Idle", stateMachine) 
    {
        _sm = (FSM)stateMachine;
    }

    public override void Enter()
    {
        base.Enter();
        _sm.numcoin.numberCoin = 0;
    }

    public override void UpdateLogic()
    {
        base.UpdateLogic();
        if (_sm.numcoin.numberCoin == 5)
        {
            stateMachine.ChangeState(((FSM)stateMachine).movingState);
        }
    }

}
