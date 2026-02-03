using System.Collections;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;

public class FSM : StateMachine
{
    public float speed = 4f;
    public SpawnCoin numcoin;
    public Transform coinsPos;
    public Transform center;

    [HideInInspector]
    public Idle idleState;
    [HideInInspector]
    public Moving movingState;
    [HideInInspector]
    public Returning returningState;
    [HideInInspector]
    public ChangingColor changingColorState;


    private void Awake()
    {
        idleState = new Idle(this);
        movingState = new Moving(this);
        returningState = new Returning(this);
        changingColorState = new ChangingColor(this);
    }

    protected override BaseState GetInitialState()
    {
        return idleState;
    }

    public void destroy(GameObject coin)
    {
        Destroy(coin);
    }
}
