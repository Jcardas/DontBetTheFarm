using System.Collections;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

// A Buildable now owns its broken state and repair logic.
// No separate repair component is needed.
public abstract class Buildable : MonoBehaviour, IDamageable
{
    [Header("Buildable Settings")]
    [SerializeField] protected string buildableName;
    [SerializeField] protected int maxHealth;
    [SerializeField] protected int buildingCost;
    [SerializeField] protected ResourceType resourceType;

    [Header("Repair Settings")]
    [SerializeField] private int repairWoodCost = 10;
    [SerializeField] private bool startsBroken;

    [Header("Models")]
    [SerializeField] private GameObject repairedVisual;
    [SerializeField] private GameObject brokenVisual;

    private NavMeshObstacle obstacle;

    public virtual (ResourceType, int) CostAndResource { get; private set; }
    public string BuildableName => buildableName;
    public int MaxHealth => maxHealth;
    private int CurrentHealth { get; set; }
    public bool Down => CurrentHealth <= 0;
    public Vector3 Position => transform.position;
    public Collider Body { get; private set; }
    public Transform GroundElement { get; private set; }

    public bool IsBroken { get; private set; }
    public int RepairWoodCost => repairWoodCost;

    protected virtual void Awake()
    {
        obstacle = GetComponent<NavMeshObstacle>();
        IsBroken = startsBroken;
        UpdateVisuals();

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name != "Ground") continue;
            GroundElement = child;
            break;
        }

        Body = GroundElement && GroundElement.TryGetComponent(out Collider ground)
            ? ground
            : GetComponent<Collider>();

        if (!Body) Body = GetComponentInChildren<Collider>();
        if (!Body) Debug.LogWarning($"{name} needs a collider for repair movement.", this);

        Reset();
        CurrentHealth = IsBroken ? 0 : maxHealth;
    }

    protected virtual void Reset()
    {
        maxHealth = GameConstants.Instance.BaseBuilding.MaxHealth;
        CurrentHealth = maxHealth;
        buildingCost = GameConstants.Instance.BaseBuilding.BuildingCost;
        resourceType = GameConstants.Instance.BaseBuilding.ResourceType;
        CostAndResource = (resourceType, buildingCost);
    }

    public virtual void Build()
    {
        Debug.Log(buildableName + " has been built.");
    }

    public virtual void Remove()
    {
        Debug.Log(buildableName + " has been removed.");

        Destroy(gameObject);
    }

    // Spend wood only after the repair animation has finished.
    public virtual bool TryRepair()
    {
        if (!IsBroken || !ResourceManager.Spend(ResourceType.Wood, repairWoodCost))
            return false;

        IsBroken = false;
        OnRepaired();
        UpdateVisuals();
        return true;
    }

    [ContextMenu("Break (Test)")]
    public void Break()
    {
        if (IsBroken) return;
        IsBroken = true;
        UpdateVisuals();
        OnBroken();

        StartCoroutine(RefreshNavMeshAfterBreak());
    }

    protected virtual void OnRepaired() => Repair(maxHealth);

    protected virtual void OnBroken()
    {
        CurrentHealth = 0;
        HandleDestroyed();
    }

     protected virtual void Repair(int amount)
    {
        CurrentHealth = Mathf.Clamp(
            CurrentHealth + amount, 0, maxHealth
        );
    }

    private void UpdateVisuals()
    {
        if (repairedVisual) repairedVisual.SetActive(!IsBroken);
        if (brokenVisual) brokenVisual.SetActive(IsBroken);
        if (obstacle) obstacle.enabled = !IsBroken;
    }

    public void ApplyDamage(int damage)
    {
        TakeDamage(damage);
    }
    public virtual void TakeDamage(int damage)
    {
        if (IsBroken || damage <= 0) return;

         CurrentHealth = Mathf.Max(
            0, CurrentHealth - damage
        );

        if (CurrentHealth == 0)
            Break();
    }

   protected virtual void HandleDestroyed()
    {
        Debug.Log(buildableName + " destroyed!");
    }

    private IEnumerator RefreshNavMeshAfterBreak()
    {
        yield return null;

        foreach (NavMeshSurface surface in
            FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None))
        {
            if (surface.isActiveAndEnabled)
                surface.BuildNavMesh();
        }
    }
}