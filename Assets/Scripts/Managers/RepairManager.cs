using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Unity.AI.Navigation;

public class RepairManager : MonoBehaviour
{
    [Header("Repair")]
    [SerializeField] private float repairRange = 3f;
    [SerializeField] private float exitTimeout = 8f;

    [Header("UI")]
    [SerializeField] private RectTransform repairPanel;
    [SerializeField] private Button repairButton;
    [SerializeField] private TMP_Text repairButtonText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private float panelOffset = 28f;
    [SerializeField] private float screenPadding = 12f;

    [Header("Hammer Animation")]
    [SerializeField] private GameObject hammerPrefab;
    [SerializeField] private float hammerDuration = 1.2f;

    private Camera cam;
    private RepairWorker worker;
    private RepairableStructure building;
    private Bounds footprint;
    private bool exiting;

    public RepairableStructure CurrentRepairTarget => building;

    private void Awake()
    {
        cam = Camera.main;

        // GameManager.prefab can't point at the scene's UI, so look the panel up when it isn't assigned.
        if (!repairPanel) FindPanel();
        if (!repairPanel || !repairButton || !repairButtonText || !statusText)
        {
            Debug.LogWarning("RepairManager: no repair panel in this scene, so repairs are off.");
            enabled = false;
            return;
        }

        repairPanel.gameObject.SetActive(false);
        repairButton.onClick.AddListener(RepairSelected);
        repairButton.interactable = false;
    }

    private void OnDestroy()
    {
        if (repairButton) repairButton.onClick.RemoveListener(RepairSelected);
    }

