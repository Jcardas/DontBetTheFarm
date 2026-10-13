using UnityEngine;

public class ResourceManager : MonoBehaviour
{
    [Header("Resources")]
    [SerializeField] private int hay = 0;
    [SerializeField] private int wood = 0;
    [SerializeField] private int technology = 0;

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

    public int GetHay()
    {
        return hay;
    }

    public int GetWood()
    {
        return wood;
    }

    public int GetTechnology()
    {
        return technology;
    }
}