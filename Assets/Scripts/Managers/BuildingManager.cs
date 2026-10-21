using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.AI;

public class BuildingManager: MonoBehaviour { 
  [Header("Building Prefabs")]
  [SerializeField] private GameObject wheatFieldPrefab;
  [SerializeField] private GameObject animalPenPrefab;

  [Header("Building Costs")][SerializeField] private int woodCost = 5;
  [SerializeField] private int animalPenWoodCost = 10;

  [Header("References")][SerializeField] private Camera mainCamera;
  [SerializeField] private LayerMask groundLayer;

  [Header("Preview Colors")][SerializeField] private Color validColor = new Color(0f, 1f, 0f, 0.5f);

  [SerializeField] private Color invalidColor = new Color(1f, 0f, 0f, 0.5f);

  private GameObject selectedPrefab;
  private GameObject preview;

  private Renderer groundRenderer;
  private Material previewMaterial;

  private int selectedWoodCost;
  private bool building;
  private bool canBuild;
  
  // Singleton pattern to ensure only one instance of BuildingManager exists
  public static BuildingManager Instance { get; private set; }
  
  private void InitSingleton() {
    if (Instance != null && Instance != this) {
      Destroy(gameObject);
      return;
    }
    Instance = this;
  }
  
  private void Awake(){
    InitSingleton();
  }
  
  // Ensure that the instance is set in Start() as well, in case Awake() is called before this script is initialized.
  private void Start() {
    InitSingleton();
  }

  public void StartBuilding() {
    BeginBuilding(wheatFieldPrefab, woodCost, "Soil");
  }

  public void StartBuildingAnimalPen() {
    BeginBuilding(animalPenPrefab, animalPenWoodCost, "Ground");
  }

  private void BeginBuilding(
  GameObject prefab, int cost, string groundName) {
    if (building || prefab == null) return;

    if (mainCamera == null) {
      Debug.LogError("Main Camera is not assigned!");
      return;
    }

    if (ResourceManager.Count(ResourceType.Wood) < cost) {
      Debug.Log("Not enough Wood!");
      return;
    }

    preview = Instantiate(prefab);

    Transform ground = preview.transform.Find(groundName);

    if (ground == null || !ground.TryGetComponent(out Renderer renderer)) {
      Debug.LogError($"Missing Renderer on {groundName}!");

      ClearPreview();
      return;
    }

    selectedPrefab = prefab;
    selectedWoodCost = cost;
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
  }

  private void Update() {
    if (!building || Mouse.current == null) return;

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

  private bool CanPlace(Vector3 position) {
    Bounds bounds = groundRenderer.bounds;

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

  private void PlaceBuilding(Vector3 position) {
    if (!ResourceManager.Spend(ResourceType.Wood, selectedWoodCost)) {
      CancelBuilding();
      return;
    }

    GameObject prefabToPlace = selectedPrefab;

    ClearPreview();

    Instantiate(
    prefabToPlace, position, Quaternion.identity);

    Debug.Log($"{prefabToPlace.name} built!");
  }

  private void CancelBuilding() {
    ClearPreview();
  }

  private void ClearPreview() {
    if (preview != null) Destroy(preview);

    if (previewMaterial != null) Destroy(previewMaterial);

    preview = null;
    previewMaterial = null;
    groundRenderer = null;
    selectedPrefab = null;

    selectedWoodCost = 0;
    building = false;
    canBuild = false;
  }

  private void OnDisable() {
    ClearPreview();
  }
}