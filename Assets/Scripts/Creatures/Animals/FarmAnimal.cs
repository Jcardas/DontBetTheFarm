using UnityEngine;

/// <summary>
/// A farm animal is a friendly creature that can be controlled by the farmer.
/// It inherits from the Creature class and can be used to create different types of farm animals.
/// </summary>
[RequireComponent(typeof(Selectable), typeof(Gatherer))]
public abstract class FarmAnimal : Creature
{
    [Header("Prefab Reference")]
    [Tooltip("Prefab reference for the farm animal. This is used for spawning and instantiation.")]
    [SerializeField] protected FarmAnimal sourcePrefab;
    
    [SerializeField] protected int breedingCost;
    [SerializeField] protected int healCost;

    public FarmAnimal SourcePrefab
    {
        get => sourcePrefab != null ? sourcePrefab : this;
        set => sourcePrefab = value;
    }
    
    public int BreedingCost => breedingCost;
    public int HealCost => healCost;

    protected override void Reset()
    {
        base.Reset();
        // Use global constants for farm animal stats
        maxHealth = GameConstants.Instance.BaseAnimal.MaxHealth;
        moveSpeed = GameConstants.Instance.BaseAnimal.Speed;
        attackSpeed = GameConstants.Instance.BaseAnimal.AttackSpeed;
        team = GameConstants.Instance.BaseAnimal.Team;
        breedingCost = GameConstants.Instance.BaseBreedingCost;
        healCost = GameConstants.Instance.BaseHealCost;
    }
    

    protected override void HandleZeroHealth()
    {
        // Animals do not die, they are knocked out.
        AnimalKnockedOut();
    }
    
    protected override void HandleRevive()
    {
        
    }

    protected virtual void AnimalKnockedOut()
    {
        
    }

    public override void HealToFull()
    {
        base.HealToFull();
        // Additional logic for farm animals when healed, if needed
    }
}