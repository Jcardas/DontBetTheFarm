using UnityEngine;
using UnityEngine.AI;

// Collect-and-haul loop: walk to a resource, harvest one load, carry it to the nearest stockpile, repeat.
// When a resource runs out it moves on to the nearest one of the same type.
// Animals start working on their own; the farmer starts idle and FarmerController points him at things.
[RequireComponent(typeof(NavMeshAgent))]
public class Gatherer : MonoBehaviour
{
    [Tooltip("Run the loop from the start. Off for the farmer, who waits for a click.")]
    public bool working = true;
    public ResourceType collects = ResourceType.Wood;
    public int loadSize = 5;
    public float secondsPerLoad = 2f;
    [Tooltip("How close its centre must get to a resource or stockpile's edge. The NavMesh (baked at 0.5 radius) keeps units about 0.5-0.7 off barns and trees, so keep this at 0.8 or more or it never arrives.")]
    public float reach = 1f;

    NavMeshAgent agent;
    Health health;
    ToyBob toy;
    ResourceNode node;
    Stockpile store;
    Collider heading;
    ResourceType carryType;
    int carrying;
    float harvested, nextBump;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        toy = GetComponentInChildren<ToyBob>();
    }

    // Start harvesting this resource (or the nearest one like it, if it is used up).
    public void Work(ResourceNode target)
    {
        if (target != node) harvested = 0f; // re-clicking the tree being chopped keeps its progress
        node = target;
        collects = target.type;
        working = true;
        heading = null;
    }

    // Stop working and walk to a point. Anything being carried is kept until the next Work.
    public void MoveTo(Vector3 point)
    {
        if (!agent.isOnNavMesh) return;
        working = false;
        heading = null;
        if (NavMesh.SamplePosition(point, out NavMeshHit hit, 3f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
    }

    void Update()
    {
        if (!working || (health && health.Down)) return;

        if (carrying > 0)
        {
            if (store == null) store = Stockpile.Nearest(transform.position);
            if (store == null || !Arrive(store.Body)) return;
            store.Deposit(carryType, carrying);
            carrying = 0;
            store = null;
            return;
        }

        if (node == null || node.Empty)
        {
            node = ResourceNode.Nearest(transform.position, collects);
            harvested = 0f;
            if (node == null) return;
        }
        if (!Arrive(node.Body)) return;

        harvested += Time.deltaTime;
        if (toy && Time.time >= nextBump)
        {
            toy.Bump();
            nextBump = Time.time + 0.5f;
        }
        if (harvested < secondsPerLoad) return;
        harvested = 0f;
        carryType = node.type;
        carrying = node.Take(loadSize);
    }

    // Walks toward `target` and returns true once standing next to it.
    bool Arrive(Collider target)
    {
        if (Reach.Gap(transform.position, target) <= reach)
        {
            if (agent.hasPath) agent.ResetPath();
            heading = null;
            return true;
        }
        if (heading != target || !(agent.hasPath || agent.pathPending))
        {
            heading = target;
            Reach.WalkTo(agent, target);
        }
        return false;
    }
}
