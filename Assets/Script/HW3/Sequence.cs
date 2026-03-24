using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Sequence : Node {

    public Sequence(string n) {

        name = n;
    }

    public override Status Process() {

        Status childStatus = children[currentChild].Process();
        if (childStatus == Status.RUNNING) return Status.RUNNING;
        if (childStatus == Status.FAILURE)
        {
            foreach (Node node in children)
            {
                node.Reset();
            }
            return childStatus;
        }

        currentChild++;
        if (currentChild >= children.Count) {

            currentChild = 0;
            foreach (Node node in children)
            {
                node.Reset();
            }
            return Status.SUCCESS;
        }
        return Status.RUNNING;
    }
}