    // The panel lives in UI.prefab (ResourceCanvas > RepairPanel) and starts hidden, so search inactive objects too.
    private void FindPanel()
    {
        foreach (var t in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name != "RepairPanel") continue;

            repairPanel = t;
            repairButton = t.GetComponentInChildren<Button>(true);
            if (repairButton) repairButtonText = repairButton.GetComponentInChildren<TMP_Text>(true);
            Transform status = t.Find("StatusText");
            if (status) statusText = status.GetComponent<TMP_Text>();
            return;
        }
    }

    private void LateUpdate()
    {
        if (!exiting)
        {
            foreach (Selectable unit in Selectable.All)
            {
                if (!unit || !unit.Selected) continue;
                RepairWorker chosen = unit.GetComponent<RepairWorker>();
                if (chosen && chosen.CanRepair) SelectWorker(chosen);
                break;
            }
            HandleClick();
        }

        if (building && (!building.IsBroken || !worker || !worker.CanRepair))
            ClearBuilding();

        bool show = !exiting && building && InRange() && PlacePanel();
        if (show)
        {
            int cost = building.RepairWoodCost;
            int missing = cost - ResourceManager.Count(ResourceType.Wood);
            repairButton.interactable = missing <= 0;
            repairButtonText.text = missing <= 0 ? $"Repair ({cost} Wood)" : "Collect Wood First";
            statusText.text = missing <= 0 ? "Ready to repair" : $"Not enough wood! Need {missing} more.";
        }
        else repairButton.interactable = false;

        if (repairPanel.gameObject.activeSelf != show)
            repairPanel.gameObject.SetActive(show);
    }

    public void SelectWorker(RepairWorker selected)
    {
        if (worker == selected) return;
        worker = selected;
        ClearBuilding();
    }

    public void CancelSelection()
    {
        worker = null;
        ClearBuilding();
    }

    private void ClearBuilding()
    {
        building = null;
        repairPanel.gameObject.SetActive(false);
    }

    private void HandleClick()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame ||
            !worker || !worker.CanRepair || !cam ||
            (EventSystem.current && EventSystem.current.IsPointerOverGameObject())) return;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        RepairableStructure closest = null;
        float minDistance = float.PositiveInfinity;

        foreach (RepairableStructure target in FindObjectsByType<RepairableStructure>(FindObjectsSortMode.None))
        {
            if (!target.IsBroken || !GetFootprint(target, out Bounds bounds)) continue;
            bounds.Expand(0.3f);
            if (bounds.IntersectRay(ray, out float distance) && distance < minDistance)
            {
                minDistance = distance;
                closest = target;
            }
        }

        if (closest && closest != building) SelectBuilding(closest);
    }

    public void SelectBuilding(RepairableStructure target)
    {
        if (!worker || !worker.CanRepair || !target || !target.IsBroken ||
            !GetFootprint(target, out Bounds bounds)) return;

        building = target;
        footprint = bounds;
        if (InRange()) return;

        NavMeshAgent agent = GetWorkerAgent();
        if (!agent || !FindExit(agent, footprint, Mathf.Min(1.5f, repairRange * 0.5f), out Vector3 exit) ||
            !MoveCreature(worker.GetComponent<Creature>(), exit))
            Debug.LogWarning("No reachable repair position for " + target.name);
    }

    private static bool GetFootprint(RepairableStructure target, out Bounds bounds)
    {
        if (target is Buildable buildable && buildable.GroundElement)
        {
            Renderer ground = buildable.GroundElement.GetComponentInChildren<Renderer>(true);
            if (ground) { bounds = ground.bounds; return true; }
        }

        bounds = default;
        bool found = false;
        foreach (Renderer part in target.GetComponentsInChildren<Renderer>())
        {
            if (!part.enabled) continue;
            if (found) bounds.Encapsulate(part.bounds);
            else { bounds = part.bounds; found = true; }
        }
        return found;
    }

    private static bool Inside(Bounds bounds, Vector3 position, float margin = 0f) =>
        position.x >= bounds.min.x - margin && position.x <= bounds.max.x + margin &&
        position.z >= bounds.min.z - margin && position.z <= bounds.max.z + margin;

    private bool InRange()
    {
        if (!building || !worker || !worker.CanRepair) return false;
        Vector3 p = worker.transform.position;
        float dx = Mathf.Max(0f, footprint.min.x - p.x, p.x - footprint.max.x);
        float dz = Mathf.Max(0f, footprint.min.z - p.z, p.z - footprint.max.z);
        return dx * dx + dz * dz <= repairRange * repairRange;
    }

    private bool PlacePanel()
    {
        if (!cam) return false;
        Vector3 point = cam.WorldToScreenPoint(new Vector3(footprint.center.x, footprint.max.y, footprint.center.z));
        if (point.z <= 0f) return false;

        Vector2 size = Vector2.Scale(repairPanel.rect.size, repairPanel.lossyScale);
        repairPanel.position = new Vector3(
            Mathf.Clamp(point.x, size.x / 2f + screenPadding, Screen.width - size.x / 2f - screenPadding),
            Mathf.Clamp(point.y + panelOffset, screenPadding, Screen.height - size.y - screenPadding), 0f);
        return true;
    }

    private NavMeshAgent GetWorkerAgent()
    {
        Creature creature = worker ? worker.GetComponent<Creature>() : null;
        NavMeshAgent agent = creature ? creature.NavMeshAgent : null;
        return agent && agent.enabled && agent.isOnNavMesh ? agent : null;
    }

    private static bool FindExit(NavMeshAgent agent, Bounds area, float margin, out Vector3 exit)
    {
        Vector3 p = agent.transform.position;
        float x = Mathf.Clamp(p.x, area.min.x, area.max.x);
        float z = Mathf.Clamp(p.z, area.min.z, area.max.z);
        Vector3[] candidates = {
            new(area.min.x - margin, p.y, z), new(area.max.x + margin, p.y, z),
            new(x, p.y, area.min.z - margin), new(x, p.y, area.max.z + margin)
        };

        exit = p;
        float shortest = float.PositiveInfinity;
        NavMeshPath path = new NavMeshPath();
        foreach (Vector3 candidate in candidates)
        {
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f, agent.areaMask) ||
                Inside(area, hit.position, 0.01f) || !agent.CalculatePath(hit.position, path) ||
                path.status != NavMeshPathStatus.PathComplete) continue;

            float distance = Vector3.Distance(p, hit.position);
            if (distance >= shortest) continue;
            shortest = distance;
            exit = hit.position;
        }
        return shortest < float.PositiveInfinity;
    }

    private static bool MoveCreature(Creature creature, Vector3 destination)
    {
        NavMeshAgent agent = creature ? creature.NavMeshAgent : null;
        if (!agent || !agent.enabled || !agent.isOnNavMesh) return false;

        if (creature.CreatureBrain) creature.CreatureBrain.Obey();
        agent.isStopped = false;
        if (!agent.SetDestination(destination)) return false;
        if (creature.CreatureBrain) creature.CreatureBrain.SetGuardPost(destination);
        return true;
    }

    public void RepairSelected()
    {
        if (exiting || !building || !InRange()) return;
        if (ResourceManager.Count(ResourceType.Wood) < building.RepairWoodCost)
        {
            statusText.text = "Not enough wood! Collect wood first.";
            return;
        }
        StartCoroutine(ExitThenRepair());
    }

    private bool CanFinishRepair(RepairableStructure target, RepairWorker repairer) =>
        target && building == target && worker == repairer && repairer && repairer.CanRepair &&
        target.IsBroken && ResourceManager.Count(ResourceType.Wood) >= target.RepairWoodCost;

    private IEnumerator ExitThenRepair()
    {
        exiting = true;
        RepairableStructure target = building;
        RepairWorker repairer = worker;

        float radius = 0f;
        foreach (Creature creature in FindObjectsByType<Creature>(FindObjectsSortMode.None))
            if (creature && creature.NavMeshAgent)
                radius = Mathf.Max(radius, creature.NavMeshAgent.radius);

        Bounds area = footprint;
        area.Expand(new Vector3((radius + 0.1f) * 2f, 0f, (radius + 0.1f) * 2f));

        bool cleared = false;
        if (CanFinishRepair(target, repairer) && GetWorkerAgent())
        {
            yield return ClearArea(area, ok => cleared = ok);
            // if (cleared && CanFinishRepair(target, repairer))
            // {
            //     yield return PlayHammerAnimation(target);
            //     cleared = false;
            //     yield return ClearArea(area, ok => cleared = ok);
            //     if (cleared && CanFinishRepair(target, repairer) && target.TryRepair())
            //     {
            //         ClearBuilding();
            //         exiting = false;
            //         yield break;
            //     }
            // }
            if (cleared && CanFinishRepair(target, repairer) && target.TryRepair())
{
    RebuildNavMesh();

    ClearBuilding();
    exiting = false;
    yield break;
}
        }

        statusText.text = "Repair cancelled: not enough wood or a creature could not leave";
        exiting = false;
    }

    private IEnumerator ClearArea(Bounds area, System.Action<bool> finished)
    {
        HashSet<Creature> commanded = new HashSet<Creature>();
        float deadline = Time.time + exitTimeout;
        WaitForSeconds pause = new WaitForSeconds(0.1f);

        while (Time.time < deadline)
        {
            bool occupied = false;
            foreach (Creature creature in FindObjectsByType<Creature>(FindObjectsSortMode.None))
            {
                if (!creature || !creature.gameObject.activeInHierarchy) continue;
                if (!Inside(area, creature.transform.position))
                {
                    commanded.Remove(creature);
                    continue;
                }

                occupied = true;
                if (commanded.Contains(creature)) continue;
                NavMeshAgent agent = creature.NavMeshAgent;
                if (!agent || !agent.enabled || !agent.isOnNavMesh ||
                    !FindExit(agent, area, Mathf.Max(0.6f, agent.radius + 0.2f), out Vector3 exit) ||
                    !MoveCreature(creature, exit))
                {
                    Debug.LogWarning("Cannot safely move " + creature.name + " out of the building.");
                    finished(false);
                    yield break;
                }
                commanded.Add(creature);
            }

            if (!occupied) { finished(true); yield break; }
            yield return pause;
        }

        Debug.LogWarning("Repair cancelled: creatures did not leave in time.");
        finished(false);
    }

    private IEnumerator PlayHammerAnimation(RepairableStructure target)
    {
        if (!hammerPrefab || !target) yield break;

        Vector3 origin = new Vector3(footprint.center.x, footprint.max.y + 0.5f, footprint.center.z);
        GameObject hammer = Instantiate(hammerPrefab, origin, Quaternion.identity);
        Quaternion facing = cam ? cam.transform.rotation : Quaternion.identity;

        for (float time = 0f; time < hammerDuration && target; time += Time.deltaTime)
        {
            float swing = Mathf.Sin(time * 18f);
            hammer.transform.rotation = facing * Quaternion.Euler(0f, 0f, swing * 40f);
            hammer.transform.position = origin + Vector3.up * (Mathf.Abs(swing) * 0.15f);
            yield return null;
        }
        Destroy(hammer);
    }

    private void RebuildNavMesh()
    {
        foreach (NavMeshSurface surface in
            FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None))
        {
            if (surface.isActiveAndEnabled)
                surface.BuildNavMesh();
        }
    }
}