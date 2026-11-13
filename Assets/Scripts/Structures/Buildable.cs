using System.Collections.Generic;
using UnityEngine;

public abstract class Buildable : RepairableStructure, IDamageable
{
    [Header("Buildable Settings")]
    [SerializeField] protected string buildableName;
    [SerializeField] protected int maxHealth; // Health of the buildable structure
    [SerializeField] protected int buildingCost;
    [SerializeField] protected ResourceType resourceType; // The type of resource used to build this structure
    
    // Var holding cost and resource type for building this structure
    public virtual (ResourceType, int) CostAndResource { get; private set; }

    public string BuildableName => buildableName;
    public int MaxHealth => maxHealth;
    private int CurrentHealth { get; set; }
    public bool Down => CurrentHealth <= 0;
    public Vector3 Position => transform.position;
    public Collider Body { get; private set; }

    public Transform GroundElement { get; private set; }

    protected override void Awake()
    {
        base.Awake();
       
        GroundElement = null;

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "Ground")
            {
                GroundElement = child;
                break;
            }
        }

        if (GroundElement == null)
        {
            Debug.LogError("Ground element not found for " + buildableName + ". Please ensure that the buildable prefab has a child named 'Ground'.");
        }

        // The ground plate covers the whole footprint, so attackers measure their reach to the building's edge.
        Body = GroundElement != null && GroundElement.TryGetComponent(out Collider ground)
            ? ground
            : GetComponentInChildren<Collider>();
        
        Reset();

        CurrentHealth = IsBroken ? 0 : maxHealth;

    }
    
    protected virtual void Reset()
    {
        maxHealth = GameConstants.Instance.BaseBuilding.MaxHealth;
        CurrentHealth = maxHealth;
        buildingCost = GameConstants.Instance.BaseBuilding.BuildingCost;
        resourceType = GameConstants.Instance.BaseBuilding.ResourceType;
        CostAndResource = 
            (GameConstants.Instance.BaseBuilding.ResourceType,
                GameConstants.Instance.BaseBuilding.BuildingCost);

        
        // Override this method in derived classes to set default values for stats if needed.
    }
    
    // This method is called when the buildable is built in the game. Note that it
    // Does not subtract resources, it is assumed that the resource management system will handle that.
    public virtual void Build()
    {
        Debug.Log(buildableName + " has been built.");
    }

    public virtual void Remove()
    {
        Debug.Log(buildableName + " has been removed.");

        Destroy(gameObject);
    }

    protected override void OnRepaired()
    {
        Repair(maxHealth);
    }

    protected override void OnBroken()
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
}