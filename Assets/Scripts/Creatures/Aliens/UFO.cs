using System.Collections.Generic;
using UnityEngine;

// The abduction saucer. It flies to a knocked-out farm creature, beams it up and carries it off the map edge.
// It flies in a straight line above everything by moving its transform, so it needs no NavMesh. It is not a
// Creature, so nothing targets or fights it.
public class UFO : MonoBehaviour
{
    protected enum State { Idle, Approach, Beam, Carry, Leave }

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

    // Bodies a saucer is already going for, so two saucers never chase the same one.
    static readonly HashSet<Creature> claimed = new();
    public static bool IsClaimed(Creature c) => claimed.Contains(c);

    // Clears the lists between plays even when "Reload Domain" is off in Enter Play Mode settings.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Abducted.Clear();
        claimed.Clear();
    }

    protected State state;
    protected Creature victim;
    protected Vector3 exitPoint;
    Vector3 victimScale, liftFrom;
    float lift;

    protected virtual void Awake()
    {
        if (beam) beam.SetActive(false);
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
        victim.transform.SetParent(null, true);
        victim.transform.localScale = victimScale;
        victim.gameObject.SetActive(false);
        Abducted.Add(victim);
        victim = null;
        state = State.Leave;
    }

    // Puts a body that was being lifted back on the ground and frees it for anyone else.
    void Drop()
    {
        if (beam) beam.SetActive(false);
        if (victim != null)
        {
            claimed.Remove(victim);
            if (state == State.Beam)
            {
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
