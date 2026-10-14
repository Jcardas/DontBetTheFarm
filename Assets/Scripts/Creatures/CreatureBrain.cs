using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Creature))]
public class CreatureBrain : MonoBehaviour
{
    private Creature creature;
    private NavMeshAgent agent;

    private Creature currentTarget;
    private Creature playerOrderedTarget;

    private Vector3 guardPost;
    private float nextThinkTime;
    private float nextAttackTime;
    private float obeyGraceEnds;
    private bool isObeying;

    public bool IsPassive { get; set; }
    public bool Engaged => currentTarget != null && !currentTarget.Down;

    private void Awake()
    {
        creature = GetComponent<Creature>();
        agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        // Start sets guard post so spawn positions from spawners are respected
        guardPost = transform.position;
    }

    public void Guard(Vector3 point)
    {
        currentTarget = playerOrderedTarget = null;
        isObeying = false;

        guardPost = NavMesh.SamplePosition(point, out NavMeshHit hit, 3f, NavMesh.AllAreas)
            ? hit.position
            : point;

        if (agent.isOnNavMesh)
        {
            agent.SetDestination(guardPost);
        }
    }

    public void Obey()
    {
        currentTarget = playerOrderedTarget = null;
        isObeying = true;
        obeyGraceEnds = Time.time + 0.5f;
    }

    public void Attack(Creature enemy)
    {
        isObeying = false;
        currentTarget = playerOrderedTarget = enemy;
        nextThinkTime = 0f;
    }

    private void Update()
    {
        if (creature == null || creature.Down || IsPassive) return;

        // Player move commands take precedence until destination is reached
        if (isObeying)
        {
            bool arrived = !agent.pathPending && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.2f);
            if (Time.time < obeyGraceEnds || !arrived) return;
            isObeying = false;
        }

        // AI decision tick (4 Hz)
        if (Time.time >= nextThinkTime)
        {
            nextThinkTime = Time.time + 0.25f;

            if (playerOrderedTarget != null && playerOrderedTarget.Down)
                playerOrderedTarget = null;

            // Unanchored units (like the player/farmer) drag their guard post with them
            if (!creature.Anchored && !Engaged)
                guardPost = transform.position;

            currentTarget = playerOrderedTarget != null
                ? playerOrderedTarget
                : CreatureManager.Instance.GetNearestEnemy(creature.Team, transform.position, guardPost, creature.GuardRadius);

            if (currentTarget == null)
            {
                if (Vector3.Distance(transform.position, guardPost) > agent.stoppingDistance + 0.5f)
                {
                    if (agent.isOnNavMesh) agent.SetDestination(guardPost);
                }
                return;
            }

            // Target exists: walk towards it if out of range
            if (currentTarget.Body != null && Reach.Gap(transform.position, currentTarget.Body) > creature.AttackRange)
            {
                Reach.WalkTo(agent, currentTarget.Body);
            }
        }

        // Combat execution
        if (currentTarget == null || currentTarget.Down || currentTarget.Body == null) return;
        if (Reach.Gap(transform.position, currentTarget.Body) > creature.AttackRange) return;

        // Stop moving to swing
        if (agent.hasPath) agent.ResetPath();

        // Rotate smoothly towards target
        Vector3 toTarget = currentTarget.transform.position - transform.position;
        toTarget.y = 0;
        if (toTarget.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.LookRotation(toTarget),
                agent.angularSpeed * Time.deltaTime
            );
        }

        // Attack timing check
        if (Time.time < nextAttackTime) return;
        nextAttackTime = Time.time + creature.AttackSpeed;

        currentTarget.TakeDamage(creature.AttackDamage);
        creature.TriggerAttackVisual();
    }
}