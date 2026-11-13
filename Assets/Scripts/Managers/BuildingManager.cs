using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class BuildingManager : MonoBehaviour
{
    [Header("Building Prefabs")]
    [SerializeField] private Buildable[] buildingPrefabs;

    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private NavMeshSurface navMeshSurface;

    [Header("Placement Settings")]
    [SerializeField] private float minBuildingSpacing = 1f;
    [SerializeField] private Color validColor = new Color(0, 1, 0, 0.5f);
    [SerializeField] private Color invalidColor = new Color(1, 0, 0, 0.5f);

    public static BuildingManager Instance { get; private set; }

    private readonly List<Bounds> occupiedBounds = new List<Bounds>();
    private Buildable preview;
    private GameObject selectedPrefab;
    private Renderer groundRenderer;
    private Material previewMaterial;
    private (ResourceType, int) selectedCostAndResource;
    private bool building, canBuild;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (!mainCamera) mainCamera = Camera.main;
        if (!navMeshSurface) navMeshSurface = FindFirstObjectByType<NavMeshSurface>();
        RefreshBuildings();
    }

    private void Start()
    {
        if (navMeshSurface) navMeshSurface.BuildNavMesh();
    }

    private void OnValidate()
    {
        if (!mainCamera) mainCamera = Camera.main;
        if (!navMeshSurface) navMeshSurface = FindFirstObjectByType<NavMeshSurface>();
    }

    private void RefreshBuildings()
    {
        occupiedBounds.Clear();
        foreach (GameObject obj in GameObject.FindGameObjectsWithTag("Building"))
            if (TryGetBounds(obj, out Bounds bounds)) occupiedBounds.Add(bounds);
    }

    public void StartBuilding(Buildable prefab)
    {
        if (building || !prefab) return;

        var cost = prefab.CostAndResource;
        if (ResourceManager.Count(cost.Item1) < cost.Item2)
        {
            Debug.Log($"Not enough {cost.Item1}!");
            return;
        }

        if (!mainCamera) mainCamera = Camera.main;
        if (!mainCamera) return;

        RefreshBuildings();
        if (!prefab.CompareTag("Building"))
            Debug.LogWarning($"Set the root of {prefab.name} to Tag = Building.");

        preview = Instantiate(prefab);
        Transform ground = preview.GroundElement;
        groundRenderer = ground ? ground.GetComponent<Renderer>() : null;

        if (!groundRenderer)
        {
            Debug.LogError($"{prefab.name} needs a renderer on GroundElement.");
            ClearPreview();
            return;
        }

        selectedPrefab = prefab.gameObject;
        selectedCostAndResource = cost;
        previewMaterial = new Material(groundRenderer.sharedMaterial);
        groundRenderer.material = previewMaterial;

        foreach (MonoBehaviour script in preview.GetComponentsInChildren<MonoBehaviour>())
            script.enabled = false;
        foreach (Collider collider in preview.GetComponentsInChildren<Collider>())
            collider.enabled = false;
        foreach (NavMeshObstacle obstacle in preview.GetComponentsInChildren<NavMeshObstacle>())
            obstacle.enabled = false;
        foreach (Canvas canvas in preview.GetComponentsInChildren<Canvas>())
            canvas.enabled = false;

        building = true;
        canBuild = false;
    }

    public void StartBuildingAnimalPen()
    {
        Debug.LogError("Use StartBuilding with the AnimalPen prefab instead.");
    }

    private void Update()
    {
        if (Instance != this || !building || Mouse.current == null) return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ClearPreview();
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        bool hitGround = Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer);
        canBuild = false;

        if (hitGround)
        {
            preview.transform.position = hit.point;
            canBuild = CanPlace(hit.point);
        }

        previewMaterial.color = canBuild ? validColor : invalidColor;

        if (Mouse.current.leftButton.wasPressedThisFrame &&
            !(EventSystem.current && EventSystem.current.IsPointerOverGameObject()))
        {
            if (canBuild) PlaceBuilding(hit.point);
            else Debug.LogWarning("Cannot build here!");
        }
    }
    private static bool TryGetBounds(GameObject obj, out Bounds bounds)
    {
        bounds = default;
        if (!obj) return false;

        Buildable buildable = obj.GetComponent<Buildable>();
        if (buildable && buildable.GroundElement)
        {
            Renderer ground = buildable.GroundElement.GetComponentInChildren<Renderer>(true);
            if (ground)
            {
                bounds = ground.bounds;
                return true;
            }
        }

        bool found = false;
        foreach (Renderer r in obj.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
            AddBounds(ref bounds, r.bounds, ref found);
        }
        foreach (NavMeshObstacle o in obj.GetComponentsInChildren<NavMeshObstacle>(true))
        {
            if (o.shape != NavMeshObstacleShape.Box) continue;
            Vector3 x = o.transform.TransformVector(Vector3.right * o.size.x * 0.5f);
            Vector3 z = o.transform.TransformVector(Vector3.forward * o.size.z * 0.5f);
            Bounds area = new Bounds(o.transform.TransformPoint(o.center), new Vector3(
                2 * (Mathf.Abs(x.x) + Mathf.Abs(z.x)), 1f,
                2 * (Mathf.Abs(x.z) + Mathf.Abs(z.z))));
            AddBounds(ref bounds, area, ref found);
        }

        if (!found)
            foreach (Collider col in obj.GetComponentsInChildren<Collider>(true))
                if (!col.isTrigger) AddBounds(ref bounds, col.bounds, ref found);

        return found;
    }

    private static void AddBounds(ref Bounds all, Bounds next, ref bool found)
    {
        if (found) all.Encapsulate(next);
        else { all = next; found = true; }
    }

    private bool TooCloseToBuilding(Bounds bounds)
    {
        foreach (Bounds other in occupiedBounds)
        {
            float dx = Mathf.Max(0f, Mathf.Max(bounds.min.x - other.max.x,
                                               other.min.x - bounds.max.x));
            float dz = Mathf.Max(0f, Mathf.Max(bounds.min.z - other.max.z,
                                               other.min.z - bounds.max.z));
            if (dx * dx + dz * dz < minBuildingSpacing * minBuildingSpacing)
                return true;
        }
        return false;
    }

    private bool CanPlace(Vector3 position)
    {
        Bounds b = groundRenderer.bounds;
        if (TooCloseToBuilding(b)) return false;

        float x = Mathf.Max(0, b.extents.x - 0.1f);
        float z = Mathf.Max(0, b.extents.z - 0.1f);
        Vector3[] corners = { new Vector3(-x, 0, -z), new Vector3(x, 0, -z),
                              new Vector3(-x, 0, z), new Vector3(x, 0, z) };

        foreach (Vector3 corner in corners)
        {
            if (!Physics.Raycast(b.center + corner + Vector3.up * 10f,
                                 Vector3.down, out RaycastHit hit, 20f, groundLayer) ||
                Mathf.Abs(hit.point.y - position.y) > 0.2f) return false;
        }

        Vector3 center = new Vector3(b.center.x, position.y + 1f, b.center.z);
        Vector3 half = new Vector3(b.extents.x, 0.95f, b.extents.z);
        foreach (Collider col in Physics.OverlapBox(center, half, Quaternion.identity,
                                                    ~0, QueryTriggerInteraction.Ignore))
        {
            if (col.transform.IsChildOf(preview.transform)) continue;
            if (((1 << col.gameObject.layer) & groundLayer.value) != 0) continue;
            return false;
        }
        return true;
    }

    private void PlaceBuilding(Vector3 position)
    {
        if (!ResourceManager.Spend(selectedCostAndResource.Item1, selectedCostAndResource.Item2))
        {
            ClearPreview();
            return;
        }

        GameObject prefab = selectedPrefab;
        ClearPreview();
        GameObject placed = Instantiate(prefab, position, Quaternion.identity);
        if (placed.CompareTag("Building") && TryGetBounds(placed, out Bounds bounds))
            occupiedBounds.Add(bounds);
        StartCoroutine(RefreshNavMesh());
    }

    private void ClearPreview()
    {
        if (preview)
        {
            preview.gameObject.SetActive(false);
            Destroy(preview.gameObject);
        }
        if (previewMaterial) Destroy(previewMaterial);

        preview = null;
        selectedPrefab = null;
        previewMaterial = null;
        groundRenderer = null;
        building = canBuild = false;
    }

    private void OnDisable() => ClearPreview();

    private IEnumerator RefreshNavMesh()
    {
        yield return null;
        if (!navMeshSurface) yield break;

        if (!navMeshSurface.navMeshData) navMeshSurface.BuildNavMesh();
        else yield return navMeshSurface.UpdateNavMesh(navMeshSurface.navMeshData);
    }
}