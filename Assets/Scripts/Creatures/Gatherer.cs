using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(Creature), typeof(CreatureBrain))]
public class Gatherer : MonoBehaviour
{
    private static readonly Color WorkTint = new(0.8f, 1f, 0.7f);

    public enum State { Idle, GatheringNode, HarvestingField, RepairingBuilding }

    [Header("Work Settings")]
    public float reach = 1.2f;
    public float secondsPerHarvest = 1.5f;
    public int repairAmount = 10;
    public float secondsPerRepair = 1.5f;

    private NavMeshAgent agent;
    private Creature creature;
    private CreatureBrain brain;
    private ToyBob toy;
    private ProgressRing progress;

    private ResourceNode node;
    private ResourceType activeResourceType;
    private WheatField field;
    private Buildable building; 
    private Collider targetCollider;
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
        progress = GetComponentInChildren<ProgressRing>();
    }

    // Maps ResourceType to the capability required to harvest it
    public bool CanWork(ResourceNode target)
    {
        if (target == null) return false;
        return target.type switch
        {
            ResourceType.Wood => creature.HasCapability(CreatureCapabilities.CanChopWood),
            // Add additional ResourceTypes here as you add capabilities (e.g. CanMineStone)
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
        if (target == null || !creature.HasCapability(CreatureCapabilities.CanRepair)) return;
        BeginTask(State.RepairingBuilding, target.transform.position, target.GetComponentInChildren<Collider>());
        building = target;
    }

    private void BeginTask(State newState, Vector3 sitePosition, Collider col)
    {
        node = null;
        field = null;
        building = null;
        heading = null;
        workTimer = 0f;

        targetCollider = col;
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
        targetCollider = null;
        heading = null;
        workTimer = 0f;

        if (agent != null && agent.isOnNavMesh && agent.hasPath) agent.ResetPath();
        if (brain != null)
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
            case State.GatheringNode:     UpdateNodeGathering(); break;
            case State.HarvestingField:   UpdateFieldHarvest(); break;
            case State.RepairingBuilding: UpdateRepair(); break;
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
            int taken = node.Take(1); // Assuming 1 unit per harvest for simplicity
            if (taken > 0 && ResourceManager.Instance != null)
                ResourceManager.Instance.Deposit(node.type, taken);

            if (node.Empty)
            {
                node = null;
                heading = null;
            }
        }
    }

    private void UpdateFieldHarvest()
    {
        if (!creature.HasCapability(CreatureCapabilities.CanHarvestCrops) || field == null || targetCollider == null)
        {
            AbortWork();
            return;
        }

        if (!Arrive(targetCollider) || !field.mature) return;

        if (DoWorkTick(secondsPerHarvest))
        {
            field.HarvestWheat();
            AbortWork();
        }
    }

    private void UpdateRepair()
    {
        //TODO use sazias repair system once merged, you can safely ignore this.
        
        Debug.Log("REMOVE REDUNDANT REPAIR CODE IN GATHERER.CS ONCE SAZIA'S REPAIR SYSTEM IS MERGED");
        
        // if (!creature.HasCapability(CreatureCapabilities.CanRepair) || building == null || building.CurrentHealth >= building.MaxHealth)
        // {
        //     AbortWork();
        //     return;
        // }
        //
        // if (!Arrive(targetCollider)) return;
        //
        // if (DoWorkTick(secondsPerRepair))
        // {
        //     building.Repair(repairAmount);
        //     if (building.CurrentHealth >= building.MaxHealth) AbortWork();
        // }
    }

    private bool DoWorkTick(float duration)
    {
        workTimer += Time.deltaTime;
        if (progress) progress.Set(workTimer / duration, WorkTint);

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
        if (brain != null) brain.IsPassive = false;
    }
}