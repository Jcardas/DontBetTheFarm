using TMPro;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

[RequireComponent(typeof(NavMeshAgent), typeof(Creature), typeof(CreatureBrain))]
public class Gatherer : MonoBehaviour
{
    private static readonly Color WorkTint = new(0.8f, 1f, 0.7f);
    private static Gatherer activeRepairer; // The RepairPanel is shared by all workers.

    public enum State { Idle, GatheringNode, HarvestingField, RepairingBuilding }

    [Header("Work Settings")]
    public float reach = 1.2f;
    public float secondsPerHarvest = 1.5f;
    public float secondsPerRepair = 1.5f;

    [Header("Repair")]
    [SerializeField] private float repairRange = 3f;
    [SerializeField] private RectTransform repairPanel;
    [SerializeField] private Button repairButton;
    [SerializeField] private TMP_Text repairButtonText;
    [SerializeField] private TMP_Text statusText;

    private NavMeshAgent agent;
    private Creature creature;
    private CreatureBrain brain;
    private ToyBob toy;
    private ProgressRing progress;

    private ResourceNode node;
    private ResourceType activeResourceType;
    private WheatField field;
    private Buildable building;
    private Bounds repairArea;
    private Collider targetCollider;
    private Collider heading;

    private State state = State.Idle;
    private float workTimer;
    private float nextBump;
    private bool repairing;

    public bool IsWorking => state != State.Idle;
    public bool CanRepair => creature && !creature.Down &&
                             creature.HasCapability(CreatureCapabilities.CanRepair);
    public Buildable CurrentRepairTarget => building;

    // True when `unit` is the worker currently holding the shared repair panel.
    public static bool IsActiveRepairer(Selectable unit) =>
        unit != null && activeRepairer != null && unit.gameObject == activeRepairer.gameObject;

    // True when `unit` is already queued up to repair `target`.
    public static bool IsActiveRepairerOn(Selectable unit, Buildable target) =>
        IsActiveRepairer(unit) && activeRepairer.building == target;

    // Closes the shared repair panel and cancels any pending repair stance when the player clicks elsewhere.
    public static void DismissRepairUI()
    {
        if (activeRepairer == null) return;
        if (!activeRepairer.repairing) activeRepairer.AbortWork();
        else if (activeRepairer.repairPanel) activeRepairer.repairPanel.gameObject.SetActive(false);
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        creature = GetComponent<Creature>();
        brain = GetComponent<CreatureBrain>();
        toy = GetComponentInChildren<ToyBob>();
        progress = GetComponentInChildren<ProgressRing>();
    }

    // Existing resource gathering is unchanged.
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

    // This is called by the existing CreatureBrain when it receives a Repair order.
    public void Repair(Buildable target)
    {
        if (!CanRepair || !target || !target.IsBroken || !FindRepairUI()) return;

        Collider col = target.Body && target.Body.enabled && target.Body.gameObject.activeInHierarchy
            ? target.Body : target.GetComponentInChildren<Collider>();

        if (!col)
        {
            Debug.LogWarning($"{target.name} needs an active Collider to be repaired.", target);
            return;
        }

        if (activeRepairer && activeRepairer != this)
            activeRepairer.AbortWork();

        BeginTask(State.RepairingBuilding, target.transform.position, col);
        building = target;
        repairArea = Footprint(target);
        activeRepairer = this;
        repairButton.onClick.AddListener(StartRepair);
        repairPanel.gameObject.SetActive(false);
    }

    private void BeginTask(State newState, Vector3 sitePosition, Collider col)
    {
        if (state != State.Idle) AbortWork();
        node = null;
        field = null;
        building = null;
        heading = null;
        targetCollider = col;
        workTimer = 0f;
        repairing = false;
        brain.IsPassive = true;
        brain.SetGuardPost(sitePosition);
        state = newState;
    }

    public void AbortWork()
    {
        if (activeRepairer == this)
        {
            if (repairButton) repairButton.onClick.RemoveListener(StartRepair);
            if (repairPanel) repairPanel.gameObject.SetActive(false);
            activeRepairer = null;
        }

        state = State.Idle;
        node = null;
        field = null;
        building = null;
        targetCollider = null;
        heading = null;
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

    // Repair reaches its target exactly like wood gathering (Arrive -> Reach.WalkTo).
    private void UpdateRepair()
    {
        if (!CanRepair || !building || !building.IsBroken || !targetCollider)
        {
            AbortWork();
            return;
        }

        if (repairing)
        {
            if (DoWorkTick(secondsPerRepair))
            {
                if (building.TryRepair())
                {
                    foreach (NavMeshSurface surface in FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None))
                        if (surface.isActiveAndEnabled) surface.BuildNavMesh();
                }
                AbortWork();
            }
            return;
        }

        if (!Arrive(targetCollider, repairRange))
        {
            repairPanel.gameObject.SetActive(false);
            return;
        }

        ShowRepairUI();
    }

    private void StartRepair()
    {
        if (repairing || state != State.RepairingBuilding ||
            !building || !building.IsBroken || !targetCollider ||
            Reach.Gap(transform.position, targetCollider) > repairRange ||
            ResourceManager.Count(ResourceType.Wood) < building.RepairWoodCost) return;

        repairing = true;
        workTimer = 0f;
        repairPanel.gameObject.SetActive(false);
    }

    private bool FindRepairUI()
    {
        if (!repairPanel)
            foreach (RectTransform panel in FindObjectsByType<RectTransform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (panel.name == "RepairPanel") { repairPanel = panel; break; }

        if (repairPanel)
        {
            if (!repairButton) repairButton = repairPanel.GetComponentInChildren<Button>(true);
            if (!repairButtonText && repairButton)
                repairButtonText = repairButton.GetComponentInChildren<TMP_Text>(true);
            if (!statusText) statusText = repairPanel.Find("StatusText")?.GetComponent<TMP_Text>();
        }

        if (repairPanel && repairButton && repairButtonText && statusText) return true;
        Debug.LogWarning("Gatherer: RepairPanel UI is missing.", this);
        return false;
    }

    private void ShowRepairUI()
    {
        Camera cam = Camera.main;
        if (!cam) return;

        Vector3 screen = cam.WorldToScreenPoint(
            new Vector3(repairArea.center.x, repairArea.max.y, repairArea.center.z));
        if (screen.z <= 0f) { repairPanel.gameObject.SetActive(false); return; }

        repairPanel.position = screen + Vector3.up * 28f;
        int missing = building.RepairWoodCost - ResourceManager.Count(ResourceType.Wood);
        repairButton.interactable = missing <= 0;
        repairButtonText.text = missing <= 0
            ? $"Repair ({building.RepairWoodCost} Wood)" : "Collect Wood First";
        statusText.text = missing <= 0 ? "Ready to repair" : $"Need {missing} more wood";
        repairPanel.gameObject.SetActive(true);
    }

    private static Bounds Footprint(Buildable target)
    {
        if (target.GroundElement &&
            target.GroundElement.GetComponentInChildren<Renderer>(true) is Renderer ground)
            return ground.bounds;

        Bounds area = new(target.transform.position, Vector3.zero);
        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>())
            if (renderer.enabled) area.Encapsulate(renderer.bounds);
        return area;
    }

    // Shared by chopping, harvesting and repairing.
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
        if (!target || !target.enabled || !target.gameObject.activeInHierarchy ||
            !agent.enabled || !agent.isOnNavMesh) return false;

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
            Reach.WalkTo(agent, target);
        }
        return false;
    }

    private void OnDisable() => AbortWork();
}