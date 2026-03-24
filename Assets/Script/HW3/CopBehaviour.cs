using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class CopBehaviour : BTAgent
{
    public GameObject backdoor;
    public GameObject frontdoor;
    public GameObject painting;
    public GameObject[] paintings;

    private Leaf goToBackdoor;
    private Leaf goToFrontdoor;

    void Start()
    {

        // Speed up game play.
        Time.timeScale = 5.0f;
        base.Start();

        Sequence stroll = new Sequence("stroll around painting");

        goToBackdoor = new Leaf("Go To Backdoor", GoToBackdoor, 2);
        goToFrontdoor = new Leaf("Go To Frontdoor", GoToFrontdoor, 1);

        PrioritySelector opendoor = new PrioritySelector("Open Door");
        opendoor.AddChild(goToBackdoor);
        opendoor.AddChild(goToFrontdoor);

        // NEW: 2026-02-25c array of paintings 
        RandomSelector walk = new RandomSelector("Walk to some painting");
        for (int i = 0; i < paintings.Length; i++)
        {
            Leaf goToPainting = new Leaf("Go To Painting #" + i, i, GoToPaintings, i + 1);
            walk.AddChild(goToPainting);
        }
        stroll.AddChild(walk);
        tree.AddChild(stroll);

        tree.PrintTree();

        // NEW: 2026-02-25a
        // Start CoRoutine
        StartCoroutine("TickTree");
    }

    public Node.Status GoToPainting()
    {
        if (!painting.activeSelf) return Node.Status.FAILURE;

        Node.Status s = GoToLocation(painting.transform.position);
        if (s == Node.Status.SUCCESS)
        {

        }
        return s;
    }

    // NEW: 2026-02-25c array of paintings 
    public Node.Status GoToPaintings(int id)
    {
        if (!paintings[id].activeSelf) return Node.Status.FAILURE;

        Node.Status s = GoToLocation(paintings[id].transform.position);
        return s;
    }

    public Node.Status GoToBackdoor()
    {
        Node.Status s = GoToDoor(backdoor);

        if (s == Node.Status.SUCCESS)
        {
            // higher its priority
            goToBackdoor.sortValue = 1;
        }
        else
        {
            // lower its priority
            goToBackdoor.sortValue = 10;
        }

        return s;
    }

    public Node.Status GoToFrontdoor()
    {
        Node.Status s = GoToDoor(frontdoor);

        if (s == Node.Status.SUCCESS)
        {
            // higher its priority
            goToFrontdoor.sortValue = 1;
        }
        else
        {
            // lower its priority
            goToFrontdoor.sortValue = 10;
        }

        return s;
    }

    public Node.Status GoToDoor(GameObject door)
    {

        Node.Status s = GoToLocation(door.transform.position);
        if (s == Node.Status.SUCCESS)
        {

            if (!door.GetComponent<Lock>().isLocked)
            {
                door.GetComponent<NavMeshObstacle>().enabled = false;
                // door.SetActive(false);
                return Node.Status.SUCCESS;
            }

            return Node.Status.FAILURE;
        }
        return s;
    }
    void Update()
    {
        /*
        if (treeStatus != Node.Status.SUCCESS)
            treeStatus = tree.Process();
        */
    }
}
