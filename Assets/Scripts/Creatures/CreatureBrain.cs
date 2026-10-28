using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Creature))]
public class CreatureBrain : MonoBehaviour
{
    private Creature creature;
    private NavMeshAgent agent;
    private Gatherer gatherer;

    private IDamageable currentTarget;
    private Creature playerOrderedTarget;

    private Vector3 guardPost;
    private float nextThinkTime;
    private float nextAttackTime;
    private float obeyGraceEnds;
    private bool isObeying;
    private bool retreating;

    public bool IsPassive { get; set; }
    public bool Engaged => currentTarget != null && !currentTarget.Down;

    private void Awake()
    {
        creature = GetComponent<Creature>();
        agent = GetComponent<NavMeshAgent>();
        gatherer = GetComponent<Gatherer>();
    }

    private void Start()
    {
        guardPost = transform.position;
    }

    public void ExecuteCommand(Order order)
    {
        if (creature == null || creature.Down) return;

        // An incoming attack command
        if (order.kind == OrderKind.Attack && creature.AttackDamage > 0)
        {
            if (gatherer != null) gatherer.AbortWork();
            Attack(order.enemy);
            return;
        }

        // Work / Harvest commands
        if (order.kind == OrderKind.Work && gatherer != null)
        {
            currentTarget = playerOrderedTarget = null;
            isObeying = false;
            gatherer.Work(order.node);
            return;
        }

        if (order.kind == OrderKind.Harvest && gatherer != null && gatherer.harvestsFields)
        {
            currentTarget = playerOrderedTarget = null;
            isObeying = false;
            gatherer.Harvest(order.field);
            return;
        }

        // Generic Move / Guard command: immediately cancel gathering
        if (gatherer != null) gatherer.AbortWork();
        Guard(order.point);
    }

    public void Guard(Vector3 point)
    {
        if (gatherer != null) gatherer.AbortWork();

        currentTarget = playerOrderedTarget = null;
        isObeying = true;
        obeyGraceEnds = Time.time + 0.5f;

        guardPost = NavMesh.SamplePosition(point, out NavMeshHit hit, 3f, NavMesh.AllAreas)
            ? hit.position
            : point;

        if (agent.isOnNavMesh)
        {
            agent.updateRotation = true;
            agent.SetDestination(guardPost);
        }
    }

    public void Obey()
    {
        if (gatherer != null) gatherer.AbortWork();
        currentTarget = playerOrderedTarget = null;
        isObeying = true;
        obeyGraceEnds = Time.time + 0.5f;
    }

    public void Attack(Creature enemy)
    {
        if (gatherer != null) gatherer.AbortWork();
        isObeying = false;
        currentTarget = playerOrderedTarget = enemy;
        nextThinkTime = 0f;
    }

    public void SetGuardPost(Vector3 point)
    {
        guardPost = point;
    }

    private void Update()
    {
        if (creature == null || creature.Down || IsPassive) return;

        // Player move commands take precedence until destination is reached
        if (isObeying)
        {
            bool arrived = !agent.pathPending &&
                           (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.2f);
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

            currentTarget = playerOrderedTarget != null ? playerOrderedTarget : creature.FindTarget(guardPost);

            if (currentTarget == null)
            {
                if (Vector3.Distance(transform.position, guardPost) > agent.stoppingDistance + 0.5f)
                {
                    if (agent.isOnNavMesh)
                    {
                        agent.updateRotation = true;
                        agent.SetDestination(guardPost);
                    }
                }

                return;
            }

            // Target exists: walk towards it if out of range, or back away if it is too close for a ranged creature
            retreating = false;
            if (currentTarget.Body != null)
            {
                float gap = Reach.Gap(transform.position, currentTarget.Body);
                if (gap > creature.AttackRange)
                {
                    agent.updateRotation = true;
                    Reach.WalkTo(agent, currentTarget.Body);
                }
                else if (gap < creature.KeepAwayRange)
                {
                    BackAwayFrom(currentTarget, gap);
                }
            }
        }

        // Combat execution
        if (currentTarget == null || currentTarget.Down || currentTarget.Body == null) return;
        if (Reach.Gap(transform.position, currentTarget.Body) > creature.AttackRange) return;

        if (agent.hasPath && !retreating) agent.ResetPath();
        agent.updateRotation = false;

        Vector3 toTarget = currentTarget.Position - transform.position;
        toTarget.y = 0;
        if (toTarget.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(toTarget);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                agent.angularSpeed * Time.deltaTime
            );
        }

        if (Time.time < nextAttackTime) return;
        nextAttackTime = Time.time + creature.AttackSpeed;

        creature.PerformAttack(currentTarget);
    }

    // Steps straight away from the enemy until it is KeepAwayRange off, still facing and attacking it on the way.
    // A unit with nowhere to back into (wall, map edge) stands and fights.
    private void BackAwayFrom(IDamageable enemy, float gap)
    {
        Vector3 away = transform.position - enemy.Position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.001f) away = -transform.forward;

        Vector3 to = transform.position + away.normalized * (creature.KeepAwayRange - gap + 1f);
        if (!agent.isOnNavMesh || !NavMesh.SamplePosition(to, out NavMeshHit hit, 2f, NavMesh.AllAreas)) return;

        agent.SetDestination(hit.position);
        retreating = true;
    }
}