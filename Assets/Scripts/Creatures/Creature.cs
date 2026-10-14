using UnityEngine;

///
/// A base class for all creatures, including the farmer, aliens and animals.
/// It is a MonoBehaviour so it can be attached to a GameObject in the scene,
/// but it does not have any functionality on its own.
/// It is meant to be inherited by other classes that implement specific creature behaviors.
///
public abstract class Creature : MonoBehaviour
{
    /// <summary>
    /// The current health of the creature.
    /// </summary>
    [SerializeField] protected int currentHealth;

    /// <summary>
    /// The maximum health of the creature.
    /// </summary>
    [SerializeField] protected int maxHealth;
    
    /// <summary>
    /// The movement speed of the creature.
    /// </summary>
    [SerializeField] protected float moveSpeed;
    
    /// <summary>
    /// The attack speed (time between attacks) of the creature.
    /// </summary>
    [SerializeField] protected float attackSpeed;
    
    // Public Getters
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public float MoveSpeed => moveSpeed;
    public float AttackSpeed => attackSpeed;

    protected virtual void Awake()
    {
        // Initialize the creature with default values or specific parameters if needed
        if(currentHealth <= 0 && maxHealth <= 0)
        {
            currentHealth = maxHealth; // Start with full health
        }
    }

    public virtual void TakeDamage(int amount)
    {
        currentHealth = Mathf.Max(0, currentHealth - amount);

        if (currentHealth <= 0)
        {
            HandleZeroHealth();
        }
    }

    protected virtual void HandleZeroHealth()
    {
        // Override this method to handle what happens when the creature's health reaches zero.
    }
}
