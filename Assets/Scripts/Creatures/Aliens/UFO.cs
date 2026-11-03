using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// The abduction saucer. Its team decides the job:
// - Alien team (enemy): beams up a knocked-out farm creature and flies it off the map edge. The creature is stored in
//   Abducted so a later wave can bring it back as an enemy.
// - Farm team (the player's one ship): beams up a knocked-out alien, carries it to the barn and lets it go there on the
//   farm's side, healed and selectable.
// It flies in a straight line at altitude by moving its transform, so fences and the NavMesh don't matter. It never
// registers with CreatureManager, so aliens and the auto-battle ignore it.
public class UFO : Alien
{
    enum State { Idle, Approach, Beam, Carry, Leave }

    [Header("Flight")]
    [SerializeField] float flySpeed = 7f;
    [SerializeField] float hoverHeight = 3.5f;

    [Header("Beam")]
    [Tooltip("On while the saucer lifts and carries a body.")]
    [SerializeField] GameObject beam;
    [Tooltip("Seconds to lift a body with nobody holding on.")]
    [SerializeField] float liftTime = 2.5f;
    [Tooltip("Extra lift time per creature holding the body down, as a share of liftTime.")]
    [SerializeField] float holdSlowdown = 0.35f;
    [SerializeField] int maxHolders = 3;
    [Tooltip("Standing teammates of the victim within this many metres run over and hold on.")]
    [SerializeField] float holderRadius = 6f;

    [Header("Stored in the ship")]
    [SerializeField] Vector3 storedOffset = new(0f, -0.4f, 0f);
    [SerializeField] float storedScale = 0.5f;

