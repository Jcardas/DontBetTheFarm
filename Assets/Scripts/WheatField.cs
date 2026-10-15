using System.Collections.Generic;
using UnityEngine;

public class WheatField: Buildable { [Header("Wheat Growth")][SerializeField] private float growTime = 15f;
  [SerializeField] private float startHeight = 0.1f;
  [SerializeField] private float finalHeight = 0.6f;

  [Header("Field")][SerializeField] private GameObject soil;
  [SerializeField] private float spacing = 0.3f;
  [SerializeField] private float edgePadding = 0.15f;

  [Header("Player Interaction")][SerializeField] private Transform player;
  [SerializeField] private float harvestDistance = 2f;

  [Header("UI")][SerializeField] private GameObject harvestButton;

  private bool planted = false;
  public bool mature = false;

  private float growthProgress = 0f;

  private List < GameObject > wheatPlants = new List < GameObject > ();

  void Start() {
    buildableName = "Wheat Field";

    PositionHarvestIcon();
    if (harvestButton != null) {
      harvestButton.SetActive(false);
    }
    if (player == null) {
      GameObject playerObject = GameObject.Find("Farmer");

      if (playerObject != null) player = playerObject.transform;
    }

    StartNewCrop();
  }

  void Update() {
    if (planted && !mature) {
      GrowWheat();
    }

    CheckPlayerDistance();
  }

  void PositionHarvestIcon() {
    if (soil == null || harvestButton == null) {
      return;
    }

    Renderer soilRenderer = soil.GetComponent < Renderer > ();

    if (soilRenderer == null) {
      return;
    }

    Bounds bounds = soilRenderer.bounds;

    Vector3 iconPosition = new Vector3(
    bounds.center.x, bounds.max.y + 1.2f, bounds.center.z);

    harvestButton.transform.position = iconPosition;
  }

  public override void Build() {
    Debug.Log("Wheat Field ready.");
  }

  void StartNewCrop() {
    if (planted || soil == null) {
      return;
    }

    CreateWheat();

    planted = true;
    mature = false;
    growthProgress = 0f;

    Debug.Log("Wheat started growing.");
  }

  void CreateWheat() {
    Renderer soilRenderer = soil.GetComponent < Renderer > ();

    if (soilRenderer == null) {
      Debug.Log("Soil needs a Renderer component.");
      return;
    }

    Bounds bounds = soilRenderer.bounds;

    float topY = bounds.max.y;

    for (float x = bounds.min.x + edgePadding;
    x <= bounds.max.x - edgePadding;
    x += spacing) {
      for (float z = bounds.min.z + edgePadding;
      z <= bounds.max.z - edgePadding;
      z += spacing) {
        Vector3 position = new Vector3(
        x, topY, z);

        CreateWheatPlant(position);
      }
    }
  }

  void CreateWheatPlant(Vector3 position) {
    GameObject wheatPlant = new GameObject("WheatPlant");

    wheatPlant.transform.SetParent(transform);

    wheatPlant.transform.position = position;

    GameObject stem = GameObject.CreatePrimitive(
    PrimitiveType.Cube);

    stem.name = "Stem";

    stem.transform.SetParent(
    wheatPlant.transform);

    stem.transform.localScale = new Vector3(
    0.06f, startHeight, 0.06f);

    stem.transform.localPosition = new Vector3(
    0f, startHeight / 2f, 0f);

    stem.GetComponent < Renderer > ().material.color = new Color(
    0.45f, 0.72f, 0.25f);

    GameObject head = GameObject.CreatePrimitive(
    PrimitiveType.Capsule);

    head.name = "Head";

    head.transform.SetParent(
    wheatPlant.transform);

    head.transform.localScale = new Vector3(
    0.08f, 0.04f, 0.08f);

    head.transform.localPosition = new Vector3(
    0f, startHeight + 0.04f, 0f);

    head.GetComponent < Renderer > ().material.color = new Color(
    0.45f, 0.72f, 0.25f);

    wheatPlants.Add(wheatPlant);
  }

  void GrowWheat() {
    growthProgress += Time.deltaTime / growTime;

    growthProgress = Mathf.Clamp01(
    growthProgress);

    float currentHeight = Mathf.Lerp(
    startHeight, finalHeight, growthProgress);

    float headSize = Mathf.Lerp(
    0.04f, 0.10f, growthProgress);

    foreach(GameObject wheatPlant in wheatPlants) {
      Transform stem = wheatPlant.transform.Find("Stem");

      Transform head = wheatPlant.transform.Find("Head");

      // Grow stem
      stem.localScale = new Vector3(
      0.06f, currentHeight, 0.06f);

      stem.localPosition = new Vector3(
      0f, currentHeight / 2f, 0f);

      // Grow head
      head.localScale = new Vector3(
      0.08f, headSize, 0.08f);

      head.localPosition = new Vector3(
      0f, currentHeight + headSize, 0f);
    }

    if (growthProgress >= 1f) {
      MakeGolden();
    }
  }

  void MakeGolden() {
    Color golden = new Color(
    0.93f, 0.78f, 0.28f);

    foreach(GameObject wheatPlant in wheatPlants) {
      Renderer stemRenderer = wheatPlant.transform.Find("Stem").GetComponent < Renderer > ();

      Renderer headRenderer = wheatPlant.transform.Find("Head").GetComponent < Renderer > ();

      stemRenderer.material.color = golden;

      headRenderer.material.color = golden;
    }

    mature = true;

    Debug.Log("Wheat is ready to harvest!");
  }

  void CheckPlayerDistance() {
    if (player == null || harvestButton == null || soil == null) {
      return;
    }

    Renderer soilRenderer = soil.GetComponent < Renderer > ();

    if (soilRenderer == null) {
      return;
    }

    Bounds bounds = soilRenderer.bounds;

    Vector3 closestPoint = bounds.ClosestPoint(
    player.position);

    closestPoint.y = player.position.y;

    float distance = Vector3.Distance(
    player.position, closestPoint);

    harvestButton.SetActive(
    distance <= harvestDistance && mature);

  }

  public void HarvestWheat() {
    if (!mature) {
      Debug.Log("Wheat is not ready to harvest.");
      return;
    }

    Debug.Log("Wheat harvested!");
    Debug.Log("Depositing wheat to stockpile.");

    foreach(GameObject wheatPlant in wheatPlants) {
      Destroy(wheatPlant);
    }

    wheatPlants.Clear();

    planted = false;
    mature = false;
    growthProgress = 0f;

    ResourceManager store = ResourceManager.Nearest(transform.position);

    if (store != null) {
      store.Deposit(ResourceType.Hay, 1);
      Debug.Log("Added 1 Hay to stockpile.");
    }
    else {
      Debug.LogWarning("No stockpile found!");
    }

    if (harvestButton != null) {
      harvestButton.SetActive(false);
    }

    StartNewCrop();
  }
}