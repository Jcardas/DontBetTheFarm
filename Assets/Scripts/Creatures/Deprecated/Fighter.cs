using UnityEngine;
using UnityEngine.AI;

// Auto-battle brain for animals, aliens and the farmer: walk to the closest enemy, hit it until it is knocked out,
// then go back to its guard post (where it started, until the player moves it with Guard).
// A player Attack order picks the target instead and chases it past the guard radius; once it is down,
// the fighter goes back to its post like after any other fight.
// Aliens get a huge guard radius so they march on the whole farm. The farmer is not anchored: his post follows him,
// so he only defends himself where he stands.
[RequireComponent(typeof(NavMeshAgent), typeof(Health))]
public class Fighter : MonoBehaviour
{
    [Tooltip("Off for the farmer: the post follows the unit whenever it has no target, so it never walks home.")]
    public bool anchored = true;
    [Tooltip("Only enemies this close to the guard post are attacked, so where you place defenders matters.")]
    public float guardRadius = 8f;
    [Tooltip("How close its centre must get to an enemy's edge to hit it. The NavMesh keeps units about 0.5-0.7 off the barn's walls, so keep this at 0.8 or more.")]
    public float attackRange = 1f;
    public int damage = 10;
    public float secondsBetweenAttacks = 1f;

    NavMeshAgent agent;
    Health self;
    ToyBob toy;
    Health target, ordered;
    Vector3 post;
    float nextThink, nextAttack, obeyGraceEnds;
    bool obeying;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        self = GetComponent<Health>();
        toy = GetComponentInChildren<ToyBob>();
    }

    // Start, not Awake: Awake runs inside Instantiate, before a spawner gets to move the unit into place.
    void Start() => post = transform.position;

    public bool Engaged => target != null && !target.Down;

    // Move the guard post to `point`: walk there and defend around it from now on.
    public void Guard(Vector3 point)
    {
        target = ordered = null;
        post = NavMesh.SamplePosition(point, out NavMeshHit hit, 3f, NavMesh.AllAreas) ? hit.position : point;
        if (agent.isOnNavMesh) agent.SetDestination(post);
    }

    // A player order beats self-defence: drop the target and leave the agent alone until the unit arrives.
    public void Obey()
    {
        target = ordered = null;
        obeying = true;
        obeyGraceEnds = Time.time + 0.5f; // Gatherer.Work only sets its path on its next Update
    }

    public void Attack(Health enemy)
    {
        obeying = false;
        target = ordered = enemy;
        nextThink = 0f;
    }

    void Update()
    {
        if (self.Down) return;
        if (obeying)
        {
            bool arrived = !agent.pathPending && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.2f);
            if (Time.time < obeyGraceEnds || !arrived) return;
            obeying = false;
        }

        // Re-pick and re-path four times a second instead of every frame; path requests are the expensive part.
        if (Time.time >= nextThink)
        {
            nextThink = Time.time + 0.25f;
            if (ordered != null && ordered.Down) ordered = null;
            if (!anchored && !Engaged) post = transform.position;
            target = ordered != null ? ordered : Health.NearestEnemy(self.team, transform.position, post, guardRadius);
            if (target == null)
            {
                if (Vector3.Distance(transform.position, post) > agent.stoppingDistance + 0.5f) agent.SetDestination(post);
                return;
            }
            if (Reach.Gap(transform.position, target.Body) > attackRange) Reach.WalkTo(agent, target.Body);
        }

        if (target == null || target.Down || Reach.Gap(transform.position, target.Body) > attackRange) return;

        agent.ResetPath();
        Vector3 toTarget = target.transform.position - transform.position;
        toTarget.y = 0;
        if (toTarget.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toTarget), agent.angularSpeed * Time.deltaTime);

        if (Time.time < nextAttack) return;
        nextAttack = Time.time + secondsBetweenAttacks;
        target.TakeDamage(damage);
        if (toy) toy.Bump();
    }
}
