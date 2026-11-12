using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
        repairPanel.gameObject.SetActive(false);
        repairButton.onClick.AddListener(RepairSelected);
        repairButton.interactable = false;
    }

    private void OnDestroy() => repairButton.onClick.RemoveListener(RepairSelected);

    private void LateUpdate()
    {
        if (!exiting)
        {
            foreach (Selectable unit in Selectable.All)
            {
                if (!unit || !unit.Selected) continue;
                var chosen = unit.GetComponent<RepairWorker>();
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
            int wood = ResourceManager.Count(ResourceType.Wood);
            bool enoughWood = wood >= cost;

            
            repairButton.interactable = enoughWood;

            repairButtonText.text = enoughWood
                ? $"Repair ({cost} Wood)"
                : "Collect Wood First";


            statusText.text = enoughWood
                ? "Ready to repair"
                : $"Not enough wood! Need {cost - wood} more.";

        }
        else
        {
            repairButton.interactable = false;
        }
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

    private void HandleClick()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
        if (!worker || !worker.CanRepair || !cam) return;
        if (EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        RepairableStructure best = null;
        float bestDist = float.PositiveInfinity;

        foreach (var s in FindObjectsByType<RepairableStructure>(FindObjectsSortMode.None))
        {
            if (!s.IsBroken || !GetFootprint(s, out Bounds b)) continue;
            b.Expand(0.3f);
            if (b.IntersectRay(ray, out float d) && d < bestDist) { bestDist = d; best = s; }
        }

        if (best && best != building) SelectBuilding(best);
    }

    public void SelectBuilding(RepairableStructure found)
    {
        if (!worker || !worker.CanRepair || !found || !found.IsBroken) return;
        if (!GetFootprint(found, out Bounds bounds)) return;

        building = found;
        footprint = bounds;
        if (InRange()) return;

        var agent = GetAgent();
        if (agent && FindExit(agent, footprint, Mathf.Min(1.5f, repairRange * 0.5f), out Vector3 pos))
            SendWorkerTo(pos);
        else
            Debug.LogWarning("No reachable repair position for " + found.name);
    }

    private void ClearBuilding()
    {
        building = null;
        repairPanel.gameObject.SetActive(false);
    }

    private static bool GetFootprint(RepairableStructure s, out Bounds area)
    {
        if (s is Buildable b && b.GroundElement)
        {
            var ground = b.GroundElement.GetComponentInChildren<Renderer>(true);
            if (ground) { area = ground.bounds; return true; }
        }

        bool found = false;
        area = default;
        foreach (var r in s.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (found) area.Encapsulate(r.bounds); else { area = r.bounds; found = true; }
        }
        return found;
    }

    private static bool Inside(Bounds b, Vector3 p, float extra = 0f) =>
        p.x >= b.min.x - extra && p.x <= b.max.x + extra &&
        p.z >= b.min.z - extra && p.z <= b.max.z + extra;

    private bool InRange()
    {
        if (!building || !worker || !worker.CanRepair) return false;
        Vector3 p = worker.transform.position;
        float x = Mathf.Max(0f, footprint.min.x - p.x, p.x - footprint.max.x);
        float z = Mathf.Max(0f, footprint.min.z - p.z, p.z - footprint.max.z);
        return x * x + z * z <= repairRange * repairRange;
    }

    private bool PlacePanel()
    {
        if (!cam) return false;
        Vector3 s = cam.WorldToScreenPoint(new Vector3(footprint.center.x, footprint.max.y, footprint.center.z));
        if (s.z <= 0f) return false;

        Vector2 size = Vector2.Scale(repairPanel.rect.size, repairPanel.lossyScale);
        float x = Mathf.Clamp(s.x, size.x * 0.5f + screenPadding, Screen.width - size.x * 0.5f - screenPadding);
        float y = Mathf.Clamp(s.y + panelOffset, screenPadding, Screen.height - size.y - screenPadding);
        repairPanel.position = new Vector3(x, y, 0f);
        return true;
    }

    private NavMeshAgent GetAgent()
    {
        var creature = worker ? worker.GetComponent<Creature>() : null;
        var a = creature ? creature.NavMeshAgent : null;
        return a && a.enabled && a.isOnNavMesh ? a : null;
    }

    private static bool FindExit(NavMeshAgent agent, Bounds b, float margin, out Vector3 best)
    {
        Vector3 p = agent.transform.position;
        float cx = Mathf.Clamp(p.x, b.min.x, b.max.x);
        float cz = Mathf.Clamp(p.z, b.min.z, b.max.z);
        Vector3[] options =
        {
            new(b.min.x - margin, p.y, cz), new(b.max.x + margin, p.y, cz),
            new(cx, p.y, b.min.z - margin), new(cx, p.y, b.max.z + margin)
        };

        best = p;
        float shortest = float.PositiveInfinity;
        var path = new NavMeshPath();
        foreach (var o in options)
        {
            if (!NavMesh.SamplePosition(o, out NavMeshHit hit, 1.5f, agent.areaMask) ||
                Inside(b, hit.position, 0.01f) ||
                !agent.CalculatePath(hit.position, path) ||
                path.status != NavMeshPathStatus.PathComplete) continue;

            float d = Vector3.Distance(p, hit.position);
            if (d < shortest) { shortest = d; best = hit.position; }
        }
        return shortest < float.PositiveInfinity;
    }

    private bool SendWorkerTo(Vector3 destination)
    {
        var agent = GetAgent();
        var creature = worker ? worker.GetComponent<Creature>() : null;
        if (!agent || !creature) return false;

        if (creature.CreatureBrain) creature.CreatureBrain.Obey();
        agent.isStopped = false;
        if (!agent.SetDestination(destination)) return false;
        if (creature.CreatureBrain) creature.CreatureBrain.SetGuardPost(destination);
        return true;
    }

    
    public void RepairSelected()
    {
        if (exiting || !building || !InRange()) return;

        int wood = ResourceManager.Count(ResourceType.Wood);

        if (wood < building.RepairWoodCost)
        {
            statusText.text = "Not enough wood! Collect wood first.";
            return;
        }

        StartCoroutine(ExitThenRepair());
    }


    private IEnumerator ExitThenRepair()
    {
        exiting = true;
        var target = building;
        var repairer = worker;
        var agent = GetAgent();
        bool safe = agent && target && repairer;

        if (safe)
        {
            Bounds area = footprint;
            area.Expand(new Vector3(agent.radius * 2 + 0.2f, 0, agent.radius * 2 + 0.2f));

            if (Inside(area, repairer.transform.position))
            {
                safe = FindExit(agent, area, 0.6f, out Vector3 exit) && SendWorkerTo(exit);
                float end = Time.time + exitTimeout;
                while (safe && repairer && Inside(area, repairer.transform.position) && Time.time < end)
                    yield return null;
                safe = safe && repairer && !Inside(area, repairer.transform.position);
            }
        }

       
        if (safe && target && building == target && worker == repairer &&
            target.IsBroken &&
            ResourceManager.Count(ResourceType.Wood) >= target.RepairWoodCost)
        {
            yield return PlayHammerAnimation(target);

            if (target && building == target && worker == repairer &&
                target.TryRepair())
            {
                ClearBuilding();
            }
            else
            {
                statusText.text = "Repair cancelled";
            }
        }
        else
        {
            statusText.text = "Repair cancelled: not enough wood or no safe exit";
        }



        exiting = false;
    }


    private IEnumerator PlayHammerAnimation(RepairableStructure target)
    {
        if (!hammerPrefab || !target) yield break;

        Vector3 position = new Vector3(
            footprint.center.x,
            footprint.max.y + 0.5f,
            footprint.center.z
        );

        GameObject hammer = Instantiate(hammerPrefab, position, Quaternion.identity);
        Quaternion facing = cam ? cam.transform.rotation : Quaternion.identity;

        float timer = 0f;

        while (timer < hammerDuration && target)
        {
            timer += Time.deltaTime;

            float swing = Mathf.Sin(timer * 18f);

            hammer.transform.rotation =
                facing * Quaternion.Euler(0, 0, swing * 40f);

            hammer.transform.position =
                position + Vector3.up * (Mathf.Abs(swing) * 0.15f);

            yield return null;
        }

        Destroy(hammer);
    }

}