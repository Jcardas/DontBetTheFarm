using System.Collections;
using TMPro;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Select a worker > click a broken building > worker walks over > press Repair.
public class RepairManager : MonoBehaviour
{
    [SerializeField] private float repairRange = 3f;

    [Header("UI")]
    [SerializeField] private RectTransform repairPanel;
    [SerializeField] private Button repairButton;
    [SerializeField] private TMP_Text repairButtonText;
    [SerializeField] private TMP_Text statusText;

    private Camera camera;
    private RepairWorker worker;
    private RepairableStructure building;
    private bool repairing;

    public RepairableStructure CurrentRepairTarget => building;

    private void Awake()
    {
        camera = Camera.main;

        if (!repairPanel)
            foreach (RectTransform rectTransform in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (rectTransform.name == "RepairPanel") { repairPanel = rectTransform; break; }

        if (repairPanel)
        {
            if (!repairButton) repairButton = repairPanel.GetComponentInChildren<Button>(true);
            if (!repairButtonText && repairButton) repairButtonText = repairButton.GetComponentInChildren<TMP_Text>(true);
            if (!statusText) statusText = repairPanel.Find("StatusText")?.GetComponent<TMP_Text>();
        }

        if (!repairPanel || !repairButton || !repairButtonText || !statusText)
        {
            Debug.LogWarning("RepairManager: RepairPanel UI is missing.", this);
            enabled = false;
            return;
        }

        repairButton.onClick.AddListener(StartRepair);
    }

    private void OnDestroy()
    {
        if (repairButton) repairButton.onClick.RemoveListener(StartRepair);
    }

    private void LateUpdate()
    {
        if (!camera) camera = Camera.main;
        if (!repairing)
        {
            PickWorker();
            PickBuilding();
        }
        UpdateUI();
    }

    // Select a RepairWorker from the currently selected units.
    private void PickWorker()
    {
        foreach (Selectable unit in Selectable.All)
        {
            if (!unit || !unit.Selected) continue;
            RepairWorker candidate = unit.GetComponent<RepairWorker>();
            if (candidate && candidate.CanRepair && candidate != worker) { worker = candidate; building = null; }
            break;
        }
    }

    // Click a broken building 
    private void PickBuilding()
    {
        if (!worker || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame ||
            (EventSystem.current && EventSystem.current.IsPointerOverGameObject())) return;

        Ray ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
        float nearest = float.MaxValue;

        foreach (RepairableStructure structure in FindObjectsByType<RepairableStructure>(FindObjectsSortMode.None))
        {
            if (!structure.IsBroken || !Footprint(structure).IntersectRay(ray, out float dist) || dist >= nearest) continue;
            building = structure;
            nearest = dist;
        }

        // RepairWorker.WalkTo handles pathing
        if (nearest < float.MaxValue && !InRange())
            worker.WalkTo(building is Buildable b && b.Body ? b.Body : building.GetComponentInChildren<Collider>());
    }

    private void UpdateUI()
    {
        if (building && (!building.IsBroken || !worker || !worker.CanRepair)) building = null;

        bool show = !repairing && building && InRange();
        if (show)
        {
            Bounds area = Footprint(building);
            Vector3 screen = camera.WorldToScreenPoint(new Vector3(area.center.x, area.max.y, area.center.z));
            show = screen.z > 0f;
            repairPanel.position = screen + Vector3.up * 28f;

            int missing = building.RepairWoodCost - ResourceManager.Count(ResourceType.Wood);
            repairButton.interactable = missing <= 0;
            repairButtonText.text = missing <= 0 ? $"Repair ({building.RepairWoodCost} Wood)" : "Collect Wood First";
            statusText.text = missing <= 0 ? "Ready to repair" : $"Need {missing} more wood";
        }
        repairPanel.gameObject.SetActive(show);
    }

    private bool InRange()
    {
        Vector3 pos = worker.transform.position;
        Vector3 closest = Footprint(building).ClosestPoint(pos);
        closest.y = pos.y;
        return Vector3.Distance(pos, closest) <= repairRange;
    }

    private void StartRepair()
    {
        if (!repairing && building) StartCoroutine(RepairRoutine());
    }

    // Move everyone out of the footprint, wait until they are all out, then repair and rebuild the NavMesh.
    private IEnumerator RepairRoutine()
    {
        repairing = true;
        RepairableStructure target = building;
        Bounds area = Footprint(target);
        area.Expand(new Vector3(2f, 0f, 2f));

        foreach (Creature creature in FindObjectsByType<Creature>(FindObjectsSortMode.None))
            if (Inside(area, creature.transform.position)) SendOutside(creature, area);

        while (AnyoneInside(area)) yield return null;

        if (building == target && target.TryRepair())
        {
            foreach (NavMeshSurface navmeshsurface in FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None))
                if (navmeshsurface.isActiveAndEnabled) navmeshsurface.BuildNavMesh();
            building = null;
        }
        else Debug.LogWarning("Repair cancelled: not enough wood or the target changed.");

        repairing = false;
    }

    private static bool AnyoneInside(Bounds area)
    {
        foreach (Creature creature in FindObjectsByType<Creature>(FindObjectsSortMode.None))
            if (Inside(area, creature.transform.position)) return true;
        return false;
    }

    private static bool Inside(Bounds a, Vector3 p) =>
        p.x >= a.min.x && p.x <= a.max.x && p.z >= a.min.z && p.z <= a.max.z;

    // Send a creature just past the nearest edge of the area.
    private static void SendOutside(Creature creature, Bounds area)
    {
        Vector3 p = creature.transform.position, exit = p;
        float left = p.x - area.min.x, right = area.max.x - p.x;
        float back = p.z - area.min.z, front = area.max.z - p.z;
        float min = Mathf.Min(left, right, back, front);

        if (min == left) exit.x = area.min.x - 1f;
        else if (min == right) exit.x = area.max.x + 1f;
        else if (min == back) exit.z = area.min.z - 1f;
        else exit.z = area.max.z + 1f;

        if (NavMesh.SamplePosition(exit, out NavMeshHit hit, 2f, NavMesh.AllAreas)) exit = hit.position;

        if (creature.CreatureBrain && creature.CreatureBrain.isActiveAndEnabled) creature.CreatureBrain.Guard(exit);
        else if (creature.NavMeshAgent && creature.NavMeshAgent.isOnNavMesh) creature.NavMeshAgent.SetDestination(exit);
    }

    private static Bounds Footprint(RepairableStructure structure)
    {
        if (structure is Buildable buildable && buildable.GroundElement && buildable.GroundElement.GetComponentInChildren<Renderer>(true) is Renderer ground)
            return ground.bounds;

        Bounds area = new(structure.transform.position, Vector3.zero);
        foreach (Renderer renderer in structure.GetComponentsInChildren<Renderer>()) area.Encapsulate(renderer.bounds);
        return area;
    }
}