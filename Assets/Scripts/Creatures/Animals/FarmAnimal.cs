using UnityEngine;

/// <summary>
/// A farm animal is a friendly creature that can be controlled by the farmer.
/// It inherits from the Creature class and can be used to create different types of farm animals.
/// </summary>
public abstract class FarmAnimal : Creature
{
    [Header("Farm Animal Settings")]
    [SerializeField] private bool useGlobalConstants = true;

    protected override void Awake()
    {
        if (useGlobalConstants)
        {
            // Use global constants for farm animal stats
            maxHealth = GameConstants.Instance.BaseCreature.MaxHealth;
            moveSpeed = GameConstants.Instance.BaseCreature.Speed;
            attackSpeed = GameConstants.Instance.BaseCreature.AttackSpeed;
        }
        
        base.Awake(); // Use the base Awake method to initialize health and other properties
    }
    
    protected override void HandleZeroHealth()
    {
        // Animals do not die, they are knocked out.
        AnimalKnockedOut();
    }

    protected virtual void AnimalKnockedOut()
    {
        
    }


}
