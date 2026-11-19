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
    None             = 0,

    // Resource Capabilities
    CanChopWood      = 1 << 0, // Axe
    CanHarvestCrops  = 1 << 1, // Scythe
    CanRepair        = 1 << 2, // Hammer

    // Combat Capabilities
    CanFly           = 1 << 3, // Wings
    IsRangedAttacker = 1 << 4  // Ranged attack (e.g., spit, fire breath)
}

[RequireComponent(typeof(NavMeshAgent), typeof(CreatureBrain), typeof(CreatureMutator))]
public abstract class Creature : MonoBehaviour, IDamageable
{
    private const CreatureCapabilities WorkCapabilities =
        CreatureCapabilities.CanChopWood |
        CreatureCapabilities.CanHarvestCrops |
        CreatureCapabilities.CanRepair;

    [Header("Team & Alignment")]
    [SerializeField] protected GameConstants.Team team;

    [Header("Creature Identity")]
    [SerializeField] protected string creatureprefabName = "Creature";

    [Header("Capabilities")]
    [SerializeField] protected CreatureCapabilities baseCapabilities = CreatureCapabilities.None;

    [Header("Base Stats")]
    [SerializeField] protected int maxHealth = 50;
    [SerializeField] protected float moveSpeed = 3.5f;
    [SerializeField] protected int attackDamage = 10;
    [SerializeField] protected float attackSpeed = 1f; // Seconds between attacks
    [SerializeField] protected float attackRange = 1f;
    [SerializeField] protected float viewRadius = 8f;

    private int currentHealth;
    private NavMeshAgent navMeshAgent;
    private CreatureBrain creatureBrain;
    private Collider bodyCollider;
    private ToyBob toyBob;
    private Gatherer gatherer;
    private CreatureFlight flight;

    private CreatureMutator creatureMutator;
    private Highlight highlight;

    // Calculated stats after applying mutations
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
    public bool Down => currentHealth <= 0;
    public Gatherer Gatherer => gatherer;
    public CreatureFlight Flight => flight;
    public bool IsAirborne => flight != null && flight.IsAirborne;

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
        flight = GetComponent<CreatureFlight>();
        creatureMutator = GetComponent<CreatureMutator>();
        highlight = gameObject.AddComponent<Highlight>();

