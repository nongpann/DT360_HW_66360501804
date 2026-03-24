using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PrioritySelector : Node {
    Node[] nodeArray;
    bool sorted = false;

    public PrioritySelector(string n) {

        name = n;
    }

    void OrderChildrenNodes()
    {
        children.Sort((x, y) => x.sortValue.CompareTo(y.sortValue));
        
        /*
        nodeArray = children.ToArray();
        Sort(nodeArray, 0, children.Count-1);
        children = new List<Node>(nodeArray);
        */
    }

    public override Status Process() {
        if (!sorted)
        {
            OrderChildrenNodes();
            sorted = true;
        }

        Status childStatus = children[currentChild].Process();

        if (childStatus == Status.RUNNING) return Status.RUNNING;

        if (childStatus == Status.SUCCESS) {

            currentChild = 0;
            foreach (Node node in children)
            {
                node.Reset();
            }
            sorted = false;
            return Status.SUCCESS;
        }

        currentChild++;

        if (currentChild >= children.Count) {

            currentChild = 0;
            foreach (Node node in children)
            {
                node.Reset();
            }
            sorted = false;
            return Status.FAILURE;
        }

        return Status.RUNNING;
    }
}