using UnityEngine;

public enum ResourceType { Wood, Hay, Technology }

public class ResourceManager : MonoBehaviour
{
    [Header("Resources")]
    [SerializeField] private int hay = 0;
    [SerializeField] private int wood = 0;
    [SerializeField] private int technology = 0;
    
    public static ResourceManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void AddResource(ResourceType resourceType, int amount)
    {
        Debug.Log("Adding " + amount + " of resource: " + resourceType);

        if (amount <= 0) return;

        switch (resourceType)
        {
            case ResourceType.Hay:
                AddHay(amount);
                break;
            case ResourceType.Wood:
                AddWood(amount);
                break;
            case ResourceType.Technology:
                AddTechnology(amount);
                break;
            default:
                Debug.LogWarning("Unknown resource type: " + resourceType);
                break;
        }
    }

    public void AddHay(int amount)
    {
        hay += amount;
        Debug.Log("Hay: " + hay);
    }

    public void AddWood(int amount)
    {
        wood += amount;
        Debug.Log("Wood: " + wood);
    }

    public void AddTechnology(int amount)
    {
        technology += amount;
        Debug.Log("Technology: " + technology);
    }

    public int GetHay() => hay;
    public int GetWood() => wood;
    public int GetTechnology() => technology;
}