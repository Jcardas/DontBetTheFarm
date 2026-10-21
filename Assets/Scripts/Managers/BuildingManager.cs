using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;


public class BuildingManager: MonoBehaviour { 
  [Header("Building Prefabs")]
  [SerializeField] private Buildable[] buildingPrefabs;
  
  // Replaced by a list of building prefabs for easier management
  // [SerializeField] private GameObject wheatFieldPrefab;
  // [SerializeField] private GameObject animalPenPrefab;

  [Header("References")][SerializeField] private Camera mainCamera;
  [SerializeField] private LayerMask groundLayer;

  [Header("Placement Settings")]
  [SerializeField] private float minBuildingSpacing = 1f;

  [Header("Preview Colors")][SerializeField] private Color validColor = new Color(0f, 1f, 0f, 0.5f);

  [SerializeField] private Color invalidColor = new Color(1f, 0f, 0f, 0.5f);

  [SerializeField] private NavMeshSurface navMeshSurface;

  private GameObject selectedPrefab;
  private Buildable preview;

  private Renderer groundRenderer;
  private Material previewMaterial;

  private (ResourceType, int) selectedCostAndResource;
  private bool building;
  private bool canBuild;
  
  // Frame check to prevent double placing
  private int startBuildingFrame;
  
  // Singleton pattern to ensure only one instance of BuildingManager exists
  public static BuildingManager Instance { get; private set; }
  
  private void InitSingleton() {
    if (Instance != null && Instance != this) {
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
    foreach (Buildable prefab in buildingPrefabs)
    {
      if (prefab == null)
      {
        Debug.LogError("Building prefab \"" + prefab.name + "\" is not assigned!");
        continue;
      }

      if (prefab.GetComponent<Buildable>() == null)
      {
        Debug.LogError("Building prefab \"" + prefab.name + "\" does not have a Buildable component!");
        continue;
      }
    }
  }

  private void Awake(){
    InitSingleton();
  }
  
  // accepts one argument for the prefab to build
  public void StartBuilding(Buildable prefab) {
    BeginBuilding(prefab, prefab.GetComponent<Buildable>().CostAndResource);
  }

  // Deprecated method for starting to build a wheat field, kept for backward compatibility
  public void StartBuildingAnimalPen() {
    // BeginBuilding(animalPenPrefab, animalPenWoodCost, "Ground");
    Debug.LogError("StartBuildingAnimalPen is deprecated. Use StartBuilding with the appropriate prefab instead.");
  }

  // Instead of int cost, get the cost and material type from the Buildable class
  private void BeginBuilding(Buildable buildable, (ResourceType, int) costAndResource) 
  {
    if (building || buildable == null) {
      Debug.LogWarning("Already building or prefab is null!");
      return;
    }

    if (mainCamera == null) {
      Debug.LogError("Main Camera is not assigned!");
      return;
    }

    // Check if the player has enough resources to build the structure
    if (ResourceManager.Count(costAndResource.Item1) < costAndResource.Item2) {
      Debug.Log($"Not enough {costAndResource.Item1}!");
      return;
    }

    preview = Instantiate(buildable.gameObject).GetComponent<Buildable>();
    
    Transform ground = preview.GroundElement; // Instead of searching for a child named "Ground",
                                             // we use the GroundElement property from the Buildable class

    if (ground == null || !ground.TryGetComponent(out Renderer renderer)) {
      Debug.LogError($"Missing Renderer on " + preview.name + "!");

      ClearPreview();
      return;
    }

    selectedPrefab = buildable.gameObject;
    selectedCostAndResource = costAndResource;
    groundRenderer = renderer;

    previewMaterial = new Material(renderer.sharedMaterial);
    renderer.material = previewMaterial;

    foreach(MonoBehaviour script in preview.GetComponentsInChildren < MonoBehaviour > ()) {
      script.enabled = false;
    }

    foreach(Collider col in preview.GetComponentsInChildren < Collider > ()) {
      col.enabled = false;
    }

    foreach(NavMeshObstacle obstacle in preview.GetComponentsInChildren < NavMeshObstacle > ()) {
      obstacle.enabled = false;
    }

    foreach(Canvas canvas in preview.GetComponentsInChildren < Canvas > ()) {
      canvas.enabled = false;
    }

    building = true;
    canBuild = false;
  
    startBuildingFrame = Time.frameCount; // Record the frame when building started to prevent immediate placement
  }

  private void Update() {
    if (!building || Mouse.current == null) return;
    
    // Prevent placing the building on the same frame it was started
    if (Time.frameCount == startBuildingFrame) return;

    if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) {
      CancelBuilding();
      return;
    }

    Ray ray = mainCamera.ScreenPointToRay(
    Mouse.current.position.ReadValue());

    bool hitGround = Physics.Raycast(
    ray, out RaycastHit hit, 100f, groundLayer);

    canBuild = false;

    if (hitGround) {
      preview.transform.position = hit.point;
      canBuild = CanPlace(hit.point);
    }

    previewMaterial.color = canBuild ? validColor: invalidColor;

    if (Mouse.current.leftButton.wasPressedThisFrame) {
      if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) {
        return;
      }

      if (canBuild) PlaceBuilding(hit.point);
      else Debug.Log("Cannot build here!");
    }
  }


  private bool IsTooCloseToBuilding(Bounds newBounds)
  {
      GameObject[] buildings = GameObject.FindGameObjectsWithTag("Building");

      foreach (GameObject building in buildings)
      {
          if (building == preview)
              continue;

          Collider[] colliders =
              building.GetComponentsInChildren<Collider>();

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

  private bool CanPlace(Vector3 position) {
    Bounds bounds = groundRenderer.bounds;

    if (IsTooCloseToBuilding(bounds))
      return false;

    float halfX = Mathf.Max(0f, bounds.extents.x - 0.1f);
    float halfZ = Mathf.Max(0f, bounds.extents.z - 0.1f);

    Vector3[] corners = {
      new Vector3( - halfX, 0, -halfZ),
      new Vector3(halfX, 0, -halfZ),
      new Vector3( - halfX, 0, halfZ),
      new Vector3(halfX, 0, halfZ)
    };

    foreach(Vector3 corner in corners) {
      Vector3 start = bounds.center + corner + Vector3.up * 10f;

      if (!Physics.Raycast(start, Vector3.down, out RaycastHit groundHit, 20f, groundLayer)) {
        return false;
      }

      if (Mathf.Abs(groundHit.point.y - position.y) > 0.2f) return false;
    }

    Vector3 center = new Vector3(
    bounds.center.x, position.y + 1f, bounds.center.z);

    Vector3 halfSize = new Vector3(
    bounds.extents.x, 0.95f, bounds.extents.z);

    Collider[] overlaps = Physics.OverlapBox(
    center, halfSize, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);

    foreach(Collider col in overlaps) {
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

    if (!ResourceManager.Spend( selectedCostAndResource.Item1, selectedCostAndResource.Item2))
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

    StartCoroutine(RefreshNavMesh());
  }


  private void CancelBuilding() {
    ClearPreview();
  }

  private void ClearPreview() {
    if (preview != null) Destroy(preview.gameObject);

    if (previewMaterial != null) Destroy(previewMaterial);

    preview = null;
    previewMaterial = null;
    groundRenderer = null;
    selectedPrefab = null;
    
    building = false;
    canBuild = false;
  }

  private void OnDisable() {
    ClearPreview();
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
          yield return navMeshSurface.UpdateNavMesh(
              navMeshSurface.navMeshData
          );
      }

      Debug.Log("NavMesh updated!");
  }

}