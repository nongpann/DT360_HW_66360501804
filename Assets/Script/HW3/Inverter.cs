using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Inverter : Node {

    public Inverter(string n) {

        name = n;
    }

    public override Status Process() {

        Status childStatus = children[0].Process();

        if (childStatus == Status.RUNNING) return Status.RUNNING;

        if (childStatus == Status.SUCCESS) 
            return Status.FAILURE;
        else 
            return Status.SUCCESS;
    }
}