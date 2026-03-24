using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class RobberBehaviour : BTAgent {

    public GameObject diamond;
    public GameObject van;
    public GameObject backdoor;
    public GameObject frontdoor;
    public GameObject painting;
    public GameObject [] paintings;     // NEW: 2026-02-25c array of paintings (including diamond)
    public Blackboard blackboard;

    private GameObject pickupItem;
    private Leaf goToDiamond;
    private Leaf goToPainting;

    // NEW: 2026-02-25b [dynamically change priority of leaf node]
    private Leaf goToBackdoor;
    private Leaf goToFrontdoor;

    [Range(0, 1000)]
    public int money = 800;

    void Start() {

        // Speed up game play.
        Time.timeScale = 5.0f;
        base.Start();

        Sequence steal = new Sequence("Steal Something");

        Leaf isNightTime = new Leaf("Is Night Time", IsNight);
        steal.AddChild(isNightTime);

        // TODO-2: write a code for robber to steal either diamond OR a painting

        // TODO-1: create Inverter node to reverse the logic of "hasGotMoney" leaf.
        Leaf hasGotMoney = new Leaf("Has Money", HasMoney);
        Inverter invMoney = new Inverter("Inverter");
        invMoney.AddChild(hasGotMoney);

        // NEW: 2026-02-25b [dynamically change priority of leaf node]
        goToBackdoor = new Leaf("Go To Backdoor", GoToBackdoor, 2);
        goToFrontdoor = new Leaf("Go To Frontdoor", GoToFrontdoor, 1);
        Leaf goToVan = new Leaf("Go To Van", GoToVan);

        PrioritySelector opendoor = new PrioritySelector("Open Door");
        opendoor.AddChild(goToBackdoor);
        opendoor.AddChild(goToFrontdoor);

        steal.AddChild(invMoney);
        steal.AddChild(opendoor);

        // NEW: 2026-02-25c array of paintings 
        PrioritySelector pickup = new PrioritySelector("Pick Up Something");
        for (int i = 0; i < paintings.Length; i++)
        {
            Leaf goToPainting = new Leaf("Go To Painting #" + i, i, GoToPaintings, i+1);
            pickup.AddChild(goToPainting);
        }
        steal.AddChild(pickup);

        

        /* 
        goToDiamond = new Leaf("Go To Diamond", GoToDiamond, 5);
        goToPainting = new Leaf("Go To Painting", GoToPainting, 1);
        PrioritySelector pickup = new PrioritySelector("Pick Up Something");
        pickup.AddChild(goToDiamond);
        pickup.AddChild(goToPainting);
        steal.AddChild(pickup);
        */

        // steal.AddChild(goToBackdoor);
        steal.AddChild(goToVan);

        Leaf waitAtVan = new Leaf("Wait At Van", WaitAtVan);

        Selector waitOrSteal = new Selector("Wait or Steal");
        waitOrSteal.AddChild(steal);
        waitOrSteal.AddChild(waitAtVan);

        tree.AddChild(waitOrSteal);

        tree.PrintTree();

        // NEW: 2026-02-25a
        // Start CoRoutine
        StartCoroutine("TickTree");
        StartCoroutine("decreasing_money");
    }

    public Node.Status HasMoney() {

        if (money >= 500) return Node.Status.SUCCESS;
        return Node.Status.FAILURE;
    }

    public Node.Status GoToDiamond() {
        if (!diamond.activeSelf) return Node.Status.FAILURE;

        Node.Status s = GoToLocation(diamond.transform.position);
        if (s == Node.Status.SUCCESS) {
            pickupItem = diamond;
            diamond.transform.parent = this.gameObject.transform;
        }
        return s;
    }
    public Node.Status GoToPainting()
    {
        if (!painting.activeSelf) return Node.Status.FAILURE;

        Node.Status s = GoToLocation(painting.transform.position);
        if (s == Node.Status.SUCCESS)
        {
            pickupItem = painting;
            painting.transform.parent = this.gameObject.transform;
        }
        return s;
    }

    // NEW: 2026-02-25c array of paintings 
    public Node.Status GoToPaintings(int id)
    {
        if (!paintings[id].activeSelf || blackboard.timeOfDay < 18) return Node.Status.FAILURE;

        Node.Status s = GoToLocation(paintings[id].transform.position);
        if (s == Node.Status.SUCCESS)
        {
            pickupItem = paintings[id];
            paintings[id].transform.parent = this.gameObject.transform;
        }
        return s;
    }

    public Node.Status GoToVan() {

        Node.Status s = GoToLocation(van.transform.position);
        if (s == Node.Status.SUCCESS) {
            if (pickupItem != null)
            {
                money += 300;
                pickupItem.SetActive(false);
                pickupItem = null;
            }
        }
        return s;
    }

    public Node.Status WaitAtVan()
    {
        Node.Status s = GoToLocation(van.transform.position);
        return s;
    }

    public Node.Status GoToBackdoor() {
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

    public Node.Status GoToFrontdoor() {
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

    public Node.Status GoToDoor(GameObject door) {

        Node.Status s = GoToLocation(door.transform.position);
        if (s == Node.Status.SUCCESS) {

            if (!door.GetComponent<Lock>().isLocked) {
                door.GetComponent<NavMeshObstacle>().enabled = false;
                // door.SetActive(false);
                return Node.Status.SUCCESS;
            }

            return Node.Status.FAILURE;
        }
        return s;
    }

    // NEW: 2026-02-25d decreasing money
    IEnumerator decreasing_money()
    {
        while (true)
        {
            this.money -= Random.Range(10, 20);
            yield return new WaitForSeconds(1.0f);
        }
    }

    public Node.Status IsNight()
    {
        // Assuming 18 is 6 PM and 6 is 6 AM
        if (blackboard.timeOfDay >= 18 || blackboard.timeOfDay < 6)
        {
            return Node.Status.SUCCESS;
        }
        return Node.Status.FAILURE;
    }
    void Update() {
        /*
        if (treeStatus != Node.Status.SUCCESS)
            treeStatus = tree.Process();
        */
    }
}
