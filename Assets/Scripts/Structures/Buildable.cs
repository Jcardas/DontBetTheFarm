using System.Collections.Generic;
using UnityEngine;

public abstract class Buildable : MonoBehaviour
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

    public Transform GroundElement { get; private set; }

    protected virtual void Awake()
    {
        // Get the ground element for this building.
        // This is needed for determining the bounds of the building for placement and other purposes.
        // Note: This assumes that the ground element is a child of the buildable object.
        GroundElement = transform.Find("Ground");
        if (GroundElement == null)
        {
            Debug.LogError("Ground element not found for " + buildableName + ". Please ensure that the buildable prefab has a child named 'Ground'.");
        }
    }
    
    protected virtual void Reset()
    {
        CostAndResource = 
            (GameConstants.Instance.BaseBuilding.ResourceType,
            GameConstants.Instance.BaseBuilding.BuildingCost);
        
        maxHealth = GameConstants.Instance.BaseBuilding.MaxHealth; 
        buildingCost = GameConstants.Instance.BaseBuilding.BuildingCost;
        
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

    protected virtual void HandleDestroyed()
    {
        Debug.Log(buildableName + " has been destroyed.");
        // Override this method in derived classes to handle specific destruction behavior, 
        // such as changing building state or ending the game (if it's the barn)
    }
    
    protected virtual void Repair(int repairAmount)
    {
        CurrentHealth += repairAmount;
        Debug.Log(buildableName + " has been repaired by " + repairAmount + ". Current health: " + CurrentHealth); // Todo Remove this test
    }
    
    protected virtual void TakeDamage(int damage)
    {
        CurrentHealth -= damage;
        Debug.Log(buildableName + " took " + damage + " damage. Remaining health: " + CurrentHealth); // Todo Remove this test

        if (CurrentHealth <= 0)
        {
            HandleDestroyed(); // Call the destruction handler when health reaches zero
        }
    }
}