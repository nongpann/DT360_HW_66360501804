using TMPro;
using UnityEditor;
using UnityEngine;

public class Moving : BaseState
{
    private FSM _sm;
    private Transform coin;
    Vector3 coinPos;
    public Moving(FSM stateMachine) : base("Moving", stateMachine) 
    {
	    _sm = (FSM)stateMachine;
    }

    public override void Enter()
    {
        base.Enter();
        coin = _sm.coinsPos.GetChild(0);
        coinPos = coin.position;
    }

    public override void UpdateLogic()
    {
        base.UpdateLogic();
        if (_sm.transform.position == coinPos)
        {
            stateMachine.ChangeState(((FSM)stateMachine).changingColorState);
        }
    }

    public override void UpdateAction()
    {
        base.UpdateAction();
        _sm.transform.position = Vector3.MoveTowards(_sm.transform.position, coinPos, _sm.speed * Time.deltaTime);
        if (_sm.transform.position == coinPos)
        {
            _sm.destroy(coin.gameObject);
            _sm.numcoin.numberCoin -= 1;
        }
    }

}
