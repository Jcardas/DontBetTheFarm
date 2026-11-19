using TMPro;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(Creature), typeof(CreatureBrain))]
public class Gatherer : MonoBehaviour
{
    private static readonly Color WorkTint = new(0.8f, 1f, 0.7f);

    public enum State { Idle, GatheringNode, HarvestingField, RepairingBuilding, RevivingAnimal }

    [Header("Work Settings")]
    public float reach = 1.2f;
    public float secondsPerHarvest = 1.5f;
    public float secondsPerRepair = 1.5f;

    [Header("Repair")]
    [SerializeField] private float repairRange = 3f;

    private NavMeshAgent agent;
    private Creature creature;
    private CreatureBrain brain;
    private ToyBob toy;
    private ProgressRing progress;

    private ResourceNode node;
    private ResourceType activeResourceType;
    private WheatField field;
    private Buildable building;
    private Animal reviveTarget;
    private Collider targetCollider;
    private Collider heading;
    private Collider flewTo; // work site a flier has already been sent flying to

    private State state = State.Idle;
    private float workTimer;
    private float nextBump;
    private bool repairing;

    public bool IsWorking => state != State.Idle;
    public bool CanRepair => creature && !creature.Down &&
                             creature.HasCapability(CreatureCapabilities.CanRepair);
    public Buildable CurrentRepairTarget => building;

