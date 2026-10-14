using System;
using UnityEngine;
using UnityEngine.AI;

///
/// A base class for all creatures, including the farmer, aliens and animals.
/// It is a MonoBehaviour so it can be attached to a GameObject in the scene,
/// but it does not have any functionality on its own.
/// It is meant to be inherited by other classes that implement specific creature behaviors.
///
[RequireComponent(typeof(NavMeshAgent),
    typeof(CreatureBrain))] // Ensure that any GameObject with a Creature has a NavMeshAgent for movement.
public abstract class Creature : MonoBehaviour
{
    private NavMeshAgent navMeshAgent;

    /// <summary>
    /// The CreatureBrain for the creature.
    /// Handles Pathfinding, decision-making, and other AI behaviors.
    /// </summary>
    private CreatureBrain creatureBrain;

    /// <summary>
    /// The team to which the creature belongs.
    /// </summary>
    [SerializeField] protected GameConstants.Team team;

    private int currentHealth;

    [Header("Creature Stats")] [SerializeField]
    protected int maxHealth;

    [SerializeField] protected float moveSpeed;

    /// <summary>
    /// The attack speed (time between attacks) of the creature.
    /// </summary>
    [SerializeField] protected float attackSpeed;

    private Vector3 position;


    // Public Getters
    protected int CurrentHealth => currentHealth;
    protected int MaxHealth => maxHealth;
    protected float MoveSpeed => moveSpeed;
    protected float AttackSpeed => attackSpeed;

    public NavMeshAgent NavMeshAgent
    {
        get => navMeshAgent;
        set => navMeshAgent = value;
    }

    public CreatureBrain CreatureBrain
    {
        get => creatureBrain;
        set => creatureBrain = value;
    }

    public Vector3 Position => transform.position;

    protected virtual void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        creatureBrain = GetComponent<CreatureBrain>();

        if (navMeshAgent != null)
            navMeshAgent.speed = moveSpeed; // Set the NavMeshAgent's speed to the creature's move speed

        if (creatureBrain != null) creatureBrain.Initialize(this); // Initialize the CreatureBrain with this creature

        // Initialize the creature with default values or specific parameters if needed
        if (currentHealth <= 0 && maxHealth > 0)
        {
            currentHealth = maxHealth; // Start with full health
        }
    }

    protected virtual void Reset()
    {
        // Override this method in derived classes to set default values for the creature's stats.
    }

    public virtual void TakeDamage(int amount)
    {
        currentHealth = Mathf.Max(0, currentHealth - amount);

        if (currentHealth <= 0)
        {
            HandleZeroHealth();
        }
    }

    public virtual void Heal(int amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    protected virtual void HandleZeroHealth()
    {
        // Override this method to handle what happens when the creature's health reaches zero.
    }
}