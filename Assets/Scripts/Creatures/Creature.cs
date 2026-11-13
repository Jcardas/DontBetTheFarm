using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.AI;
using System.Collections;


/// <summary>
/// Capabilities:
///
/// <list type="bullet">
/// <item>  (1) CanChopWood</item>
/// <item>  (2) CanHarvestCrops</item>
/// <item>  (3) CanRepair</item>
/// <item>  (4) CanFly</item>
/// <item>  (5) IsRangedAttacker</item>
/// </list>
///
/// </summary>
[Flags]
public enum CreatureCapabilities
{
    None           = 0,
    
    // Resource Capabilities
    CanChopWood    = 1 << 0, // Axe
    CanHarvestCrops = 1 << 1, // Scythe
    CanRepair      = 1 << 2, // Hammer
    
    // Combat Capabilities
    CanFly         = 1 << 3, // Wings
    IsRangedAttacker = 1 << 4 // Ranged attack (e.g., spit, fire breath)
}

[RequireComponent(typeof(NavMeshAgent), typeof(CreatureBrain), typeof(CreatureMutator)) ]
public abstract class Creature : MonoBehaviour, IDamageable
{
    [Header("Team & Alignment")]
    [SerializeField] protected GameConstants.Team team;
    
    [Header("Capabilities")]
    [SerializeField] protected CreatureCapabilities baseCapabilities = CreatureCapabilities.None;
    
    [Header("Base Stats")]
    [SerializeField] protected int maxHealth = 50;
    [SerializeField] protected float moveSpeed = 3.5f;
    [SerializeField] protected int attackDamage = 10;
    [SerializeField] protected float attackSpeed = 1f; // Seconds between attacks
    [SerializeField] protected float attackRange = 1f;
    [SerializeField] protected float viewRadius = 8f;
    [SerializeField] protected bool anchored = true;
    
    private int currentHealth;
    private NavMeshAgent navMeshAgent;
    private CreatureBrain creatureBrain;
    private Collider bodyCollider;
    private ToyBob toyBob;
    private Gatherer gatherer;
    
    private CreatureMutator creatureMutator;
    private Highlight highlight;

    // Public properties
    
    // These are the calculated stats after applying mutations. They are read-only and updated whenever mutations change.
    public int CalculatedMaxHealth { get; private set; }
    public float CalculatedMoveSpeed { get; private set; }
    public int CalculatedAttackDamage { get; private set; }
    public float CalculatedAttackRange { get; private set; }
    public float CalculatedAttackSpeed { get; private set; }
    public float CalculatedDamageResistance { get; private set; }
    public CreatureCapabilities ActiveCapabilities { get; private set; }
    
    public float CalculatedViewRadius { get; private set; }
    
    // "Cow", "Cow (1)" and "Cow(Clone)" are all a Cow: drops Unity's duplicate numbers and spawn suffixes.
    public string CreatureName => Regex.Replace(gameObject.name, @"(\s*\(\d+\)|\s*\(Clone\))+$", "").Trim();
    public GameConstants.Team Team => team;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public float MoveSpeed => moveSpeed;
    public int AttackDamage => attackDamage;
    public float AttackSpeed => attackSpeed;
    public float AttackRange => attackRange;
    public bool Anchored => anchored;
    public bool Down => currentHealth <= 0;
    public Gatherer Gatherer => gatherer;

    public Collider Body => bodyCollider;
    public NavMeshAgent NavMeshAgent => navMeshAgent;
    public CreatureBrain CreatureBrain => creatureBrain;
    public CreatureMutator CreatureMutator => creatureMutator;
    public Highlight Highlight => highlight;
    public Vector3 Position => transform.position;

    [Header("Retreat & Despawn")]
    [SerializeField] protected GameObject beamEffectPrefab;
    protected Vector3 despawnPoint;
    protected bool isRetreating = false;

    
    
    public bool HasCapability(CreatureCapabilities capability) => (ActiveCapabilities & capability) != 0;
    
