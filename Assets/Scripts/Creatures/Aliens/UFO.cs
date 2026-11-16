using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// The abduction saucer. It flies to a knocked-out farm creature, beams it up and carries it off the map edge.
// It flies in a straight line above everything by moving its transform, so it needs no NavMesh.
// Implements IDamageable so the farmer's shotgun and ranged defenders can shoot it down before it escapes.
public class UFO : MonoBehaviour, IDamageable
{
    protected enum State { Idle, Approach, Beam, Carry, Leave }

    [Header("Health & Combat")]
    [SerializeField] protected int maxHealth = 40;
    [Tooltip("Optional explosion prefab. If left empty, a built-in particle burst spawns automatically.")]
    [SerializeField] protected GameObject destroyedEffectPrefab;

    [Header("Flight")]
    [SerializeField] protected float flySpeed = 2f;
    [SerializeField] protected float hoverHeight = 3.5f;

    [Header("Beam")]
    [Tooltip("Turned on while the saucer lifts and carries a body.")]
    [SerializeField] protected GameObject beam;
    [Tooltip("Seconds to lift a body into the ship.")]
    [SerializeField] protected float liftTime = 5f;
    [Tooltip("Where a body rides under the hull, and how much it shrinks to fit.")]
    [SerializeField] protected Vector3 storedOffset = new(0f, -0.4f, 0f);
    [SerializeField] protected float storedScale = 0.5f;

    // Creatures enemy saucers took off the map, oldest first.
    public static readonly List<Creature> Abducted = new();

    // All currently active saucers in the scene so ranged defenders can auto-target them.
    public static readonly HashSet<UFO> Active = new();

    // Fired when an enemy UFO is shot down (passes the released victim, or null if none).
    public static event Action<UFO, Creature> OnUFODestroyed;

    // Bodies a saucer is already going for, so two saucers never chase the same one.
    static readonly HashSet<Creature> claimed = new();
    public static bool IsClaimed(Creature c) => claimed.Contains(c);
    protected static void Release(Creature c) => claimed.Remove(c);

