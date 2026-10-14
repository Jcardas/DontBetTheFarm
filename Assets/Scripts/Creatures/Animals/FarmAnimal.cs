using UnityEngine;

/// <summary>
/// A farm animal is a friendly creature that can be controlled by the farmer.
/// It inherits from the Creature class and can be used to create different types of farm animals.
/// </summary>
public abstract class FarmAnimal : Creature
{
    protected override void Reset()
    {
        // Use global constants for farm animal stats
        maxHealth = GameConstants.Instance.BaseAnimal.MaxHealth;
        moveSpeed = GameConstants.Instance.BaseAnimal.Speed;
        attackSpeed = GameConstants.Instance.BaseAnimal.AttackSpeed;
        team = GameConstants.Instance.BaseAnimal.Team;
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