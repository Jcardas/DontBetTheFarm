using UnityEngine;

[RequireComponent(typeof(Selectable), typeof(Gatherer), typeof(AudioSource))]
public class Farmer : Creature
{
    protected override void Reset()
    {
        // Use global constants for farm animal stats
        maxHealth = GameConstants.Instance.BaseFarmer.MaxHealth;
        moveSpeed = GameConstants.Instance.BaseFarmer.Speed;
        attackSpeed = GameConstants.Instance.BaseFarmer.AttackSpeed;
        team = GameConstants.Instance.BaseFarmer.Team;
    }


    protected override void HandleZeroHealth()
    {
        // When the farmer's health reaches zero, trigger the game over sequence.
        GameOver();
    }

    protected virtual void GameOver()
    {
        // Implement game over logic here
        Debug.Log("Game Over! The farmer has been defeated.");
    }
}
