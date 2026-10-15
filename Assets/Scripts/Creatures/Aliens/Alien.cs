using UnityEngine;

public class Alien : Creature
{
    protected override void Reset()
    {
        base.Reset();
        // Use global constants for farm animal stats
        maxHealth = GameConstants.Instance.BaseAlien.MaxHealth;
        moveSpeed = GameConstants.Instance.BaseAlien.Speed;
        attackSpeed = GameConstants.Instance.BaseAlien.AttackSpeed;
        team = GameConstants.Instance.BaseAlien.Team;
    }
    
    protected override void HandleZeroHealth()
    {
        // Aliens die when their health reaches zero.
        Die();
    }

    protected virtual void Die()
    {
        
    }
}
