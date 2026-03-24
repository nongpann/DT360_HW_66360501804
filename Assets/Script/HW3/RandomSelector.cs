using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RandomSelector : Node
{
    bool shuffled = false;
    int reshuffleInterval = 1;
    int completionCount = 0;
    public RandomSelector(string n)
    {
        name = n;
    }
    public override Status Process()
    {
        if (!shuffled)
        {
            for (int i = children.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                Node tempNode = children[i];
                children[i] = children[j];
                children[j] = tempNode;
            }
            shuffled = true;
        }

        Status childStatus = children[currentChild].Process();

        if (childStatus == Status.RUNNING)
        {
            return Status.RUNNING;
        }

        if (childStatus == Status.SUCCESS)
        {
            currentChild = 0;
            foreach (Node node in children)
                node.Reset();

            completionCount++;
            if (reshuffleInterval > 0 && completionCount >= reshuffleInterval)
            {
                completionCount = 0;
                shuffled = false;
            }

            return Status.SUCCESS;
        }

        currentChild++;

        if (currentChild >= children.Count)
        {
            currentChild = 0;
            foreach (Node node in children)
                node.Reset();

            completionCount++;
            if (reshuffleInterval > 0 && completionCount >= reshuffleInterval)
            {
                completionCount = 0;
                shuffled = false;
            }

            return Status.FAILURE;
        }

        return Status.RUNNING;
    }
}