    // Kept as no-op stubs so existing calls in PlayerControls compile cleanly
    public static bool IsActiveRepairer(Selectable unit) => false;
    public static bool IsActiveRepairerOn(Selectable unit, Buildable target) => false;
    public static void DismissRepairUI() { }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        creature = GetComponent<Creature>();
        brain = GetComponent<CreatureBrain>();
        toy = GetComponentInChildren<ToyBob>();
        progress = GetComponentInChildren<ProgressRing>();
    }

    public bool CanWork(ResourceNode target)
    {
        if (target == null) return false;
        return target.type switch
        {
            ResourceType.Wood => creature.HasCapability(CreatureCapabilities.CanChopWood),
            _ => false
        };
    }

    public void Work(ResourceNode target)
    {
        if (!CanWork(target)) return;
        BeginTask(State.GatheringNode, target.transform.position, target.Body);
        node = target;
        activeResourceType = target.type;
    }

    public void Harvest(WheatField target)
    {
        if (target == null || !creature.HasCapability(CreatureCapabilities.CanHarvestCrops)) return;
        BeginTask(State.HarvestingField, target.transform.position, target.GetComponentInChildren<Collider>());
        field = target;
    }

    public void Repair(Buildable target)
    {
        if (!CanRepair || !target || !target.IsBroken) return;
        if (ResourceManager.Count(ResourceType.Wood) < target.RepairWoodCost) return;

        Collider col = target.Body && target.Body.enabled && target.Body.gameObject.activeInHierarchy
            ? target.Body : target.GetComponentInChildren<Collider>();

        if (!col)
        {
            Debug.LogWarning($"{target.name} needs an active Collider to be repaired.", target);
            return;
        }

        BeginTask(State.RepairingBuilding, target.transform.position, col);
        building = target;
    }

    public void Revive(Animal target)
    {
        if (creature is not Farmer || creature.Down || target == null || !target.Down) return;
        if (ResourceManager.Count(ResourceType.Hay) < target.HealCost) return;

        Collider col = target.Body != null ? target.Body : target.GetComponentInChildren<Collider>();
        if (col == null) return;

        BeginTask(State.RevivingAnimal, target.transform.position, col);
        reviveTarget = target;
    }

    private void BeginTask(State newState, Vector3 sitePosition, Collider col)
    {
        if (state != State.Idle) AbortWork();
        node = null;
        field = null;
        building = null;
        reviveTarget = null;
        heading = null;
        flewTo = null;
        targetCollider = col;
        workTimer = 0f;
        repairing = false;
        brain.IsPassive = true;
        brain.SetGuardPost(sitePosition);
        state = newState;
    }

    public void AbortWork()
    {
        state = State.Idle;
        node = null;
        field = null;
        building = null;
        reviveTarget = null;
        targetCollider = null;
        heading = null;
        flewTo = null;
        workTimer = 0f;
        repairing = false;

        if (agent && agent.enabled && agent.isOnNavMesh && agent.hasPath)
            agent.ResetPath();
        if (brain)
        {
            brain.IsPassive = false;
            brain.SetGuardPost(transform.position);
        }
    }

    private void Update()
    {
        if (state == State.Idle) return;
        if (creature.Down) { AbortWork(); return; }

        switch (state)
        {
            case State.GatheringNode: UpdateNodeGathering(); break;
            case State.HarvestingField: UpdateFieldHarvest(); break;
            case State.RepairingBuilding: UpdateRepair(); break;
            case State.RevivingAnimal: UpdateRevive(); break;
        }
    }

    private void UpdateNodeGathering()
    {
        if (!creature.HasCapability(CreatureCapabilities.CanChopWood)) { AbortWork(); return; }

        if (node == null || node.Empty)
        {
            node = ResourceNode.Nearest(transform.position, activeResourceType);
            if (node == null) { AbortWork(); return; }

            targetCollider = node.Body;
            workTimer = 0f;
            brain.SetGuardPost(node.transform.position);
        }

        if (!Arrive(targetCollider)) return;

        if (DoWorkTick(secondsPerHarvest))
        {
            int taken = node.Take(1);
            if (taken > 0 && ResourceManager.Instance != null)
                ResourceManager.Instance.Deposit(node.type, taken);
            if (node.Empty) { node = null; heading = null; }
        }
    }

    private void UpdateFieldHarvest()
    {
        if (!creature.HasCapability(CreatureCapabilities.CanHarvestCrops) || field == null || field.IsBroken || targetCollider == null)
        {
            AbortWork();
            return;
        }

        if (!field.mature)
        {
            WheatField readyField = FindNearestMatureField();
            if (readyField != null)
            {
                Collider col = readyField.GetComponentInChildren<Collider>();
                if (col != null)
                {
                    field = readyField;
                    targetCollider = col;
                    heading = null;
                    flewTo = null;
                    workTimer = 0f;
                    brain.SetGuardPost(field.transform.position);
                }
            }
        }

        if (!Arrive(targetCollider) || !field.mature) return;

        if (DoWorkTick(secondsPerHarvest))
        {
            field.HarvestWheat();
        }
    }
    
    private WheatField FindNearestMatureField()
    {
        if (BuildingManager.Instance == null) return null;

        WheatField best = null;
        float bestDist = float.MaxValue;

        foreach (Buildable b in BuildingManager.Instance.Buildings)
        {
            if (b is not WheatField candidate || candidate.IsBroken || !candidate.mature)
                continue;

            Collider col = candidate.GetComponentInChildren<Collider>();
            if (col == null) continue;

            float dist = Reach.Gap(transform.position, col);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = candidate;
            }
        }

        return best;
    }

    private void UpdateRepair()
    {
        if (!CanRepair || !building || !building.IsBroken || !targetCollider)
        {
            AbortWork();
            return;
        }

        if (!repairing)
        {
            if (!Arrive(targetCollider, repairRange)) return;

            if (ResourceManager.Count(ResourceType.Wood) < building.RepairWoodCost)
            {
                AbortWork();
                return;
            }

            repairing = true;
            workTimer = 0f;
        }

        if (DoWorkTick(secondsPerRepair))
        {
            if (building.TryRepair())
            {
                foreach (NavMeshSurface surface in FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None))
                    if (surface.isActiveAndEnabled) surface.BuildNavMesh();
            }
            AbortWork();
        }
    }

    private void UpdateRevive()
    {
        if (creature is not Farmer || creature.Down || reviveTarget == null || !reviveTarget.Down || targetCollider == null)
        {
            AbortWork();
            return;
        }

        if (!Arrive(targetCollider)) return;

        if (DoWorkTick(secondsPerRepair))
        {
            if (reviveTarget.Down && ResourceManager.Spend(ResourceType.Hay, reviveTarget.HealCost))
            {
                reviveTarget.HealToFull();
            }
            AbortWork();
        }
    }

    private bool DoWorkTick(float duration)
    {
        workTimer += Time.deltaTime;
        if (progress) progress.Set(workTimer / Mathf.Max(duration, 0.01f), WorkTint);
        if (toy && Time.time >= nextBump)
        {
            toy.Bump();
            nextBump = Time.time + 0.5f;
        }
        if (workTimer < duration) return false;
        workTimer = 0f;
        return true;
    }

    private bool Arrive(Collider target, float stopDistance = -1f)
    {
        if (!target || !target.enabled || !target.gameObject.activeInHierarchy) return false;

        // Fliers fly to the work site (field, tree, broken building) and land by it. In the air the agent is off,
        // so without this an airborne goose hovered in place forever.
        CreatureFlight flight = creature.Flight;
        if (flight != null && flight.IsAirborne)
        {
            if (flewTo != target)
            {
                flewTo = target;
                flight.FlyToAndLand(Reach.NearestPoint(target, transform.position));
            }
            return false;
        }
        if (!agent.enabled || !agent.isOnNavMesh) return false;

        float distance = stopDistance < 0f ? reach : stopDistance;
        if (Reach.Gap(transform.position, target) <= distance)
        {
            if (agent.hasPath) agent.ResetPath();
            heading = null;
            return true;
        }
        if (heading != target || !(agent.hasPath || agent.pathPending))
        {
            heading = target;
            if (flewTo != target && flight != null && flight.FlyToAndLand(Reach.NearestPoint(target, transform.position)))
                flewTo = target;
            else
                Reach.WalkTo(agent, target);
        }
        return false;
    }

    private void OnDisable() => AbortWork();
}