    // Clears the lists between plays even when "Reload Domain" is off in Enter Play Mode settings.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Abducted.Clear();
        claimed.Clear();
        Active.Clear();
    }

    protected State state;
    protected Creature victim;
    protected Vector3 exitPoint;
    Vector3 victimScale, liftFrom;
    float lift;
    int currentHealth;
    Collider bodyCollider;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool Down => currentHealth <= 0;
    public Vector3 Position => transform.position;
    public Collider Body => bodyCollider;
    public virtual bool CanBeShotDown => true;

    protected virtual void Awake()
    {
        currentHealth = maxHealth;
        bodyCollider = GetComponentInChildren<Collider>();
        if (beam) beam.SetActive(false);
    }

    protected virtual void OnEnable() => Active.Add(this);
    protected virtual void OnDisable() => Active.Remove(this);

    public void TakeDamage(int amount)
    {
        if (!CanBeShotDown || Down || amount <= 0) return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        if (currentHealth == 0)
        {
            HandleShotDown();
        }
    }

    protected virtual void HandleShotDown()
    {
        Creature releasedVictim = victim;

        if (victim != null)
        {
            claimed.Remove(victim);

            // Only drop the creature from the UFO's position if it was actually being beamed or carried.
            // If the UFO was still in State.Approach, leave the creature untouched on the ground where it fell.
            if (state is State.Beam or State.Carry)
            {
                victim.transform.SetParent(null, true);
                victim.transform.localScale = victimScale != Vector3.zero ? victimScale : Vector3.one;

                Vector3 dropPos = new Vector3(transform.position.x, liftFrom.y, transform.position.z);
                if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit groundHit, hoverHeight + 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    dropPos.y = groundHit.point.y;
                }
                victim.transform.position = dropPos;
                victim.gameObject.SetActive(true);
            }

            victim = null;
        }

        if (beam) beam.SetActive(false);

        SpawnDestroyedEffect();
        OnUFODestroyed?.Invoke(this, releasedVictim);
        Destroy(gameObject);
    }

    private void SpawnDestroyedEffect()
    {
        if (destroyedEffectPrefab != null)
        {
            Instantiate(destroyedEffectPrefab, transform.position, Quaternion.identity);
            return;
        }

        // Built-in explosion burst when no prefab is assigned in the Inspector
        var fxObj = new GameObject("UFO_ExplosionFX");
        fxObj.transform.position = transform.position;

        var ps = fxObj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.duration = 0.6f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 11f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.65f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.1f, 1f), new Color(0.4f, 0.95f, 1f, 1f));
        main.gravityModifier = 0.6f;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.enabled = true;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 35) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.6f;

        var renderer = fxObj.GetComponent<ParticleSystemRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
        if (shader != null)
            renderer.material = new Material(shader);

        ps.Play();
    }

    // Called by the AbductionManager: go take this creature, then leave through `exit`.
    public void Send(Creature target, Vector3 exit)
    {
        exitPoint = exit;
        Claim(target);
    }

    protected void Claim(Creature target)
    {
        victim = target;
        claimed.Add(target);
        state = State.Approach;
    }

    void Update()
    {
        if (Down) return;

        // The body got back up (healed) or is gone before it was aboard: put it down and give up on it.
        if (state is State.Approach or State.Beam && (victim == null || !victim.Down)) Drop();

        switch (state)
        {
            case State.Idle: OnIdle(); break;
            case State.Approach: if (FlyTo(victim.Position)) StartBeam(); break;
            case State.Beam: Lift(); break;
            case State.Carry: if (FlyTo(exitPoint)) Deliver(); break;
            case State.Leave: if (FlyTo(exitPoint)) Destroy(gameObject); break;
        }
    }

    // An enemy saucer with nothing to carry just leaves.
    protected virtual void OnIdle() => state = State.Leave;

    // Moves toward the spot `hoverHeight` above `ground`. True once it is there.
    protected bool FlyTo(Vector3 ground)
    {
        Vector3 to = ground + Vector3.up * hoverHeight;
        transform.position = Vector3.MoveTowards(transform.position, to, flySpeed * Time.deltaTime);
        return (transform.position - to).sqrMagnitude < 0.01f;
    }

    void StartBeam()
    {
        state = State.Beam;
        lift = 0f;
        if (beam) beam.SetActive(true);
        if (victim.Gatherer) victim.Gatherer.AbortWork();
        if (victim.NavMeshAgent) victim.NavMeshAgent.enabled = false;
        victimScale = victim.transform.localScale;
        liftFrom = victim.transform.position;
    }

    // Raises and shrinks the body into the hull over liftTime seconds.
    void Lift()
    {
        lift += Time.deltaTime / liftTime;
        victim.transform.position = Vector3.Lerp(liftFrom, transform.TransformPoint(storedOffset), lift);
        victim.transform.localScale = Vector3.Lerp(victimScale, victimScale * storedScale, lift);
        if (lift < 1f) return;

        if (beam) beam.SetActive(false);
        victim.transform.SetParent(transform, true);
        OnAboard();
    }

    // An enemy saucer heads for the map edge with its catch.
    protected virtual void OnAboard() => state = State.Carry;

    // Off the map: the creature is put away for a later wave to bring back.
    protected virtual void Deliver()
    {
        claimed.Remove(victim);
        bool farmerAbducted = victim is Farmer;

        victim.transform.SetParent(null, true);
        victim.transform.localScale = victimScale;
        victim.gameObject.SetActive(false);
        Abducted.Add(victim);
        victim = null;
        state = State.Leave;

        if (farmerAbducted)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    // Puts a body that was being lifted back on the ground and frees it for anyone else.
    void Drop()
    {
        if (beam) beam.SetActive(false);
        if (victim != null)
        {
            claimed.Remove(victim);
            if (state is State.Beam or State.Carry)
            {
                victim.transform.SetParent(null, true);
                victim.transform.position = liftFrom;
                victim.transform.localScale = victimScale;
                if (victim.NavMeshAgent && !victim.Down) victim.NavMeshAgent.enabled = true; // a body stays out of walkers' way
            }
        }
        victim = null;
        state = State.Idle;
    }

    // Return to the edge of the map no matter what.
    public void Retreat()
    {
        if (beam) beam.SetActive(false);
        if (victim != null) Drop();
        victim = null;

        state = State.Leave;
    }

    // Returns true if the saucer has the creature it was sent to abduct inside it.
    // False if it is still on the ground or has been dropped.
    public bool IsVictimInUFO()
    {
        return state is State.Carry or State.Leave && victim != null;
    }
}