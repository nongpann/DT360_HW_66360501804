using UnityEngine;

public class Returning : BaseState
{
    private FSM _sm;
    public Returning(FSM stateMachine) : base("Returning", stateMachine)
    {
        _sm = (FSM)stateMachine;
    }

    public override void Enter()
    {
        base.Enter();
    }

    public override void UpdateLogic()
    {
        base.UpdateLogic();
        if (_sm.transform.position == _sm.center.position)
        {
            stateMachine.ChangeState(((FSM)stateMachine).idleState);
        }
    }

    public override void UpdateAction()
    {
        base.UpdateAction();
        _sm.transform.position = Vector3.MoveTowards(_sm.transform.position, _sm.center.position, _sm.speed * Time.deltaTime);
    }
}