        OnMutationsChanged();
        currentHealth = CalculatedMaxHealth;
    }

    /// <summary>
    /// Recalculates stats and capabilities whenever mutations change.
    /// </summary>
    public void OnMutationsChanged()
    {
        CreatureCapabilities caps = baseCapabilities;

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
        CalculatedDamageResistance = Mathf.Clamp(resistance, 0f, 100f);
        ActiveCapabilities = caps;
        CalculatedViewRadius = Mathf.Max(1f, viewRadius + bonusViewRadius);

        // Attack range is capped by view radius: a creature can't attack something it can't see.
        CalculatedAttackRange = Mathf.Min(CalculatedViewRadius, attackRange + bonusRange);

        // Ensure Gatherer is attached and synced if any work capability (Axe, Scythe, Hammer) is active
        if ((ActiveCapabilities & WorkCapabilities) != 0)
        {
            if (gatherer == null)
                gatherer = GetComponentInChildren<Gatherer>() ?? gameObject.AddComponent<Gatherer>();

            if (creatureBrain != null)
                creatureBrain.RefreshGatherer(gatherer);
        }

        // Ensure CreatureFlight is attached whenever CanFly is granted
        if (HasCapability(CreatureCapabilities.CanFly))
        {
            if (flight == null)
                flight = GetComponent<CreatureFlight>() ?? gameObject.AddComponent<CreatureFlight>();
        }
        else if (flight != null && flight.IsAirborne)
        {
            flight.StopAndLand();
        }

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
    }

    protected virtual void OnEnable()
    {
        if (CreatureManager.Instance != null)
            CreatureManager.Instance.Register(this);

        if (creatureMutator != null)
        {
            creatureMutator.OnMutationsUpdated -= OnMutationsChanged;
            creatureMutator.OnMutationsUpdated += OnMutationsChanged;
        }

        OnMutationsChanged();
    }

    protected virtual void Start()
    {
        if (CreatureManager.Instance != null)
            CreatureManager.Instance.Register(this);

        // Final sync after CreatureMutator.Start() has applied defaultMutations
        OnMutationsChanged();
    }

    protected virtual void OnDisable()
    {
        if (CreatureManager.Instance != null)
            CreatureManager.Instance.Unregister(this);

        if (creatureMutator != null)
        {
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

    public void HealAmount(int amount)
    {
        if (amount <= 0) return;
        Heal(amount);
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

    // Gets a downed creature back up on `health` HP instead of full (daytime auto-revive)
    public void ReviveWithHealth(int health)
    {
        if (!Down) return;
        HandleRevive();
        currentHealth = Mathf.Clamp(health, 1, CalculatedMaxHealth);
    }

    protected virtual void HandleRevive()
    {
        if (currentHealth > 0) return;

        isRetreating = false;
        currentHealth = CalculatedMaxHealth;

        if (navMeshAgent != null && !navMeshAgent.enabled)
        {
            navMeshAgent.enabled = true;
        }
    }

    protected virtual void HandleZeroHealth()
    {
        if (flight != null)
            flight.ForceGroundImmediate();

        if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.ResetPath();
            navMeshAgent.enabled = false;
        }
    }

    public void SetTeam(GameConstants.Team newTeam)
    {
        if (CreatureManager.Instance != null) CreatureManager.Instance.Unregister(this);
        team = newTeam;
        if (isActiveAndEnabled && CreatureManager.Instance != null) CreatureManager.Instance.Register(this);
    }

    public virtual float KeepAwayRange => 0f;

    protected bool CanTargetAirborne =>
        this is Farmer ||
        this is RangedAnimal ||
        HasCapability(CreatureCapabilities.IsRangedAttacker) ||
        HasCapability(CreatureCapabilities.CanFly);

    public virtual IDamageable FindTarget(Vector3 guardPost) =>
        CreatureManager.Instance != null
            ? CreatureManager.Instance.GetNearestEnemy(team, transform.position, guardPost, CalculatedViewRadius, CanTargetAirborne)
            : null;

    public virtual void PerformAttack(IDamageable target)
    {
        if (target == null || target.Down) return;

        TriggerAttackVisual();
        target.TakeDamage(CalculatedAttackDamage);

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
            CreatureBrain.enabled = false;
        }
        if (NavMeshAgent != null) NavMeshAgent.updateRotation = true; // the brain may have left it off mid-fight

        // Flying creatures can fly straight to the despawn point without needing NavMesh
        if (flight != null && flight.CanFly)
        {
            flight.FlyToHover(despawnPoint);
            StartCoroutine(FlightRetreatRoutine());
            return;
        }

        if (flight != null)
            flight.ForceGroundImmediate();

        if (NavMeshAgent != null && NavMeshAgent.enabled)
        {
            if (!NavMeshAgent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                NavMeshAgent.Warp(hit.position);
            }

            if (NavMeshAgent.isOnNavMesh)
            {
                NavMeshAgent.SetDestination(despawnPoint);
                StartCoroutine(RetreatRoutine());
            }
            else
            {
                BeamUp();
            }
        }
    }
    
    private IEnumerator FlightRetreatRoutine()
    {
        while (flight != null && !Down)
        {
            float flatDist = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(despawnPoint.x, despawnPoint.z)
            );
            if (flatDist <= 1.5f)
            {
                BeamUp();
                yield break;
            }
            yield return null;
        }
    }

    private IEnumerator RetreatRoutine()
    {
        float retreatTolerance = Mathf.Max(NavMeshAgent.stoppingDistance, 1.5f);
        while (NavMeshAgent != null && NavMeshAgent.enabled && NavMeshAgent.isOnNavMesh)
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

    public string GetCreatureName()
    {
        return creatureprefabName;
    }
    
    public void SetHealthFraction(float fraction)
    {
        if (Down) return;
        int targetHealth = Mathf.Clamp(Mathf.RoundToInt(CalculatedMaxHealth * fraction), 1, CalculatedMaxHealth);
        currentHealth = targetHealth;
    }
}