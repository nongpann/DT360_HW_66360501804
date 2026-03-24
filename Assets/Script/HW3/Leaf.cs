using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Leaf : Node {

    public delegate Status Tick();
    public Tick ProcessMethod;

    // NEW: 2026-02-25c array of paintings 
    public delegate Status Tick2(int x);
    public Tick2 ProcessMethod2;
    private int index;

    public Leaf() { }
    public Leaf(string n, Tick pm) {

        name = n;
        ProcessMethod = pm;
    }

    public Leaf(string n, Tick pm, int order)
    {
        name = n;
        ProcessMethod = pm;
        sortValue = order;
    }

    // NEW: 2026-02-25c array of paintings 
    public Leaf(string n, int id, Tick2 pm, int order)
    {
        name = n;
        ProcessMethod2 = pm;
        index = id;
        sortValue = order;
    }

    public override Status Process() {

        if (ProcessMethod != null)
            return ProcessMethod();
        else if (ProcessMethod2 != null)
            return ProcessMethod2(index);
        else
            return Status.FAILURE;
    }
}