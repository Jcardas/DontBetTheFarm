using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(CreatureBrain))]
public abstract class Creature : MonoBehaviour
{
    [Header("Team & Alignment")]
    [SerializeField] protected GameConstants.Team team;

    [Header("Base Stats")]
    [SerializeField] protected int maxHealth = 50;
    [SerializeField] protected float moveSpeed = 3.5f;
    [SerializeField] protected int attackDamage = 10;
    [SerializeField] protected float attackSpeed = 1f; // Seconds between attacks
    [SerializeField] protected float attackRange = 1f;
    [SerializeField] protected float guardRadius = 8f;
    [SerializeField] protected bool anchored = true;

    private int currentHealth;
    private NavMeshAgent navMeshAgent;
    private CreatureBrain creatureBrain;
    private Collider bodyCollider;
    private ToyBob toyBob;
    private Gatherer gatherer;

    // Public properties
    string CreatureName => gameObject.name.Replace("(Clone)", "").Trim();    
    public GameConstants.Team Team => team;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public float MoveSpeed => moveSpeed;
    public int AttackDamage => attackDamage;
    public float AttackSpeed => attackSpeed;
    public float AttackRange => attackRange;
    public float GuardRadius => guardRadius;
    public bool Anchored => anchored;
    public bool Down => currentHealth <= 0;
    public Gatherer Gatherer => gatherer;

    public Collider Body => bodyCollider;
    public NavMeshAgent NavMeshAgent => navMeshAgent;
    public CreatureBrain CreatureBrain => creatureBrain;
    public Vector3 Position => transform.position;
    
    protected virtual void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        creatureBrain = GetComponent<CreatureBrain>();
        bodyCollider = GetComponentInChildren<Collider>();
        toyBob = GetComponentInChildren<ToyBob>();
        gatherer = GetComponentInChildren<Gatherer>();

        if (navMeshAgent != null)
            navMeshAgent.speed = moveSpeed;

        currentHealth = maxHealth;
    }

    protected virtual void Reset()
    {
        // Override this method in derived classes to set default values for stats if needed.
    }

    protected virtual void OnEnable()
    {
        // Register this creature with the CreatureManager when enabled
        if (CreatureManager.Instance != null)
            CreatureManager.Instance.Register(this);
    }

    protected virtual void OnDisable()
    {
        // Unregister this creature from the CreatureManager when disabled
        if (CreatureManager.Instance != null)
            CreatureManager.Instance.Unregister(this);
    }

    public void TakeDamage(int amount)
    {
        if (Down) return;

        currentHealth = Mathf.Max(0, currentHealth - amount);

        if (currentHealth <= 0)
        {
            HandleZeroHealth();
        }
    }

    public void Heal(int amount)
    {
        if (Down) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    protected virtual void HandleZeroHealth()
    {
        // By default, disable the NavMeshAgent when health reaches zero.
        // Derived classes can override this method for custom behavior.
        if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.ResetPath();
            navMeshAgent.enabled = false;
        }
    }

    public void TriggerAttackVisual()
    {
        if (toyBob != null) toyBob.Bump();
    }
}