    // Creatures enemy saucers took off the map, oldest first, for the waves to bring back.
    public static readonly List<Creature> Abducted = new();
    static readonly HashSet<Creature> claimed = new();
    public static bool IsClaimed(Creature c) => claimed.Contains(c);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Abducted.Clear();
        claimed.Clear();
    }

    State state;
    Creature victim;
    Vector3 victimScale, dropSpot, home, exitPoint;
    float lift;
    readonly List<Creature> holders = new();

    public Creature Victim => victim;
    public bool Beaming => state == State.Beam;
    public int Holding => holders.Count;
    bool IsEnemy => Team == GameConstants.Team.Alien;

    protected override void Awake()
    {
        base.Awake();
        NavMeshAgent.enabled = false;
        CreatureBrain.IsPassive = true;
        home = transform.position - Vector3.up * hoverHeight; // the ground spot it hovers over when idle
        exitPoint = home;
        if (beam) beam.SetActive(false);
    }

    // Saucers stay out of the CreatureManager so nothing targets or fights them.
    protected override void OnEnable() { }
    protected override void Start() { }

    // Used by the AbductionManager: go take this creature, then leave through `exit`.
    public void Send(Creature target, Vector3 exit)
    {
        exitPoint = exit;
        Claim(target);
    }

    void Update()
    {
        if (victim == null && state is State.Approach or State.Beam or State.Carry) Drop();

        switch (state)
        {
            case State.Idle:
                Creature next = NearestFreeBody();
                if (next != null) Claim(next);
                else if (IsEnemy) state = State.Leave;
                else FlyTo(home);
                break;

            case State.Approach:
                if (!victim.Down) { Drop(); break; }
                if (FlyTo(victim.Position)) StartBeam();
                break;

            case State.Beam:
                if (!victim.Down) { Drop(); break; }
                Lift();
                break;

            case State.Carry:
                if (FlyTo(IsEnemy ? exitPoint : dropSpot)) Deliver();
                break;

            case State.Leave:
                if (FlyTo(exitPoint)) Destroy(gameObject);
                break;
        }
    }

    // Moves toward the spot `hoverHeight` above `ground`. True once it is there.
    bool FlyTo(Vector3 ground)
    {
        Vector3 to = new(ground.x, ground.y + hoverHeight, ground.z);
        transform.position = Vector3.MoveTowards(transform.position, to, flySpeed * Time.deltaTime);
        return (transform.position - to).sqrMagnitude < 0.01f;
    }

    Creature NearestFreeBody()
    {
        if (CreatureManager.Instance == null) return null;
        Creature best = null;
        float bestDistance = float.MaxValue;
        foreach (Creature c in CreatureManager.Instance.GetEnemies(Team))
        {
            if (c == null || !c.Down || claimed.Contains(c) || !c.gameObject.activeInHierarchy) continue;
            float d = Vector3.Distance(transform.position, c.Position);
            if (d < bestDistance)
            {
                best = c;
                bestDistance = d;
            }
        }
        return best;
    }

    void Claim(Creature target)
    {
        victim = target;
        claimed.Add(target);
        state = State.Approach;
    }

    void StartBeam()
    {
        state = State.Beam;
        lift = 0f;
        if (beam) beam.SetActive(true);
        if (victim.NavMeshAgent) victim.NavMeshAgent.enabled = false;
        if (victim.Gatherer) victim.Gatherer.AbortWork();
        victimScale = victim.transform.localScale;
        CallHolders();
    }

    // Up to maxHolders standing teammates of the victim run to the body and hold it down.
    void CallHolders()
    {
        if (CreatureManager.Instance == null) return;
        var candidates = new List<Creature>();
        foreach (Creature c in CreatureManager.Instance.GetEnemies(Team))
            if (c != null && c != victim && c.Team == victim.Team && !c.Down && !c.CreatureBrain.IsPassive
                && c.NavMeshAgent.isOnNavMesh && Vector3.Distance(c.Position, victim.Position) <= holderRadius)
                candidates.Add(c);
        candidates.Sort((a, b) => Vector3.Distance(a.Position, victim.Position).CompareTo(Vector3.Distance(b.Position, victim.Position)));

        for (int i = 0; i < candidates.Count && holders.Count < maxHolders; i++)
        {
            Creature c = candidates[i];
            c.CreatureBrain.IsPassive = true;
            c.NavMeshAgent.SetDestination(victim.Position);
            holders.Add(c);
        }
    }

    void Lift()
    {
        int holding = 0;
        foreach (Creature h in holders)
            if (h != null && !h.Down && Vector3.Distance(h.Position, victim.Position) < 1.8f) holding++;

        lift += Time.deltaTime / (liftTime * (1f + holdSlowdown * holding));
        Vector3 stored = transform.TransformPoint(storedOffset);
        Vector3 ground = new(stored.x, transform.position.y - hoverHeight, stored.z);
        victim.transform.position = Vector3.Lerp(ground, stored, lift);
        victim.transform.localScale = Vector3.Lerp(victimScale, victimScale * storedScale, lift);
        if (lift < 1f) return;

        ReleaseHolders();
        victim.transform.SetParent(transform, true);
        victim.transform.localPosition = storedOffset;
        dropSpot = IsEnemy ? exitPoint : BarnDropSpot();
        state = State.Carry;
    }

    // A ground spot just outside the barn, on the side the saucer comes from. Ground height only, so it never lands a
    // creature on the barn roof.
    Vector3 BarnDropSpot()
    {
        ResourceManager barn = ResourceManager.Nearest(transform.position);
        if (barn == null) return home;
        Vector3 from = barn.Body != null ? barn.Body.ClosestPointOnBounds(transform.position) : barn.transform.position;
        from.y = barn.transform.position.y;
        Vector3 dir = transform.position - from;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
        for (float d = 1.5f; d <= 15f; d += 1.5f)
            if (NavMesh.SamplePosition(from + dir * d, out NavMeshHit hit, 1f, NavMesh.AllAreas)
                && Mathf.Abs(hit.position.y - from.y) < 0.5f && ClearAt(hit.position))
                return hit.position;
        return home;
    }

    // Nothing over the spot and room for the hull at hover height, so the saucer doesn't clip into the barn.
    // Creatures don't count, they walk off.
    public bool ClearAt(Vector3 ground)
    {
        float top = hoverHeight + 2f;
        foreach (var hit in Physics.RaycastAll(ground + Vector3.up * top, Vector3.down, top - 0.3f, ~0, QueryTriggerInteraction.Ignore))
            if (hit.collider.GetComponentInParent<Creature>() == null) return false;
        foreach (var c in Physics.OverlapBox(ground + Vector3.up * (hoverHeight + 0.3f), new Vector3(2.6f, 0.6f, 2.6f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            if (c.GetComponentInParent<Creature>() == null) return false;
        return true;
    }

    void Deliver()
    {
        Creature c = victim;
        victim = null;
        claimed.Remove(c);
        if (beam) beam.SetActive(false);
        c.transform.SetParent(null, true);
        c.transform.localScale = victimScale;

        if (IsEnemy)
        {
            c.gameObject.SetActive(false);
            Abducted.Add(c);
            state = State.Leave;
            return;
        }

        // ponytail: an enemy-modified animal would have its mutations stripped here for ResourceType.Technology; no
        // mutation system exists yet (jcardas/Animal-Mutation is empty), so it just changes sides like an alien.
        Land(c, dropSpot);
        c.SetTeam(GameConstants.Team.Farm);
        c.HealToFull();
        if (c.TryGetComponent(out Selectable selectable)) selectable.enabled = true;
        else c.gameObject.AddComponent<Selectable>();
        c.CreatureBrain.SetGuardPost(c.Position);
        state = State.Idle;
    }

    // The victim was revived or lost mid-abduction: put it back on the ground and look for another.
    void Drop()
    {
        if (beam) beam.SetActive(false);
        ReleaseHolders();
        if (victim != null)
        {
            claimed.Remove(victim);
            victim.transform.SetParent(null, true);
            if (state == State.Beam || state == State.Carry) victim.transform.localScale = victimScale;
            Vector3 ground = victim.Position;
            ground.y = transform.position.y - hoverHeight;
            if (state != State.Approach) Land(victim, ground);
        }
        victim = null;
        state = State.Idle;
    }

    static void Land(Creature c, Vector3 at)
    {
        if (NavMesh.SamplePosition(at, out var hit, 4f, NavMesh.AllAreas)) at = hit.position;
        c.transform.position = at;
        if (c.NavMeshAgent)
        {
            c.NavMeshAgent.enabled = true;
            c.NavMeshAgent.Warp(at);
        }
    }

    void ReleaseHolders()
    {
        foreach (Creature h in holders)
            if (h != null) h.CreatureBrain.IsPassive = false;
        holders.Clear();
    }

    protected override void OnDisable()
    {
        if (victim != null) Drop();
        base.OnDisable();
    }
}
