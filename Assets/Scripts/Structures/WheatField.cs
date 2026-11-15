using UnityEngine;

public class WheatField : Buildable
{
    [Header("Wheat Growth")]
    [SerializeField] private Transform wheatVisual;
    [SerializeField] private float growTime = 15f;
    [SerializeField] private float startingYScale = 0.15f;
    [SerializeField] private float fullyGrownYScale = 1f;
    [SerializeField] private int wheatPerHarvest = 1;

    [Header("Wheat Materials")]
    [SerializeField] private Material growingMaterial;
    [SerializeField] private Material matureMaterial;

    public bool mature = false;

    private float growthTimer;
    private Renderer[] wheatRenderers;

    public override (ResourceType, int) CostAndResource =>
        (GameConstants.Instance.BaseWheatField.ResourceType,
         GameConstants.Instance.BaseWheatField.BuildingCost);

    protected override void Reset()
    {
        base.Reset();
    }

    protected override void HandleDestroyed()
    {
        base.HandleDestroyed();
    }

    private void Start()
    {
        buildableName = "Wheat Field";

        if (wheatVisual == null)
        {
            // Wheat Mesh
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "WheatPlane")
                {
                    wheatVisual = child;
                    break;
                }
            }
        }

        if (wheatVisual == null)
        {
            Debug.LogError("WheatField: Assign WheatPlane to Wheat Visual.", this);
            enabled = false;
            return;
        }

        wheatRenderers = wheatVisual.GetComponentsInChildren<Renderer>(true);
        StartNewCrop();
    }

    private void Update()
    {
        if (IsBroken || mature || wheatVisual == null)
            return;

        growthTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(growthTimer / Mathf.Max(growTime, 0.01f));
        SetWheatHeight(Mathf.Lerp(startingYScale, fullyGrownYScale, progress));

        if (progress >= 1f)
        {
            mature = true;
            SetWheatMaterial(matureMaterial);
        }
    }

    private void SetWheatHeight(float yScale)
    {
        Vector3 scale = wheatVisual.localScale;
        scale.y = yScale;
        wheatVisual.localScale = scale;
    }

    private void SetWheatMaterial(Material material)
    {
        if (material == null || wheatRenderers == null) return;

        foreach (Renderer renderer in wheatRenderers)
        {
            if (renderer == null) continue;

            Material[] slots = renderer.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
                slots[i] = material;
            renderer.sharedMaterials = slots;
        }
    }

    private void StartNewCrop()
    {
        growthTimer = 0f;
        mature = false;
        if (wheatVisual != null)
            SetWheatHeight(startingYScale);
        SetWheatMaterial(growingMaterial);
    }

    protected override void OnBroken()
    {
        base.OnBroken();
        StartNewCrop();
    }

    protected override void OnRepaired()
    {
        base.OnRepaired();
        StartNewCrop();
    }

    public void HarvestWheat()
    {
        if (IsBroken || !mature)
            return;

        ResourceManager store = ResourceManager.Nearest(transform.position);
        if (store != null)
            store.Deposit(ResourceType.Hay, wheatPerHarvest);

        StartNewCrop();
    }

    public override void Build()
    {
    }
}
