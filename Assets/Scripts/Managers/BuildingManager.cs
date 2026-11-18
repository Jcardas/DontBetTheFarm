using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;

public class BuildingManager : MonoBehaviour
{
    [Header("Building Prefabs")]
    [SerializeField] private Buildable[] buildingPrefabs;

    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask groundLayer;

    [Header("Placement Settings")]
    [SerializeField] private float minBuildingSpacing = 1f;

    [Header("Preview Colors")]
    [SerializeField] private Color validColor = new Color(0f, 1f, 0f, 0.5f);
    [SerializeField] private Color invalidColor = new Color(1f, 0f, 0f, 0.5f);

    [SerializeField] private NavMeshSurface navMeshSurface;

    // List of all buildings in the scene, used for checking spacing between buildings
    private List<Buildable> buildings = new List<Buildable>();
    public IReadOnlyList<Buildable> Buildings => buildings;

    private GameObject selectedPrefab;
    private Buildable preview;

    private Renderer groundRenderer;
    private Material previewMaterial;

    private (ResourceType, int) selectedCostAndResource;
    private bool building;
    private bool canBuild;

    // Demolish mode state
    private bool demolishing;
    private Buildable hoveredDemolishTarget;
    private Highlight hoveredDemolishHighlight;

    public bool IsBuilding => building;
    public bool IsDemolishing => demolishing;
    public bool IsBusy => building || demolishing;

    // Singleton pattern to ensure only one instance of BuildingManager exists
    public static BuildingManager Instance { get; private set; }

    private void Start()
    {
        if (navMeshSurface != null)
            navMeshSurface.BuildNavMesh();
    }

    private void InitSingleton()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // initialize main camera and navMeshSurface if they are not assigned in the inspector
    private void OnValidate()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (navMeshSurface == null)
        {
            navMeshSurface = FindFirstObjectByType<NavMeshSurface>();
        }