    protected virtual void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        creatureBrain = GetComponent<CreatureBrain>();
        bodyCollider = GetComponentInChildren<Collider>();
        toyBob = GetComponentInChildren<ToyBob>();
        gatherer = GetComponentInChildren<Gatherer>();
        creatureMutator = GetComponent<CreatureMutator>();
        highlight = gameObject.AddComponent<Highlight>(); // outlined while hidden, hovered or selected


        OnMutationsChanged();
        currentHealth = CalculatedMaxHealth;
    }

    /// <summary>
    /// This method gets called by the CreatureMutator whenever a mutation is equipped or unequipped,
    /// so the creature can recalculate its stats.
    /// </summary>
    public void OnMutationsChanged()
    {
        // Calculate the capabilities granted by mutations. This is a bitmask of CreatureCapabilities.
        CreatureCapabilities caps = baseCapabilities;
        
        // Calculate the creature's effective stats based on its base stats and any equipped mutations.
        int bonusHealth = 0;
        float resistance = 0f;
        float bonusSpeed = 0f;
        int bonusDamage = 0;
        float bonusRange = 0f;
        float bonusAttackSpeed = 0f;
        float bonusViewRadius = 0f;

        if (creatureMutator != null)
        {
            foreach (var mutation in creatureMutator.EquippedMutations.Values)
            {
                if (mutation == null) continue;
                bonusHealth += mutation.bonusMaxHealth;
                bonusSpeed += mutation.bonusMoveSpeed;
                bonusDamage += mutation.bonusAttackDamage;
                bonusRange += mutation.bonusAttackRange;
                bonusViewRadius += mutation.bonusViewRadius;
                bonusAttackSpeed += mutation.bonusAttackSpeed;
                resistance += mutation.damageResistance;
                caps |= mutation.grantedCapabilities;
            }
        }

        int oldMaxHealth = CalculatedMaxHealth;

        CalculatedMaxHealth = Mathf.Max(1, maxHealth + bonusHealth);
        CalculatedMoveSpeed = Mathf.Max(0.1f, moveSpeed + bonusSpeed);
        CalculatedAttackDamage = Mathf.Max(0, attackDamage + bonusDamage);
        CalculatedAttackSpeed = Mathf.Max(0.05f, attackSpeed + bonusAttackSpeed);
        CalculatedDamageResistance = Mathf.Max(0f, Mathf.Min(100f, resistance)); // Clamp to 0-100%
        ActiveCapabilities = caps;
        CalculatedViewRadius = Mathf.Max(1f, viewRadius + bonusViewRadius);
        
        // Attack range is capped by view radius: a creature can't attack something it can't see.
        CalculatedAttackRange = Mathf.Min(CalculatedViewRadius, attackRange + bonusRange);

        // Apply speed directly to the NavMeshAgent if active
        if (navMeshAgent != null)
        {
            navMeshAgent.speed = CalculatedMoveSpeed;
        }

        // Scale current health proportionally if MaxHealth changes mid-game
        if (oldMaxHealth > 0 && CalculatedMaxHealth != oldMaxHealth && currentHealth > 0)
        {
            float healthPct = (float)currentHealth / oldMaxHealth;
            currentHealth = Mathf.Clamp(Mathf.RoundToInt(CalculatedMaxHealth * healthPct), 1, CalculatedMaxHealth);
        }
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

        if (creatureMutator != null)
        {
            // Subscribe to the mutation update event to recalculate stats when mutations change
            creatureMutator.OnMutationsUpdated += OnMutationsChanged;
        }
    }

    protected virtual void Start()
    {
        // Ensure the creature is registered with the CreatureManager at the start
        if (CreatureManager.Instance != null)
            CreatureManager.Instance.Register(this);
    }

    protected virtual void OnDisable()
    {
        // Unregister this creature from the CreatureManager when disabled
        if (CreatureManager.Instance != null)
            CreatureManager.Instance.Unregister(this);

        if (creatureMutator != null)
        {
            // Unsubscribe from the mutation update event to avoid memory leaks
            creatureMutator.OnMutationsUpdated -= OnMutationsChanged;
        }
    }

    public void TakeDamage(int amount)
    {
        if (Down) return;
        
        int mitigated = Mathf.RoundToInt(amount * (1f - CalculatedDamageResistance / 100f));
        
        currentHealth = Mathf.Max(0, currentHealth - mitigated);
        if (currentHealth <= 0) HandleZeroHealth();
    }

    private void Heal(int amount)
    {
        if (Down)
        {
            HandleRevive();
        }
        currentHealth = Mathf.Min(CalculatedMaxHealth, currentHealth + amount);
    }
    
    public virtual void HealToFull()
    {
        if (Down)
        {
            HandleRevive();
        }
        Heal(CalculatedMaxHealth - currentHealth);
    }

    protected virtual void HandleRevive()
    {
        
        if (currentHealth > 0) return;

        currentHealth = CalculatedMaxHealth;

        // Re-enable the NavMeshAgent if it was disabled
        if (navMeshAgent != null && !navMeshAgent.enabled)
        {
            navMeshAgent.enabled = true;
        }
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

    // Switches sides (a stolen animal the aliens send back) and re-files it with the CreatureManager under the new team.
    public void SetTeam(GameConstants.Team newTeam)
    {
        if (CreatureManager.Instance != null) CreatureManager.Instance.Unregister(this);
        team = newTeam;
        if (isActiveAndEnabled && CreatureManager.Instance != null) CreatureManager.Instance.Register(this);
    }

    // Ranged creatures back away when an enemy gets closer than this, so they keep fighting from range. 0 = stand and fight.
    public virtual float KeepAwayRange => 0f;

    // What this creature fights when it has no orders: the nearest enemy creature near its guard post.
    public virtual IDamageable FindTarget(Vector3 guardPost) =>
        CreatureManager.Instance != null
            ? CreatureManager.Instance.GetNearestEnemy(team, transform.position, guardPost, CalculatedViewRadius)
            : null;

    // How this creature lands a hit once its target is in range. Melee by default; ranged and special creatures override it.
    public virtual void PerformAttack(IDamageable target)
    {
        if (target == null || target.Down) return;

        TriggerAttackVisual();

        // Deal damage directly to target (TakeDamage handles resistance internally on Creatures)
        target.TakeDamage(CalculatedAttackDamage);

        // Notify attached mutation behaviors if target is a creature
        if (creatureMutator != null && target is Creature targetCreature)
        {
            foreach (var behavior in creatureMutator.ActiveBehaviors)
            {
                behavior.OnCreatureAttack(targetCreature);
            }
        }
    }

    public void TriggerAttackVisual()
    {
        if (toyBob != null) toyBob.Bump();
    }
    public void SetDespawnPoint(Vector3 point)
    {
        despawnPoint = point;
    }

    public void Retreat()
    {
        if (Down || isRetreating) return;

        isRetreating = true;

        if (CreatureBrain != null)
        {
            CreatureBrain.enabled = false; // Disable the AI behavior
        }

        if (NavMeshAgent != null && NavMeshAgent.enabled)
        {
            NavMeshAgent.SetDestination(despawnPoint);
            StartCoroutine(RetreatRoutine());
        }
    }

    private IEnumerator RetreatRoutine()
    {
        float retreatTolerance = Mathf.Max(NavMeshAgent.stoppingDistance, 1.5f); // Ensure a minimum tolerance)
        while (NavMeshAgent != null && NavMeshAgent.enabled)
        {
            if (!NavMeshAgent.pathPending && NavMeshAgent.remainingDistance <= retreatTolerance)
            {
                BeamUp();
                yield break;
            }
            yield return null;
        }
    }

    private void BeamUp()
    {
        if (beamEffectPrefab != null)
        {
            Vector3 beamDespawnPosition = transform.position;
            beamDespawnPosition.y -= 2.5f;

            Instantiate(beamEffectPrefab, beamDespawnPosition, Quaternion.identity);
        }
        Destroy(gameObject);
    }
}