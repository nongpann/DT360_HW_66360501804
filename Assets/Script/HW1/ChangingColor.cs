using UnityEngine;

public class ChangingColor : BaseState
{
    private FSM _sm;
    private Renderer objectRenderer;
    private bool changed = false;
    private float coolDown = 0;
    public ChangingColor(FSM stateMachine) : base("ChangingColor", stateMachine)
    {
        _sm = (FSM)stateMachine;
        objectRenderer = _sm.gameObject.GetComponent<Renderer>();
    }

    public override void Enter()
    {
        base.Enter();
    }

    public override void UpdateLogic()
    {
        base.UpdateLogic();
        if (_sm.numcoin.numberCoin == 0 && changed)
        {
            stateMachine.ChangeState(((FSM)stateMachine).returningState);
        }
        if (_sm.numcoin.numberCoin > 0 && changed)
        {
            stateMachine.ChangeState(((FSM)stateMachine).movingState);
        }
    }

    public override void UpdateAction()
    {
        base.UpdateAction();
        float r = Random.Range(0.0f, 1.0f);
        float g = Random.Range(0.0f, 1.0f);
        float b = Random.Range(0.0f, 1.0f);
        objectRenderer.material.color = new Color(r,g,b,1);

        coolDown += Time.deltaTime;
        if (coolDown > 0.25)
        {
            changed = true;
            coolDown = 0.0f;
        }

        else
        {
            changed = false;
        }
    }
}