        // Check to make sure the prefabs are all buildable and have a Buildable component
        if (buildingPrefabs == null || buildingPrefabs.Length <= 0) return;
        foreach (Buildable prefab in buildingPrefabs)
        {
            if (prefab == null)
            {
                Debug.LogError("Building prefab is not assigned!");
                continue;
            }

            if (prefab.GetComponent<Buildable>() != null) continue;
            Debug.LogError("Building prefab \"" + prefab.name + "\" does not have a Buildable component!");
        }
    }

    private void Awake()
    {
        InitSingleton();

        // Check if there are buildings already in the scene and register them
        Buildable[] existingBuildings = FindObjectsByType<Buildable>(FindObjectsSortMode.None);
        foreach (Buildable existing in existingBuildings)
        {
            RegisterBuilding(existing);
        }
    }

    public void RegisterBuilding(Buildable buildingToRegister)
    {
        if (buildingToRegister != null && !buildings.Contains(buildingToRegister))
        {
            buildings.Add(buildingToRegister);
        }
    }

    public void UnregisterBuilding(Buildable buildingToRemove)
    {
        if (buildingToRemove != null && buildings.Contains(buildingToRemove))
        {
            buildings.Remove(buildingToRemove);
        }
    }

    public void RequestNavMeshRefresh()
    {
        if (isActiveAndEnabled)
        {
            StartCoroutine(RefreshNavMesh());
        }
    }

    // accepts one argument for the prefab to build
    public void StartBuilding(Buildable prefab)
    {
        CancelDemolishMode();
        BeginBuilding(prefab, prefab.GetComponent<Buildable>().CostAndResource);
    }

    public void ToggleDemolishMode()
    {
        if (demolishing) CancelDemolishMode();
        else StartDemolishMode();
    }

    public void StartDemolishMode()
    {
        CancelBuilding();
        ContextMenuEventBus.RequestClose();
        demolishing = true;
    }

    public void CancelDemolishMode()
    {
        demolishing = false;
        ClearDemolishHover();
    }

    // Deprecated method for starting to build a wheat field, kept for backward compatibility
    public void StartBuildingAnimalPen()
    {
        Debug.LogError("StartBuildingAnimalPen is deprecated. Use StartBuilding with the appropriate prefab instead.");
    }

    // Instead of int cost, get the cost and material type from the Buildable class
    private void BeginBuilding(Buildable buildable, (ResourceType, int) costAndResource)
    {
        if (building || buildable == null)
        {
            Debug.LogWarning("Already building or prefab is null!");
            return;
        }

        if (mainCamera == null)
        {
            Debug.LogError("Main Camera is not assigned!");
            return;
        }

        // Check if the player has enough resources to build the structure
        if (ResourceManager.Count(costAndResource.Item1) < costAndResource.Item2)
        {
            Debug.Log($"Not enough {costAndResource.Item1}!");
            return;
        }

        preview = Instantiate(buildable.gameObject).GetComponent<Buildable>();

        Transform ground = preview.GroundElement;

        if (ground == null || !ground.TryGetComponent(out Renderer renderer))
        {
            Debug.LogError($"Missing Renderer on " + preview.name + "!");

            ClearPreview();
            return;
        }

        selectedPrefab = buildable.gameObject;
        selectedCostAndResource = costAndResource;
        groundRenderer = renderer;

        previewMaterial = new Material(renderer.sharedMaterial);
        renderer.material = previewMaterial;

        foreach (MonoBehaviour script in preview.GetComponentsInChildren<MonoBehaviour>())
        {
            script.enabled = false;
        }

        foreach (Collider col in preview.GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }

        foreach (NavMeshObstacle obstacle in preview.GetComponentsInChildren<NavMeshObstacle>())
        {
            obstacle.enabled = false;
        }

        foreach (Canvas canvas in preview.GetComponentsInChildren<Canvas>())
        {
            canvas.enabled = false;
        }

        building = true;
        canBuild = false;
    }

    private void Update()
    {
        if (Instance != this || Mouse.current == null)
            return;

        if (demolishing)
        {
            UpdateDemolishMode();
            return;
        }

        if (!building)
            return;

        if ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            Mouse.current.rightButton.wasPressedThisFrame)
        {
            CancelBuilding();
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        bool hitGround = Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer);

        canBuild = false;

        if (hitGround)
        {
            preview.transform.position = hit.point;
            canBuild = CanPlace(hit.point, hit.normal);
        }

        previewMaterial.color = canBuild ? validColor : invalidColor;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (canBuild) PlaceBuilding(hit.point);
            else Debug.LogWarning("Cannot build here!");
        }
    }

    private void UpdateDemolishMode()
    {
        if ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            Mouse.current.rightButton.wasPressedThisFrame)
        {
            CancelDemolishMode();
            return;
        }

        if (mainCamera == null) return;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        if (UIBlocker.IsPointerOverUI(screenPos))
        {
            ClearDemolishHover();
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        Buildable target = FindRemovableBuildingAt(ray);
        SetDemolishHover(target);

        if (Mouse.current.leftButton.wasPressedThisFrame && target != null)
        {
            ClearDemolishHover();
            target.Remove();
            // Stay in demolish mode so multiple buildings can be demolished consecutively
        }
    }

    private Buildable FindRemovableBuildingAt(Ray ray)
    {
        Buildable broken = Order.BrokenAt(ray);
        if (broken != null && broken.CanBeRemoved)
            return broken;

        if (MouseTarget.Find(ray, out RaycastHit hit))
        {
            Buildable b = MouseTarget.Building(ray, hit) ?? hit.collider.GetComponentInParent<Buildable>();
            if (b != null && b.CanBeRemoved)
                return b;
        }

        return null;
    }

    private void SetDemolishHover(Buildable target)
    {
        if (target == hoveredDemolishTarget) return;

        ClearDemolishHover();
        hoveredDemolishTarget = target;

        if (target != null)
        {
            if (!target.TryGetComponent(out hoveredDemolishHighlight))
                hoveredDemolishHighlight = target.gameObject.AddComponent<Highlight>();
            hoveredDemolishHighlight.Hovered = true;
        }
    }

    private void ClearDemolishHover()
    {
        if (hoveredDemolishHighlight != null)
        {
            hoveredDemolishHighlight.Hovered = false;
            hoveredDemolishHighlight = null;
        }
        hoveredDemolishTarget = null;
    }

    private bool IsTooCloseToBuilding(Bounds newBounds)
    {
        foreach (Buildable existing in buildings)
        {
            if (existing == null || existing == preview)
                continue;

            Collider[] colliders = existing.GetComponentsInChildren<Collider>();

            Bounds existingBounds = default;
            bool found = false;

            foreach (Collider col in colliders)
            {
                if (!col.enabled || col.isTrigger)
                    continue;

                if (!found)
                {
                    existingBounds = col.bounds;
                    found = true;
                }
                else
                {
                    existingBounds.Encapsulate(col.bounds);
                }
            }

            if (!found) continue;

            float gapX = Mathf.Max(
                0f,
                Mathf.Max(
                    newBounds.min.x - existingBounds.max.x,
                    existingBounds.min.x - newBounds.max.x
                )
            );

            float gapZ = Mathf.Max(
                0f,
                Mathf.Max(
                    newBounds.min.z - existingBounds.max.z,
                    existingBounds.min.z - newBounds.max.z
                )
            );

            float distance = Mathf.Sqrt(gapX * gapX + gapZ * gapZ);

            if (distance < minBuildingSpacing)
                return true;
        }

        return false;
    }

    private bool CanPlace(Vector3 position, Vector3 surfaceNormal)
    {
        // CHECKS SLOPES, rejects being placed on ramps.
        if (Vector3.Angle(surfaceNormal, Vector3.up) > 2f)
        {
            return false;
        }

        Bounds bounds = groundRenderer.bounds;

        if (IsTooCloseToBuilding(bounds))
            return false;

        float halfX = Mathf.Max(0f, bounds.extents.x - 0.1f);
        float halfZ = Mathf.Max(0f, bounds.extents.z - 0.1f);

        Vector3[] corners =
        {
            new Vector3(-halfX, 0, -halfZ),
            new Vector3(halfX, 0, -halfZ),
            new Vector3(-halfX, 0, halfZ),
            new Vector3(halfX, 0, halfZ)
        };

        // CHECKS CLIFFS, and makes sure no corners are hanging off.
        foreach (Vector3 cornerOffset in corners)
        {
            Vector3 cornerBase = position + cornerOffset;
            Vector3 start = cornerBase + Vector3.up * 10f;

            if (!Physics.Raycast(start, Vector3.down, out RaycastHit groundHit, 20f, groundLayer))
            {
                return false; // hanging over the void!
            }

            // Check if corner ground height is apx. same as center
            if (Mathf.Abs(groundHit.point.y - position.y) > 0.2f)
                return false;
        }

        // CHECKS OVERLAP
        Vector3 center = new Vector3(position.x, position.y + bounds.extents.y, position.z);
        Vector3 halfSize = new Vector3(halfX, Mathf.Max(0.1f, bounds.extents.y - 0.1f), halfZ);

        // Shrinks overlap box to avoid clipping into ground.
        Collider[] overlaps = Physics.OverlapBox(
            center, halfSize, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);

        foreach (Collider col in overlaps)
        {
            if (col.transform.IsChildOf(preview.transform)) continue;
            if (((1 << col.gameObject.layer) & groundLayer.value) != 0) continue;

            return false;
        }

        return true;
    }

    private void PlaceBuilding(Vector3 position)
    {
        building = false;
        canBuild = false;

        if (!ResourceManager.Spend(selectedCostAndResource.Item1, selectedCostAndResource.Item2))
        {
            CancelBuilding();
            return;
        }

        GameObject prefabToPlace = selectedPrefab;

        ClearPreview();

        GameObject newBuilding = Instantiate(
            prefabToPlace,
            position,
            Quaternion.identity
        );

        Debug.Log($"[Instance: {GetInstanceID()}] {newBuilding.name} built!");

        RegisterBuilding(newBuilding.GetComponent<Buildable>());

        StartCoroutine(RefreshNavMesh());
    }

    private void CancelBuilding()
    {
        ClearPreview();
    }

    private void ClearPreview()
    {
        if (preview != null)
        {
            preview.gameObject.SetActive(false);
            Destroy(preview.gameObject);
        }

        if (previewMaterial != null) Destroy(previewMaterial);

        preview = null;
        previewMaterial = null;
        groundRenderer = null;
        selectedPrefab = null;

        building = false;
        canBuild = false;
    }

    private void OnDisable()
    {
        ClearPreview();
        CancelDemolishMode();
    }

    private IEnumerator RefreshNavMesh()
    {
        yield return null;

        if (navMeshSurface == null)
        {
            Debug.LogError("NavMeshSurface is not assigned!");
            yield break;
        }

        if (navMeshSurface.navMeshData == null)
        {
            navMeshSurface.BuildNavMesh();
        }
        else
        {
            yield return navMeshSurface.UpdateNavMesh(navMeshSurface.navMeshData);
        }

        Debug.Log("NavMesh updated!");
    }
}