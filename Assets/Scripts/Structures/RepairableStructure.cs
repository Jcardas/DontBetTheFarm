using UnityEngine;
using UnityEngine.AI;

public interface IRepairable
{
    bool IsBroken { get; }
    bool TryRepair();
}

public class RepairableStructure : MonoBehaviour, IRepairable
{
    [Header("Repair Settings")]
    [SerializeField] private int repairWoodCost = 10;
    [SerializeField] private bool startsBroken;

    [Header("Models")]
    [SerializeField] private GameObject repairedVisual;
    [SerializeField] private GameObject brokenVisual;

    private NavMeshObstacle obstacle;

    public bool IsBroken { get; private set; }
    public int RepairWoodCost => repairWoodCost;

    protected virtual void Awake()
    {
        obstacle = GetComponent<NavMeshObstacle>();
        IsBroken = startsBroken;
        UpdateVisuals();
    }

    public virtual bool TryRepair()
    {
        if (!IsBroken) return false;

        if (!ResourceManager.Spend(ResourceType.Wood, repairWoodCost))
        {
            Debug.Log("Not enough wood!");
            return false;
        }

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
    }

    protected virtual void OnRepaired() { }
    protected virtual void OnBroken() { }

    private void UpdateVisuals()
    {
        if (repairedVisual) repairedVisual.SetActive(!IsBroken);
        if (brokenVisual) brokenVisual.SetActive(IsBroken);
        if (obstacle) obstacle.enabled = !IsBroken;
    }
}