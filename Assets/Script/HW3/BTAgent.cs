using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
public class BTAgent : MonoBehaviour
{
    public BehaviourTree tree;
    public NavMeshAgent agent;
    public WaitForSeconds waitTime;

    public enum ActionState
    {
        IDLE,
        WORKING
    };

    ActionState state = ActionState.IDLE;
    Node.Status treeStatus = Node.Status.RUNNING;

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    protected virtual void Start()
    {
        tree = new BehaviourTree();
        BuildTree();
        tree.PrintTree();
    }

    protected virtual void BuildTree() { }
    private IEnumerator TickTree()
    {
        waitTime = new WaitForSeconds(Random.Range(0.1f, 0.5f));
        while (true)
        {
            treeStatus = tree.Process();
            yield return waitTime;
        }
    }
    protected Node.Status GoToLocation(Vector3 destination)
    {
        float distanceToTarget = Vector3.Distance(destination, transform.position);

        if (state == ActionState.IDLE)
        {
            agent.SetDestination(destination);
            state = ActionState.WORKING;
        }
        else if (Vector3.Distance(agent.pathEndPosition, destination) >= 2.0f)
        {
            state = ActionState.IDLE;
            return Node.Status.FAILURE;
        }
        else if (distanceToTarget < 2.0f)
        {
            state = ActionState.IDLE;
            return Node.Status.SUCCESS;
        }

        return Node.Status.RUNNING;
    }
}
