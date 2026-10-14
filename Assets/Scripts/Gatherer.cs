using UnityEngine;
using UnityEngine.AI;

// Collect-and-haul loop: walk to a resource, harvest one load, carry it to the nearest stockpile, repeat.
// When a resource runs out it moves on to the nearest one of the same type.
// Animals start working on their own; the farmer starts idle and PlayerControls points him at things.
// A unit that also fights (the farmer) pauses the loop while it is fighting, then picks it back up.
// The farmer can also harvest a crop field: walk next to it, wait until it is ripe, work it for a moment, then call
// WheatField.HarvestWheat, which adds the hay to the field's own ResourceManager. After that he stands idle.
[RequireComponent(typeof(NavMeshAgent))]
public class Gatherer : MonoBehaviour
{
    static readonly Color WorkTint = new(0.8f, 1f, 0.7f);

    [Tooltip("Run the loop from the start. Off for the farmer, who waits for a click.")]
    public bool working = true;
    public ResourceType collects = ResourceType.Wood;
    public int loadSize = 5;
    public float secondsPerLoad = 2f;
    [Tooltip("How close its centre must get to a resource or stockpile's edge. The NavMesh (baked at 0.5 radius) keeps units about 0.5-0.7 off barns and trees, so keep this at 0.8 or more or it never arrives.")]
    public float reach = 1f;
    [Tooltip("Can be ordered to harvest crop fields. On for the farmer only; other units just walk to a clicked field.")]
    public bool harvestsFields;
    [Tooltip("Seconds of work on a ripe field before it is harvested.")]
    public float secondsPerHarvest = 1.5f;

    NavMeshAgent agent;
    Health health;
    Fighter fighter;
    ToyBob toy;
    ProgressRing progress;
    ResourceNode node;
    WheatField field;
    Collider soil;
    Stockpile store;
    Collider heading;
    ResourceType carryType;
    int carrying;
    float harvested, nextBump;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        fighter = GetComponent<Fighter>();
        toy = GetComponentInChildren<ToyBob>();
        progress = GetComponent<ProgressRing>();
    }

    // Start harvesting this resource (or the nearest one like it, if it is used up).
    public void Work(ResourceNode target)
    {
        if (target != node) harvested = 0f; // re-clicking the tree being chopped keeps its progress
        node = target;
        field = null;
        collects = target.type;
        working = true;
        heading = null;
    }

    // Walk next to the field, wait there until it is ripe, then harvest it once.
    public void Harvest(WheatField target)
    {
        field = target;
        soil = target.GetComponentInChildren<Collider>(); // the soil comes first; the wheat plants are added after it
        working = true;
        heading = null;
        harvested = 0f;
    }

    // Stop working and walk to a point. Anything being carried is kept until the next Work.
    public void MoveTo(Vector3 point)
    {
        if (!agent.isOnNavMesh) return;
        working = false;
        field = null;
        heading = null;
        if (NavMesh.SamplePosition(point, out NavMeshHit hit, 3f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
    }

    void Update()
    {
        if (!working || (health && health.Down) || (fighter && fighter.Engaged)) return;
        if (field != null)
        {
            HarvestField();
            return;
        }

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

        if (!WorkFor(secondsPerLoad)) return;
        carryType = node.type;
        carrying = node.Take(loadSize);
    }

    void HarvestField()
    {
        if (!Arrive(soil) || !field.mature || !WorkFor(secondsPerHarvest)) return;
        field.HarvestWheat();
        field = null;
        working = false;
    }

    // Adds this frame to the work in progress, hopping now and then and filling the ring, and returns true once it has taken `seconds`.
    bool WorkFor(float seconds)
    {
        harvested += Time.deltaTime;
        if (progress) progress.Set(harvested / seconds, WorkTint);
        if (toy && Time.time >= nextBump)
        {
            toy.Bump();
            nextBump = Time.time + 0.5f;
        }
        if (harvested < seconds) return false;
        harvested = 0f;
        return true;
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
