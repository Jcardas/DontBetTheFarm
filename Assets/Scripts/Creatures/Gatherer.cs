using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(Creature), typeof(CreatureBrain))]
public class Gatherer : MonoBehaviour
{
    private static readonly Color WorkTint = new(0.8f, 1f, 0.7f);

    public enum State
    {
        Idle,
        GatheringNode,
        HarvestingField
    }

    [Header("Gathering Settings")] public ResourceType collects = ResourceType.Wood;
    public int loadSize = 5;
    public float secondsPerLoad = 2f;

    [Tooltip("How close its centre must get to a resource's edge.")]
    public float reach = 1.2f;

    [Tooltip("Can be ordered to harvest crop fields. On for the farmer only.")]
    public bool harvestsFields;

    [Tooltip("Seconds of work on a ripe field before it is harvested.")]
    public float secondsPerHarvest = 1.5f;

    private NavMeshAgent agent;
    private Creature creature;
    private CreatureBrain brain;
    private ToyBob toy;
    private ProgressRing progress;

    private ResourceNode node;
    private WheatField field;
    private Collider soil;
    private Collider heading;

    private State state = State.Idle;
    private float workTimer;
    private float nextBump;

    public bool IsWorking => state != State.Idle;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        creature = GetComponent<Creature>();
        brain = GetComponent<CreatureBrain>();
        toy = GetComponentInChildren<ToyBob>();
        progress = GetComponent<ProgressRing>();
    }

    public void Work(ResourceNode target)
    {
        if (target == null) return;

        field = null;
        soil = null;
        node = target;
        collects = target.type;
        workTimer = 0f;
        heading = null;

        brain.IsPassive = true;
        // Tether unit to the work site rather than its old spawn post
        brain.SetGuardPost(target.transform.position);

        state = State.GatheringNode;
    }

    public void Harvest(WheatField target)
    {
        if (target == null || !harvestsFields) return;

        node = null;
        field = target;
        soil = target.GetComponentInChildren<Collider>();
        workTimer = 0f;
        heading = null;

        brain.IsPassive = true;
        brain.SetGuardPost(target.transform.position);

        state = State.HarvestingField;
    }

    public void MoveTo(Vector3 point)
    {
        AbortWork();

        if (agent.isOnNavMesh && NavMesh.SamplePosition(point, out NavMeshHit hit, 3f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    public void AbortWork()
    {
        state = State.Idle;
        node = null;
        field = null;
        soil = null;
        heading = null;
        workTimer = 0f;

        if (agent != null && agent.isOnNavMesh && agent.hasPath)
        {
            agent.ResetPath();
        }

        if (brain != null)
        {
            brain.IsPassive = false;
            // Guard where the unit currently stands when interrupted
            brain.SetGuardPost(transform.position);
        }
    }

    private void Update()
    {
        if (state == State.Idle || creature.Down)
        {
            if (creature.Down && state != State.Idle) AbortWork();
            return;
        }

        if (state == State.HarvestingField)
        {
            UpdateFieldHarvest();
        }
        else if (state == State.GatheringNode)
        {
            UpdateNodeGathering();
        }
    }

    private void UpdateNodeGathering()
    {
        if (node == null || node.Empty)
        {
            node = ResourceNode.Nearest(transform.position, collects);
            workTimer = 0f;
            if (node == null)
            {
                AbortWork();
                return;
            }

            brain.SetGuardPost(node.transform.position);
        }

        if (!Arrive(node.Body)) return;

        if (DoWorkTick(secondsPerLoad))
        {
            int amountTaken = node.Take(loadSize);

            if (ResourceManager.Instance == null)
            {
                Debug.LogError(
                    "[Gatherer] ResourceManager.Instance is NULL! Ensure a GameObject in the scene has ResourceManager attached.");
                return;
            }

            if (amountTaken > 0)
            {
                ResourceManager.Instance.AddResource(node.type, amountTaken);
            }
            else
            {
                Debug.LogWarning(
                    $"[Gatherer] Node {node.name} returned 0 resources on Take({loadSize}). Is the node empty or unconfigured?");
            }

            if (node.Empty)
            {
                node = null;
                heading = null;
            }
        }
    }

    private void UpdateFieldHarvest()
    {
        if (field == null || soil == null)
        {
            AbortWork();
            return;
        }

        if (!Arrive(soil)) return;
        if (!field.mature) return;

        if (DoWorkTick(secondsPerHarvest))
        {
            field.HarvestWheat();
            AbortWork();
        }
    }

    private bool DoWorkTick(float duration)
    {
        workTimer += Time.deltaTime;

        if (progress)
            progress.Set(workTimer / duration, WorkTint);

        if (toy && Time.time >= nextBump)
        {
            toy.Bump();
            nextBump = Time.time + 0.5f;
        }

        if (workTimer < duration) return false;

        workTimer = 0f;
        return true;
    }

    private bool Arrive(Collider target)
    {
        if (target == null) return false;

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

    private void OnDisable()
    {
        if (brain != null)
            brain.IsPassive = false;
    }
}